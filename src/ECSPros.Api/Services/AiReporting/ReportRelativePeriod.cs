namespace ECSPros.Api.Services.AiReporting;

public static class ReportRelativePeriod
{
    public static OrderReportScope Resolve(string period, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var month = new DateTime(today.Year, today.Month, 1);
        var (from, to) = period switch
        {
            "thisMonth" => (month, month.AddMonths(1)),
            "lastMonth" => (month.AddMonths(-1), month),
            "last30Days" => (today.AddDays(-29), today.AddDays(1)),
            "last6Months" => (month.AddMonths(-5), month.AddMonths(1)),
            _ => throw new ArgumentException("Göreli rapor dönemi desteklenmiyor.")
        };
        DateTimeOffset Utc(DateTime value) => new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), zone));
        return new(Utc(from), Utc(to));
    }
}
