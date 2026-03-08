using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Entities;

public class PosQuickProduct : BaseEntity
{
    public Guid? RegisterId { get; set; }
    public Guid VariantId { get; set; }
    public string ButtonText { get; set; } = string.Empty;
    public string? ButtonColor { get; set; }
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public PosRegister? Register { get; set; }
}
