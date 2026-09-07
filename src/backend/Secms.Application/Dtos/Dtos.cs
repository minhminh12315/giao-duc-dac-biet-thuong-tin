using Secms.Domain.Enums;
using Secms.Domain.Services;

namespace Secms.Application.Dtos;

public record LoginRequest(string UserName, string Password);
public record LoginResponse(string AccessToken, int ExpiresIn, string Role, string DisplayName, string UserId, Guid? TeacherProfileId);
public record MeResponse(string UserId, string UserName, string Email, string Role, string DisplayName, Guid? TeacherProfileId);

public record FacilityDto(Guid Id, string Name, FacilityType Type, string? Address, string? ContactPhone, int? TravelBufferMinutesOverride, bool IsActive);
public record UpsertFacilityRequest(string Name, FacilityType Type, string? Address, string? ContactPhone, int? TravelBufferMinutesOverride, bool IsActive = true);

public record TeacherDto(Guid Id, string UserId, string UserName, string Email, string FullName, string? Phone, string? Specialization, DateOnly? HireDate, bool IsActive);
public record CreateTeacherRequest(string UserName, string Email, string Password, string FullName, string? Phone, string? Specialization, DateOnly? HireDate, string? Address, string? Notes);
public record UpdateTeacherRequest(string FullName, string? Phone, string? Specialization, DateOnly? HireDate, string? Address, string? Notes, bool IsActive);

public record StudentDto(Guid Id, string FullName, DateOnly? DateOfBirth, string? Gender, Guid FacilityId, string FacilityName, FacilityType FacilityType, string? GuardianName, string? GuardianPhone, string? MedicalNotes, StudentStatus Status);
public record UpsertStudentRequest(string FullName, DateOnly? DateOfBirth, string? Gender, Guid FacilityId, string? GuardianName, string? GuardianPhone, string? MedicalNotes, StudentStatus Status = StudentStatus.Active);

public record ClassGroupDto(Guid Id, string Name, Guid? FacilityId, string? FacilityName, ServiceType ServiceType, bool IsActive, List<Guid> StudentIds);
public record UpsertClassGroupRequest(string Name, Guid? FacilityId, ServiceType ServiceType, bool IsActive = true);
public record SetClassGroupStudentsRequest(List<Guid> StudentIds);

public record DocumentDto(Guid Id, string DocumentType, string FileName, string ContentType, long SizeBytes, DateTimeOffset UploadedAt, string StoragePath);

public record SessionDto(
    Guid Id,
    Guid? ClassGroupId,
    string? ClassGroupName,
    Guid TeacherProfileId,
    string TeacherName,
    Guid FacilityId,
    string FacilityName,
    FacilityType FacilityType,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Title,
    SessionStatus Status,
    DateTimeOffset? AttendanceFinalizedAt,
    string? Notes,
    List<Guid> StudentIds,
    List<ScheduleConflictDto>? Conflicts);

public record UpsertSessionRequest(
    Guid? ClassGroupId,
    Guid TeacherProfileId,
    Guid FacilityId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Title,
    string? Notes,
    List<Guid>? StudentIds,
    bool Force = false);

public record ScheduleConflictDto(
    ConflictType Type,
    ConflictSeverity Severity,
    string Message,
    Guid? SessionIdA,
    Guid? SessionIdB,
    int? GapMinutes,
    int? RequiredMinutes);

public record CloneWeekRequest(DateOnly SourceWeekStart, DateOnly TargetWeekStart, Guid? TeacherId = null, bool Force = false, bool SkipOnError = true);
public record CloneWeekResult(int Created, int Skipped, List<ScheduleConflictDto> Warnings);

public record AttendanceItemDto(Guid StudentId, string StudentName, AttendanceStatus Status, string? Comment, short? MoodOrLevel, List<string>? Tags);
public record SaveAttendanceRequest(List<AttendanceItemInput> Items);
public record AttendanceItemInput(Guid StudentId, AttendanceStatus Status, string? Comment, short? MoodOrLevel, List<string>? Tags);

public record ProgressDto(
    Guid StudentId,
    string StudentName,
    int PresentCount,
    int AbsentCount,
    int ExcusedCount,
    double AttendanceRate,
    int CommentCount,
    double? AvgMood,
    List<MoodPointDto> MoodSeries,
    List<CommentFeedItemDto> Comments);

public record MoodPointDto(DateTimeOffset At, short Mood);
public record CommentFeedItemDto(DateTimeOffset At, string TeacherName, string Content, short? MoodOrLevel, AttendanceStatus Status);

public record TuitionRateDto(Guid Id, TuitionRateScope Scope, Guid? StudentId, Guid? ClassGroupId, ServiceType? ServiceType, decimal Amount, string Currency, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);
public record UpsertTuitionRateRequest(TuitionRateScope Scope, Guid? StudentId, Guid? ClassGroupId, ServiceType? ServiceType, decimal Amount, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive = true);

public record InvoiceDto(Guid Id, Guid StudentId, string StudentName, int PeriodYear, int PeriodMonth, decimal TotalAmount, string Currency, InvoiceStatus Status, int Version, DateTimeOffset GeneratedAt, List<InvoiceLineDto> Lines);
public record InvoiceLineDto(Guid Id, Guid SessionId, DateTimeOffset SessionStart, decimal UnitRate, decimal Amount, string? Description);
public record GenerateInvoiceRequest(Guid StudentId, int Year, int Month, bool Regenerate = false);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public static class ConflictMapper
{
    public static ScheduleConflictDto ToDto(ScheduleConflict c) => new(
        c.Type, c.Severity, c.Message, c.SessionIdA, c.SessionIdB, c.GapMinutes, c.RequiredMinutes);
}
