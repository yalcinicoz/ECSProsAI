namespace ECSPros.Shared.Contracts;

/// <summary>
/// Bir ürünün platform bağlamındaki ETKİN kampanyası (en yüksek öncelikli, kapsam+dışlama sonrası).
/// BenefitKind: "percent"/"amount" → ürün-bazlı kampanyalı fiyat hesaplanabilir (kart/detay);
/// "cart_only" → sepet-bağımlı (yalnız rozet + "Sepette").
/// </summary>
public record ProductCampaignInfo(
    Guid CampaignId,
    string Code,
    string Name,
    string? BadgeLabel,
    string TypeCode,
    string BenefitKind,
    decimal BenefitValue,
    decimal? MaxDiscount,
    string? BadgeColor = null);   // 2026-08-03: bant renk anahtarı (CampaignBadgePalette)

/// <summary>Kart bandı için kampanya rozeti: ad + çözülmüş renk (hex; null = varsayılan marka rengi).</summary>
public record CampaignBadge(string Name, string? Color);

/// <summary>Kampanya bant renk paleti — panel seçenekleri ve hex çözümü TEK yerden.
/// Anahtarlar admin formundaki seçeneklerle birebir; bilinmeyen anahtar varsayılana düşer.</summary>
public static class CampaignBadgePalette
{
    public static readonly IReadOnlyDictionary<string, string> Renkler = new Dictionary<string, string>
    {
        ["kirmizi"] = "#DC2626",   // indirim / fiyat vurgusu
        ["turuncu"] = "#EA580C",   // fırsat / kampanya
        ["amber"] = "#D97706",     // sınırlı süre / aciliyet
        ["yesil"] = "#16A34A",     // kargo / kazanç
        ["mavi"] = "#2563EB",      // bilgi / sepet kampanyası
        ["mor"] = "#7C3AED",       // özel / ayrıcalık
        ["pembe"] = "#DB2777",     // sezon / moda
        ["siyah"] = "#111827"      // premium / kapsül
    };

    /// <summary>Anahtar → hex; null/boş/bilinmeyen → null (istemci varsayılan marka rengini kullanır).</summary>
    public static string? Resolve(string? key) =>
        key is { Length: > 0 } && Renkler.TryGetValue(key, out var hex) ? hex : null;
}

/// <summary>
/// F2: kampanya çözümleme — tek kural seti (platform + kapsam FillType/materyalize + dışlama +
/// öncelik). Hem vitrin fiyatı görünümü (F3 kart/detay) hem sepet/sipariş (F4) bu servisten beslenir.
/// </summary>
/// <summary>Checkout sepet kalemi — kampanya çözümlemesi için (varyant + ürün + adet + kanal birim fiyatı).</summary>
public record CartCampaignItem(Guid VariantId, Guid ProductId, int Quantity, decimal UnitPrice);

/// <summary>Uygulanan kampanya özeti (sepet/ödeme özeti + operasyon için).
/// VariantIds: indirime konu kalemler (satır rozetleri için, 2026-08-03 additive).</summary>
public record AppliedCampaign(string Code, string Name, decimal Amount, string Kind,
    List<Guid>? VariantIds = null);

/// <summary>
/// Checkout sonucu: ürün-bazlı kampanyalı birim fiyatlar (varyant → fiyat) + sepet-seviyesi
/// kampanya indirimi (buy_x_get_y/min_cart…) + uygulanan kampanya özeti. Çift sayım yok: her ürünün
/// TEK etkin kampanyası vardır — ürün-bazlıysa fiyata yansır, cart_only ise indirime.
/// </summary>
/// <summary>ItemDiscounts (2026-08-03, additive): sepet-seviyesi indirimin kalemlere
/// AĞIRLIKLI dağılımı (varyant → satır indirimi; satır tutarına oranla, kuruş artığı son
/// kaleme). buy_x_get_y'de "en ucuz bedava" TUTARI belirler; müşteriye tek kalemde değil
/// sete katılan tüm kalemlere yansıtılır — iade tutarı kalem bazında doğru olur.</summary>
public record CartCampaignResult(
    Dictionary<Guid, decimal> ItemUnitPrices,
    decimal CartDiscount,
    List<AppliedCampaign> Applied,
    Dictionary<Guid, decimal>? ItemDiscounts = null);

public interface IProductCampaignResolver
{
    /// <summary>Verilen ürünler için ürün başına etkin kampanya (yoksa anahtar yok).</summary>
    Task<Dictionary<Guid, ProductCampaignInfo>> ResolveForProductsAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    /// <summary>2026-08-03: ürün başına kapsam+dışlamadan geçen TÜM aktif kampanyalar
    /// (öncelik azalan; ilk eleman = etkin/kazanan). Kart bandında birden çok kampanyanın
    /// dönüşümlü gösterimi için — fiyat hesabı yine yalnız kazanandan yapılır.</summary>
    Task<Dictionary<Guid, List<ProductCampaignInfo>>> ResolveAllForProductsAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    /// <summary>F4 checkout: sepet için kampanyaları uygular — ürün-bazlı fiyat (fiyata) +
    /// sepet-seviyesi indirim (toplama). Sunucu-taraflı; istemci fiyatına güvenilmez.</summary>
    Task<CartCampaignResult> ResolveCartAsync(
        Guid firmPlatformId, IReadOnlyList<CartCampaignItem> items, CancellationToken ct = default);
}

/// <summary>Kampanyalı birim fiyat hesabı — F3 (vitrin) ve F4 (checkout) AYNI mantığı kullanır.</summary>
public static class CampaignPricing
{
    /// <summary>Ürün-bazlı kampanyalı fiyat; hesaplanamıyorsa (cart_only / geçersiz / fiyatı düşürmüyor) null.</summary>
    public static decimal? EffectivePrice(ProductCampaignInfo info, decimal basePrice)
    {
        if (basePrice <= 0) return null;

        decimal price = info.BenefitKind switch
        {
            "percent" => Math.Round(basePrice * (1 - info.BenefitValue / 100m), 2),
            "amount" => basePrice - info.BenefitValue,
            _ => -1m
        };
        if (price < 0) return null;

        // Yüzde indirimde tavan tutar
        if (info.BenefitKind == "percent" && info.MaxDiscount is { } max)
        {
            var disc = basePrice - price;
            if (disc > max) price = basePrice - max;
        }

        return price >= basePrice ? null : Math.Max(0m, price);
    }
}
