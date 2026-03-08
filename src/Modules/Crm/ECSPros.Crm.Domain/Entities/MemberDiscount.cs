using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class MemberDiscount : BaseEntity
{
    public Guid MemberId { get; set; }
    public string DiscountType { get; set; } = string.Empty; // category, product_group, brand, all
    public Guid? TargetId { get; set; }
    public decimal DiscountRate { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }

    public Member Member { get; set; } = null!;
}
