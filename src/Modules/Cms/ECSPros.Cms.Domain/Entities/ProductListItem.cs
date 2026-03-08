using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class ProductListItem : BaseEntity
{
    public Guid ProductListId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public int SortOrder { get; set; }

    public ProductList ProductList { get; set; } = null!;
}
