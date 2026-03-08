using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Core.Domain.Entities;

public class CargoRule : BaseEntity
{
    public Guid FirmId { get; set; }
    public Guid FirmIntegrationId { get; set; }
    public string RuleType { get; set; } = "default"; // default, neighborhood, payment_type, combined
    public string? PaymentType { get; set; } // prepaid, cod_cash, cod_card
    public Guid? NeighborhoodId { get; set; }
    public Guid? CityId { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public Firm Firm { get; set; } = null!;
    public FirmIntegration FirmIntegration { get; set; } = null!;
}
