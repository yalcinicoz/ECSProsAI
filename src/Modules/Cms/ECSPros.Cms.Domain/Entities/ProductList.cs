using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Cms.Domain.Entities;

public class ProductList : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string ListType { get; set; } = string.Empty;
    public Dictionary<string, object>? FilterRules { get; set; }
    public int? ProductLimit { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductListItem> Items { get; set; } = new List<ProductListItem>();
}
