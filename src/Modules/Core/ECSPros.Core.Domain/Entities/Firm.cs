using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class Firm : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, string> NameI18n { get; set; } = new();
    public string TaxOffice { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsMain { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public ICollection<FirmPlatform> FirmPlatforms { get; set; } = new List<FirmPlatform>();
    public ICollection<FirmIntegration> FirmIntegrations { get; set; } = new List<FirmIntegration>();
    public ICollection<CargoRule> CargoRules { get; set; } = new List<CargoRule>();
    public ICollection<FirmNotificationSetting> NotificationSettings { get; set; } = new List<FirmNotificationSetting>();
}
