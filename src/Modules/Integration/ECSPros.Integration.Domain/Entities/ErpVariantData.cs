using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

public class ErpVariantData : BaseEntity
{
    public Guid FirmIntegrationId { get; set; }
    public Guid VariantId { get; set; }
    public Dictionary<string, object> Payload { get; set; } = new();
}
