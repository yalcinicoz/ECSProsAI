using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Pos.Domain.Entities;

public class PosSaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Guid VariantId { get; set; }
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }

    public PosSale Sale { get; set; } = null!;
}
