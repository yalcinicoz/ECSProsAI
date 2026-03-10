namespace ECSPros.Integration.Application.Adapters;

public interface ICargoAdapter
{
    string ServiceCode { get; }
    Task<CargoShipmentResult> CreateShipmentAsync(Guid firmIntegrationId, CargoShipmentRequest request, CancellationToken ct = default);
    Task<CargoTrackingResult> TrackShipmentAsync(Guid firmIntegrationId, string trackingNumber, CancellationToken ct = default);
    Task<bool> CancelShipmentAsync(Guid firmIntegrationId, string trackingNumber, CancellationToken ct = default);
}

public record CargoShipmentRequest(
    Guid OrderId,
    string RecipientName,
    string RecipientPhone,
    string AddressLine,
    string CityCode,
    string DistrictCode,
    int PackageCount,
    decimal? TotalWeight,
    decimal? TotalDesi,
    bool IsCod = false,
    decimal CodAmount = 0);

public record CargoShipmentResult(bool Success, string? TrackingNumber, string? TrackingUrl, string? ErrorMessage);

public record CargoTrackingResult(bool Success, string Status, List<CargoEventDto> Events, string? ErrorMessage);

public record CargoEventDto(string Code, string Description, string Location, DateTime OccurredAt);
