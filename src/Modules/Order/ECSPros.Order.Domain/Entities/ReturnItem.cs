using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class ReturnItem : BaseEntity
{
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

    public Return Return { get; set; } = null!;
}
