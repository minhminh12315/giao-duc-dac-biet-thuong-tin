namespace Secms.Infrastructure;

public static class TimeZones
{
    public static TimeZoneInfo Vietnam { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");

    public static DateOnly ToVietnamDate(DateTimeOffset utcOrAny) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcOrAny, Vietnam).DateTime);

    public static DateTimeOffset VietnamDayStartUtc(DateOnly localDate)
    {
        var local = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Vietnam), TimeSpan.Zero);
    }
}
