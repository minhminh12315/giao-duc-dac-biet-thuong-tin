namespace Secms.Domain.Enums;

public enum FacilityType
{
    MainCenter = 0,
    PartnerSchool = 1
}

public enum ServiceType
{
    AtCenter = 0,
    AtPartnerSchool = 1
}

public enum StudentStatus
{
    Active = 0,
    Inactive = 1,
    Graduated = 2
}

public enum SessionStatus
{
    Scheduled = 0,
    Completed = 1,
    Cancelled = 2
}

public enum AttendanceStatus
{
    NotMarked = 0,
    Present = 1,
    Absent = 2,
    Excused = 3
}

public enum TeacherDocumentType
{
    Contract = 0,
    Certificate = 1,
    Other = 2
}

public enum StudentDocumentType
{
    MedicalReport = 0,
    Assessment = 1,
    Other = 2
}

public enum TuitionRateScope
{
    SystemDefault = 0,
    ServiceType = 1,
    ClassGroup = 2,
    Student = 3
}

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Void = 2
}

public enum ConflictType
{
    TeacherOverlap = 0,
    InsufficientTravelTime = 1,
    Other = 2
}

public enum ConflictSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Teacher = "Teacher";
}
