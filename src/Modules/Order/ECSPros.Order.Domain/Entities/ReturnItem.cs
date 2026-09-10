using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class ReturnItem : BaseEntity
{
    /// <summary>Geçici legacy MySQL importunda opiadeurunler.Id.</summary>
    public int? LegacyReturnItemId { get; set; }
    public Guid ReturnId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public Guid ReturnReasonId { get; set; }
    public string? CustomerNotes { get; set; }
    public decimal UnitRefundAmount { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InspectionResult { get; set; }
    public string? InspectionNotes { get; set; }
    /// <summary>İade planı §2.3/5: kargoya verilmemiş faturalı siparişin teslimatsız iadesinde stok hiç
    /// çıkmamıştı (yalnız rezerve) — rezervasyon iade anında serbest bırakılır ve "teslim al" adımı bu kalem
    /// için stok girişi YAPMAZ (çift sayım önlenir).</summary>
    public bool StockAlreadyIn { get; set; }

    public Return Return { get; set; } = null!;
}
