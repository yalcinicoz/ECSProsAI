using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Storefront.Domain.Entities;

/// <summary>
/// Bir varyantın belirli bir satış kanalındaki (site, pazaryeri, bayi) fiyat override'ı.
/// Kanala özel veri olduğu için Catalog değil Storefront şemasında tutulur.
/// </summary>
public class ChannelVariant : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid VariantId { get; set; }
    public string? PriceType { get; set; } // manual, multiplier
    public decimal? PriceMultiplier { get; set; }
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
