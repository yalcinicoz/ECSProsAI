using ECSPros.Inventory.Domain.Events;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Infrastructure.Messaging;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Store;

/// <summary>
/// H8: "Stok gelince haber ver" tüketicisi (C9'un bekleyen yarısı) — StockIncreasedEvent
/// üzerine ilgili varyantların `active` stock_alerts kayıtlarına e-posta gönderir ve
/// kaydı `notified` yapar (tek seferlik; idempotenlik Status'ta). Kompozisyon Api
/// katmanında (E7/E8 deseni): Storefront kayıtları + Catalog gösterim bilgisi + Shared
/// e-posta. Bildirim hatası stok işlemini ASLA düşürmez: gönderilemeyen kayıt `active`
/// kalır (bir sonraki stok girişinde yeniden denenir), tüm istisnalar yutulup loglanır.
/// </summary>
public class StockAlertNotifier(
    IStorefrontDbContext storefrontDb,
    IProductService productService,
    IEmailService emailService,
    IStoreLinkBuilder linkBuilder,
    ILogger<StockAlertNotifier> logger) : INotificationHandler<StockIncreasedEvent>
{
    public async Task Handle(StockIncreasedEvent notification, CancellationToken ct)
    {
        try
        {
            var kayitlar = await storefrontDb.StockAlerts
                .Where(a => a.Status == "active" && notification.VariantIds.Contains(a.VariantId))
                .ToListAsync(ct);
            if (kayitlar.Count == 0) return;

            var gorunumler = await productService.GetVariantDisplayAsync(
                kayitlar.Select(k => k.VariantId).Distinct().ToList(), ct);

            foreach (var kayit in kayitlar)
            {
                if (string.IsNullOrWhiteSpace(kayit.Email))
                {
                    // Gönderilecek adres yok — kayıt açık bırakılmaz, döngüye girmesin.
                    kayit.Status = "cancelled";
                    logger.LogWarning("Stok bildirimi e-postasız kayıt iptal edildi: {AlertId}", kayit.Id);
                    continue;
                }

                gorunumler.TryGetValue(kayit.VariantId, out var g);
                var urunAdi = g?.ProductNameI18n.GetValueOrDefault("tr")
                              ?? g?.ProductNameI18n.Values.FirstOrDefault()
                              ?? kayit.ProductCode ?? "Ürününüz";
                var kod = g?.ProductCode ?? kayit.ProductCode;
                var link = kod is null ? null : await linkBuilder.BuildAsync(kayit.FirmPlatformId, "/urun/" + kod, ct);
                var secenek = string.IsNullOrWhiteSpace(kayit.VariantInfo) ? g?.OptionsText : kayit.VariantInfo;

                var govde = $"""
                    <div style="font-family:Arial,sans-serif;max-width:520px;margin:0 auto;color:#333">
                      <h2 style="font-size:18px">Beklediğiniz ürün stokta! 🎉</h2>
                      <p><strong>{urunAdi}</strong>{(string.IsNullOrWhiteSpace(secenek) ? "" : $" ({secenek})")} yeniden stoklarımızda.</p>
                      {(link is null ? "" : $"""<p><a href="{link}" style="display:inline-block;background:#f27a1a;color:#fff;padding:10px 18px;border-radius:10px;text-decoration:none">Ürüne Git</a></p>""")}
                      <p style="font-size:12px;color:#888">Bu e-posta, ürün için "stok gelince haber ver" isteğiniz üzerine bir kez gönderildi.</p>
                    </div>
                    """;

                try
                {
                    await emailService.SendAsync(kayit.Email, $"Stokta! {urunAdi}", govde, ct);
                    kayit.Status = "notified";
                    kayit.NotifiedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    // Kayıt active kalır — bir sonraki stok girişinde yeniden denenir.
                    logger.LogWarning(ex, "Stok bildirimi gönderilemedi: {AlertId} → {Email}", kayit.Id, kayit.Email);
                }
            }

            await storefrontDb.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stok bildirimi tüketicisi hata verdi — stok işlemi etkilenmedi.");
        }
    }
}
