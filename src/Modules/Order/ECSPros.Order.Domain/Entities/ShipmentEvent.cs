namespace ECSPros.Order.Domain.Entities;

public class ShipmentEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string? EventLocation { get; set; }
    public DateTime EventDate { get; set; }
    public Dictionary<string, object>? RawData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Shipment Shipment { get; set; } = null!;
}
