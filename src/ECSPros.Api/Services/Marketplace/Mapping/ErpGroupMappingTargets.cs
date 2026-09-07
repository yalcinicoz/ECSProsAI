using System.Text.Json;

namespace ECSPros.Api.Services.Marketplace.Mapping;

/// <summary>One target projection for dictionary visibility, deletion protection and inbound lookup.</summary>
public static class ErpGroupMappingTargets
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? ValidatePool(IReadOnlyList<PoolTargetDto>? pool)
    {
        if (pool is not { Count: > 1 }) return "Havuz eşlemesinde en az iki aday kategori seçilmeli.";
        if (pool.Any(p => string.IsNullOrWhiteSpace(p.ExternalId))
            || pool.Select(p => p.ExternalId.Trim()).Distinct(StringComparer.Ordinal).Count() != pool.Count)
            return "Havuz adayları boş olamaz ve farklı kodlardan oluşmalıdır.";
        return null;
    }

    public static string[] Read(string kind, string? target, string? rulesJson, string? poolJson)
    {
        IEnumerable<string?> codes = kind switch
        {
            "direct" => [target],
            "rules" => new[] { target }.Concat(
                (JsonSerializer.Deserialize<List<MappingRuleDto>>(rulesJson ?? "[]", JsonOptions) ?? [])
                .Select(r => r.TargetExternalId)),
            "pool" => (JsonSerializer.Deserialize<List<PoolTargetDto>>(poolJson ?? "[]", JsonOptions) ?? [])
                .Select(p => p.ExternalId),
            _ => throw new InvalidOperationException($"Geçersiz ERP eşleme kipi: {kind}")
        };
        return codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim())
            .Distinct(StringComparer.Ordinal).ToArray();
    }

    public static Guid? Resolve(IEnumerable<Guid> candidates)
    {
        var groups = candidates.Distinct().Take(2).ToArray();
        if (groups.Length > 1) throw new InvalidOperationException("ERP kodu birden fazla ürün grubuna eşli; çakışma çözülmeden aktarım yapılamaz.");
        return groups.Length == 1 ? groups[0] : null;
    }
}
