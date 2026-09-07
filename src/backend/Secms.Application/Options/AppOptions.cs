namespace Secms.Application.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = "SecmsDevSecretKey_ChangeInProduction_AtLeast32Chars!";
    public string Issuer { get; set; } = "Secms";
    public string Audience { get; set; } = "Secms";
    public int AccessTokenMinutes { get; set; } = 480;
}

public class SchedulingOptions
{
    public const string SectionName = "Scheduling";
    public int MinTravelMinutes { get; set; } = 30;
    public string WeekStartsOn { get; set; } = "Monday";
    public string WorkDayStart { get; set; } = "07:00";
    public string WorkDayEnd { get; set; } = "18:00";
    public bool AllowForceSaveWithConflicts { get; set; } = true;
}

public class AttendanceOptions
{
    public const string SectionName = "Attendance";
    public bool RequireCommentOnFinalize { get; set; } = true;
    public int EditWindowHours { get; set; } = 24;
    public bool AdminCanAlwaysEdit { get; set; } = true;
}

public class StorageOptions
{
    public const string SectionName = "Storage";
    public string Provider { get; set; } = "Local";
    public string LocalRoot { get; set; } = "App_Data/uploads";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
}
