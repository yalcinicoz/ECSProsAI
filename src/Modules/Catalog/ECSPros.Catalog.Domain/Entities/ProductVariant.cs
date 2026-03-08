using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal? BaseCost { get; set; }
    public bool IsActive { get; set; } = true;

    public Product Product { get; set; } = null!;
    public ICollection<ProductVariantAttribute> VariantAttributes { get; set; } = new List<ProductVariantAttribute>();
    public ICollection<ProductVariantImage> Images { get; set; } = new List<ProductVariantImage>();
    public ICollection<FirmPlatformVariant> FirmPlatformVariants { get; set; } = new List<FirmPlatformVariant>();
    public ICollection<ProductUnit> Units { get; set; } = new List<ProductUnit>();
}
