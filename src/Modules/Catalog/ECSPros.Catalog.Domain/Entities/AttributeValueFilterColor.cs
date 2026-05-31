using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class AttributeValueFilterColor : BaseEntity
{
    public Guid AttributeValueId { get; set; }
    public Guid FilterColorId { get; set; }

    public AttributeValue AttributeValue { get; set; } = null!;
    public FilterColor FilterColor { get; set; } = null!;
}
