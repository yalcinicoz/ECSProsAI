using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECSPros.Api.Services.AiReporting;

// Logical IDs and literals only. No SQL, property paths, permissions or table names.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReportPredicate
{
    public string? Kind { get; init; }
    public string? Field { get; init; }
    public string? Operator { get; init; }
    public string[]? Values { get; init; }
    public string? Relation { get; init; }
    public ReportPredicate[]? Children { get; init; }

    public static ReportPredicate Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 16_384)
            throw new ArgumentException("Rapor koşulu boyutu geçerli değil.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 20 });
            if (ReportDefinitionValidator.HasDuplicateProperties(document.RootElement))
                throw new ArgumentException("Rapor koşulunda yinelenen alan var.");
            return JsonSerializer.Deserialize<ReportPredicate>(json, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 20 })
                ?? throw new ArgumentException("Rapor koşulu boş.");
        }
        catch (JsonException ex) { throw new ArgumentException("Rapor koşulu geçerli değil.", ex); }
    }
}
