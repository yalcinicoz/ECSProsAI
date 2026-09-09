using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Queries.CheckoutPreview;

/// <summary>
/// M2 (2026-09-09, mobil isteği): <b>sipariş ön izlemesi</b> — checkout ile AYNI hesap,
/// sipariş oluşturmadan.
///
/// Neden gerekli: mobil, ödeme ekranındaki tutarı kendi hesaplamak zorundaydı; sunucu
/// checkout'ta başka bir tutar bulunca müşteri sürprizle karşılaşıyordu. Bu uç, ödemeden
/// önce "ne ödeyeceğim?" sorusunun tek doğru yanıtını verir.
///
/// Yan etkisi YOKTUR: sipariş/kupon kullanımı yazılmaz, stok düşülmez. Fiyat, kampanya,
/// kupon ve kargo aynı servislerden çözülür; birleştirme <c>SiparisTutarKurali</c> ile
/// yapılır — iki yol birbirinden ayrışamaz.
/// </summary>
public record CheckoutOnizlemeQuery(
    Guid FirmPlatformId,
    List<OnizlemeKalemi> Kalemler,
    string? PaymentMethod = null,
    string? CouponCode = null,
    Guid? MemberId = null,
    string CurrencyCode = "TRY") : IRequest<Result<CheckoutOnizlemesi>>;

/// <param name="ItemId">Sepet satırının kimliği — istemci yanıtı satırla eşleştirsin diye taşınır.</param>
public record OnizlemeKalemi(Guid VariantId, int Quantity, Guid? ItemId = null);

public record OnizlemeSatiri(
    Guid? ItemId,
    Guid VariantId,
    int Quantity,
    decimal UnitPrice,               // satış fiyatı (ürün-bazlı kampanya varsa kampanyalı)
    decimal? CompareAtPrice,         // çizili referans (indirim yoksa null)
    decimal LineTotal,
    decimal? CompareAtLineTotal,
    int DiscountPercent);

public record CheckoutOnizlemesi(
    decimal Subtotal,
    decimal CampaignDiscount,        // sepet-seviyesi kampanya indirimi (satır kampanyaları fiyata gömülü)
    decimal CouponDiscount,
    decimal CodFee,                  // kapıda ödeme hizmet bedeli (yöntem kapıda değilse 0)
    decimal ShippingFee,
    string? ShippingFreeReason,      // threshold | campaign | none — ücretliyse null
    decimal? RemainingForFreeShipping,
    decimal Total,
    string CurrencyCode,
    List<OnizlemeSatiri> Lines,
    List<AppliedCampaign>? Campaigns = null,
    string? CouponError = null);     // kupon geçersizse SEBEP (ön izleme durmaz; ekran uyarır)

