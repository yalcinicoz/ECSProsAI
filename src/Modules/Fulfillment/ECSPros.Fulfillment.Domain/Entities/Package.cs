using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Fulfillment.Domain.Entities;

public class Package : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? ShipmentId { get; set; }
    public int PackageNumber { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Length { get; set; }
    public decimal? Desi { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PackedAt { get; set; }
    public Guid? PackedBy { get; set; }
    public DateTime? LabelPrintedAt { get; set; }
}
