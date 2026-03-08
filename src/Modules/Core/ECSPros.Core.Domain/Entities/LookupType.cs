using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class LookupType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = false;

    public ICollection<LookupValue> Values { get; set; } = new List<LookupValue>();
}