public class CheckoutOnizlemeQueryHandler(
    IProductService productService,
    IChannelProductFlagService flagService,
    IChannelPricingService pricingService,
    IProductCampaignResolver campaignResolver,
    ICouponValidator couponValidator,
    IShippingOptionsProvider shippingOptions,
    IPaymentOptionsProvider paymentOptions)
    : IRequestHandler<CheckoutOnizlemeQuery, Result<CheckoutOnizlemesi>>
{
    public async Task<Result<CheckoutOnizlemesi>> Handle(CheckoutOnizlemeQuery r, CancellationToken ct)
    {
        if (r.Kalemler.Count == 0) return Result.Failure<CheckoutOnizlemesi>("Sepet boş.");

        var varyantIdler = r.Kalemler.Select(k => k.VariantId).Distinct().ToList();
        var kanalDisi = await flagService.GetChannelExcludedProductIdsAsync(r.FirmPlatformId, ct);
        var kanalFiyatlar = await pricingService.GetActiveVariantPricesAsync(r.FirmPlatformId, varyantIdler, ct);

        // Checkout ile AYNI fiyat kaynağı: kanal fiyatı → varyantın taban fiyatı.
        var sunucuFiyat = new Dictionary<Guid, decimal>();
        var urunIdByVariant = new Dictionary<Guid, Guid>();
        foreach (var vid in varyantIdler)
        {
            var bilgi = await productService.GetVariantAsync(vid, ct);
            if (bilgi is null || !bilgi.IsActive)
                return Result.Failure<CheckoutOnizlemesi>("Sepetteki ürünlerden biri şu an satışa kapalı.");
            if (kanalDisi.Contains(bilgi.ProductId))
                return Result.Failure<CheckoutOnizlemesi>("Sepetteki ürünlerden biri bu kanalda satışa kapalı.");

            var fiyat = kanalFiyatlar.TryGetValue(vid, out var cp) && cp.Price is > 0 ? cp.Price!.Value : bilgi.BasePrice;
            if (fiyat <= 0)
                return Result.Failure<CheckoutOnizlemesi>("Sepetteki ürünlerden birinin fiyatı doğrulanamadı.");
            sunucuFiyat[vid] = fiyat;
            urunIdByVariant[vid] = bilgi.ProductId;
        }

        var kargoAyar = await shippingOptions.GetAsync(r.FirmPlatformId, ct);
        var kampanya = await campaignResolver.ResolveCartAsync(
            r.FirmPlatformId,
            r.Kalemler.Select(k => new CartCampaignItem(
                k.VariantId, urunIdByVariant[k.VariantId], k.Quantity, sunucuFiyat[k.VariantId])).ToList(),
            ct, kargoAyar.Fee, r.PaymentMethod);

        decimal EtkinFiyat(Guid vid) => kampanya.ItemUnitPrices.GetValueOrDefault(vid, sunucuFiyat[vid]);
        var subtotal = r.Kalemler.Sum(k => k.Quantity * EtkinFiyat(k.VariantId));

        // Kupon: checkout ile AYNI doğrulayıcı. Fark: ön izleme HATA DÖNMEZ — kupon geçersizse
        // sebebi taşır ve tutarı kuponsuz gösterir (kullanıcı ödeme öncesi görsün, akış durmasın).
        var kuponIndirim = 0m;
        string? kuponHatasi = null;
        if (!string.IsNullOrWhiteSpace(r.CouponCode))
        {
            var kupon = await couponValidator.ValidateAsync(r.CouponCode!.Trim(), subtotal, r.MemberId, ct);
            if (kupon.Gecerli) kuponIndirim = Math.Min(kupon.DiscountAmount, subtotal);
            else kuponHatasi = kupon.Error;
        }

        var odemeSecenekleri = await paymentOptions.GetAsync(r.FirmPlatformId, ct);
        if (!string.IsNullOrWhiteSpace(r.PaymentMethod) && !odemeSecenekleri.YontemAcik(r.PaymentMethod!))
            return Result.Failure<CheckoutOnizlemesi>(
                "Seçilen ödeme yöntemi bu mağazada şu an kullanılamıyor; lütfen başka bir yöntem seçin.");

        var kapidaOdeme = r.PaymentMethod is "kapida-nakit" or "kapida-kart";
        var tutar = ECSPros.Order.Application.Services.SiparisTutarKurali.Hesapla(
            subtotal, kuponIndirim, kampanya.CartDiscount,
            kapidaOdeme, odemeSecenekleri.CodServiceFee,
            kargoAyar.Fee, kargoAyar.FreeThreshold, kampanya.Shipping?.Fee);

        var satirlar = r.Kalemler.Select(k =>
        {
            var taban = sunucuFiyat[k.VariantId];
            var kampanyaFiyat = kampanya.ItemUnitPrices.TryGetValue(k.VariantId, out var kf) ? (decimal?)kf : null;
            var kanalCizili = kanalFiyatlar.TryGetValue(k.VariantId, out var cp) ? cp.CompareAtPrice : null;
            var (fiyat, cizili) = KartFiyatGorunumu.Hesapla(taban, kanalCizili, kampanyaFiyat);
            var yuzde = cizili is { } c && c > 0
                ? (int)Math.Round((c - fiyat) / c * 100m, MidpointRounding.AwayFromZero) : 0;
            return new OnizlemeSatiri(k.ItemId, k.VariantId, k.Quantity, fiyat, cizili,
                fiyat * k.Quantity, cizili is { } c2 ? c2 * k.Quantity : null, yuzde);
        }).ToList();

        return Result.Success(new CheckoutOnizlemesi(
            Subtotal: subtotal,
            CampaignDiscount: kampanya.CartDiscount,
            CouponDiscount: kuponIndirim,
            CodFee: tutar.Masraf,
            ShippingFee: tutar.KargoBedeli,
            ShippingFreeReason: tutar.KargoBedavaSebebi,
            RemainingForFreeShipping: KargoUcretiKurali.EsigeKalan(
                kargoAyar.Fee, kargoAyar.FreeThreshold, subtotal - tutar.Indirim, kampanya.Shipping?.Fee),
            Total: tutar.GenelToplam,
            CurrencyCode: r.CurrencyCode,
            Lines: satirlar,
            Campaigns: kampanya.Applied.Count > 0 ? kampanya.Applied : null,
            CouponError: kuponHatasi));
    }
}
