using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

/// <summary>
/// Bir ürünün belirli eksen değerine ait alt özellik değerini tutar.
/// Örn: Ürün P001, Beden 38 → Paça Boyu = 74
/// Değer ürüne özgüdür; aynı "38 beden" başka üründe farklı değer alabilir.
/// </summary>
public class ProductAxisSubAttributeValue : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid AttributeValueId { get; set; }   // Eksen değeri (örn. Beden "38")
    public Guid SubAttributeTypeId { get; set; } // Alt özellik tipi (örn. Paça Boyu)
    public string Value { get; set; } = string.Empty;

    public Product Product { get; set; } = null!;
    public AttributeValue AttributeValue { get; set; } = null!;
    public AttributeType SubAttributeType { get; set; } = null!;
}
