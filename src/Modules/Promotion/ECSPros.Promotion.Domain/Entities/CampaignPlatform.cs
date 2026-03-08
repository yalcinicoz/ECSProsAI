using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

public class CampaignPlatform : BaseEntity
{
    public Guid CampaignId { get; set; }
    public Guid FirmPlatformId { get; set; }
    public bool IsIncluded { get; set; }

    public Campaign Campaign { get; set; } = null!;
}
