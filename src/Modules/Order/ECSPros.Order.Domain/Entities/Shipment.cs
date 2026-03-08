using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Shipment : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid FirmIntegrationId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CargoStatusRaw { get; set; }
    public string ApiStatus { get; set; } = string.Empty;
    public Dictionary<string, object>? ApiRequestPayload { get; set; }
    public Dictionary<string, object>? ApiResponsePayload { get; set; }
    public DateTime? ApiSentAt { get; set; }
    public DateOnly? EstimatedDeliveryDate { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? DeliverySignature { get; set; }
    public string? DeliveryNotes { get; set; }
    public int PackageCount { get; set; } = 1;
    public decimal? TotalWeight { get; set; }
    public decimal? TotalDesi { get; set; }

    public Order Order { get; set; } = null!;
    public ICollection<ShipmentItem> Items { get; set; } = new List<ShipmentItem>();
    public ICollection<ShipmentEvent> Events { get; set; } = new List<ShipmentEvent>();
}
