using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Crm.Domain.Entities;

public class Address : BaseEntity
{
    public Guid MemberId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? CountryId { get; set; }
    public string CountryName { get; set; } = string.Empty;
    public Guid? CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public Guid? DistrictId { get; set; }
    public string DistrictName { get; set; } = string.Empty;
    public Guid? NeighborhoodId { get; set; }
    public string? NeighborhoodName { get; set; }
    public Guid? StreetId { get; set; }
    public string? StreetName { get; set; }
    public Guid? BuildingId { get; set; }
    public string? BuildingNumber { get; set; }
    public string? DoorNumber { get; set; }
    public string? AddressCode { get; set; }
    public string? AddressLine { get; set; }
    public string? PostalCode { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string? DeliveryNotes { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsValidated { get; set; } = false;
    public DateTime? ValidatedAt { get; set; }
    public Guid? ValidatedBy { get; set; }

    public Member Member { get; set; } = null!;
}
