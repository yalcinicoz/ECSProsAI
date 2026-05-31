using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

/// <summary>
/// Bir özellik değerinin alt özellik değerini tutar.
/// Örn: Beden "38" → Paça Boyu = "74"
/// Değer, onu kullanan tüm ürün gruplarında geçerlidir.
/// </summary>
public class AttributeValueProperty : BaseEntity
{
    public Guid AttributeValueId { get; set; }      // Beden: 38
    public Guid SubAttributeTypeId { get; set; }    // Paça Boyu
    public string Value { get; set; } = string.Empty;

    public AttributeValue AttributeValue { get; set; } = null!;
    public AttributeType SubAttributeType { get; set; } = null!;
}
