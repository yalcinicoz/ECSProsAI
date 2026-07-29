using System.Text.Json;
using ECSPros.Api.Services.Marketplace.Reference;
using Npgsql;

namespace ECSPros.Api.Services.Marketplace.Mapping;

/// <summary>
/// Eşleme sağlık job'ı (§2.5): referans senkronunun mp_change_log'a düşürdüğü olayları
/// eşleme durumlarına işler — hedef silindi → broken, ad/zorunluluk değişti → needs_review,
/// serbest giriş listeye bağlandı → pass_literal eşlemeler needs_review, kaybolan değer →
/// broken. Olaylar işlenince processed_at damgalanır; idempotent, tekrar çalıştırılabilir.
/// Her referans senkron koşusu sonunda otomatik + panelden elle tetiklenebilir.
/// Raw SQL (iki datasource) — singleton, EF scope gerektirmez.
/// </summary>
public sealed class MappingHealthService(
    NpgsqlDataSource mainDb,
    MarketplaceRefDb refDb,
    ILogger<MappingHealthService> logger)
{
    public sealed record HealthResult(int ProcessedEvents, int BrokenCount, int ReviewCount);

    public async Task<HealthResult> ProcessAsync(string marketplace, CancellationToken ct = default)
    {
        var ds = await refDb.GetAsync(ct);
        if (ds is null) return new HealthResult(0, 0, 0);

        // İşlenmemiş olayları çek
        var events = new List<(long Id, string EntityType, string Key, string Type, string? Detail)>();
        await using (var cmd = ds.CreateCommand(
            """
            SELECT id, entity_type, external_key, change_type, change_detail::text
            FROM mp_change_log WHERE marketplace=$1 AND processed_at IS NULL ORDER BY id
            """))
        {
            cmd.Parameters.AddWithValue(marketplace);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                events.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        if (events.Count == 0) return new HealthResult(0, 0, 0);

        int broken = 0, review = 0;

        foreach (var e in events)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                switch (e.EntityType)
                {
                    case "category" when e.Type == "removed":
                        broken += await MarkCategoryTargetAsync(marketplace, e.Key, "broken",
                            "Pazaryeri bu kategoriyi kaldırdı — yeni hedef seçilmeli.", ct);
                        break;
                    case "category" when e.Type == "changed":
                        review += await OnCategoryChangedAsync(marketplace, e.Key, e.Detail, ct);
                        break;
                    case "attribute" when e.Type == "removed":
                        broken += await MarkAttributeAsync(marketplace, e.Key, "broken",
                            "Pazaryeri bu özelliği kategoriden kaldırdı.", onlyPassLiteral: false, ct);
                        break;
                    case "attribute" when e.Type == "changed":
                        review += await OnAttributeChangedAsync(marketplace, e.Key, e.Detail, ct);
                        break;
                    case "value": // kategori kapsam özeti — kapsamdaki değer eşlemelerini doğrula
                        broken += await VerifyValueMappingsAsync(ds, marketplace, e.Key, ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Tek olayın işlenememesi turu düşürmesin; damgalanmadığı için sonraki turda yeniden denenir.
                logger.LogWarning(ex, "Eşleme sağlık olayı işlenemedi: {Marketplace} #{Id}", marketplace, e.Id);
                continue;
            }

            await using var done = ds.CreateCommand("UPDATE mp_change_log SET processed_at=now() WHERE id=$1");
            done.Parameters.AddWithValue(e.Id);
            await done.ExecuteNonQueryAsync(ct);
        }

        if (broken + review > 0)
            logger.LogInformation("Eşleme sağlık taraması ({Marketplace}): {Broken} broken, {Review} needs_review",
                marketplace, broken, review);
        return new HealthResult(events.Count, broken, review);
    }

    /// <summary>Direct hedefi, kural hedefi veya havuz üyesi bu kategori olan eşlemeleri işaretler.</summary>
    private async Task<int> MarkCategoryTargetAsync(
        string marketplace, string categoryExternalId, string status, string? note, CancellationToken ct)
    {
        await using var cmd = mainDb.CreateCommand(
            """
            UPDATE integration.marketplace_category_mappings
            SET "Status"=$3, "StatusNote"=$4, "UpdatedAt"=now()
            WHERE "Marketplace"=$1 AND NOT "IsDeleted" AND "Status" <> $3
              AND ("TargetExternalId"=$2
                   OR ("RulesJson" IS NOT NULL AND "RulesJson"::jsonb @> $5::jsonb)
                   OR ("PoolJson" IS NOT NULL AND "PoolJson"::jsonb @> $6::jsonb))
            """);
        cmd.Parameters.AddWithValue(marketplace);
        cmd.Parameters.AddWithValue(categoryExternalId);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue((object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(new[] { new { targetExternalId = categoryExternalId } }));
        cmd.Parameters.AddWithValue(JsonSerializer.Serialize(new[] { new { externalId = categoryExternalId } }));
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> OnCategoryChangedAsync(
        string marketplace, string categoryExternalId, string? detailJson, CancellationToken ct)
    {
        // Yeniden görünme (reappeared) → daha önce broken olan eşlemeler kendiliğinden düzelir.
        var detail = Parse(detailJson);
        var reappeared = detail.TryGetProperty("reappeared", out var r) && r.GetBoolean();
        if (reappeared)
        {
            // Kategori geri geldi → daha önce broken olan eşlemeler kendiliğinden düzelir.
            await MarkCategoryTargetAsync(marketplace, categoryExternalId, "active", null, ct);
            return 0; // düzeltme; needs_review sayacına girmesin
        }

        var oldName = detail.TryGetProperty("oldName", out var on) ? on.GetString() : null;
        var newName = detail.TryGetProperty("name", out var nn) ? nn.GetString() : null;
        if (oldName == newName) return 0; // yalnız path değişimi — snapshot zaten path'i gösterir
        return await MarkCategoryTargetAsync(marketplace, categoryExternalId, "needs_review",
            $"Hedef kategorinin adı değişti: \"{oldName}\" → \"{newName}\". Eşlemenin hâlâ doğru olduğunu onaylayın.", ct);
    }

    /// <summary>external_key biçimi: categoryId|attributeId (senkron motoru böyle yazar).</summary>
    private async Task<int> MarkAttributeAsync(
        string marketplace, string key, string status, string note, bool onlyPassLiteral, CancellationToken ct)
    {
        var parts = key.Split('|', 2);
        if (parts.Length != 2) return 0;
        await using var cmd = mainDb.CreateCommand(
            $"""
            UPDATE integration.marketplace_attribute_mappings
            SET "Status"=$4, "StatusNote"=$5, "UpdatedAt"=now()
            WHERE "Marketplace"=$1 AND "MpCategoryExternalId"=$2 AND "MpAttributeExternalId"=$3
              AND NOT "IsDeleted" AND "Status" <> $4
              {(onlyPassLiteral ? """AND "Strategy"='pass_literal'""" : "")}
            """);
        cmd.Parameters.AddWithValue(marketplace);
        cmd.Parameters.AddWithValue(parts[0]);
        cmd.Parameters.AddWithValue(parts[1]);
        cmd.Parameters.AddWithValue(status);
        cmd.Parameters.AddWithValue(note);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> OnAttributeChangedAsync(
        string marketplace, string key, string? detailJson, CancellationToken ct)
    {
        var detail = Parse(detailJson);
        var required = detail.TryGetProperty("required", out var r) && r.GetBoolean();
        var allowCustom = detail.TryGetProperty("allowCustom", out var ac) && ac.GetBoolean();
        var name = detail.TryGetProperty("name", out var n) ? n.GetString() : "?";

        var count = 0;
        if (!allowCustom)
            // Serbest giriş kapandıysa metni aynen geçiren eşlemeler artık geçersiz olabilir.
            count += await MarkAttributeAsync(marketplace, key, "needs_review",
                $"\"{name}\" özelliğinde serbest giriş kapandı — değer listesinden eşleme gerekli.",
                onlyPassLiteral: true, ct);
        if (required)
            count += await MarkAttributeAsync(marketplace, key, "needs_review",
                $"\"{name}\" özelliği zorunlu hale geldi — eşlemenin eksiksiz olduğunu doğrulayın.",
                onlyPassLiteral: false, ct);
        return count;
    }

    /// <summary>Kategori kapsamındaki değer eşlemelerini referans değerlerle doğrular;
    /// hedefi artık listede olmayanlar broken olur.</summary>
    private async Task<int> VerifyValueMappingsAsync(
        NpgsqlDataSource refDs, string marketplace, string categoryExternalId, CancellationToken ct)
    {
        // Referanstaki canlı değer anahtarları (attr|externalId ve attr|value)
        var live = new HashSet<string>();
        await using (var cmd = refDs.CreateCommand(
            """
            SELECT attribute_external_id, value_external_id, value FROM mp_attribute_values
            WHERE marketplace=$1 AND category_external_id=$2 AND removed_at IS NULL
            """))
        {
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(categoryExternalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var attr = reader.GetString(0);
                if (!reader.IsDBNull(1)) live.Add($"{attr}|id:{reader.GetString(1)}");
                live.Add($"{attr}|val:{TextSimilarity.Normalize(reader.GetString(2))}");
            }
        }

        // Kapsamdaki eşlemeler
        var stale = new List<Guid>();
        await using (var cmd = mainDb.CreateCommand(
            """
            SELECT "Id", "MpAttributeExternalId", "TargetExternalId", "TargetValue"
            FROM integration.marketplace_value_mappings
            WHERE "Marketplace"=$1 AND "MpCategoryExternalId"=$2 AND NOT "IsDeleted" AND "Status" <> 'broken'
            """))
        {
            cmd.Parameters.AddWithValue(marketplace);
            cmd.Parameters.AddWithValue(categoryExternalId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var attr = reader.GetString(1);
                var ok = !reader.IsDBNull(2) && live.Contains($"{attr}|id:{reader.GetString(2)}")
                         || live.Contains($"{attr}|val:{TextSimilarity.Normalize(reader.GetString(3))}");
                if (!ok) stale.Add(reader.GetGuid(0));
            }
        }
        if (stale.Count == 0) return 0;

        await using (var upd = mainDb.CreateCommand(
            """
            UPDATE integration.marketplace_value_mappings
            SET "Status"='broken', "StatusNote"='Hedef değer pazaryeri listesinden kaldırıldı — yeni değer seçin.',
                "UpdatedAt"=now()
            WHERE "Id" = ANY($1)
            """))
        {
            upd.Parameters.AddWithValue(stale.ToArray());
            await upd.ExecuteNonQueryAsync(ct);
        }
        return stale.Count;
    }

    private static JsonElement Parse(string? json) =>
        json is null ? default : JsonDocument.Parse(json).RootElement;
}
