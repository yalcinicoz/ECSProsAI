using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class ProductGroup : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public ProductGroup? Parent { get; set; }
    public ICollection<ProductGroup> Children { get; set; } = new List<ProductGroup>();
    public ICollection<ProductGroupAttribute> Attributes { get; set; } = new List<ProductGroupAttribute>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
