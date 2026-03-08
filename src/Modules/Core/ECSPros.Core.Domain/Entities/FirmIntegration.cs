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

    public Firm Firm { get; set; } = null!;
    public IntegrationService IntegrationService { get; set; } = null!;
}
