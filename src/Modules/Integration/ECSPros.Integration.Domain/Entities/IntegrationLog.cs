using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Integration.Domain.Entities;

public class IntegrationLog : BaseEntity
{
    public Guid FirmIntegrationId { get; set; }
    public string ServiceType { get; set; } = string.Empty;     // marketplace, cargo, invoice_integrator
    public string OperationType { get; set; } = string.Empty;   // sync_product, update_stock, fetch_orders, create_shipment, track_shipment, send_invoice
    public string Status { get; set; } = string.Empty;          // success, failure, pending
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int DurationMs { get; set; }
    public Guid? ReferenceId { get; set; }                      // orderId, variantId, shipmentId etc.
    public string? ReferenceType { get; set; }                  // Order, Variant, Shipment etc.
}
