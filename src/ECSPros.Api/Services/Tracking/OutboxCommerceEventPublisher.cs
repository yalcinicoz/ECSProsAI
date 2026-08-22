using System.Text.Json;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Contracts.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Tracking;

/// <summary>
/// ICommerceEventPublisher — event'i integration.tracking_event_outbox'a yazar (İE-2 Faz B-4).
/// HATA-GÜVENLİ: her hata yutulur, checkout/üyelik akışı asla bozulmaz. Tracking:Enabled=false
/// (staging/5051 varsayılanı) → hiç yazmaz. Aynı (kanal, event, dedupId) varsa sessizce atlar
/// (purchase dedup = OrderId; unique index ikinci güvence).
/// </summary>
public sealed class OutboxCommerceEventPublisher(
    IIntegrationDbContext db,
    IConfiguration config,
    ILogger<OutboxCommerceEventPublisher> logger) : ICommerceEventPublisher
{
    public static readonly JsonSerializerOptions JsonAyar = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(CommerceEvent e, CancellationToken ct = default)
    {
        try
        {
            if (!config.GetValue("Tracking:Enabled", false)) return;
            if (!CommerceEventNames.IsValid(e.Name) || e.FirmPlatformId == Guid.Empty || string.IsNullOrWhiteSpace(e.DedupId)) return;

            var var = await db.TrackingEventOutbox
                .AnyAsync(x => x.FirmPlatformId == e.FirmPlatformId && x.EventName == e.Name && x.DedupId == e.DedupId, ct);
            if (var) return;

            db.TrackingEventOutbox.Add(new TrackingEventOutbox
            {
                FirmPlatformId = e.FirmPlatformId,
                EventName = e.Name,
                DedupId = e.DedupId,
                Source = e.Source,
                OccurredAt = e.OccurredAt,
                PayloadJson = JsonSerializer.Serialize(e, JsonAyar),
                Status = "pending"
            });
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // unique index — eşzamanlı çift yazım; ilk kazandı
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Commerce event outbox'a yazılamadı ({Event}, dedup={Dedup})", e.Name, e.DedupId);
        }
    }
}
