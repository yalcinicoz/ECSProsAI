using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Finance.Domain.Entities;

public class SupplierTransaction : BaseEntity
{
    public Guid SupplierId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Debit { get; set; } = 0;
    public decimal Credit { get; set; } = 0;
    public decimal BalanceAfter { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateOnly TransactionDate { get; set; }
}
