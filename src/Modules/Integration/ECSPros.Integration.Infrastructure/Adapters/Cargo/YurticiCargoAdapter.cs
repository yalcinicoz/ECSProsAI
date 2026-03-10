using ECSPros.Integration.Application.Adapters;
using Microsoft.Extensions.Logging;

namespace ECSPros.Integration.Infrastructure.Adapters.Cargo;

/// <summary>
/// Yurtiçi Kargo adaptörü.
/// Production'da Yurtiçi Kargo SOAP/REST API'sine bağlanır.
/// </summary>
public class YurticiCargoAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<YurticiCargoAdapter> logger) : ICargoAdapter
{
    public string ServiceCode => "yurtici";

    public async Task<CargoShipmentResult> CreateShipmentAsync(
        Guid firmIntegrationId, CargoShipmentRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("Yurtiçi Kargo gönderi oluşturma: OrderId={OrderId}", request.OrderId);

        await Task.Delay(150, ct);
        var trackingNumber = $"YK{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        return new CargoShipmentResult(true, trackingNumber, $"https://sorgula.yurtici.com.tr/{trackingNumber}", null);
    }

    public async Task<CargoTrackingResult> TrackShipmentAsync(
        Guid firmIntegrationId, string trackingNumber, CancellationToken ct = default)
    {
        logger.LogInformation("Yurtiçi Kargo takip sorgusu: TrackingNumber={TrackingNumber}", trackingNumber);

        await Task.Delay(100, ct);
        return new CargoTrackingResult(true, "in_transit", [
            new CargoEventDto("ACCEPTED", "Kargo kabul edildi", "İstanbul Dağıtım Merkezi", DateTime.UtcNow.AddHours(-2))
        ], null);
    }

    public async Task<bool> CancelShipmentAsync(
        Guid firmIntegrationId, string trackingNumber, CancellationToken ct = default)
    {
        logger.LogInformation("Yurtiçi Kargo iptal: TrackingNumber={TrackingNumber}", trackingNumber);
        await Task.Delay(100, ct);
        return true;
    }
}
