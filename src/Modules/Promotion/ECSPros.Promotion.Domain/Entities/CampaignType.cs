using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

public class CampaignType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public string HandlerClass { get; set; } = string.Empty;
    public Dictionary<string, object>? SettingsSchema { get; set; }
    public bool RequiresProducts { get; set; }
    public bool IsStackable { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
}
