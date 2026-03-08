using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class FirmPlatformVariant : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid VariantId { get; set; }
    public string? PriceType { get; set; } // manual, multiplier
    public decimal? PriceMultiplier { get; set; }
    public decimal? Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductVariant Variant { get; set; } = null!;
}
