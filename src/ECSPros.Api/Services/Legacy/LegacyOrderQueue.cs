using ECSPros.Core.Application.Services;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Legacy;

/// <summary>
/// Eski sisteme sipariş senkron kuyruğu yazıcısı (F1, 2026-08-04). Kuyruğa yazım
/// HATA-GÜVENLİ: her hata yutulur — checkout/onay akışı asla bozulmaz. Kanalın
/// legacyPlatformId ayarı yoksa kuyruğa hiç yazılmaz (senkron o kanal için kapalı).
/// Aynı (OrderId, JobType) için tek kayıt (unique index + ön kontrol).
/// </summary>
public interface ILegacyOrderQueue
{
    Task EnqueueAsync(Guid orderId, Guid firmPlatformId, string jobType = "create", CancellationToken ct = default);
}

public sealed class LegacyOrderQueue(
    IIntegrationDbContext integrationDb,
    ICoreDbContext coreDb,
    ILogger<LegacyOrderQueue> logger) : ILegacyOrderQueue
{
    public async Task EnqueueAsync(Guid orderId, Guid firmPlatformId, string jobType = "create", CancellationToken ct = default)
    {
        try
        {
            var settings = await coreDb.FirmPlatforms.AsNoTracking()
                .Where(p => p.Id == firmPlatformId)
                .Select(p => p.Settings)
                .FirstOrDefaultAsync(ct);
            if (LegacyPlatformIdOf(settings) is null) return; // kanal eskiye bağlı değil

            var mevcut = await integrationDb.LegacyOrderOutbox
                .AnyAsync(x => x.OrderId == orderId && x.JobType == jobType, ct);
            if (mevcut) return;

            integrationDb.LegacyOrderOutbox.Add(new LegacyOrderOutbox { OrderId = orderId, JobType = jobType });
            await integrationDb.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Kuyruk yazımı sipariş akışını asla bozmaz — worker tarafı log/telafi noktasıdır
            logger.LogError(ex, "Legacy sipariş kuyruğuna yazılamadı (orderId={OrderId}, job={Job})", orderId, jobType);
        }
    }

    /// <summary>FirmPlatform.Settings["legacyPlatformId"] (panel Kanallar formu) — yoksa null.</summary>
    public static int? LegacyPlatformIdOf(Dictionary<string, object>? settings)
    {
        if (settings is null || !settings.TryGetValue("legacyPlatformId", out var v)) return null;
        return v switch
        {
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } je =>
                je.GetInt32() is var i && i > 0 ? i : null,
            int i2 when i2 > 0 => i2,
            long l when l > 0 => (int)l,
            _ => null
        };
    }
}
