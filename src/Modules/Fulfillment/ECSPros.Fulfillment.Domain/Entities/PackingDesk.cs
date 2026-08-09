using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>
/// OP4: paketleme masası OTURUMU — masalar tamamen sanaldır (kurgu): personel koli için
/// masa açar, mümkün olan EN KÜÇÜK numara verilir; koli işi bitince masa kapanır, numara
/// yeniden kullanılabilir. Slot (son ayrıştırma rafı) ataması SortingBin.DeskSlotNumber'da.
/// </summary>
public class PackingDesk : BaseEntity
{
    public Guid PickingPlanId { get; set; }
    public Guid SortingBoxId { get; set; }
    public int DeskNumber { get; set; }

    /// <summary>open | closed</summary>
    public string Status { get; set; } = "open";

    public Guid OpenedBy { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
