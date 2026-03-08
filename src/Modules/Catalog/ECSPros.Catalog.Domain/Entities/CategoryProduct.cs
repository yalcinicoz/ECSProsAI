using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class CategoryProduct : BaseEntity
{
    public Guid CategoryId { get; set; }
    public Guid ProductId { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsPinned { get; set; } = false;

    public Category Category { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
