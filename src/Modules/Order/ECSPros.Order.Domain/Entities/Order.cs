using ECSPros.Shared.Kernel.Domain;

namespace ECSPros.Order.Domain.Entities;

public class Order : BaseEntity
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

    // POS
    public bool IsPosSale { get; set; } = false;
    public Guid? PosSessionId { get; set; }
    public Guid? PosRegisterId { get; set; }
    public string? ReceiptNumber { get; set; }

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
}
