using System.Text.RegularExpressions;
using ECSPros.Api.Services.Marketplace.Reference;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services.Marketplace.Send;

public sealed record ClassifiedError(string Code, string? SuggestedCategoryExternalId);

/// <summary>
/// Pazaryeri hata sınıflandırıcı (§4.3): ham hata metni → normalize ErrorCode + (kategori
/// çakışmasında) beklenen kategori. Önce DB kalıpları (marketplace_error_patterns — kod
/// değişikliği gerektirmeden genişletilebilir), sonra yerleşik varsayılanlar denenir.
/// Yakalanan kategori ADI referans DB'de kimliğe çözülür (yaprak tercih edilir).
/// </summary>
public sealed class MarketplaceErrorClassifier(
    NpgsqlDataSource mainDb,
    MarketplaceRefDb refDb,
    IMemoryCache cache)
{
    private static readonly TimeSpan PatternCacheTtl = TimeSpan.FromMinutes(5);

    // Yerleşik varsayılanlar — Trendyol Türkçe hata metinleri hedeflenmiştir; DB kalıpları önce gelir.
    private static readonly (string Pattern, string Code, int CategoryGroup)[] Builtin =
    [
        (@"beklenen kategori[:\s]+([^.|]+)", "category_conflict", 1),
        (@"(daha önce|önceden).{0,80}(farklı|başka).{0,40}kategori", "category_conflict", 0),
        (@"kategori(si)? değiştirilemez", "category_conflict", 0),
        (@"zorunlu.{0,60}(özellik|attribute)|(özellik|attribute).{0,40}zorunlu", "missing_attribute", 0),
        (@"geçersiz.{0,40}(değer|value)|invalid.{0,20}value", "invalid_value", 0),
        (@"barkod.{0,60}(kayıtlı|mevcut|kullanıl)", "duplicate_barcode", 0),
        (@"too many request|rate limit|çok fazla istek", "rate_limited", 0),
        (@"marka|brand", "invalid_brand", 0),
    ];

    public async Task<ClassifiedError> ClassifyAsync(
        string marketplace, string? errorText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(errorText))
            return new ClassifiedError("unknown", null);

        foreach (var (pattern, code, group) in await GetPatternsAsync(marketplace, ct))
        {
            Match match;
            try { match = Regex.Match(errorText, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException) { continue; } // bozuk DB kalıbı sınıflandırmayı düşürmesin
            catch (RegexMatchTimeoutException) { continue; }
            if (!match.Success) continue;

            string? suggestedId = null;
            if (group > 0 && match.Groups.Count > group && match.Groups[group].Success)
                suggestedId = await ResolveCategoryIdAsync(marketplace, match.Groups[group].Value.Trim(), ct);
            return new ClassifiedError(code, suggestedId);
        }
        return new ClassifiedError("unknown", null);
    }

    private async Task<List<(string Pattern, string Code, int Group)>> GetPatternsAsync(
        string marketplace, CancellationToken ct)
    {
        var cacheKey = $"mp-error-patterns:{marketplace}";
        if (cache.TryGetValue(cacheKey, out List<(string, string, int)>? cached) && cached is not null)
            return cached;

        var patterns = new List<(string, string, int)>();
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT "Pattern", "ErrorCode", "SuggestedCategoryGroup"
            FROM integration.marketplace_error_patterns
            WHERE "Marketplace"=$1 AND "IsActive" AND NOT "IsDeleted"
            ORDER BY "SortOrder"
            """))
        {
            cmd.Parameters.AddWithValue(marketplace);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                patterns.Add((r.GetString(0), r.GetString(1), r.GetInt32(2)));
        }
        patterns.AddRange(Builtin.Select(b => (b.Pattern, b.Code, b.CategoryGroup)));
        cache.Set(cacheKey, patterns, PatternCacheTtl);
        return patterns;
    }

    private async Task<string?> ResolveCategoryIdAsync(string marketplace, string name, CancellationToken ct)
    {
        if (name.Length is 0 or > 200) return null;
        var ds = await refDb.GetAsync(ct);
        if (ds is null) return null;
        await using var cmd = ds.CreateCommand(
            """
            SELECT external_id FROM mp_categories
            WHERE marketplace=$1 AND removed_at IS NULL AND lower(name)=lower($2)
            ORDER BY is_leaf DESC LIMIT 1
            """);
        cmd.Parameters.AddWithValue(marketplace);
        cmd.Parameters.AddWithValue(name);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }
}
