using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Secms.Application.Dtos;
using Secms.Application.Interfaces;
using Secms.Application.Options;
using Secms.Domain.Entities;
using Secms.Domain.Enums;
using Secms.Domain.Services;
using Secms.Infrastructure;
using Secms.Infrastructure.Persistence;

namespace Secms.Infrastructure.Services;

public class SchedulingService
{
    private readonly SecmsDbContext _db;
    private readonly SchedulingOptions _opts;
    private readonly ICurrentUser _current;

    public SchedulingService(SecmsDbContext db, IOptions<SchedulingOptions> opts, ICurrentUser current)
    {
        _db = db;
        _opts = opts.Value;
        _current = current;
    }

    public async Task<List<SessionDto>> ListAsync(DateTimeOffset from, DateTimeOffset to, Guid? teacherId = null, Guid? facilityId = null, CancellationToken ct = default)
    {
        if (_current.IsTeacher && !_current.IsAdmin)
        {
            if (_current.TeacherProfileId is not Guid tid)
                return new List<SessionDto>();
            teacherId = tid;
        }

        var q = _db.Sessions.AsNoTracking()
            .Include(s => s.TeacherProfile)
            .Include(s => s.Facility)
            .Include(s => s.ClassGroup)
            .Include(s => s.SessionStudents)
            .Where(s => s.StartAt < to && s.EndAt > from)
            .Where(s => s.Status != SessionStatus.Cancelled);

        if (teacherId.HasValue) q = q.Where(s => s.TeacherProfileId == teacherId);
        if (facilityId.HasValue) q = q.Where(s => s.FacilityId == facilityId);

        var list = await q.OrderBy(s => s.StartAt).ToListAsync(ct);
        return list.Select(Map).Where(x => x != null).Cast<SessionDto>().ToList();
    }

    public async Task<(SessionDto? Session, List<ScheduleConflictDto> Conflicts, bool Saved)> CreateAsync(UpsertSessionRequest req, CancellationToken ct = default)
    {
        if (req.EndAt <= req.StartAt) throw new InvalidOperationException("Thời gian kết thúc phải sau thời gian bắt đầu.");

        var startAt = req.StartAt.ToUniversalTime();
        var endAt = req.EndAt.ToUniversalTime();

        var teacherSessions = await _db.Sessions
            .Where(s => s.TeacherProfileId == req.TeacherProfileId && s.Status != SessionStatus.Cancelled)
            .ToListAsync(ct);

        var conflicts = ConflictDetector.Detect(req.TeacherProfileId, startAt, endAt, req.FacilityId, teacherSessions, _opts.MinTravelMinutes);
        var conflictDtos = conflicts.Select(ConflictMapper.ToDto).ToList();

        if (ConflictDetector.HasBlockingErrors(conflicts) && !(req.Force && _opts.AllowForceSaveWithConflicts))
            return (null, conflictDtos, false);

        var studentIds = req.StudentIds;
        if ((studentIds == null || studentIds.Count == 0) && req.ClassGroupId.HasValue)
        {
            studentIds = await _db.ClassGroupStudents
                .Where(x => x.ClassGroupId == req.ClassGroupId && x.LeftAt == null)
                .Select(x => x.StudentId)
                .ToListAsync(ct);
        }

        var session = new TeachingSession
        {
            ClassGroupId = req.ClassGroupId,
            TeacherProfileId = req.TeacherProfileId,
            FacilityId = req.FacilityId,
            StartAt = startAt,
            EndAt = endAt,
            Title = req.Title,
            Notes = req.Notes
        };
        foreach (var sid in (studentIds ?? new List<Guid>()).Distinct())
            session.SessionStudents.Add(new SessionStudent { StudentId = sid });

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Sessions.AsNoTracking()
            .Include(s => s.TeacherProfile).Include(s => s.Facility).Include(s => s.ClassGroup).Include(s => s.SessionStudents)
            .FirstAsync(s => s.Id == session.Id, ct);

        return (Map(created)! with { Conflicts = conflictDtos }, conflictDtos, true);
    }

    public async Task<(SessionDto? Session, List<ScheduleConflictDto> Conflicts, bool Saved)> UpdateAsync(Guid id, UpsertSessionRequest req, CancellationToken ct = default)
    {
        var session = await _db.Sessions.Include(s => s.SessionStudents).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session == null) return (null, new(), false);
        if (session.AttendanceFinalizedAt != null) throw new InvalidOperationException("Ca đã điểm danh, không thể sửa lịch.");

        var startAt = req.StartAt.ToUniversalTime();
        var endAt = req.EndAt.ToUniversalTime();

        var teacherSessions = await _db.Sessions
            .Where(s => s.TeacherProfileId == req.TeacherProfileId && s.Status != SessionStatus.Cancelled)
            .ToListAsync(ct);

        var conflicts = ConflictDetector.Detect(req.TeacherProfileId, startAt, endAt, req.FacilityId, teacherSessions, _opts.MinTravelMinutes, id);
        var conflictDtos = conflicts.Select(ConflictMapper.ToDto).ToList();
        if (ConflictDetector.HasBlockingErrors(conflicts) && !(req.Force && _opts.AllowForceSaveWithConflicts))
            return (null, conflictDtos, false);

