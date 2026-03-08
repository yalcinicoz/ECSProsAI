using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class MemberGroup : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public bool IsDefault { get; set; } = false;
    public bool IsWholesale { get; set; } = false;
    public bool RequiresApproval { get; set; } = false;
    public bool ShowPricesBeforeLogin { get; set; } = true;
    public decimal? MinOrderAmount { get; set; }
    public int? PaymentTermsDays { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public ICollection<Member> Members { get; set; } = new List<Member>();
}
