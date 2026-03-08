using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class Cart : BaseEntity
{
    public Guid? MemberId { get; set; }
    public string? SessionId { get; set; }
    public Guid FirmPlatformId { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public string? Notes { get; set; }
    public Guid? MergedFromCartId { get; set; }

    public Member? Member { get; set; }
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
