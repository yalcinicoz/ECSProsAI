using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class MemberCredit : BaseEntity
{
    public Guid MemberId { get; set; }
    public decimal CreditLimit { get; set; } = 0;
    public decimal UsedCredit { get; set; } = 0;
    public decimal AvailableCredit => CreditLimit - UsedCredit;
    public string CurrencyCode { get; set; } = "TRY";
    public DateTime? LastReviewAt { get; set; }
    public Guid? LastReviewBy { get; set; }
    public string? Notes { get; set; }

    public Member Member { get; set; } = null!;
}
