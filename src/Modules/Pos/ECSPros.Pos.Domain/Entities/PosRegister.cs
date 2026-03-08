using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Entities;

public class PosRegister : BaseEntity
{
    public Guid WarehouseId { get; set; }
    public Guid FirmPlatformId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ReceiptPrefix { get; set; } = string.Empty;
    public int ReceiptSequence { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public ICollection<PosSession> Sessions { get; set; } = new List<PosSession>();
    public ICollection<PosQuickProduct> QuickProducts { get; set; } = new List<PosQuickProduct>();
}
