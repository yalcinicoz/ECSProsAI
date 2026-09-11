using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECSPros.Api.Services.AiReporting;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DynamicReportPlan
{
    public static bool SupportsSource(string? source) => source is "stock" or "orders" or MovementReportSource.Id or ReturnReportSource.Id or CustomerReportSource.Id or StaffActivitySource.Id or ProductCardReportSource.Id;
    public int Version { get; init; }
    public string? Source { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? Period { get; init; }
    public string? StockGrain { get; init; }
    public int? CardWindowMonths { get; init; }
    public ReportPredicate? Predicate { get; init; }
    public ReportAggregatePlan? Aggregate { get; init; }
    public ReportDetailPlan? Detail { get; init; }

    public static DynamicReportPlan Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 16_384)
            throw new ArgumentException("Dinamik rapor planı boyutu geçerli değil.");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 24 });
            if (ReportDefinitionValidator.HasDuplicateProperties(doc.RootElement)) throw new ArgumentException("Yinelenen rapor alanı.");
            var plan = JsonSerializer.Deserialize<DynamicReportPlan>(json, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 24 });
            if (plan?.Version != 2 || !SupportsSource(plan.Source) || (plan.Aggregate is null) == (plan.Detail is null))
                throw new ArgumentException("Dinamik rapor kaynağı/sürümü desteklenmiyor.");
            plan.ValidateCardWindow();
            if (plan.Source == "stock")
            {
                if (plan.StockGrain is not ("variant" or "location") || plan.From is not null || plan.To is not null || plan.Period is not null)
                    throw new ArgumentException("Güncel stokta varyant/konum kapsamı seçilmeli; dönem yalnız kart tarihi filtresinde kullanılabilir.");
            }
            else
            {
                if (plan.StockGrain is not null) throw new ArgumentException("Bu kaynakta stok kapsamı kullanılamaz.");
                _ = plan.Scope();
            }
            plan.Aggregate?.ValidateResultLimit();
            return plan;
        }
        catch (JsonException ex) { throw new ArgumentException("Dinamik rapor planı geçerli değil.", ex); }
    }

    public OrderReportScope Scope()
    {
        static DateTimeOffset Date(string? value)
        {
            if (value is null || !value.Contains('T') || !(value.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(value, @"[+-]\d{2}:\d{2}$"))
                || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new ArgumentException("Açık saat dilimli tarih gerekli.");
            return date;
        }
        var from = Date(From); var to = Date(To);
        if (to <= from || to - from > TimeSpan.FromDays(366)) throw new ArgumentException("Tarih aralığı en çok366 gün olmalı.");
        // Keep the fixed fallback valid too: the user can switch a saved relative recipe back to it.
        if (Period is not null) return ReportRelativePeriod.Resolve(Period, DateTimeOffset.UtcNow);
        return new(from, to);
    }

    public void ValidateCardWindow()
    {
        if (CardWindowMonths is not null && (Source != ProductCardReportSource.Id || CardWindowMonths is < 1 or > 12))
            throw new ArgumentException("Kart açılışı gözlem süresi yalnız ürün kartlarında 1..12 ay olabilir.");
    }
}
