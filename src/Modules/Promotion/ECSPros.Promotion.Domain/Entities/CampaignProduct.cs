using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

public class CampaignProduct : BaseEntity
{
    public Guid CampaignId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string AddedType { get; set; } = string.Empty;

    public Campaign Campaign { get; set; } = null!;
}
