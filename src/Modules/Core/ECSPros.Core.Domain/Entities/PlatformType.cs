using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class PlatformType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public bool IsMarketplace { get; set; } = false;
    public bool IsActive { get; set; } = true;
    /// <summary>JSON string — uygulama katmanında serialize/deserialize edilir.</summary>
    public string? SettingsSchemaJson { get; set; }

    public ICollection<FirmPlatform> FirmPlatforms { get; set; } = new List<FirmPlatform>();
}
