using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class LookupValue : BaseEntity
{
    public Guid LookupTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public Dictionary<string, object>? ExtraData { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public LookupType LookupType { get; set; } = null!;
}
