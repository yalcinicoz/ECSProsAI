using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

/// <summary>
/// Bir ürün grubundaki varyant ekseninin alt özelliklerini tanımlar.
/// Örn: Pantolon grubu → Beden ekseni → Paça Boyu alt özelliği
/// </summary>
public class ProductGroupAxisSubAttribute : BaseEntity
{
    public Guid ProductGroupId { get; set; }
    public Guid AxisAttributeTypeId { get; set; }   // Varyant ekseni (Beden)
    public Guid SubAttributeTypeId { get; set; }    // Alt özellik tipi (Paça Boyu)
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; } = 0;

    public ProductGroup ProductGroup { get; set; } = null!;
    public AttributeType AxisAttributeType { get; set; } = null!;
    public AttributeType SubAttributeType { get; set; } = null!;
}
