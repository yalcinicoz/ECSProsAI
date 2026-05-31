using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class FilterColor : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string? HexCode { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ICollection<AttributeValueFilterColor> AttributeValueMappings { get; set; } = new List<AttributeValueFilterColor>();
}
