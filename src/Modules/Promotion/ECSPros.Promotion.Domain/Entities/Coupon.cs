using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Promotion.Domain.Entities;

public class Coupon : BaseEntity
{
    public Guid? CampaignId { get; set; }
    public Guid? MemberId { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string CouponType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public int? UsageLimitTotal { get; set; }
    public int? UsageLimitPerMember { get; set; }
    public int UsageCount { get; set; } = 0;
    public decimal? MinimumCartTotal { get; set; }
    public bool ValidForFirstOrderOnly { get; set; } = false;
    public Guid? MemberGroupId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<CouponUsage> Usages { get; set; } = new List<CouponUsage>();
}
