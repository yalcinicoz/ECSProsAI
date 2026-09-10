using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Inventory.Domain.Entities;

/// <summary>
/// Raf sayım oturumu (FAZ 15.3 R5, 2026-09-10): bir gözün sistem adedi (beklenen) ile personelin okuttuğu adet
/// (sayılan) karşılaştırılır; fark ayrı yetkiyle (<c>inventory.count.apply</c>) uygulanır ve <c>adjustment</c>
/// hareketleri üretilir. Aynalama kipinde (stok otoritesi eskide) yalnız "finished"e kadar gider — rapor üretir.
/// </summary>
public class BinCount : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public Guid BinId { get; set; }
    /// <summary>open | finished | applied | cancelled</summary>
    public string Status { get; set; } = "open";
    public Guid? StartedBy { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public Guid? AppliedBy { get; set; }
    public DateTime? AppliedAt { get; set; }
    public int ExpectedTotal { get; set; }
    public int CountedTotal { get; set; }
    /// <summary>Sayılan − beklenen (satırlar toplamı; fazla + eksik birbirini götürebilir, satırlara bak).</summary>
    public int DiffTotal { get; set; }
    public string? Notes { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseBin Bin { get; set; } = null!;
    public ICollection<BinCountLine> Lines { get; set; } = new List<BinCountLine>();
}

public static class BinCountStatus
{
    public const string Open = "open", Finished = "finished", Applied = "applied", Cancelled = "cancelled";
}
