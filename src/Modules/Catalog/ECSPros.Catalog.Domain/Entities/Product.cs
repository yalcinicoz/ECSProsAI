using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class Product : BaseEntity
{
    public Guid ProductGroupId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? ShortDescriptionI18n { get; set; }
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public decimal BasePrice { get; set; } = 0;
    public decimal? BaseCost { get; set; }
    public int TaxRate { get; set; } = 18;
    public bool IsActive { get; set; } = true;
    public Guid? SupplierId { get; set; }
    public string? SupplierProductCode { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Slug { get; set; }
    public Dictionary<string, string>? MetaTitleI18n { get; set; }
    public Dictionary<string, string>? MetaDescriptionI18n { get; set; }
    public Dictionary<string, string>? MetaKeywordsI18n { get; set; }

    public ProductGroup ProductGroup { get; set; } = null!;
    public ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<FirmPlatformProduct> FirmPlatformProducts { get; set; } = new List<FirmPlatformProduct>();
}
