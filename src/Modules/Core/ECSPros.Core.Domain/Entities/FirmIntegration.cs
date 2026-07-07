using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class FirmIntegration : BaseEntity
{
    public Guid FirmId { get; set; }
    public Guid IntegrationServiceId { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, object> Credentials { get; set; } = new();
    public Dictionary<string, object> Settings { get; set; } = new();
    public bool IsActive { get; set; } = true;

    public string? ContractNumber { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "draft"; // draft, active, expired, cancelled
    public Dictionary<string, object>? Terms { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? DocumentUrl { get; set; }

    public Firm Firm { get; set; } = null!;
    public IntegrationService IntegrationService { get; set; } = null!;
}
