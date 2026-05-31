using ECSPros.Shared.Kernel.Domain;
namespace ECSPros.Accounts.Domain.Entities;
public class CurrentAccount : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AccountType { get; set; } = "customer"; // supplier, customer, both
    public Guid? GroupId { get; set; }
    public string? TaxNumber { get; set; }
    public string? TaxOffice { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; } = "TR";
    public decimal CreditLimit { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public CurrentAccountGroup? Group { get; set; }
}
