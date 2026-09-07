namespace Secms.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public abstract class SoftDeletableEntity : BaseEntity
{
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public class TeacherProfile : SoftDeletableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Specialization { get; set; }
    public DateOnly? HireDate { get; set; }
    public string? Notes { get; set; }

    public ICollection<TeacherDocument> Documents { get; set; } = new List<TeacherDocument>();
    public ICollection<TeachingSession> Sessions { get; set; } = new List<TeachingSession>();
}

public class Facility : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public Enums.FacilityType Type { get; set; }
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
    public int? TravelBufferMinutesOverride { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<TeachingSession> Sessions { get; set; } = new List<TeachingSession>();
}

public class Student : SoftDeletableEntity
{
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? MedicalNotes { get; set; }
    public Enums.StudentStatus Status { get; set; } = Enums.StudentStatus.Active;

    public ICollection<StudentDocument> Documents { get; set; } = new List<StudentDocument>();
    public ICollection<ClassGroupStudent> ClassGroupStudents { get; set; } = new List<ClassGroupStudent>();
}

public class TeacherDocument : BaseEntity
{
    public Guid TeacherProfileId { get; set; }
    public TeacherProfile TeacherProfile { get; set; } = null!;
    public Enums.TeacherDocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UploadedByUserId { get; set; } = string.Empty;
}

public class StudentDocument : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Enums.StudentDocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string UploadedByUserId { get; set; } = string.Empty;
}

public class ClassGroup : SoftDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public Enums.ServiceType ServiceType { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ClassGroupStudent> ClassGroupStudents { get; set; } = new List<ClassGroupStudent>();
}

public class ClassGroupStudent
{
    public Guid ClassGroupId { get; set; }
    public ClassGroup ClassGroup { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeftAt { get; set; }
}

public class TeachingSession : SoftDeletableEntity
{
    public Guid? ClassGroupId { get; set; }
    public ClassGroup? ClassGroup { get; set; }
    public Guid TeacherProfileId { get; set; }
    public TeacherProfile TeacherProfile { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string? Title { get; set; }
    public Enums.SessionStatus Status { get; set; } = Enums.SessionStatus.Scheduled;
    public DateTimeOffset? AttendanceFinalizedAt { get; set; }
    public string? Notes { get; set; }
    public Guid? SourceCloneSessionId { get; set; }

    public ICollection<SessionStudent> SessionStudents { get; set; } = new List<SessionStudent>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}

public class SessionStudent
{
    public Guid SessionId { get; set; }
    public TeachingSession Session { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}

public class AttendanceRecord : BaseEntity
{
    public Guid SessionId { get; set; }
    public TeachingSession Session { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Enums.AttendanceStatus Status { get; set; } = Enums.AttendanceStatus.NotMarked;
    public DateTimeOffset? MarkedAt { get; set; }
    public string? MarkedByUserId { get; set; }
    public TeachingComment? Comment { get; set; }
}

public class TeachingComment : BaseEntity
{
    public Guid AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public short? MoodOrLevel { get; set; }
    public string? TagsJson { get; set; }
}

public class TuitionRate : BaseEntity
{
    public Enums.TuitionRateScope Scope { get; set; }
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }
    public Guid? ClassGroupId { get; set; }
    public ClassGroup? ClassGroup { get; set; }
    public Enums.ServiceType? ServiceType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Invoice : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public Enums.InvoiceStatus Status { get; set; } = Enums.InvoiceStatus.Issued;
    public int Version { get; set; } = 1;
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? GeneratedByUserId { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}

public class InvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public Guid SessionId { get; set; }
    public TeachingSession Session { get; set; } = null!;
    public Guid AttendanceRecordId { get; set; }
    public AttendanceRecord AttendanceRecord { get; set; } = null!;
    public decimal UnitRate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