        session.ClassGroupId = req.ClassGroupId;
        session.TeacherProfileId = req.TeacherProfileId;
        session.FacilityId = req.FacilityId;
        session.StartAt = startAt;
        session.EndAt = endAt;
        session.Title = req.Title;
        session.Notes = req.Notes;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        if (req.StudentIds != null)
        {
            _db.SessionStudents.RemoveRange(session.SessionStudents);
            foreach (var sid in req.StudentIds.Distinct())
                _db.SessionStudents.Add(new SessionStudent { SessionId = id, StudentId = sid });
        }

        await _db.SaveChangesAsync(ct);
        var updated = await ListAsync(session.StartAt.AddDays(-1), session.EndAt.AddDays(1), ct: ct);
        return (updated.First(s => s.Id == id) with { Conflicts = conflictDtos }, conflictDtos, true);
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session == null) return false;
        session.Status = SessionStatus.Cancelled;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<ScheduleConflictDto>> CheckConflictsAsync(UpsertSessionRequest req, Guid? excludeId = null, CancellationToken ct = default)
    {
        var startAt = req.StartAt.ToUniversalTime();
        var endAt = req.EndAt.ToUniversalTime();
        var teacherSessions = await _db.Sessions
            .Where(s => s.TeacherProfileId == req.TeacherProfileId && s.Status != SessionStatus.Cancelled)
            .ToListAsync(ct);
        return ConflictDetector.Detect(req.TeacherProfileId, startAt, endAt, req.FacilityId, teacherSessions, _opts.MinTravelMinutes, excludeId)
            .Select(ConflictMapper.ToDto).ToList();
    }

    public async Task<CloneWeekResult> CloneWeekAsync(CloneWeekRequest req, CancellationToken ct = default)
    {
        var sourceStart = TimeZones.VietnamDayStartUtc(req.SourceWeekStart);
        var sourceEnd = sourceStart.AddDays(7);
        var delta = req.TargetWeekStart.DayNumber - req.SourceWeekStart.DayNumber;

        var sources = await _db.Sessions
            .Include(s => s.SessionStudents)
            .Where(s => s.StartAt >= sourceStart && s.StartAt < sourceEnd && s.Status != SessionStatus.Cancelled)
            .Where(s => !req.TeacherId.HasValue || s.TeacherProfileId == req.TeacherId)
            .ToListAsync(ct);

        var created = 0;
        var skipped = 0;
        var warnings = new List<ScheduleConflictDto>();

        foreach (var src in sources)
        {
            var newStart = src.StartAt.AddDays(delta).ToUniversalTime();
            var newEnd = src.EndAt.AddDays(delta).ToUniversalTime();
            var existing = await _db.Sessions
                .Where(s => s.TeacherProfileId == src.TeacherProfileId && s.Status != SessionStatus.Cancelled)
                .ToListAsync(ct);

            var conflicts = ConflictDetector.Detect(src.TeacherProfileId, newStart, newEnd, src.FacilityId, existing, _opts.MinTravelMinutes);
            var dtos = conflicts.Select(ConflictMapper.ToDto).ToList();
            warnings.AddRange(dtos.Where(c => c.Severity != ConflictSeverity.Error));

            if (ConflictDetector.HasBlockingErrors(conflicts) && req.SkipOnError && !req.Force)
            {
                skipped++;
                warnings.AddRange(dtos.Where(c => c.Severity == ConflictSeverity.Error));
                continue;
            }

            if (ConflictDetector.HasBlockingErrors(conflicts) && !req.Force)
            {
                skipped++;
                continue;
            }

            var copy = new TeachingSession
            {
                ClassGroupId = src.ClassGroupId,
                TeacherProfileId = src.TeacherProfileId,
                FacilityId = src.FacilityId,
                StartAt = newStart,
                EndAt = newEnd,
                Title = src.Title,
                Notes = src.Notes,
                SourceCloneSessionId = src.Id
            };
            foreach (var ss in src.SessionStudents.Where(x => x.IsActive))
                copy.SessionStudents.Add(new SessionStudent { StudentId = ss.StudentId });

            _db.Sessions.Add(copy);
            created++;
            await _db.SaveChangesAsync(ct);
        }

        return new CloneWeekResult(created, skipped, warnings);
    }

    private static SessionDto? Map(TeachingSession s)
    {
        if (s.TeacherProfile == null || s.Facility == null) return null;
        return new SessionDto(
            s.Id,
            s.ClassGroupId,
            s.ClassGroup?.Name,
            s.TeacherProfileId,
            s.TeacherProfile.FullName,
            s.FacilityId,
            s.Facility.Name,
            s.Facility.Type,
            s.StartAt,
            s.EndAt,
            s.Title,
            s.Status,
            s.AttendanceFinalizedAt,
            s.Notes,
            s.SessionStudents.Where(x => x.IsActive).Select(x => x.StudentId).ToList(),
            null);
    }
}
