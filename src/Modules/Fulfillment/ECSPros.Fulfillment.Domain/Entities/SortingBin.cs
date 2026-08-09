using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class SortingBin : BaseEntity
{
    public Guid PickingPlanId { get; set; }
    public Guid? OrderId { get; set; }
    public int BinNumber { get; set; }
    public string Status { get; set; } = string.Empty; // "empty" | "filling" | "ready"

    /// <summary>OP3: siparişin atandığı ara ayrıştırma koli OTURUMU (null = henüz kolisiz).
    /// BinNumber artık koli numarası olarak da kullanılır (SortingBox.BoxNumber ile senkron).</summary>
    public Guid? SortingBoxId { get; set; }

    /// <summary>OP4: masadaki son-ayrıştırma slot (raf gözü) numarası — paket kapanınca
    /// boşaltılır (null), göz başka sipariş için yeniden kullanılır.</summary>
    public int? DeskSlotNumber { get; set; }

    /// <summary>OP4: OBM'ye transfer edildi (K-6 — çözüm personel insiyatifinde).</summary>
    public bool ObmTransferred { get; set; }

    public PickingPlan PickingPlan { get; set; } = null!;
}
