namespace ECSPros.Order.Application.Services;

/// <summary>
/// FE1 tarih-sıra kuralı (plan §2.5, K3 varsayılan öneri — 2026-09-06): bizim serimizden kesilen faturada
/// (1) gelecek tarih yasak, (2) serinin son fatura tarihinden geriye kesim yasak, (3) seride daha yeni bir yıl
/// açıldıysa eski yıla kesim yasak. Takvim günü Türkiye saatine göre alınır (23:00 UTC = ertesi gün TR).
/// </summary>
public static class InvoiceDateRules
{
    private static readonly TimeZoneInfo Tr = ResolveTr();

    private static TimeZoneInfo ResolveTr()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch { return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "TR", "TR"); }
    }

    public static DateOnly TrDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tr));

    public static string? Validate(
        DateTime invoiceDateUtc,
        IReadOnlyCollection<(string Year, DateTime? LastInvoiceDate)> counters,
        DateTime nowUtc)
    {
        var date = TrDate(invoiceDateUtc);
        var today = TrDate(nowUtc);
        if (date > today)
            return $"Gelecek tarihli fatura kesilemez (fatura {date:dd.MM.yyyy}, bugün {today:dd.MM.yyyy}).";

        var maxYear = counters.Select(c => int.TryParse(c.Year, out var y) ? y : 0).DefaultIfEmpty(0).Max();
        if (maxYear > date.Year)
            return $"Bu seride {maxYear} yılına fatura kesildi; {date.Year} yılına geri dönülemez.";

        var same = counters.FirstOrDefault(c => c.Year == date.Year.ToString());
        if (same.LastInvoiceDate is { } last && date < TrDate(last))
            return $"Fatura tarihi serinin son fatura tarihinden ({TrDate(last):dd.MM.yyyy}) geri olamaz.";

        return null;
    }
}
