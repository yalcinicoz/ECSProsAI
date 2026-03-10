using ECSPros.Order.Domain.Events;
using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Order : AggregateRoot
{
    public string OrderNumber { get; set; } = string.Empty;
    public Guid FirmPlatformId { get; set; }
    public Guid MemberId { get; set; }
    public Guid? CartId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;

    // B2B
    public string OrderType { get; set; } = "retail";
    public bool RequiresApproval { get; set; } = false;
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid? QuoteId { get; set; }

    // Payment Terms (B2B)
    public int? PaymentTermsDays { get; set; }
    public DateOnly? PaymentDueDate { get; set; }

    // Currency
    public string CurrencyCode { get; set; } = "TRY";
    public string InvoiceCurrencyCode { get; set; } = "TRY";
    public decimal ExchangeRate { get; set; } = 1.00m;

    // Shipping Address
    public string ShippingRecipientName { get; set; } = string.Empty;
    public string ShippingRecipientPhone { get; set; } = string.Empty;
    public Guid ShippingCountryId { get; set; }
    public Guid ShippingCityId { get; set; }
    public Guid ShippingDistrictId { get; set; }
    public Guid? ShippingNeighborhoodId { get; set; }
    public string ShippingAddressLine { get; set; } = string.Empty;
    public string? ShippingPostalCode { get; set; }
    public string? ShippingDeliveryNotes { get; set; }

    // Billing Address
    public bool BillingSameAsShipping { get; set; } = true;
    public string? BillingRecipientName { get; set; }
    public string? BillingTaxOffice { get; set; }
    public string? BillingTaxNumber { get; set; }
    public string? BillingCompanyName { get; set; }
    public Guid? BillingCountryId { get; set; }
    public Guid? BillingCityId { get; set; }
    public Guid? BillingDistrictId { get; set; }
    public string? BillingAddressLine { get; set; }

    // Amounts
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal TotalTax { get; set; }
    public decimal GrandTotal { get; set; }

    // Cargo
    public Guid? DefaultCargoFirmId { get; set; }

    // Notes
    public Dictionary<string, object>? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }

    // Confirmation
    public bool ConfirmationRequired { get; set; } = false;
    public DateTime? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }

    // Operations
    public Guid? PickingPlanId { get; set; }
    public Guid? SortingBinId { get; set; }
    public string? PackingStationCode { get; set; }
    public int? PackingSlotNumber { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderDiscount> Discounts { get; set; } = new List<OrderDiscount>();
    public ICollection<OrderExpense> Expenses { get; set; } = new List<OrderExpense>();
    public ICollection<OrderTax> Taxes { get; set; } = new List<OrderTax>();
    public ICollection<OrderPayment> Payments { get; set; } = new List<OrderPayment>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    public ICollection<OrderNotification> Notifications { get; set; } = new List<OrderNotification>();
    public ICollection<OrderGift> Gifts { get; set; } = new List<OrderGift>();

    private static readonly string[] ConfirmableStatuses = ["pending"];
    private static readonly string[] CancellableStatuses = ["pending", "confirmed"];
    private static readonly string[] ProcessableStatuses = ["confirmed"];
    private static readonly string[] ShippableStatuses = ["processing"];
    private static readonly string[] DeliverableStatuses = ["shipped"];

    public void Confirm(Guid warehouseId, Guid confirmedBy)
    {
        if (!ConfirmableStatuses.Contains(Status))
            throw new InvalidOperationException($"'{Status}' durumundaki sipariş onaylanamaz.");

        Status = "confirmed";
        ConfirmedAt = DateTime.UtcNow;
        ConfirmedBy = confirmedBy;

        var reservedItems = Items
            .Select(i => new OrderedItem(i.VariantId, i.Quantity))
            .ToList();

        AddDomainEvent(new OrderConfirmedEvent(Id, warehouseId, confirmedBy, reservedItems));
    }

    public void Cancel(Guid cancelledBy, string? reason = null)
    {
        if (!CancellableStatuses.Contains(Status))
            throw new InvalidOperationException($"'{Status}' durumundaki sipariş iptal edilemez.");

        Status = "cancelled";
        InternalNotes = reason is not null
            ? $"[İptal] {reason}\n{InternalNotes}"
            : InternalNotes;

        AddDomainEvent(new OrderCancelledEvent(Id, cancelledBy));
    }

    public void StartProcessing(Guid updatedBy, Guid? pickingPlanId = null)
    {
        if (!ProcessableStatuses.Contains(Status))
            throw new InvalidOperationException($"'{Status}' durumundaki sipariş işleme alınamaz.");

        Status = "processing";
        PickingPlanId = pickingPlanId ?? PickingPlanId;
    }

    public void MarkShipped(Guid updatedBy)
    {
        if (!ShippableStatuses.Contains(Status))
            throw new InvalidOperationException($"'{Status}' durumundaki sipariş kargoya verilemez.");

        Status = "shipped";

        var shippedItems = Items
            .Select(i => new OrderedItem(i.VariantId, i.Quantity))
            .ToList();

        AddDomainEvent(new OrderShippedEvent(Id, updatedBy, shippedItems));
    }

    public void MarkDelivered(Guid updatedBy)
    {
        if (!DeliverableStatuses.Contains(Status))
            throw new InvalidOperationException($"'{Status}' durumundaki sipariş teslim edildi olarak işaretlenemez.");

        Status = "delivered";
    }
}
