using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductVariantAttribute : BaseEntity
{
    public Guid VariantId { get; set; }
    public Guid AttributeTypeId { get; set; }
    public Guid AttributeValueId { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public AttributeType AttributeType { get; set; } = null!;
    public AttributeValue AttributeValue { get; set; } = null!;
}
