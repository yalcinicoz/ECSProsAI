using ECSPros.Shared.Kernel.Domain;
namespace ECSPros.Accounts.Domain.Entities;
public class CurrentAccountLedger : BaseEntity
{
    public Guid CurrentAccountId { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public decimal Balance { get; set; }  // running balance, updated via transactions
    public CurrentAccount? CurrentAccount { get; set; }
}
