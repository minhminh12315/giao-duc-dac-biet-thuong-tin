using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Secms.Application.Dtos;
using Secms.Application.Interfaces;
using Secms.Application.Options;
using Secms.Domain.Entities;
using Secms.Domain.Enums;
using Secms.Infrastructure.Persistence;

namespace Secms.Infrastructure.Services;

public class AttendanceService
{
    private readonly SecmsDbContext _db;
    private readonly AttendanceOptions _opts;
    private readonly ICurrentUser _current;

    public AttendanceService(SecmsDbContext db, IOptions<AttendanceOptions> opts, ICurrentUser current)
    {
        _db = db;
        _opts = opts.Value;
        _current = current;
    }

    public async Task<List<AttendanceItemDto>> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        await EnsureCanAccess(sessionId, ct);
        var session = await _db.Sessions
            .Include(s => s.SessionStudents).ThenInclude(ss => ss.Student)
            .Include(s => s.AttendanceRecords).ThenInclude(a => a.Comment)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy ca học.");

        return session.SessionStudents
            .Where(ss => ss.IsActive && ss.Student != null)
            .Select(ss =>
            {
                var att = session.AttendanceRecords.FirstOrDefault(a => a.StudentId == ss.StudentId);
                List<string>? tags = null;
                if (att?.Comment?.TagsJson != null)
                    tags = JsonSerializer.Deserialize<List<string>>(att.Comment.TagsJson);
                return new AttendanceItemDto(
                    ss.StudentId,
                    ss.Student.FullName,
                    att?.Status ?? AttendanceStatus.NotMarked,
                    att?.Comment?.Content,
                    att?.Comment?.MoodOrLevel,
                    tags);
            }).OrderBy(x => x.StudentName).ToList();
    }

    public async Task SaveDraftAsync(Guid sessionId, SaveAttendanceRequest req, CancellationToken ct = default)
    {
        await EnsureCanAccess(sessionId, ct);
        var session = await _db.Sessions
            .Include(s => s.AttendanceRecords).ThenInclude(a => a.Comment)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy ca học.");

        if (session.Status == SessionStatus.Cancelled)
            throw new InvalidOperationException("Ca đã hủy.");

        EnsureEditable(session);
        await UpsertItems(session, req, finalize: false, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task FinalizeAsync(Guid sessionId, SaveAttendanceRequest req, CancellationToken ct = default)
    {
        await EnsureCanAccess(sessionId, ct);
        var session = await _db.Sessions
            .Include(s => s.SessionStudents)
            .Include(s => s.AttendanceRecords).ThenInclude(a => a.Comment)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy ca học.");

        if (session.Status == SessionStatus.Cancelled)
            throw new InvalidOperationException("Ca đã hủy.");

        EnsureEditable(session);

        var activeIds = session.SessionStudents.Where(x => x.IsActive).Select(x => x.StudentId).ToHashSet();
        if (!activeIds.SetEquals(req.Items.Select(i => i.StudentId)))
            throw new InvalidOperationException("Phải điểm danh đủ tất cả học sinh trong ca.");

        if (req.Items.Any(i => i.Status == AttendanceStatus.NotMarked))
            throw new InvalidOperationException("Không được để trạng thái 'Chưa điểm danh' khi hoàn tất.");

        if (_opts.RequireCommentOnFinalize)
        {
            var missing = req.Items.Where(i => string.IsNullOrWhiteSpace(i.Comment)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException("Bắt buộc nhập nhận xét cho từng học sinh khi hoàn tất điểm danh.");
        }

        await UpsertItems(session, req, finalize: true, ct);
        session.AttendanceFinalizedAt = DateTimeOffset.UtcNow;
        session.Status = SessionStatus.Completed;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = _current.UserId,
            Action = "FinalizeAttendance",
            EntityType = nameof(TeachingSession),
            EntityId = sessionId
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertItems(TeachingSession session, SaveAttendanceRequest req, bool finalize, CancellationToken ct)
    {
        foreach (var item in req.Items)
        {
            var att = session.AttendanceRecords.FirstOrDefault(a => a.StudentId == item.StudentId);
            if (att == null)
            {
                att = new AttendanceRecord { SessionId = session.Id, StudentId = item.StudentId };
                _db.AttendanceRecords.Add(att);
                session.AttendanceRecords.Add(att);
            }
            att.Status = item.Status;
            att.MarkedAt = DateTimeOffset.UtcNow;
            att.MarkedByUserId = _current.UserId;
            att.UpdatedAt = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(item.Comment) || item.MoodOrLevel.HasValue)
            {
                if (att.Comment == null)
                {
                    att.Comment = new TeachingComment { AttendanceRecord = att };
                    _db.TeachingComments.Add(att.Comment);
                }
                att.Comment.Content = item.Comment?.Trim() ?? att.Comment.Content;
                att.Comment.MoodOrLevel = item.MoodOrLevel;
                att.Comment.TagsJson = item.Tags == null ? null : JsonSerializer.Serialize(item.Tags);
                att.Comment.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else if (finalize && att.Comment == null)
            {
                att.Comment = new TeachingComment { Content = "", AttendanceRecord = att };
                _db.TeachingComments.Add(att.Comment);
            }
        }
        await Task.CompletedTask;
    }

    private async Task EnsureCanAccess(Guid sessionId, CancellationToken ct)
    {
        if (_current.IsAdmin) return;
        var ok = await _db.Sessions.AnyAsync(s => s.Id == sessionId && s.TeacherProfileId == _current.TeacherProfileId, ct);
        if (!ok) throw new UnauthorizedAccessException("Bạn không được phân công ca này.");
    }

    private void EnsureEditable(TeachingSession session)
    {
        if (session.AttendanceFinalizedAt == null) return;
        if (_current.IsAdmin && _opts.AdminCanAlwaysEdit) return;
        var hours = (DateTimeOffset.UtcNow - session.AttendanceFinalizedAt.Value).TotalHours;
        if (hours > _opts.EditWindowHours)
            throw new InvalidOperationException("Đã hết thời gian chỉnh sửa điểm danh.");
    }
}
