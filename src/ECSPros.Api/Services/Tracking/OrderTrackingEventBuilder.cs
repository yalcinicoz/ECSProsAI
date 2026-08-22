using ECSPros.Api.Services.Store;
using ECSPros.Crm.Application.Services;
using ECSPros.Order.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Contracts.Tracking;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Services.Tracking;

public interface IOrderTrackingEventBuilder
{
    /// <summary>order_completed — kaynak filtresi (§4.2) geçmeyen siparişte null döner.</summary>
    Task<CommerceEvent?> BuildOrderCompletedAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>refund — tam (iptal) veya kısmi (iade kalemleri). Yalnız daha önce onaylanmış sipariş.</summary>
    Task<CommerceEvent?> BuildRefundAsync(Guid orderId, IReadOnlyList<(Guid VariantId, int Quantity)>? returnedItems, string reason, CancellationToken ct = default);

    /// <summary>Kanalın purchaseAt ayarı (confirmed | created).</summary>
    Task<string> PurchaseAtAsync(Guid firmPlatformId, CancellationToken ct = default);
}

/// <summary>
/// Sipariş → CommerceEvent dönüştürücü (İE-2 Faz B-2). Kaynak filtresi: LegacyOrderId dolu
/// (eski sistem senkronu) ve ExternalOrderNumber dolu (pazaryeri) siparişler purchase ÜRETMEZ;
/// satıcı paneli/partner API ürünlerinin vitrin siparişleri ÜRETİR (karar §7-9). Tarayıcı
/// bağlamı + consent checkout anında saklanan tracking_order_context'ten; üye e-posta/telefon
/// CRM'den hash'lenerek eklenir (ham PII event'e girmez).
/// </summary>
public sealed class OrderTrackingEventBuilder(
    IOrderDbContext orderDb,
    ICrmDbContext crmDb,
    IProductService productService,
    ITrackingOrderContextRecorder contextRecorder,
    ITrackingSettingsProvider trackingSettings,
    ILogger<OrderTrackingEventBuilder> logger) : IOrderTrackingEventBuilder
{
    public async Task<string> PurchaseAtAsync(Guid firmPlatformId, CancellationToken ct = default)
        => (await trackingSettings.GetAsync(firmPlatformId, ct)).PurchaseAt;

    public async Task<CommerceEvent?> BuildOrderCompletedAsync(Guid orderId, CancellationToken ct = default)
    {
        var siparis = await SiparisOkuAsync(orderId, ct);
        if (siparis is null) return null;

        var items = await KalemleriKurAsync(siparis, null, ct);
        var (client, consent) = await BaglamAsync(siparis, ct);

        var extra = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shipping"] = siparis.TotalExpense.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["tax"] = siparis.TotalTax.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["discount"] = siparis.TotalDiscount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            ["payment_method"] = siparis.PaymentMethod ?? "",
            ["order_number"] = siparis.OrderNumber
        };
        var kupon = siparis.Discounts.FirstOrDefault(d => d.DiscountType == "coupon");
        if (kupon is not null) extra["coupon"] = kupon.DiscountName;

        return new CommerceEvent(
            Name: CommerceEventNames.OrderCompleted,
            OccurredAt: DateTime.UtcNow,
            FirmPlatformId: siparis.FirmPlatformId,
            DedupId: siparis.Id.ToString("D"),
            Source: "server",
            MemberId: siparis.MemberId,
            Currency: siparis.CurrencyCode,
            Value: siparis.GrandTotal,
            TransactionId: siparis.OrderNumber,
            Items: items,
            Client: client,
            Consent: consent,
            Extra: extra);
    }

    public async Task<CommerceEvent?> BuildRefundAsync(Guid orderId, IReadOnlyList<(Guid VariantId, int Quantity)>? returnedItems, string reason, CancellationToken ct = default)
    {
        var siparis = await SiparisOkuAsync(orderId, ct);
        if (siparis is null || siparis.ConfirmedAt is null) return null; // hiç onaylanmadıysa purchase da gitmemişti

        var items = await KalemleriKurAsync(siparis, returnedItems, ct);
        var value = returnedItems is null
            ? siparis.GrandTotal
            : items.Sum(i => i.Price * i.Quantity - i.Discount);
        var (client, consent) = await BaglamAsync(siparis, ct);

        return new CommerceEvent(
            Name: CommerceEventNames.Refund,
            OccurredAt: DateTime.UtcNow,
            FirmPlatformId: siparis.FirmPlatformId,
            DedupId: returnedItems is null ? $"{siparis.Id:D}:cancel" : $"{siparis.Id:D}:return:{Guid.NewGuid():N}",
            Source: "server",
            MemberId: siparis.MemberId,
            Currency: siparis.CurrencyCode,
            Value: value,
            TransactionId: siparis.OrderNumber,
            Items: items,
            Client: client,
            Consent: consent,
            Extra: new Dictionary<string, string> { ["reason"] = reason, ["order_number"] = siparis.OrderNumber, ["partial"] = returnedItems is null ? "false" : "true" });
    }

    // ───────────────────────── yardımcılar ─────────────────────────

    private async Task<ECSPros.Order.Domain.Entities.Order?> SiparisOkuAsync(Guid orderId, CancellationToken ct)
    {
        var o = await orderDb.Orders.AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Discounts)
            .FirstOrDefaultAsync(x => x.Id == orderId, ct);
        if (o is null) return null;
        if (o.LegacyOrderId is not null) return null;            // eski sistem senkronu
        if (!string.IsNullOrWhiteSpace(o.ExternalOrderNumber)) return null; // pazaryeri
        return o;
    }

    private async Task<List<CommerceItem>> KalemleriKurAsync(
        ECSPros.Order.Domain.Entities.Order siparis, IReadOnlyList<(Guid VariantId, int Quantity)>? sadece, CancellationToken ct)
    {
        var kalemler = siparis.Items.Where(i => i.Status != "cancelled").ToList();
        Dictionary<Guid, VariantDisplayInfo> gorunum = new();
        try
        {
            gorunum = await productService.GetVariantDisplayAsync(kalemler.Select(k => k.VariantId).Distinct().ToList(), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Takip kalem zenginleştirmesi başarısız (orderId={OrderId})", siparis.Id);
        }

        var sonuc = new List<CommerceItem>();
        foreach (var k in kalemler)
        {
            var adet = k.Quantity;
            if (sadece is not null)
            {
                var eslesen = sadece.Where(s => s.VariantId == k.VariantId).Sum(s => s.Quantity);
                if (eslesen <= 0) continue;
                adet = Math.Min(eslesen, k.Quantity);
            }
            gorunum.TryGetValue(k.VariantId, out var g);
            var ad = g?.ProductNameI18n is { } n && (n.TryGetValue("tr", out var tr) || n.Values.FirstOrDefault() is { } tr2 && (tr = tr2) is not null)
                ? tr : k.ProductName;
            var indirim = k.Quantity > 0 ? Math.Round(k.DiscountAmount * adet / k.Quantity, 2) : 0m;
            sonuc.Add(new CommerceItem(
                ItemId: g?.Sku ?? (string.IsNullOrWhiteSpace(k.Sku) ? k.VariantId.ToString("D") : k.Sku), // varyant SKU (feed id) > sipariş kalemi Sku (ürün kodu)
                ItemGroupId: g?.ProductCode ?? "",
                Name: ad ?? k.ProductName,
                Brand: null,
                Category: null,
                Variant: string.IsNullOrWhiteSpace(g?.OptionsText) ? (string.IsNullOrWhiteSpace(k.VariantInfo) ? null : k.VariantInfo) : g!.OptionsText,
                Price: k.UnitPrice,
                Quantity: adet,
                Discount: indirim));
        }
        return sonuc;
    }

    private async Task<(ClientContext, ConsentState)> BaglamAsync(ECSPros.Order.Domain.Entities.Order siparis, CancellationToken ct)
    {
        var (client, consent) = await contextRecorder.ReadAsync(siparis.Id, ct);
        if (client.EmailSha256 is null || client.PhoneSha256 is null)
        {
            string? email = null, phone = siparis.ShippingRecipientPhone;
            if (siparis.MemberId is { } mid)
            {
                try
                {
                    var uye = await crmDb.Members.AsNoTracking().Where(m => m.Id == mid)
                        .Select(m => new { m.Email, m.Phone }).FirstOrDefaultAsync(ct);
                    email = uye?.Email; phone = uye?.Phone ?? phone;
                }
                catch (Exception ex) { logger.LogWarning(ex, "Takip için üye okunamadı ({MemberId})", mid); }
            }
            client = client with
            {
                EmailSha256 = client.EmailSha256 ?? TrackingHttpContextReader.Sha256(TrackingHttpContextReader.NormalizeEmail(email)),
                PhoneSha256 = client.PhoneSha256 ?? TrackingHttpContextReader.Sha256(TrackingHttpContextReader.NormalizePhone(phone)),
                ExternalIdSha256 = client.ExternalIdSha256 ?? (siparis.MemberId is { } m2 ? TrackingHttpContextReader.Sha256(m2.ToString("D")) : null)
            };
        }
        return (client, consent);
    }
}
