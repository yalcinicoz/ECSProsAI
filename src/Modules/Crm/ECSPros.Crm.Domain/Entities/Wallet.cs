using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class Wallet : BaseEntity
{
    public Guid MemberId { get; set; }
    public decimal Balance { get; set; } = 0;
    public string CurrencyCode { get; set; } = "TRY";

    public Member Member { get; set; } = null!;
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
