using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Catalog.Domain.Entities;

public class FirmPlatformProduct : BaseEntity
{
    public Guid FirmPlatformId { get; set; }
    public Guid ProductId { get; set; }
    public Dictionary<string, string>? NameI18n { get; set; }
    public Dictionary<string, string>? ShortDescriptionI18n { get; set; }
    public bool IsActive { get; set; } = true;

    public Product Product { get; set; } = null!;
}
