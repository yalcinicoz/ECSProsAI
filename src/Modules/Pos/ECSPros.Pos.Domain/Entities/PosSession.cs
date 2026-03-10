using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Entities;

public class PosSession : BaseEntity
{
    public Guid RegisterId { get; set; }
    public Guid UserId { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CashDifference { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public PosRegister Register { get; set; } = null!;
    public ICollection<PosSessionTransaction> Transactions { get; set; } = new List<PosSessionTransaction>();
    public ICollection<PosSale> Sales { get; set; } = new List<PosSale>();
}
