using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetCart;

public record GetCartQuery(
    Guid? CartId = null,
    Guid? MemberId = null,
    string? SessionId = null,
    Guid? FirmPlatformId = null,
    List<Guid>? ExcludedVariantIds = null) : IRequest<Result<CartDto?>>;
    // ExcludedVariantIds (2026-08-03): sepet sayfasında checkbox'ı KALDIRILAN varyantlar —
    // kalem listesinde kalırlar ama kampanya hesabına girmezler (seçim anında yeniden hesap).

// Kampanya alanları (2026-08-03, additive): CampaignDiscount sepet-seviyesi indirim
// (buy_x_get_y/min_cart — ödenecek tutardan düşülür), Campaigns uygulanan kampanya özetleri.
// Ürün-bazlı kampanya fiyatı kalemde CampaignUnitPrice olarak döner (görsel çizik fiyat için).
public record CartDto(
    Guid Id,
    Guid? MemberId,
    string? SessionId,
    Guid FirmPlatformId,
    string CurrencyCode,
    List<CartItemDto> Items,
    decimal Subtotal,
    decimal CampaignDiscount = 0m,
    List<AppliedCampaign>? Campaigns = null,
    // 2026-09-09 (additive) kargo: ShippingFee = kanalın sabit kargo bedeli (0 = kanalda kargo
    // ücreti tanımlı değil), FreeShippingThreshold = kanal ücretsiz kargo eşiği (0 = eşik yok),
    // FreeShippingCampaign = kargo kampanyası varsa adı. Nihai bedel KargoUcretiKurali ile
    // hesaplanır; sepette kupon istemci tarafında olduğundan ekran aynı kuralı aynadan uygular,
    // TAHSİLAT checkout'ta sunucuda yeniden hesaplanır.
    decimal ShippingFee = 0m,
    decimal FreeShippingThreshold = 0m,
    string? FreeShippingCampaign = null,
    decimal? ShippingCampaignFee = null,
    // ── M1 (2026-09-09, mobil isteği): ÇÖZÜLMÜŞ değerler — istemci kuralı taklit etmesin.
    // ShippingFeeResolved: KargoUcretiKurali'nın sonucu (kanal ücreti + eşik + kargo kampanyası).
    // ShippingFreeReason: "threshold" | "campaign" | "none" (ücretsizse sebebi), ücretliyse null.
    // RemainingForFreeShipping: eşiğe kalan tutar ("X TL daha ekleyin"); eşik yoksa/bedavaysa null.
    // TotalDiscount: TÜM kampanya indirimlerinin toplamı (ürün-bazlı + sepet-seviyesi payı) —
    //   MK1 kararı: CampaignDiscount'un anlamı DEĞİŞMEDİ (yalnız sepet-seviyesi), bu ayrı alandır.
    //   Kupon buraya GİRMEZ: kupon sepette istemci tarafındadır, tahsilat checkout'ta hesaplanır.
    // Total: ödenecek tutar = Subtotal − TotalDiscount + ShippingFeeResolved (kupon hariç).
    decimal ShippingFeeResolved = 0m,
    string? ShippingFreeReason = null,
    decimal? RemainingForFreeShipping = null,
    decimal TotalDiscount = 0m,
    decimal Total = 0m);

// B5: gösterim alanları (ProductCode/NameI18n/ImageUrl/OptionsText) additive eklendi —
// IProductService (Catalog) üzerinden zenginleştirilir; eski istemciler etkilenmez.
public record CartItemDto(
    Guid Id,
    Guid VariantId,
    int Quantity,
    decimal AddedPrice,
    decimal LineTotal,
    bool IsAvailable,
    int AvailableQuantity,
    string? ProductCode = null,
    Dictionary<string, string>? ProductNameI18n = null,
    string? ImageUrl = null,
    string? OptionsText = null,
    decimal? CampaignUnitPrice = null,
    decimal CampaignLineDiscount = 0m,    // satırın toplam kampanya indirimi (ürün-bazlı + ağırlıklı sepet payı)
    string? Sku = null,                    // İE-3 (2026-08-22): varyant SKU — takip item_id
    Guid? ColorValueId = null,             // A2 (2026-09-07): renk değer kimliği
    Guid? SizeValueId = null,              // A2: beden değer kimliği
    // ── M1 (2026-09-09): liste kartıyla AYNI fiyat sözleşmesi (KartFiyatGorunumu).
    // UnitPrice = satış fiyatı (ürün-bazlı kampanya varsa kampanyalı) — listedeki `price`.
    // CompareAtPrice = çizili referans (indirim yoksa null) — listedeki `compareAtPrice`.
    // AddedPrice/LineTotal ESKİ anlamlarını korur (kampanya öncesi taban) — web istemcisi kırılmaz.
    decimal UnitPrice = 0m,
    decimal? CompareAtPrice = null,
    decimal? CompareAtLineTotal = null,
    int DiscountPercent = 0);

