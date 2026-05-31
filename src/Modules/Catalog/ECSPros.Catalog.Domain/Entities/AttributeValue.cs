using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class AttributeValue : BaseEntity
{
    public Guid AttributeTypeId { get; set; }
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, object>? ExtraData { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public AttributeType AttributeType { get; set; } = null!;
    public ICollection<AttributeValueProperty> Properties { get; set; } = new List<AttributeValueProperty>();
    public ICollection<AttributeValueFilterColor> FilterColors { get; set; } = new List<AttributeValueFilterColor>();
}
