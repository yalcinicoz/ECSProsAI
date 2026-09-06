using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductGroupAttribute : BaseEntity
{
    public Guid ProductGroupId { get; set; }
    public Guid AttributeTypeId { get; set; }
    public bool IsVariant { get; set; } = false;
    public bool IsRequired { get; set; } = false;
    public bool IsPrimaryAxis { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    /// <summary>Seçim tipli özellik için grup varsayılanı (2026-09-06): yeni ürün / ERP aktarımı / geri dolum bu değeri yazar
    /// (örn. Kot Ceket grubunda "Ürün Grubu" = Ceket). definition.attribute_values gevşek referansı.</summary>
    public Guid? DefaultAttributeValueId { get; set; }

    public ProductGroup ProductGroup { get; set; } = null!;
    public AttributeType AttributeType { get; set; } = null!;
}
