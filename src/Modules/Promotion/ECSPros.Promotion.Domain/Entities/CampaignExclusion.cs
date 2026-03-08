using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

public class CampaignExclusion : BaseEntity
{
    public Guid CampaignId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string? Reason { get; set; }

    public Campaign Campaign { get; set; } = null!;
}
