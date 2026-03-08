using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class Category : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Guid? ParentId { get; set; }
    public string FillType { get; set; } = "manual"; // manual, filter, mixed
    public Dictionary<string, object>? FilterRules { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<CategoryProduct> CategoryProducts { get; set; } = new List<CategoryProduct>();
}
