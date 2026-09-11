namespace ECSPros.Order.Application.Services;

/// <summary>Read-only projection, not an entity/table. Catalog identifiers are current, other values are order snapshots.</summary>
public sealed class OrderReportLine
{
    public Guid OrderId { get; set; }
    public bool IsDeleted { get; set; }
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? ProductCode { get; set; }
    public string? Barcode { get; set; }
    public int Quantity { get; set; }
}
