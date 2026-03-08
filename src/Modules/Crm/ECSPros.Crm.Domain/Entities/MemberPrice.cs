using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class MemberPrice : BaseEntity
{
    public Guid MemberId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Price { get; set; }
    public int MinQuantity { get; set; } = 1;
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }

    public Member Member { get; set; } = null!;
}
