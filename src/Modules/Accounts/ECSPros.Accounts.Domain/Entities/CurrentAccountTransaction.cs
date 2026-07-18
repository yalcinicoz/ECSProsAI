using ECSPros.Shared.Kernel.Domain;
namespace ECSPros.Accounts.Domain.Entities;

/// <summary>
/// Tüm cari/kavram defterlerinin ortak hareket kaydı. Bakiye güncellemesi yalnız
/// PostAccountTransactionCommand üzerinden yapılır; hareketler silinmez, düzeltme = ters kayıt.
/// </summary>
public class CurrentAccountTransaction : BaseEntity
{
    public Guid LedgerId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // manual_adjustment, return_refund, payment, ...
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateOnly TransactionDate { get; set; }
    public CurrentAccountLedger? Ledger { get; set; }
}
