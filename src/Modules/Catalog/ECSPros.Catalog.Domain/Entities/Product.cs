using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class Product : BaseEntity
{
    public Guid ProductGroupId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? ShortDescriptionI18n { get; set; }
    public bool IsActive { get; set; } = true;

    public ProductGroup ProductGroup { get; set; } = null!;
    public ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<CategoryProduct> CategoryProducts { get; set; } = new List<CategoryProduct>();
    public ICollection<FirmPlatformProduct> FirmPlatformProducts { get; set; } = new List<FirmPlatformProduct>();
}
