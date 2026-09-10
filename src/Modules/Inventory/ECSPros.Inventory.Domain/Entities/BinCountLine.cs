using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

/// <summary>Sayım satırı: varyant başına beklenen (oturum açıldığındaki sistem adedi) ve sayılan.</summary>
public class BinCountLine : BaseEntity
{
    public Guid BinCountId { get; set; }
    public Guid VariantId { get; set; }
    public int ExpectedQuantity { get; set; }
    public int CountedQuantity { get; set; }
    public int Diff => CountedQuantity - ExpectedQuantity;

    public BinCount BinCount { get; set; } = null!;
}
