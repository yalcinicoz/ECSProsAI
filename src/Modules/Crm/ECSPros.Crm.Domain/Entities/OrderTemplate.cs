using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class OrderTemplate : BaseEntity
{
    public Guid MemberId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }

    public Member Member { get; set; } = null!;
    public ICollection<OrderTemplateItem> Items { get; set; } = new List<OrderTemplateItem>();
}
