using ECSPros.Shared.Contracts;
using Npgsql;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// Popüler aramalar (2026-09-01, kullanıcı kararı) — iki parça:
/// AramaTerimIzleyici: /urunler ve store products aramalarını gün kovalı sayaca yazar
/// (istek akışında await edilir; bot UA ve sayfa>1 sayılmaz; hata aramayı asla etkilemez).
/// PopulerAramaServisi: son 30 günün en çok arananları (eşik ≥3) + veri azken tohum liste
/// (Store:PopularSearchSeed, yoksa dropdown'ın eski statik chip listesi).
/// </summary>
public sealed class AramaTerimIzleyici(NpgsqlDataSource dataSource, AramaKufurFiltresi kufur, ILogger<AramaTerimIzleyici> logger)
{
    /// <param name="visitorHash">Ziyaretçi anahtarı (AramaKufurFiltresi.ZiyaretciAnahtari) — "birden fazla kişi" ölçütü.</param>
    /// <param name="resultCount">Aramanın getirdiği sonuç sayısı — yalnız sonuç getirenler popüler listeye girer.
    /// 2026-09-07: kayıt artık sorgudan SONRA yapılır; küfür içeren terim hiç yazılmaz.</param>
    public async Task KaydetAsync(
        Guid firmPlatformId, string? terim, string? userAgent, string visitorHash, int resultCount, CancellationToken ct = default)
    {
        var t = Normalize(terim);
        if (t is null || firmPlatformId == Guid.Empty) return;
        if (TrackingScriptProvider.BotMu(userAgent)) return;
        if (kufur.Engelli(t)) return;
        visitorHash = (visitorHash ?? "").Length > 32 ? visitorHash![..32] : visitorHash ?? "";

        try
        {
            using var yazmaSuresi = CancellationTokenSource.CreateLinkedTokenSource(ct);
            yazmaSuresi.CancelAfter(TimeSpan.FromSeconds(2));
            var yazmaCt = yazmaSuresi.Token;
            await using var conn = await dataSource.OpenConnectionAsync(yazmaCt);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO storefront.search_term_stats
                    ("Id","FirmPlatformId","Term","Day","Count","VisitorHash","ResultCount","CreatedAt","IsDeleted")
                VALUES (gen_random_uuid(), $1, $2, current_date, 1, $3, $4, now(), false)
                ON CONFLICT ("FirmPlatformId","Term","Day","VisitorHash")
                DO UPDATE SET "Count" = storefront.search_term_stats."Count" + 1,
                              "ResultCount" = GREATEST(COALESCE(storefront.search_term_stats."ResultCount", 0), EXCLUDED."ResultCount"),
                              "UpdatedAt" = now()
                """, conn);
            cmd.Parameters.AddWithValue(firmPlatformId);
            cmd.Parameters.AddWithValue(t);
            cmd.Parameters.AddWithValue(visitorHash);
            cmd.Parameters.AddWithValue(Math.Max(0, resultCount));
            await cmd.ExecuteNonQueryAsync(yazmaCt);

            // Fırsatçı temizlik (~%1): 90 günden eski kovalar düşer — ayrı iş/worker gerekmez.
            if (Random.Shared.Next(100) == 0)
            {
                await using var prune = new NpgsqlCommand(
                    "DELETE FROM storefront.search_term_stats WHERE \"Day\" < current_date - 90", conn);
                await prune.ExecuteNonQueryAsync(yazmaCt);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Arama terimi sayacı yazılamadı (terim: {Terim})", t);
        }
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
    ICacheService cache,
    AramaKufurFiltresi kufur,
    ILogger<PopulerAramaServisi> logger)
{
    // Dropdown'ın 2026-09-01 öncesi statik chip listesi — gerçek veri birikene kadar tohum.
    private static readonly string[] VarsayilanTohum =
        ["elbise", "tunik", "şal", "gömlek", "bluz", "pantolon", "triko", "etek", "hırka", "pijama"];

    public async Task<IReadOnlyList<string>> GetirAsync(Guid firmPlatformId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 20);
        var anahtar = $"populer-arama:{firmPlatformId:N}";
        var terimler = await cache.GetAsync<List<string>>(anahtar, ct);
        if (terimler is null)
        {
            terimler = [];
            try
            {
                await using var conn = await dataSource.OpenConnectionAsync(ct);
                // 2026-09-07 ölçütleri: son 30 gün, ≥3 arama, EN AZ 2 FARKLI ZİYARETÇİ, en az bir aramada SONUÇ var.
                // Eski kayıtlar (ResultCount NULL / VisitorHash boş) yeni veri birikene dek listeye girmez — tohum tamamlar.
                await using var cmd = new NpgsqlCommand("""
                    SELECT "Term" FROM storefront.search_term_stats
                    WHERE "FirmPlatformId" = $1 AND "Day" >= current_date - 30
                    GROUP BY "Term"
                    HAVING SUM("Count") >= 3
                       AND COUNT(DISTINCT NULLIF("VisitorHash", '')) >= 2
                       AND COALESCE(MAX("ResultCount"), 0) > 0
                    ORDER BY SUM("Count") DESC, "Term" LIMIT 40
                    """, conn);
                cmd.Parameters.AddWithValue(firmPlatformId);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var terim = reader.GetString(0);
                    if (kufur.Engelli(terim) || terimler.Count >= 20) continue; // liste sonradan genişlese de süzülür
                    terimler.Add(terim);
                }
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
            await cache.SetAsync(anahtar, terimler, TimeSpan.FromMinutes(5), ct);
        }
        return terimler.Take(limit).ToList();
    }
}
