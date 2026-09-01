using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Popüler aramalar (2026-09-01, kullanıcı kararı) — iki parça:
/// AramaTerimIzleyici: /urunler ve store products aramalarını gün kovalı sayaca yazar
/// (fire-and-forget; bot UA ve sayfa>1 sayılmaz; hata aramayı asla etkilemez).
/// PopulerAramaServisi: son 30 günün en çok arananları (eşik ≥3) + veri azken tohum liste
/// (Store:PopularSearchSeed, yoksa dropdown'ın eski statik chip listesi).
/// </summary>
public sealed class AramaTerimIzleyici(NpgsqlDataSource dataSource, ILogger<AramaTerimIzleyici> logger)
{
    public void Kaydet(Guid firmPlatformId, string? terim, string? userAgent)
    {
        var t = Normalize(terim);
        if (t is null || firmPlatformId == Guid.Empty) return;
        if (TrackingScriptProvider.BotMu(userAgent)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var conn = await dataSource.OpenConnectionAsync();
                await using var cmd = new NpgsqlCommand("""
                    INSERT INTO storefront.search_term_stats
                        ("Id","FirmPlatformId","Term","Day","Count","CreatedAt","IsDeleted")
                    VALUES (gen_random_uuid(), $1, $2, current_date, 1, now(), false)
                    ON CONFLICT ("FirmPlatformId","Term","Day")
                    DO UPDATE SET "Count" = storefront.search_term_stats."Count" + 1, "UpdatedAt" = now()
                    """, conn);
                cmd.Parameters.AddWithValue(firmPlatformId);
                cmd.Parameters.AddWithValue(t);
                await cmd.ExecuteNonQueryAsync();

                // Fırsatçı temizlik (~%1): 90 günden eski kovalar düşer — ayrı iş/worker gerekmez.
                if (Random.Shared.Next(100) == 0)
                {
                    await using var prune = new NpgsqlCommand(
                        "DELETE FROM storefront.search_term_stats WHERE \"Day\" < current_date - 90", conn);
                    await prune.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Arama terimi sayacı yazılamadı (terim: {Terim})", t);
            }
        });
    }

    /// <summary>trim + küçük harf (invariant) + tek boşluk; 2-60 karakter ve en az bir harf şartı.</summary>
    internal static string? Normalize(string? terim)
    {
        if (string.IsNullOrWhiteSpace(terim)) return null;
        var t = string.Join(' ', terim.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (t.Length is < 2 or > 60) return null;
        return t.Any(char.IsLetter) ? t : null;
    }
}

public sealed class PopulerAramaServisi(
    NpgsqlDataSource dataSource,
    IConfiguration configuration,
    Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
    ILogger<PopulerAramaServisi> logger)
{
    // Dropdown'ın 2026-09-01 öncesi statik chip listesi — gerçek veri birikene kadar tohum.
    private static readonly string[] VarsayilanTohum =
        ["elbise", "tunik", "şal", "gömlek", "bluz", "pantolon", "triko", "etek", "hırka", "pijama"];

    public async Task<IReadOnlyList<string>> GetirAsync(Guid firmPlatformId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 20);
        var anahtar = $"populer-arama:{firmPlatformId:N}";
        if (!cache.TryGetValue(anahtar, out List<string>? terimler) || terimler is null)
        {
            terimler = [];
            try
            {
                await using var conn = await dataSource.OpenConnectionAsync(ct);
                await using var cmd = new NpgsqlCommand("""
                    SELECT "Term" FROM storefront.search_term_stats
                    WHERE "FirmPlatformId" = $1 AND "Day" >= current_date - 30
                    GROUP BY "Term" HAVING SUM("Count") >= 3
                    ORDER BY SUM("Count") DESC, "Term" LIMIT 20
                    """, conn);
                cmd.Parameters.AddWithValue(firmPlatformId);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) terimler.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Popüler aramalar okunamadı — tohum listeyle devam.");
            }

            // Gerçek veri 20'yi doldurmuyorsa tohumla tamamla (tekrarsız) — mobil/web sözleşmesi hep dolu döner.
            var tohum = configuration.GetSection("Store:PopularSearchSeed").Get<string[]>() ?? VarsayilanTohum;
            foreach (var s in tohum)
            {
                if (terimler.Count >= 20) break;
                if (!terimler.Contains(s, StringComparer.OrdinalIgnoreCase)) terimler.Add(s);
            }
            cache.Set(anahtar, terimler, TimeSpan.FromMinutes(5));
        }
        return terimler.Take(limit).ToList();
    }
}
