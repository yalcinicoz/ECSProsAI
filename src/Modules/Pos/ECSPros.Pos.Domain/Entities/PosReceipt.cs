using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Entities;

public class PosReceipt : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid OrderId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string ReceiptType { get; set; } = string.Empty;
    public DateTime? PrintedAt { get; set; }
    public Guid? PrintedBy { get; set; }
    public int ReprintCount { get; set; } = 0;

    public PosSession Session { get; set; } = null!;
}
