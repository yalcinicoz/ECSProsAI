using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace ECSPros.Api.Services.Marketplace.Reference;

public sealed record RefSyncRunDto(
    Guid Id, string Marketplace, string Scope, string Status,
    DateTime StartedAt, DateTime? FinishedAt,
    int? TotalCategories, int ProcessedCategories,
    int AddedCount, int ChangedCount, int RemovedCount, int UnchangedCount,
    string? Error);

public sealed record RefSummaryDto(
    string Marketplace, long CategoryCount, long AttributeCount, long ValueCount,
    long RemovedCategoryCount, RefSyncRunDto? LastRun,
    // RF1 (2026-08-31): özellik kapsamı — yaprak kategorilerin kaçı taranmış (attributes_synced_at
    // damgalı) ve en eski tarama ne zaman ("her an hazır" ilkesinin panel göstergesi).
    long LeafCount = 0, long LeafSyncedCount = 0, DateTime? OldestAttributeSyncAt = null);

/// <summary>
/// Referans senkron motoru (docs/pazaryeri-entegrasyon-veri-yonetimi.md §1):
/// full snapshot + hash diff — değişmeyen satıra UPDATE atılmaz; kaybolan kayıt
/// hard-delete edilmez, removed_at alır; her fark mp_change_log'a olay olarak düşer
/// (eşleme sağlık job'ının F2'deki girdisi). Koşular arka planda yürür, durum
/// mp_sync_runs'tan izlenir; kesilen koşu heartbeat yaşlanınca failed sayılır.
/// </summary>
public sealed class MarketplaceReferenceSyncService(
    MarketplaceRefDb refDb,
    IEnumerable<IMarketplaceReferenceDownloader> downloaders,
    IHostApplicationLifetime lifetime,
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<MarketplaceReferenceSyncService> logger)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(5);
    private const int HeartbeatEvery = 20; // kategori-özellik döngüsünde ilerleme yazma adımı

    // Aynı pazaryeri için eşzamanlı ikinci koşuyu süreç içinde de engelle (DB kontrolüne ek).
    private readonly ConcurrentDictionary<string, Guid> _running = new();

    public IReadOnlyList<string> SupportedMarketplaces =>
        downloaders.Select(d => d.ServiceCode).OrderBy(x => x).ToList();

    // ── Koşu başlatma ────────────────────────────────────────────────────────

    public async Task<(Guid? RunId, string? Error)> StartAsync(
        string marketplace, string scope, List<string>? categoryIds, CancellationToken ct)
    {
        var downloader = downloaders.FirstOrDefault(d => d.ServiceCode == marketplace);
        if (downloader is null)
            return (null, $"'{marketplace}' için referans indirici yok. Desteklenenler: {string.Join(", ", SupportedMarketplaces)}");
        // RF1 (2026-08-31): 'attributes-missing' — yalnız hiç taranmamış ya da bayat (Trendyol:
        // ReferenceStaleDays, vars. 7 gün) yaprak kategorilerin özellikleri; kesinti sonrası
        // kaldığı yerden devam ve haftalık tazeleme bu modla verimli çalışır.
        if (scope is not ("categories" or "attributes" or "attributes-missing"))
            return (null, "scope 'categories', 'attributes' veya 'attributes-missing' olmalı.");

        var ds = await refDb.GetAsync(ct);
        if (ds is null)
            return (null, refDb.IsConfigured
                ? "Referans veritabanına (marketplace_ref) erişilemiyor."
                : "Referans veritabanı yapılandırılmamış (ConnectionStrings:MarketplaceRef).");

        // Kesilen koşuları kapat, canlı koşu varsa reddet.
        await using (var stale = ds.CreateCommand(
            """
            UPDATE mp_sync_runs SET status='failed', error='Koşu kesildi (heartbeat yaşlandı).', finished_at=now()
            WHERE marketplace=$1 AND status='running' AND heartbeat_at < now() - $2
            """))
        {
            stale.Parameters.AddWithValue(marketplace);
            stale.Parameters.AddWithValue(StaleAfter);
            await stale.ExecuteNonQueryAsync(ct);
        }
        await using (var live = ds.CreateCommand(
            "SELECT count(*) FROM mp_sync_runs WHERE marketplace=$1 AND status='running'"))
        {
            live.Parameters.AddWithValue(marketplace);
            if ((long)(await live.ExecuteScalarAsync(ct))! > 0)
                return (null, $"'{marketplace}' için zaten süren bir referans senkronu var.");
        }

        var runId = Guid.NewGuid();
        if (!_running.TryAdd(marketplace, runId))
            return (null, $"'{marketplace}' için bu process içinde zaten süren bir referans senkronu var.");

        try
        {
            await using var ins = ds.CreateCommand(
                "INSERT INTO mp_sync_runs (id, marketplace, scope, status) VALUES ($1,$2,$3,'running')");
            ins.Parameters.AddWithValue(runId);
            ins.Parameters.AddWithValue(marketplace);
            ins.Parameters.AddWithValue(scope);
            await ins.ExecuteNonQueryAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            _running.TryRemove(new KeyValuePair<string, Guid>(marketplace, runId));
            return (null, $"'{marketplace}' için başka bir node üzerinde zaten süren bir referans senkronu var.");
        }
        catch
        {
            _running.TryRemove(new KeyValuePair<string, Guid>(marketplace, runId));
            throw;
        }

        // Arka planda yürüt — istek beklemez, durum mp_sync_runs'tan izlenir.
        _ = Task.Run(() => ExecuteAsync(runId, downloader, scope, categoryIds, lifetime.ApplicationStopping));
        return (runId, null);
    }

    /// <summary>Günlük worker checkpoint'i: başarılı koşu DB'de durduğu için restart/node değişiminde korunur.</summary>
    public async Task<bool> HasCompletedRunOnDayAsync(
        string marketplace, string scope, DateOnly utcDay, CancellationToken ct)
    {
        var ds = await refDb.GetAsync(ct);
        if (ds is null) return false;

        var startUtc = utcDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(1);
        await using var cmd = ds.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM mp_sync_runs
                WHERE marketplace=$1 AND scope=$2 AND status='completed'
                  AND finished_at >= $3 AND finished_at < $4
            )
            """);
        cmd.Parameters.AddWithValue(marketplace);
        cmd.Parameters.AddWithValue(scope);
        cmd.Parameters.AddWithValue(startUtc);
        cmd.Parameters.AddWithValue(endUtc);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private async Task ExecuteAsync(
        Guid runId, IMarketplaceReferenceDownloader downloader, string scope,
        List<string>? categoryIds, CancellationToken ct)
    {
        var marketplace = downloader.ServiceCode;
        try
        {
            var ds = await refDb.GetAsync(ct)
                ?? throw new InvalidOperationException("Referans veritabanına erişilemiyor.");

            var totals = scope == "categories"
                ? await SyncCategoriesAsync(ds, downloader, runId, ct)
                : await SyncAttributesAsync(ds, downloader, runId, categoryIds,
                    onlyMissingOrStale: scope == "attributes-missing", ct);

            await using var done = ds.CreateCommand(
                """
                UPDATE mp_sync_runs SET status='completed', finished_at=now(), heartbeat_at=now(),
                    added_count=$2, changed_count=$3, removed_count=$4, unchanged_count=$5, error=$6
                WHERE id=$1
                """);
            done.Parameters.AddWithValue(runId);
            done.Parameters.AddWithValue(totals.Added);
            done.Parameters.AddWithValue(totals.Changed);
            done.Parameters.AddWithValue(totals.Removed);
            done.Parameters.AddWithValue(totals.Unchanged);
            done.Parameters.AddWithValue((object?)totals.WarningSummary ?? DBNull.Value);
            await done.ExecuteNonQueryAsync(ct);

            logger.LogInformation(
                "Referans senkronu tamamlandı: {Marketplace}/{Scope} — +{Added} ~{Changed} -{Removed} ={Unchanged}",
                marketplace, scope, totals.Added, totals.Changed, totals.Removed, totals.Unchanged);

            // Değişiklikler eşleme sağlığına hemen işlensin (§2.5) — hata senkron sonucunu bozmaz.
            try
            {
                var health = serviceProvider.GetRequiredService<Mapping.MappingHealthService>();
                var sonuc = await health.ProcessAsync(marketplace, ct);
                // RF5: referans değişimi eşleme kırdıysa/işaretlediyse ürün hazırlıkları
                // elle tetiksiz tazelensin (tüm katalog — günde en çok bir kez, senkron sonrası).
                if (sonuc.BrokenCount + sonuc.ReviewCount > 0)
                {
                    using var kapsam = serviceProvider.CreateScope();
                    await kapsam.ServiceProvider.GetRequiredService<Mapping.MarketplaceMappingService>()
                        .ReadinessTetikleAsync(marketplace, null, ct);
                }
            }
            catch (Exception hex)
            {
                logger.LogWarning(hex, "Eşleme sağlık taraması senkron sonrası çalıştırılamadı: {Marketplace}", marketplace);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Referans senkronu başarısız: {Marketplace}/{Scope}", marketplace, scope);
            try
            {
                var ds = await refDb.GetAsync(CancellationToken.None);
                if (ds is not null)
                {
                    await using var fail = ds.CreateCommand(
                        "UPDATE mp_sync_runs SET status='failed', finished_at=now(), error=$2 WHERE id=$1");
                    fail.Parameters.AddWithValue(runId);
                    fail.Parameters.AddWithValue(ex.Message);
                    await fail.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
            catch { /* koşu kaydı güncellenemedi — stale tespiti kapatır */ }
        }
        finally
        {
            _running.TryRemove(new KeyValuePair<string, Guid>(marketplace, runId));
        }
    }

    private sealed record SyncTotals(int Added, int Changed, int Removed, int Unchanged, string? WarningSummary = null);

    // ── Kategori senkronu ────────────────────────────────────────────────────

    private static async Task<SyncTotals> SyncCategoriesAsync(
        NpgsqlDataSource ds, IMarketplaceReferenceDownloader downloader, Guid runId, CancellationToken ct)
    {
        var marketplace = downloader.ServiceCode;
        var snapshot = await downloader.DownloadCategoriesAsync(ct);
        if (snapshot.Count == 0)
            throw new InvalidOperationException("Pazaryeri boş kategori listesi döndü — senkron iptal (mevcut veri korunur).");

        // path (kök→düğüm adları) ve is_leaf düz listeden hesaplanır.
        var byId = snapshot.ToDictionary(c => c.ExternalId);
        var hasChild = snapshot.Where(c => c.ParentExternalId is not null)
            .Select(c => c.ParentExternalId!).ToHashSet();
        string PathOf(RefCategoryDto c)
        {
            var parts = new List<string>();
            for (var cur = c; cur is not null;
                 cur = cur.ParentExternalId is not null ? byId.GetValueOrDefault(cur.ParentExternalId) : null)
                parts.Add(cur.Name);
            parts.Reverse();
            return string.Join(" > ", parts);
        }

        var existing = new Dictionary<string, (string Hash, bool Removed, string Name)>();
        await using (var cmd = ds.CreateCommand(
            "SELECT external_id, content_hash, removed_at IS NOT NULL, name FROM mp_categories WHERE marketplace=$1"))
        {
            cmd.Parameters.AddWithValue(marketplace);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                existing[reader.GetString(0)] = (reader.GetString(1), reader.GetBoolean(2), reader.GetString(3));
        }

        var rows = snapshot.Select(c =>
        {
            var path = PathOf(c);
            var isLeaf = !hasChild.Contains(c.ExternalId);
            var hash = Sha256($"{c.Name}|{c.ParentExternalId}|{path}|{isLeaf}");
            return (c.ExternalId, c.ParentExternalId, c.Name, Path: path, IsLeaf: isLeaf, c.RawJson, Hash: hash);
        }).ToList();

        var toInsert = rows.Where(r => !existing.ContainsKey(r.ExternalId)).ToList();
        var toUpdate = rows.Where(r => existing.TryGetValue(r.ExternalId, out var e) && (e.Hash != r.Hash || e.Removed)).ToList();
        var seenIds = rows.Select(r => r.ExternalId).ToHashSet();
        var toRemove = existing.Where(kv => !kv.Value.Removed && !seenIds.Contains(kv.Key)).ToList();
        var unchanged = rows.Count - toInsert.Count - toUpdate.Count;

        var changes = new List<(string EntityType, string Key, string Type, string? Detail)>();
        foreach (var r in toInsert)
            changes.Add(("category", r.ExternalId, "added", JsonSerializer.Serialize(new { name = r.Name, path = r.Path })));
        foreach (var r in toUpdate)
        {
            var wasRemoved = existing[r.ExternalId].Removed;
            changes.Add(("category", r.ExternalId, "changed", JsonSerializer.Serialize(new
            {
                oldName = existing[r.ExternalId].Name, name = r.Name, path = r.Path, reappeared = wasRemoved
            })));
        }
        foreach (var kv in toRemove)
            changes.Add(("category", kv.Key, "removed", JsonSerializer.Serialize(new { name = kv.Value.Name })));

        await using (var conn = await ds.OpenConnectionAsync(ct))
        await using (var tx = await conn.BeginTransactionAsync(ct))
        {
            if (toInsert.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    INSERT INTO mp_categories (marketplace, external_id, parent_external_id, name, path, is_leaf, raw, content_hash)
                    SELECT $1, u.eid, u.pid, u.name, u.path, u.leaf, u.raw::jsonb, u.hash
                    FROM unnest($2::text[], $3::text[], $4::text[], $5::text[], $6::boolean[], $7::text[], $8::text[])
                         AS u(eid, pid, name, path, leaf, raw, hash)
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                AddArrays(cmd, toInsert.Select(r => (r.ExternalId, r.ParentExternalId, r.Name, r.Path, r.IsLeaf, r.RawJson, r.Hash)));
                await cmd.ExecuteNonQueryAsync(ct);
            }
            if (toUpdate.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE mp_categories c
                    SET parent_external_id=u.pid, name=u.name, path=u.path, is_leaf=u.leaf,
                        raw=u.raw::jsonb, content_hash=u.hash, is_active=true, removed_at=NULL
                    FROM unnest($2::text[], $3::text[], $4::text[], $5::text[], $6::boolean[], $7::text[], $8::text[])
                         AS u(eid, pid, name, path, leaf, raw, hash)
                    WHERE c.marketplace=$1 AND c.external_id=u.eid
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                AddArrays(cmd, toUpdate.Select(r => (r.ExternalId, r.ParentExternalId, r.Name, r.Path, r.IsLeaf, r.RawJson, r.Hash)));
                await cmd.ExecuteNonQueryAsync(ct);
            }
            if (toRemove.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE mp_categories SET removed_at=now(), is_active=false
                    WHERE marketplace=$1 AND external_id = ANY($2) AND removed_at IS NULL
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                cmd.Parameters.AddWithValue(toRemove.Select(kv => kv.Key).ToArray());
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await WriteChangeLogAsync(conn, tx, runId, marketplace, changes, ct);
            await tx.CommitAsync(ct);
        }

        await AnalyzeAsync(ds, ct, "mp_categories");
        return new SyncTotals(toInsert.Count, toUpdate.Count, toRemove.Count, unchanged);
    }

    // ── Özellik + değer senkronu (kategori kapsamlı) ─────────────────────────

    private async Task<SyncTotals> SyncAttributesAsync(
        NpgsqlDataSource ds, IMarketplaceReferenceDownloader downloader, Guid runId,
        List<string>? categoryIds, bool onlyMissingOrStale, CancellationToken ct)
    {
        var marketplace = downloader.ServiceCode;

        // Hedef küme: verilen liste ya da tüm aktif yaprak kategoriler.
        // RF1: onlyMissingOrStale → yalnız hiç taranmamış ya da bayat olanlar (kaldığı yerden devam).
        List<string> targets;
        if (categoryIds is { Count: > 0 })
            targets = categoryIds;
        else
        {
            var staleDays = Math.Max(1, configuration.GetValue("Trendyol:ReferenceStaleDays", 7));
            targets = [];
            await using var cmd = ds.CreateCommand(
                onlyMissingOrStale
                    ? """
                      SELECT external_id FROM mp_categories
                      WHERE marketplace=$1 AND is_leaf AND removed_at IS NULL
                        AND (attributes_synced_at IS NULL OR attributes_synced_at < now() - make_interval(days => $2))
                      ORDER BY attributes_synced_at NULLS FIRST, external_id
                      """
                    : "SELECT external_id FROM mp_categories WHERE marketplace=$1 AND is_leaf AND removed_at IS NULL ORDER BY external_id");
            cmd.Parameters.AddWithValue(marketplace);
            if (onlyMissingOrStale) cmd.Parameters.AddWithValue(staleDays);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) targets.Add(reader.GetString(0));
        }
        if (targets.Count == 0)
        {
            if (onlyMissingOrStale) return new SyncTotals(0, 0, 0, 0); // her şey güncel — iş yok
            throw new InvalidOperationException("Hedef kategori yok — önce kategori senkronu çalıştırın (scope=categories).");
        }

        await using (var total = ds.CreateCommand("UPDATE mp_sync_runs SET total_categories=$2 WHERE id=$1"))
        {
            total.Parameters.AddWithValue(runId);
            total.Parameters.AddWithValue(targets.Count);
            await total.ExecuteNonQueryAsync(ct);
        }

        int added = 0, changed = 0, removed = 0, unchanged = 0, failedCategories = 0, processed = 0;
        foreach (var categoryId in targets)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var attrs = await downloader.DownloadCategoryAttributesAsync(categoryId, ct);
                var t = await ApplyCategoryAttributesAsync(ds, runId, marketplace, categoryId, attrs, ct);
                added += t.Added; changed += t.Changed; removed += t.Removed; unchanged += t.Unchanged;

                // RF1: kapsam damgası — 0 özellik dönen kategori de "tarandı" sayılır.
                await using var stamp = ds.CreateCommand(
                    "UPDATE mp_categories SET attributes_synced_at=now() WHERE marketplace=$1 AND external_id=$2");
                stamp.Parameters.AddWithValue(marketplace);
                stamp.Parameters.AddWithValue(categoryId);
                await stamp.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failedCategories++;
                logger.LogWarning(ex, "Kategori özellikleri indirilemedi: {Marketplace}/{CategoryId}", marketplace, categoryId);
                // İlk kategoriler art arda patlıyorsa sorun münferit değil (endpoint/kota) — koşuyu düşür.
                if (processed == 0 && failedCategories >= 5)
                    throw new InvalidOperationException($"İlk {failedCategories} kategori de indirilemedi; son hata: {ex.Message}");
            }

            processed++;
            if (processed % HeartbeatEvery == 0 || processed == targets.Count)
            {
                await using var hb = ds.CreateCommand(
                    """
                    UPDATE mp_sync_runs SET processed_categories=$2, heartbeat_at=now(),
                        added_count=$3, changed_count=$4, removed_count=$5, unchanged_count=$6
                    WHERE id=$1
                    """);
                hb.Parameters.AddWithValue(runId);
                hb.Parameters.AddWithValue(processed);
                hb.Parameters.AddWithValue(added);
                hb.Parameters.AddWithValue(changed);
                hb.Parameters.AddWithValue(removed);
                hb.Parameters.AddWithValue(unchanged);
                await hb.ExecuteNonQueryAsync(ct);
            }

            if (downloader.AttributeRequestDelay > TimeSpan.Zero && processed < targets.Count)
                await Task.Delay(downloader.AttributeRequestDelay, ct);
        }

        await AnalyzeAsync(ds, ct, "mp_category_attributes", "mp_attribute_values");
        return new SyncTotals(added, changed, removed, unchanged,
            failedCategories > 0 ? $"{failedCategories} kategori indirilemedi (log'da ayrıntı var)." : null);
    }

    private static async Task<SyncTotals> ApplyCategoryAttributesAsync(
        NpgsqlDataSource ds, Guid runId, string marketplace, string categoryId,
        List<RefAttributeDto> attrs, CancellationToken ct)
    {
        // Mevcut kapsam: bu kategorinin özellikleri ve değerleri.
        var existingAttrs = new Dictionary<string, (string Hash, bool Removed, string Name)>();
        await using (var cmd = ds.CreateCommand(
            """
            SELECT attribute_external_id, content_hash, removed_at IS NOT NULL, name
            FROM mp_category_attributes WHERE marketplace=$1 AND category_external_id=$2
            """))
        {
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(categoryId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                existingAttrs[reader.GetString(0)] = (reader.GetString(1), reader.GetBoolean(2), reader.GetString(3));
        }
        var existingValues = new Dictionary<(string AttrId, string Key), (string Hash, bool Removed)>();
        await using (var cmd = ds.CreateCommand(
            """
            SELECT attribute_external_id, value_key, content_hash, removed_at IS NOT NULL
            FROM mp_attribute_values WHERE marketplace=$1 AND category_external_id=$2
            """))
        {
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(categoryId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                existingValues[(reader.GetString(0), reader.GetString(1))] = (reader.GetString(2), reader.GetBoolean(3));
        }

        var attrRows = attrs.Select(a => (
            a.ExternalId, a.Code, a.Name, a.IsRequired, a.AllowCustom, a.IsMultiValue,
            a.IsVariantAxis, a.ValueMode, a.RawJson,
            Hash: Sha256($"{a.Name}|{a.Code}|{a.IsRequired}|{a.AllowCustom}|{a.IsMultiValue}|{a.IsVariantAxis}|{a.ValueMode}"))).ToList();
        var valueRows = attrs.SelectMany(a => a.Values.Select(v => (
            AttrId: a.ExternalId,
            Key: v.ExternalId ?? v.Value.Trim().ToLowerInvariant(),
            v.ExternalId, v.Code, v.Value,
            Hash: Sha256($"{v.Value}|{v.Code}|{v.ExternalId}"))))
            .GroupBy(v => (v.AttrId, v.Key)).Select(g => g.First()).ToList();

        var attrInsert = attrRows.Where(r => !existingAttrs.ContainsKey(r.ExternalId)).ToList();
        var attrUpdate = attrRows.Where(r => existingAttrs.TryGetValue(r.ExternalId, out var e) && (e.Hash != r.Hash || e.Removed)).ToList();
        var attrSeen = attrRows.Select(r => r.ExternalId).ToHashSet();
        var attrRemove = existingAttrs.Where(kv => !kv.Value.Removed && !attrSeen.Contains(kv.Key)).ToList();

        var valInsert = valueRows.Where(r => !existingValues.ContainsKey((r.AttrId, r.Key))).ToList();
        var valUpdate = valueRows.Where(r => existingValues.TryGetValue((r.AttrId, r.Key), out var e) && (e.Hash != r.Hash || e.Removed)).ToList();
        var valSeen = valueRows.Select(r => (r.AttrId, r.Key)).ToHashSet();
        var valRemove = existingValues.Where(kv => !kv.Value.Removed && !valSeen.Contains(kv.Key)).ToList();

        var changes = new List<(string EntityType, string Key, string Type, string? Detail)>();
        foreach (var r in attrInsert)
            changes.Add(("attribute", $"{categoryId}|{r.ExternalId}", "added",
                JsonSerializer.Serialize(new { name = r.Name, required = r.IsRequired, allowCustom = r.AllowCustom })));
        foreach (var r in attrUpdate)
            changes.Add(("attribute", $"{categoryId}|{r.ExternalId}", "changed",
                JsonSerializer.Serialize(new
                {
                    oldName = existingAttrs[r.ExternalId].Name, name = r.Name,
                    required = r.IsRequired, allowCustom = r.AllowCustom,
                    reappeared = existingAttrs[r.ExternalId].Removed
                })));
        foreach (var kv in attrRemove)
            changes.Add(("attribute", $"{categoryId}|{kv.Key}", "removed",
                JsonSerializer.Serialize(new { name = kv.Value.Name })));
        // Değer olayları satır satır değil kapsam özeti olarak loglanır — milyonlarca
        // değer satırında olay tablosunu şişirmemek için (F2 sağlık job'ı kapsamdan okur).
        if (valInsert.Count + valUpdate.Count + valRemove.Count > 0)
            changes.Add(("value", categoryId, "changed",
                JsonSerializer.Serialize(new { added = valInsert.Count, changed = valUpdate.Count, removed = valRemove.Count })));

        await using (var conn = await ds.OpenConnectionAsync(ct))
        await using (var tx = await conn.BeginTransactionAsync(ct))
        {
            if (attrInsert.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    INSERT INTO mp_category_attributes
                        (marketplace, category_external_id, attribute_external_id, code, name,
                         is_required, allow_custom, is_multi_value, is_variant_axis, value_mode, raw, content_hash)
                    SELECT $1, $2, u.eid, u.code, u.name, u.req, u.ac, u.mv, u.va, u.vm, u.raw::jsonb, u.hash
                    FROM unnest($3::text[], $4::text[], $5::text[], $6::boolean[], $7::boolean[],
                                $8::boolean[], $9::boolean[], $10::text[], $11::text[], $12::text[])
                         AS u(eid, code, name, req, ac, mv, va, vm, raw, hash)
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                cmd.Parameters.AddWithValue(categoryId);
                AddAttrArrays(cmd, attrInsert);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            if (attrUpdate.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE mp_category_attributes a
                    SET code=u.code, name=u.name, is_required=u.req, allow_custom=u.ac,
                        is_multi_value=u.mv, is_variant_axis=u.va, value_mode=u.vm,
                        raw=u.raw::jsonb, content_hash=u.hash, is_active=true, removed_at=NULL
                    FROM unnest($3::text[], $4::text[], $5::text[], $6::boolean[], $7::boolean[],
                                $8::boolean[], $9::boolean[], $10::text[], $11::text[], $12::text[])
                         AS u(eid, code, name, req, ac, mv, va, vm, raw, hash)
                    WHERE a.marketplace=$1 AND a.category_external_id=$2 AND a.attribute_external_id=u.eid
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                cmd.Parameters.AddWithValue(categoryId);
                AddAttrArrays(cmd, attrUpdate);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            if (attrRemove.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE mp_category_attributes SET removed_at=now(), is_active=false
                    WHERE marketplace=$1 AND category_external_id=$2 AND attribute_external_id = ANY($3) AND removed_at IS NULL
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                cmd.Parameters.AddWithValue(categoryId);
                cmd.Parameters.AddWithValue(attrRemove.Select(kv => kv.Key).ToArray());
                await cmd.ExecuteNonQueryAsync(ct);
            }

            if (valInsert.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    INSERT INTO mp_attribute_values
                        (marketplace, category_external_id, attribute_external_id, value_key,
                         value_external_id, value_code, value, content_hash)
                    SELECT $1, $2, u.aid, u.vkey, u.veid, u.vcode, u.val, u.hash
                    FROM unnest($3::text[], $4::text[], $5::text[], $6::text[], $7::text[], $8::text[])
                         AS u(aid, vkey, veid, vcode, val, hash)
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                cmd.Parameters.AddWithValue(categoryId);
                AddValueArrays(cmd, valInsert);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            if (valUpdate.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE mp_attribute_values v
                    SET value_external_id=u.veid, value_code=u.vcode, value=u.val,
                        content_hash=u.hash, is_active=true, removed_at=NULL
                    FROM unnest($3::text[], $4::text[], $5::text[], $6::text[], $7::text[], $8::text[])
                         AS u(aid, vkey, veid, vcode, val, hash)
                    WHERE v.marketplace=$1 AND v.category_external_id=$2
                      AND v.attribute_external_id=u.aid AND v.value_key=u.vkey
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                cmd.Parameters.AddWithValue(categoryId);
                AddValueArrays(cmd, valUpdate);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            if (valRemove.Count > 0)
            {
                await using var cmd = new NpgsqlCommand(
                    """
                    UPDATE mp_attribute_values v SET removed_at=now(), is_active=false
                    FROM unnest($3::text[], $4::text[]) AS u(aid, vkey)
                    WHERE v.marketplace=$1 AND v.category_external_id=$2
                      AND v.attribute_external_id=u.aid AND v.value_key=u.vkey AND v.removed_at IS NULL
                    """, conn, tx);
                cmd.Parameters.AddWithValue(marketplace);
                cmd.Parameters.AddWithValue(categoryId);
                cmd.Parameters.AddWithValue(valRemove.Select(kv => kv.Key.AttrId).ToArray());
                cmd.Parameters.AddWithValue(valRemove.Select(kv => kv.Key.Key).ToArray());
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await WriteChangeLogAsync(conn, tx, runId, marketplace, changes, ct);
            await tx.CommitAsync(ct);
        }

        var addedT = attrInsert.Count + valInsert.Count;
        var changedT = attrUpdate.Count + valUpdate.Count;
        var removedT = attrRemove.Count + valRemove.Count;
        var unchangedT = attrRows.Count + valueRows.Count - addedT - changedT;
        return new SyncTotals(addedT, changedT, removedT, Math.Max(0, unchangedT));
    }

    // ── Sorgu uçları (panel) ─────────────────────────────────────────────────

    public async Task<List<RefSyncRunDto>> GetRunsAsync(string? marketplace, int limit, CancellationToken ct)
    {
        var ds = await refDb.GetAsync(ct);
        if (ds is null) return [];

        await using var cmd = ds.CreateCommand(
            """
            SELECT id, marketplace, scope,
                   CASE WHEN status='running' AND heartbeat_at < now() - $3 THEN 'failed' ELSE status END,
                   started_at, finished_at, total_categories, processed_categories,
                   added_count, changed_count, removed_count, unchanged_count,
                   CASE WHEN status='running' AND heartbeat_at < now() - $3
                        THEN 'Koşu kesildi (heartbeat yaşlandı).' ELSE error END
            FROM mp_sync_runs
            WHERE ($1::text IS NULL OR marketplace=$1)
            ORDER BY started_at DESC LIMIT $2
            """);
        cmd.Parameters.AddWithValue((object?)marketplace ?? DBNull.Value);
        cmd.Parameters.AddWithValue(limit);
        cmd.Parameters.AddWithValue(StaleAfter);
        var result = new List<RefSyncRunDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new RefSyncRunDto(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetDateTime(4), reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6), reader.GetInt32(7),
                reader.GetInt32(8), reader.GetInt32(9), reader.GetInt32(10), reader.GetInt32(11),
                reader.IsDBNull(12) ? null : reader.GetString(12)));
        return result;
    }

    public async Task<List<RefSummaryDto>?> GetSummaryAsync(CancellationToken ct)
    {
        var ds = await refDb.GetAsync(ct);
        if (ds is null) return null;

        var counts = new Dictionary<string, (long Cat, long Attr, long Val, long Removed, long Leaf, long LeafSynced, DateTime? Oldest)>();
        await using (var cmd = ds.CreateCommand(
            """
            SELECT c.marketplace,
                   count(*) FILTER (WHERE c.removed_at IS NULL),
                   count(*) FILTER (WHERE c.removed_at IS NOT NULL),
                   COALESCE(a.cnt, 0), COALESCE(v.cnt, 0),
                   count(*) FILTER (WHERE c.is_leaf AND c.removed_at IS NULL),
                   count(*) FILTER (WHERE c.is_leaf AND c.removed_at IS NULL AND c.attributes_synced_at IS NOT NULL),
                   min(c.attributes_synced_at) FILTER (WHERE c.is_leaf AND c.removed_at IS NULL)
            FROM mp_categories c
            LEFT JOIN (SELECT marketplace, count(*) cnt FROM mp_category_attributes WHERE removed_at IS NULL GROUP BY 1) a USING (marketplace)
            LEFT JOIN (SELECT marketplace, count(*) cnt FROM mp_attribute_values WHERE removed_at IS NULL GROUP BY 1) v USING (marketplace)
            GROUP BY c.marketplace, a.cnt, v.cnt
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                counts[reader.GetString(0)] = (reader.GetInt64(1), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(2),
                    reader.GetInt64(5), reader.GetInt64(6), reader.IsDBNull(7) ? null : reader.GetDateTime(7));

        var runs = await GetRunsAsync(null, 200, ct);
        var marketplaces = counts.Keys.Union(runs.Select(r => r.Marketplace)).Union(SupportedMarketplaces);
        return marketplaces.OrderBy(m => m).Select(m =>
        {
            var c = counts.GetValueOrDefault(m);
            return new RefSummaryDto(m, c.Cat, c.Attr, c.Val, c.Removed,
                runs.FirstOrDefault(r => r.Marketplace == m),
                c.Leaf, c.LeafSynced, c.Oldest);
        }).ToList();
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static async Task WriteChangeLogAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, Guid runId, string marketplace,
        List<(string EntityType, string Key, string Type, string? Detail)> changes, CancellationToken ct)
    {
        if (changes.Count == 0) return;
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO mp_change_log (sync_run_id, marketplace, entity_type, external_key, change_type, change_detail)
            SELECT $1, $2, u.et, u.key, u.ct, u.detail::jsonb
            FROM unnest($3::text[], $4::text[], $5::text[], $6::text[]) AS u(et, key, ct, detail)
            """, conn, tx);
        cmd.Parameters.AddWithValue(runId);
        cmd.Parameters.AddWithValue(marketplace);
        cmd.Parameters.AddWithValue(changes.Select(c => c.EntityType).ToArray());
        cmd.Parameters.AddWithValue(changes.Select(c => c.Key).ToArray());
        cmd.Parameters.AddWithValue(changes.Select(c => c.Type).ToArray());
        cmd.Parameters.Add(new NpgsqlParameter
        {
            Value = changes.Select(c => c.Detail).ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddArrays(
        NpgsqlCommand cmd,
        IEnumerable<(string Eid, string? Pid, string Name, string Path, bool Leaf, string Raw, string Hash)> rows)
    {
        var list = rows.ToList();
        cmd.Parameters.AddWithValue(list.Select(r => r.Eid).ToArray());
        cmd.Parameters.Add(new NpgsqlParameter
        {
            Value = list.Select(r => r.Pid).ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        cmd.Parameters.AddWithValue(list.Select(r => r.Name).ToArray());
        cmd.Parameters.AddWithValue(list.Select(r => r.Path).ToArray());
        cmd.Parameters.AddWithValue(list.Select(r => r.Leaf).ToArray());
        cmd.Parameters.AddWithValue(list.Select(r => r.Raw).ToArray());
        cmd.Parameters.AddWithValue(list.Select(r => r.Hash).ToArray());
    }

    private static void AddAttrArrays(
        NpgsqlCommand cmd,
        List<(string ExternalId, string? Code, string Name, bool IsRequired, bool AllowCustom,
              bool IsMultiValue, bool IsVariantAxis, string ValueMode, string RawJson, string Hash)> rows)
    {
        cmd.Parameters.AddWithValue(rows.Select(r => r.ExternalId).ToArray());
        cmd.Parameters.Add(new NpgsqlParameter
        {
            Value = rows.Select(r => r.Code).ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        cmd.Parameters.AddWithValue(rows.Select(r => r.Name).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.IsRequired).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.AllowCustom).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.IsMultiValue).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.IsVariantAxis).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.ValueMode).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.RawJson).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.Hash).ToArray());
    }

    private static void AddValueArrays(
        NpgsqlCommand cmd,
        List<(string AttrId, string Key, string? ExternalId, string? Code, string Value, string Hash)> rows)
    {
        cmd.Parameters.AddWithValue(rows.Select(r => r.AttrId).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.Key).ToArray());
        cmd.Parameters.Add(new NpgsqlParameter
        {
            Value = rows.Select(r => r.ExternalId).ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        cmd.Parameters.Add(new NpgsqlParameter
        {
            Value = rows.Select(r => r.Code).ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        cmd.Parameters.AddWithValue(rows.Select(r => r.Value).ToArray());
        cmd.Parameters.AddWithValue(rows.Select(r => r.Hash).ToArray());
    }

    private static async Task AnalyzeAsync(NpgsqlDataSource ds, CancellationToken ct, params string[] tables)
    {
        foreach (var table in tables)
        {
            await using var cmd = ds.CreateCommand($"ANALYZE {table}");
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static string Sha256(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
