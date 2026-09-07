using Secms.Domain.Entities;
using Secms.Domain.Enums;

namespace Secms.Domain.Services;

public record ScheduleConflict(
    ConflictType Type,
    ConflictSeverity Severity,
    string Message,
    Guid? SessionIdA = null,
    Guid? SessionIdB = null,
    int? GapMinutes = null,
    int? RequiredMinutes = null,
    Guid? FromFacilityId = null,
    Guid? ToFacilityId = null);

public static class ConflictDetector
{
    public static List<ScheduleConflict> Detect(
        Guid teacherProfileId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid facilityId,
        IReadOnlyList<TeachingSession> existingTeacherSessions,
        int minTravelMinutes,
        Guid? excludeSessionId = null)
    {
        var conflicts = new List<ScheduleConflict>();
        var relevant = existingTeacherSessions
            .Where(s => s.TeacherProfileId == teacherProfileId)
            .Where(s => !s.IsDeleted && s.Status != SessionStatus.Cancelled)
            .Where(s => excludeSessionId == null || s.Id != excludeSessionId)
            .OrderBy(s => s.StartAt)
            .ToList();

        foreach (var other in relevant)
        {
            if (startAt < other.EndAt && other.StartAt < endAt)
            {
                conflicts.Add(new ScheduleConflict(
                    ConflictType.TeacherOverlap,
                    ConflictSeverity.Error,
                    $"Giáo viên bị trùng lịch {other.StartAt:HH:mm}–{other.EndAt:HH:mm}",
                    SessionIdA: excludeSessionId,
                    SessionIdB: other.Id));
            }
        }

        var daySessions = relevant
            .Where(s => s.StartAt.Date == startAt.Date || s.EndAt.Date == startAt.Date)
            .ToList();

        var candidate = new TeachingSession
        {
            Id = excludeSessionId ?? Guid.Empty,
            TeacherProfileId = teacherProfileId,
            FacilityId = facilityId,
            StartAt = startAt,
            EndAt = endAt
        };

        var timeline = daySessions
            .Concat(new[] { candidate })
            .OrderBy(s => s.StartAt)
            .ToList();

        for (var i = 0; i < timeline.Count - 1; i++)
        {
            var prev = timeline[i];
            var next = timeline[i + 1];
            if (prev.FacilityId == next.FacilityId) continue;

            var gap = (int)(next.StartAt - prev.EndAt).TotalMinutes;
            if (gap < 0) continue; // overlap already reported
            if (gap < minTravelMinutes)
            {
                conflicts.Add(new ScheduleConflict(
                    ConflictType.InsufficientTravelTime,
                    ConflictSeverity.Warning,
                    $"Chỉ còn {gap} phút di chuyển giữa hai cơ sở (cần {minTravelMinutes} phút)",
                    SessionIdA: prev.Id == Guid.Empty ? excludeSessionId : prev.Id,
                    SessionIdB: next.Id == Guid.Empty ? excludeSessionId : next.Id,
                    GapMinutes: gap,
                    RequiredMinutes: minTravelMinutes,
                    FromFacilityId: prev.FacilityId,
                    ToFacilityId: next.FacilityId));
            }
        }

        return conflicts;
    }

    public static bool HasBlockingErrors(IEnumerable<ScheduleConflict> conflicts) =>
        conflicts.Any(c => c.Severity == ConflictSeverity.Error);
}

public static class RateResolver
{
    public static TuitionRate? Resolve(
        IEnumerable<TuitionRate> rates,
        Guid studentId,
        Guid? classGroupId,
        ServiceType serviceType,
        DateOnly date)
    {
        var active = rates
            .Where(r => r.IsActive)
            .Where(r => r.EffectiveFrom <= date && (r.EffectiveTo == null || r.EffectiveTo >= date))
            .ToList();

        TuitionRate? Pick(Func<TuitionRate, bool> pred) =>
            active.Where(pred).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault();

        return Pick(r => r.Scope == TuitionRateScope.Student && r.StudentId == studentId)
               ?? Pick(r => r.Scope == TuitionRateScope.ClassGroup && r.ClassGroupId == classGroupId)
               ?? Pick(r => r.Scope == TuitionRateScope.ServiceType && r.ServiceType == serviceType)
               ?? Pick(r => r.Scope == TuitionRateScope.SystemDefault);
    }

    public static ServiceType DeriveServiceType(TeachingSession session)
    {
        if (session.ClassGroup != null)
            return session.ClassGroup.ServiceType;

        if (session.Facility == null)
            return ServiceType.AtCenter;

        return session.Facility.Type == FacilityType.MainCenter
            ? ServiceType.AtCenter
            : ServiceType.AtPartnerSchool;
    }
}
