using System.Text.Json;
using ECSPros.Integration.Application.Services;
using ECSPros.Integration.Domain.Entities;
using ECSPros.Shared.Contracts.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Tracking;

public interface ITrackingOrderContextRecorder
{
    /// <summary>Checkout başarısında istekten bağlam + consent'i siparişe bağlar (hata-güvenli).</summary>
    Task RecordAsync(Guid orderId, Guid firmPlatformId, HttpContext http, Guid? memberId, string? email, string? phone, CancellationToken ct = default);

    /// <summary>Sipariş için saklanan bağlam — yoksa (ClientContext.Bos, ConsentState.Deny).</summary>
    Task<(ClientContext Client, ConsentState Consent)> ReadAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>İE-2 Faz B-3: integration.tracking_order_context yazıcı/okuyucu.</summary>
public sealed class TrackingOrderContextRecorder(
    IIntegrationDbContext db,
    IConfiguration config,
    ILogger<TrackingOrderContextRecorder> logger) : ITrackingOrderContextRecorder
{
    public async Task RecordAsync(Guid orderId, Guid firmPlatformId, HttpContext http, Guid? memberId, string? email, string? phone, CancellationToken ct = default)
    {
        try
        {
            if (!config.GetValue("Tracking:Enabled", false)) return;
            if (await db.TrackingOrderContexts.AnyAsync(x => x.OrderId == orderId, ct)) return;

            var client = TrackingHttpContextReader.ReadClient(http, email, phone, memberId);
            var consent = TrackingHttpContextReader.ReadConsent(http);
            db.TrackingOrderContexts.Add(new TrackingOrderContext
            {
                OrderId = orderId,
                FirmPlatformId = firmPlatformId,
                ContextJson = JsonSerializer.Serialize(client, OutboxCommerceEventPublisher.JsonAyar),
                ConsentJson = JsonSerializer.Serialize(consent, OutboxCommerceEventPublisher.JsonAyar)
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sipariş takip bağlamı yazılamadı (orderId={OrderId})", orderId);
        }
    }

    public async Task<(ClientContext Client, ConsentState Consent)> ReadAsync(Guid orderId, CancellationToken ct = default)
    {
        try
        {
            var kayit = await db.TrackingOrderContexts.AsNoTracking()
                .Where(x => x.OrderId == orderId)
                .Select(x => new { x.ContextJson, x.ConsentJson })
                .FirstOrDefaultAsync(ct);
            if (kayit is null) return (ClientContext.Bos, ConsentState.Deny);
            var client = JsonSerializer.Deserialize<ClientContext>(kayit.ContextJson, OutboxCommerceEventPublisher.JsonAyar) ?? ClientContext.Bos;
            var consent = JsonSerializer.Deserialize<ConsentState>(kayit.ConsentJson, OutboxCommerceEventPublisher.JsonAyar) ?? ConsentState.Deny;
            return (client, consent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sipariş takip bağlamı okunamadı (orderId={OrderId})", orderId);
            return (ClientContext.Bos, ConsentState.Deny);
        }
    }
}
