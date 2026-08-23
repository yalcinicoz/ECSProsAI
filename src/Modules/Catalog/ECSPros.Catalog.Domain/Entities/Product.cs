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
    // Global satış anahtarı (Katman 1): ürün TÜM sistemde satışa açık mı. Kapalıysa hiçbir
    // kanalda satılmaz (kanal ayarları ne olursa olsun). Eski ürün seviyesi IsActive'in
    // yerini aldı (IsDeleted zaten varlık için yeterli). Yeni ürünlerde varsayılan KAPALI.
    public bool IsSaleOpen { get; set; } = false;
    public Guid? SupplierId { get; set; }
    /// <summary>F5 (satis-kanali-ortak-kurgu K6): ürünün kaynağı — own (bizim) | seller (üçüncü taraf satıcı,
    /// Kapı 2 onayıyla katalog'a girer) | supply (Y4 dış tedarik kaynağı, F6). Kanal kapsam/görünürlük
    /// kuralları yetenek bayraklarıyla (thirdPartySellerProducts / externalSupplyProducts) bunu süzer.</summary>
    public string SourceType { get; set; } = "own";
    public string? SupplierProductCode { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Slug { get; set; }
    public Dictionary<string, string>? MetaTitleI18n { get; set; }
    public Dictionary<string, string>? MetaDescriptionI18n { get; set; }
    public Dictionary<string, string>? MetaKeywordsI18n { get; set; }

    public ProductGroup ProductGroup { get; set; } = null!;
    public ICollection<ProductAttribute> Attributes { get; set; } = new List<ProductAttribute>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
}
