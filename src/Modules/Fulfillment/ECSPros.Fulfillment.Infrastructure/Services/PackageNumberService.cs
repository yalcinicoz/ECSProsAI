using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Infrastructure.Services;

/// <summary>Kanala özel seriden atomik paket numarası üretimi — OrderNumberService
/// (Order modülü) ile aynı desen. Varsayılan önek: kanal kodunun alfanümerik ilk 3
/// karakteri + 'P' (örn. MISP). Sayaç hiçbir durumda geri alınmaz.</summary>
public class PackageNumberService : IPackageNumberService
{
    private readonly FulfillmentDbContext _db;

    public PackageNumberService(FulfillmentDbContext db) => _db = db;

    private sealed class SeriesSlot
    {
        public string Prefix { get; set; } = string.Empty;
        public int PadLength { get; set; }
        public long Value { get; set; }
    }

    public async Task<string> GenerateAsync(Guid firmPlatformId, CancellationToken ct = default)
    {
        var slot = await TakeNextAsync(firmPlatformId, ct);
        if (slot is null)
        {
            await CreateDefaultSeriesAsync(firmPlatformId, ct);
            slot = await TakeNextAsync(firmPlatformId, ct);
        }

        if (slot is null)
            throw new InvalidOperationException(
                $"Paket numarası üretilemedi: '{firmPlatformId}' kanalı için aktif seri yok ve varsayılan seri açılamadı (kanal kaydı bulunamamış olabilir).");

        return slot.Prefix + slot.Value.ToString().PadLeft(slot.PadLength, '0');
    }

    private async Task<SeriesSlot?> TakeNextAsync(Guid firmPlatformId, CancellationToken ct)
    {
        var rows = await _db.Database.SqlQuery<SeriesSlot>($"""
            WITH taken AS (
                UPDATE fulfillment.ful_package_number_series
                SET "NextValue" = "NextValue" + 1,
                    "UpdatedAt" = timezone('utc', now())
                WHERE "FirmPlatformId" = {firmPlatformId}
                  AND "IsActive" = true AND "IsDeleted" = false
                RETURNING "Prefix", "PadLength", "NextValue" - 1 AS "Value"
            )
            SELECT "Prefix", "PadLength", "Value" FROM taken
            """).ToListAsync(ct);

        return rows.SingleOrDefault();
    }

    private async Task CreateDefaultSeriesAsync(Guid firmPlatformId, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO fulfillment.ful_package_number_series
                ("Id", "FirmPlatformId", "Prefix", "PadLength", "NextValue", "IsActive",
                 "CreatedAt", "IsDeleted")
            SELECT {Guid.NewGuid()}, fp."Id",
                   upper(left(regexp_replace(fp."Code", '[^A-Za-z0-9]', '', 'g'), 3)) || 'P',
                   6, 1, true, timezone('utc', now()), false
            FROM core.core_firm_platforms fp
            WHERE fp."Id" = {firmPlatformId} AND fp."IsDeleted" = false
            ON CONFLICT DO NOTHING
            """, ct);
    }
}
