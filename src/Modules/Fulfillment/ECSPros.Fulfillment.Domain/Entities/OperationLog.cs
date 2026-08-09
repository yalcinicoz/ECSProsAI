using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

/// <summary>
/// OP1 (2026-08-09, K-16): operasyon günlüğü — personelin yaptığı HER işlem tek tabloda.
/// Sipariş detayındaki "Operasyon Geçmişi" ve görev/koli/masa izleme ekranları buradan beslenir.
/// Zaman = CreatedAt (BaseEntity), personel = ActorId.
/// </summary>
public class OperationLog : BaseEntity
{
    public Guid? OrderId { get; set; }
    public Guid? OrderItemId { get; set; }
    public Guid? PickingPlanId { get; set; }
    public Guid? PackageId { get; set; }

    /// <summary>task_created | line_assigned | line_picked | line_short | item_returned |
    /// sorting_scanned | box_taken | station_opened | slot_assigned | final_scanned |
    /// package_packed | invoice_issued | label_printed | cargo_notified | obm_transferred |
    /// cargo_rerouted ...</summary>
    public string Action { get; set; } = string.Empty;

    public Guid ActorId { get; set; }

    /// <summary>Serbest detay (raf/koli/masa/slot/miktar/barkod) — jsonb.</summary>
    public Dictionary<string, object>? Detail { get; set; }
}
