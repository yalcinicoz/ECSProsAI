using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class LoyaltyAccount : BaseEntity
{
    public Guid MemberId { get; set; }
    public int TotalPoints { get; set; } = 0;
    public int AvailablePoints { get; set; } = 0;
    public int PendingPoints { get; set; } = 0;
    public string CurrencyCode { get; set; } = "TRY";
    public decimal PointsToCurrencyRate { get; set; } = 0;

    public Member Member { get; set; } = null!;
    public ICollection<LoyaltyTransaction> Transactions { get; set; } = new List<LoyaltyTransaction>();
}