public class GetCartQueryHandler(
    ICrmDbContext db,
    IProductService productService,
    IProductCampaignResolver campaignResolver,
    IShippingOptionsProvider shippingOptions,
    IChannelPricingService pricingService)
    : IRequestHandler<GetCartQuery, Result<CartDto?>>
{
    public async Task<Result<CartDto?>> Handle(GetCartQuery request, CancellationToken ct)
    {
        var q = db.Carts.Include(c => c.Items).AsNoTracking();

        Cart? cart = null;
        if (request.CartId.HasValue)
            cart = await q.FirstOrDefaultAsync(c => c.Id == request.CartId.Value, ct);
        else if (request.MemberId.HasValue && request.FirmPlatformId.HasValue)
            cart = await q.FirstOrDefaultAsync(c => c.MemberId == request.MemberId.Value && c.FirmPlatformId == request.FirmPlatformId.Value, ct);
        else if (request.SessionId != null && request.FirmPlatformId.HasValue)
            cart = await q.FirstOrDefaultAsync(c => c.SessionId == request.SessionId && c.FirmPlatformId == request.FirmPlatformId.Value, ct);

        if (cart is null) return Result.Success<CartDto?>(null);

        var gosterim = await productService.GetVariantDisplayAsync(
            cart.Items.Select(i => i.VariantId).ToList(), ct);

        // M1 (2026-09-09): kanal fiyatları — çizili fiyat (CompareAtPrice) buradan gelir; liste kartı
        // ile AYNI kaynak. Okunamazsa sepet düşmez, çizili fiyat gösterilmez.
        var kanalFiyatlar = new Dictionary<Guid, ChannelVariantPrice>();
        // M3 (2026-09-09): ürün düzeyi çizili referans — varyantın KENDİ çizili fiyatı yoksa
        // kartta indirim görünüp sepette görünmüyordu (mobil P-00020386 örneği). Liste/detayla
        // aynı kural: ürünün varyantlarındaki en yüksek çizili fiyat.
        var urunCizili = new Dictionary<Guid, decimal>();
        try
        {
            kanalFiyatlar = await pricingService.GetActiveVariantPricesAsync(
                cart.FirmPlatformId, cart.Items.Select(i => i.VariantId).Distinct().ToList(), ct);
            var urunIdler = gosterim.Values.Select(g => g.ProductId).Where(id => id != Guid.Empty).Distinct().ToList();
            urunCizili = await pricingService.GetProductCompareAtPricesAsync(cart.FirmPlatformId, urunIdler, ct);
        }
        catch { /* kanal fiyatı okunamadı — çizili fiyatsız devam */ }

        // Kampanya çözümü (2026-08-03): checkout'la (F4) AYNI servis — ürün-bazlı kampanya
        // birim fiyata (CampaignUnitPrice), sepet-seviyesi (buy_x_get_y/min_cart) indirime.
        // Çözüm hatası sepeti düşürmez — kampanyasız görünümle devam edilir.
        // Kanal kargo ayarı (panel Kanallar): kampanya yüzde/tutar kapsamı bu bedelin üzerine uygulanır.
        var kargo = ShippingOptions.Varsayilan;
        try { kargo = await shippingOptions.GetAsync(cart.FirmPlatformId, ct); }
        catch { /* ayar okunamadı — kargo ücretsiz varsayılır (bugünkü davranış) */ }

        // ★ M1 (2026-09-09): satır fiyatı SUNUCUDAN gelir (kanal fiyatı → varyant taban fiyatı);
        // istemcinin eklerken gönderdiği fiyat kullanılmaz. Bu olmadan mobil, listedeki KAMPANYALI
        // fiyatı gönderdiğinde kampanya bir kez daha uygulanıyor ve indirim ÇİFT sayılıyordu.
        // Kayıtlı AddedPrice yalnız son çare (varyant/fiyat çözülemezse) — sepet boş kalmasın.
        decimal SunucuFiyat(Domain.Entities.CartItem kalem)
        {
            if (kanalFiyatlar.TryGetValue(kalem.VariantId, out var kf) && kf.Price is > 0) return kf.Price!.Value;
            if (gosterim.TryGetValue(kalem.VariantId, out var gi) && gi.BasePrice > 0) return gi.BasePrice;
            return kalem.AddedPrice;
        }

        var kampanya = new CartCampaignResult(new Dictionary<Guid, decimal>(), 0m, []);
        try
        {
            var dislananlar = request.ExcludedVariantIds is { Count: > 0 }
                ? request.ExcludedVariantIds.ToHashSet() : null;
            var kalemler = cart.Items
                .Where(i => (dislananlar is null || !dislananlar.Contains(i.VariantId))
                         && gosterim.TryGetValue(i.VariantId, out var g) && g.ProductId != Guid.Empty)
                .Select(i => new CartCampaignItem(
                    i.VariantId, gosterim[i.VariantId].ProductId, i.Quantity, SunucuFiyat(i)))
                .ToList();
            if (kalemler.Count > 0)
                kampanya = await campaignResolver.ResolveCartAsync(
                    cart.FirmPlatformId, kalemler, ct, kargo.Fee);
        }
        catch { /* kampanya çözülemedi — sepet kampanyasız döner */ }

        var items = cart.Items.Select(i =>
        {
            gosterim.TryGetValue(i.VariantId, out var g);
            var birimFiyat = SunucuFiyat(i);
            var kampanyaFiyat = kampanya.ItemUnitPrices.TryGetValue(i.VariantId, out var kf)
                ? (decimal?)kf : null;
            // Satırın kampanya indirimi: ürün-bazlı fiyat farkı + sepet-seviyesi ağırlıklı pay.
            // (Çakışmaz: ürünün TEK etkin kampanyası vardır — ya fiyata ya sepet payına yansır.)
            var satirIndirim = (kampanyaFiyat is { } f ? (birimFiyat - f) * i.Quantity : 0m)
                + (kampanya.ItemDiscounts?.GetValueOrDefault(i.VariantId) ?? 0m);
            // M1: liste kartıyla AYNI hesap — satış fiyatı + çizili referans tek kuraldan.
            var kanalCizili = kanalFiyatlar.TryGetValue(i.VariantId, out var kfp) ? kfp.CompareAtPrice : null;
            // Varyantın kendi referansı yoksa ÜRÜN düzeyindeki referans (MK4 kararıyla aynı mantık).
            if (kanalCizili is not > 0 && g is not null && urunCizili.TryGetValue(g.ProductId, out var uc))
                kanalCizili = uc;
            var (satisFiyat, cizili) = ECSPros.Shared.Contracts.KartFiyatGorunumu.Hesapla(
                birimFiyat, kanalCizili, kampanyaFiyat);
            var yuzde = cizili is { } c && c > 0
                ? (int)Math.Round((c - satisFiyat) / c * 100m, MidpointRounding.AwayFromZero) : 0;

            return new CartItemDto(
                // AddedPrice/LineTotal artık SUNUCU fiyatını yansıtır: sepette gösterilen tutar
                // checkout'ta tahsil edilenle aynı olur (eskiden ekleme anındaki fiyat donuyordu).
                i.Id, i.VariantId, i.Quantity, birimFiyat, i.Quantity * birimFiyat,
                i.IsAvailable, i.AvailableQuantity,
                g?.ProductCode, g?.ProductNameI18n, g?.ImageUrl, g?.OptionsText,
                kampanyaFiyat, Math.Max(0m, Math.Round(satirIndirim, 2)), g?.Sku,
                g?.ColorValueId, g?.SizeValueId,
                satisFiyat, cizili, cizili is { } c2 ? c2 * i.Quantity : null, yuzde);
        }).ToList();

        // M1: sepet toplamları — kargo TEK kuraldan çözülür (checkout'la aynı kural).
        var araToplam = items.Sum(i => i.LineTotal);
        var toplamIndirim = Math.Round(items.Sum(i => i.CampaignLineDiscount), 2);
        var odenecekUrun = Math.Max(0m, araToplam - toplamIndirim);
        var kargoSonuc = KargoUcretiKurali.Hesapla(
            kargo.Fee, kargo.FreeThreshold, odenecekUrun, kampanya.Shipping?.Fee);
        var esigeKalan = KargoUcretiKurali.EsigeKalan(
            kargo.Fee, kargo.FreeThreshold, odenecekUrun, kampanya.Shipping?.Fee);

        var dto = new CartDto(
            cart.Id, cart.MemberId, cart.SessionId, cart.FirmPlatformId,
            cart.CurrencyCode, items, araToplam,
            kampanya.CartDiscount,
            kampanya.Applied.Count > 0 ? kampanya.Applied : null,
            kargo.Fee, kargo.FreeThreshold,
            kampanya.Shipping?.Name, kampanya.Shipping?.Fee,
            kargoSonuc.Ucret, kargoSonuc.BedavaSebebi, esigeKalan,
            toplamIndirim, odenecekUrun + kargoSonuc.Ucret);

        return Result.Success<CartDto?>(dto);
    }
}
