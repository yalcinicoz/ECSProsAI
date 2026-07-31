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
    decimal? MaxDiscount);

/// <summary>
/// F2: kampanya çözümleme — tek kural seti (platform + kapsam FillType/materyalize + dışlama +
/// öncelik). Hem vitrin fiyatı görünümü (F3 kart/detay) hem sepet/sipariş (F4) bu servisten beslenir.
/// </summary>
public interface IProductCampaignResolver
{
    /// <summary>Verilen ürünler için ürün başına etkin kampanya (yoksa anahtar yok).</summary>
    Task<Dictionary<Guid, ProductCampaignInfo>> ResolveForProductsAsync(
        Guid firmPlatformId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
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
