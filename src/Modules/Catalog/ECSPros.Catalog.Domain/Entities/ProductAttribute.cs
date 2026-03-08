using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductAttribute : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid AttributeTypeId { get; set; }
    public Guid? AttributeValueId { get; set; }
    public Dictionary<string, object>? CustomValue { get; set; }

    public Product Product { get; set; } = null!;
    public AttributeType AttributeType { get; set; } = null!;
    public AttributeValue? AttributeValue { get; set; }
}
