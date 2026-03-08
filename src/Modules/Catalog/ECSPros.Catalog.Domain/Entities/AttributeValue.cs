using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class AttributeValue : BaseEntity
{
    public Guid AttributeTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, object>? ExtraData { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public AttributeType AttributeType { get; set; } = null!;
}
