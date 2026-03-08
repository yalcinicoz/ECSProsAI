using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class Building : BaseEntity
{
    public Guid StreetId { get; set; }
    public string BuildingNumber { get; set; } = string.Empty;
    public string? AddressCode { get; set; }
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;

    public Street Street { get; set; } = null!;
}
