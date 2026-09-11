using System.Text.Json.Serialization;

namespace ECSPros.Api.Services.AiReporting;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportAggregatePlan
{
    public string[]? Dimensions { get; init; }
    public string[]? Measures { get; init; }
    public string? Sort { get; init; }
    public string Direction { get; init; } = "asc";
    public int? Top { get; init; }

    public void ValidateResultLimit()
    {
        if (Top is < 1 or > 1000 || Top is not null && (string.IsNullOrWhiteSpace(Sort) || Dimensions is not { Length: > 0 }))
            throw new ArgumentException("İlk N sonucu için 1–1000 sınırı, açık sıralama ve gruplama gerekli.");
    }
}

// Fixed transport slots keep projections translatable by EF; column IDs come from the validated plan.
public sealed record ReportAggregateKey
{
    public string? D0 { get; init; }
    public string? D1 { get; init; }
    public string? D2 { get; init; }
    public string? D3 { get; init; }
}

public sealed record ReportAggregateRow
{
    public string? D0 { get; init; }
    public string? D1 { get; init; }
    public string? D2 { get; init; }
    public string? D3 { get; init; }
    public decimal M0 { get; init; }
    public decimal M1 { get; init; }
    public decimal M2 { get; init; }
    public decimal M3 { get; init; }
}

public sealed record ReportAggregateQuery(IReadOnlyList<string> Columns, IQueryable<ReportAggregateRow> Rows);
