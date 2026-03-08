using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class Language : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string Direction { get; set; } = "ltr";
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
