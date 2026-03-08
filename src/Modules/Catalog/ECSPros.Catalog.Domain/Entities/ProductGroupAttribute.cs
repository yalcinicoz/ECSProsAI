using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductGroupAttribute : BaseEntity
{
    public Guid ProductGroupId { get; set; }
    public Guid AttributeTypeId { get; set; }
    public bool IsVariant { get; set; } = false;
    public bool IsRequired { get; set; } = false;
    public int SortOrder { get; set; } = 0;

    public ProductGroup ProductGroup { get; set; } = null!;
    public AttributeType AttributeType { get; set; } = null!;
}
