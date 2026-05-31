using ECSPros.Shared.Kernel.Domain;
namespace ECSPros.Accounts.Domain.Entities;
public class CurrentAccountGroup : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GroupType { get; set; } = "both"; // supplier, customer, both
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public ICollection<CurrentAccount> Accounts { get; set; } = new List<CurrentAccount>();
}
