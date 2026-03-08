using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class QuoteItem : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Guid VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string VariantInfo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string UnitType { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }

    public Quote Quote { get; set; } = null!;
}
