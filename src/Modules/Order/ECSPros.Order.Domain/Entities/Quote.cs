using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Quote : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ValidUntil { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }

    public string? NotesToCustomer { get; set; }
    public string? InternalNotes { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? ViewedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public Guid? ConvertedOrderId { get; set; }

    public ICollection<QuoteItem> Items { get; set; } = new List<QuoteItem>();
}
