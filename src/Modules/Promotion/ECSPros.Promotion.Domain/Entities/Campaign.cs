using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

public class Campaign : BaseEntity
{
    public Guid CampaignTypeId { get; set; }
    public Guid? FirmId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public Dictionary<string, string>? DescriptionI18n { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
    public string ProductSelectionType { get; set; } = string.Empty;
    public Dictionary<string, object>? ProductFilter { get; set; }

    public CampaignType CampaignType { get; set; } = null!;
    public ICollection<CampaignProduct> Products { get; set; } = new List<CampaignProduct>();
    public ICollection<CampaignExclusion> Exclusions { get; set; } = new List<CampaignExclusion>();
    public ICollection<CampaignPlatform> Platforms { get; set; } = new List<CampaignPlatform>();
}
