using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Return : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid MemberId { get; set; }
    public string ReturnType { get; set; } = string.Empty;
    public string? CustomerNotes { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ReturnCargoFirmId { get; set; }
    public string? ReturnTrackingNumber { get; set; }
    public DateTime? ReturnCargoSentAt { get; set; }
    public DateTime? ReturnCargoReceivedAt { get; set; }
    public string? InspectionNotes { get; set; }
    public DateTime? InspectionCompletedAt { get; set; }
    public Guid? InspectionCompletedBy { get; set; }
    public string RefundMethod { get; set; } = string.Empty;
    public string RefundStatus { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }

    public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
    public ICollection<ReturnRefund> Refunds { get; set; } = new List<ReturnRefund>();
}
