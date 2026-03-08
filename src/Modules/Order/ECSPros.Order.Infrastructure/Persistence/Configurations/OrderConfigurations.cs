using ECSPros.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Order.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Domain.Entities.Order>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Order> builder)
    {
        builder.ToTable("ord_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentStatus).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OrderType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.InvoiceCurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ExchangeRate).HasPrecision(18, 6);
        builder.Property(x => x.ReceiptNumber).HasMaxLength(50);
        builder.Property(x => x.ShippingRecipientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ShippingRecipientPhone).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ShippingPostalCode).HasMaxLength(10);
        builder.Property(x => x.BillingRecipientName).HasMaxLength(200);
        builder.Property(x => x.BillingTaxOffice).HasMaxLength(200);
        builder.Property(x => x.BillingTaxNumber).HasMaxLength(20);
        builder.Property(x => x.BillingCompanyName).HasMaxLength(200);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2);
        builder.Property(x => x.TotalExpense).HasPrecision(18, 2);
        builder.Property(x => x.TotalTax).HasPrecision(18, 2);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.Property(x => x.PackingStationCode).HasMaxLength(50);
        builder.Property(x => x.CustomerNotes).HasColumnType("jsonb");
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Discounts).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Expenses).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Taxes).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Payments).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Invoices).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Shipments).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Notifications).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        builder.HasMany(x => x.Gifts).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("ord_order_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProductName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.VariantInfo).HasMaxLength(500);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class OrderDiscountConfiguration : IEntityTypeConfiguration<OrderDiscount>
{
    public void Configure(EntityTypeBuilder<OrderDiscount> builder)
    {
        builder.ToTable("ord_order_discounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DiscountName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class OrderExpenseConfiguration : IEntityTypeConfiguration<OrderExpense>
{
    public void Configure(EntityTypeBuilder<OrderExpense> builder)
    {
        builder.ToTable("ord_order_expenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExpenseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class OrderTaxConfiguration : IEntityTypeConfiguration<OrderTax>
{
    public void Configure(EntityTypeBuilder<OrderTax> builder)
    {
        builder.ToTable("ord_order_taxes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TaxType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> builder)
    {
        builder.ToTable("ord_order_payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Details).HasColumnType("jsonb");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class InvoiceSeriesConfiguration : IEntityTypeConfiguration<InvoiceSeries>
{
    public void Configure(EntityTypeBuilder<InvoiceSeries> builder)
    {
        builder.ToTable("ord_invoice_series");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.EArchiveSerial).HasMaxLength(3).IsRequired();
        builder.Property(x => x.EInvoiceSerial).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ExportSerial).HasMaxLength(3).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Invoices).WithOne(x => x.InvoiceSeries).HasForeignKey(x => x.InvoiceSeriesId);
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("ord_invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.InvoiceSerial).HasMaxLength(3).IsRequired();
        builder.Property(x => x.InvoiceYear).HasMaxLength(4).IsRequired();
        builder.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecipientTaxOffice).HasMaxLength(200);
        builder.Property(x => x.RecipientTaxNumber).HasMaxLength(20);
        builder.Property(x => x.RecipientCompanyName).HasMaxLength(200);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2);
        builder.Property(x => x.TotalTax).HasPrecision(18, 2);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.Property(x => x.IntegratorStatus).HasMaxLength(30);
        builder.Property(x => x.IntegratorResponse).HasColumnType("jsonb");
        builder.Property(x => x.IntegratorInvoiceUrl).HasMaxLength(500);
        builder.Property(x => x.ErpStatus).HasMaxLength(30);
        builder.Property(x => x.ErpReference).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.InvoiceSerial, x.InvoiceYear, x.InvoiceSequence }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId);
    }
}

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("ord_invoice_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("ord_shipments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ShipmentNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TrackingNumber).HasMaxLength(100);
        builder.Property(x => x.TrackingUrl).HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.CargoStatusRaw).HasMaxLength(50);
        builder.Property(x => x.ApiStatus).HasMaxLength(30);
        builder.Property(x => x.ApiRequestPayload).HasColumnType("jsonb");
        builder.Property(x => x.ApiResponsePayload).HasColumnType("jsonb");
        builder.Property(x => x.DeliverySignature).HasMaxLength(200);
        builder.Property(x => x.TotalWeight).HasPrecision(10, 3);
        builder.Property(x => x.TotalDesi).HasPrecision(10, 3);
        builder.HasIndex(x => x.ShipmentNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId);
        builder.HasMany(x => x.Events).WithOne(x => x.Shipment).HasForeignKey(x => x.ShipmentId);
    }
}

public class ShipmentItemConfiguration : IEntityTypeConfiguration<ShipmentItem>
{
    public void Configure(EntityTypeBuilder<ShipmentItem> builder)
    {
        builder.ToTable("ord_shipment_items");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> builder)
    {
        builder.ToTable("ord_shipment_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EventDescription).HasMaxLength(500).IsRequired();
        builder.Property(x => x.EventLocation).HasMaxLength(200);
        builder.Property(x => x.RawData).HasColumnType("jsonb");
    }
}

public class OrderNotificationConfiguration : IEntityTypeConfiguration<OrderNotification>
{
    public void Configure(EntityTypeBuilder<OrderNotification> builder)
    {
        builder.ToTable("ord_order_notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.ProviderResponse).HasColumnType("jsonb");
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class GiftCardConfiguration : IEntityTypeConfiguration<GiftCard>
{
    public void Configure(EntityTypeBuilder<GiftCard> builder)
    {
        builder.ToTable("ord_gift_cards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OriginalAmount).HasPrecision(18, 2);
        builder.Property(x => x.RemainingAmount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Transactions).WithOne(x => x.GiftCard).HasForeignKey(x => x.GiftCardId);
    }
}

public class GiftCardTransactionConfiguration : IEntityTypeConfiguration<GiftCardTransaction>
{
    public void Configure(EntityTypeBuilder<GiftCardTransaction> builder)
    {
        builder.ToTable("ord_gift_card_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> builder)
    {
        builder.ToTable("ord_returns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReturnNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ReturnType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReturnTrackingNumber).HasMaxLength(100);
        builder.Property(x => x.RefundMethod).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RefundStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RefundAmount).HasPrecision(18, 2);
        builder.HasIndex(x => x.ReturnNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Return).HasForeignKey(x => x.ReturnId);
        builder.HasMany(x => x.Refunds).WithOne(x => x.Return).HasForeignKey(x => x.ReturnId);
    }
}

public class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
{
    public void Configure(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.ToTable("ord_return_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.InspectionResult).HasMaxLength(30);
        builder.Property(x => x.UnitRefundAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalRefundAmount).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ReturnRefundConfiguration : IEntityTypeConfiguration<ReturnRefund>
{
    public void Configure(EntityTypeBuilder<ReturnRefund> builder)
    {
        builder.ToTable("ord_return_refunds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RefundMethod).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Details).HasColumnType("jsonb");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("ord_quotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuoteNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2);
        builder.Property(x => x.TotalTax).HasPrecision(18, 2);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.QuoteNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Quote).HasForeignKey(x => x.QuoteId);
    }
}

public class OrderGiftConfiguration : IEntityTypeConfiguration<OrderGift>
{
    public void Configure(EntityTypeBuilder<OrderGift> builder)
    {
        builder.ToTable("ord_order_gifts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GiftReason).HasMaxLength(30);
        builder.Property(x => x.AddedAtStage).HasMaxLength(20).IsRequired();
        builder.Property(x => x.InvoiceDescription).HasMaxLength(200);
        builder.Property(x => x.UnitValue).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    public void Configure(EntityTypeBuilder<QuoteItem> builder)
    {
        builder.ToTable("ord_quote_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProductName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.VariantInfo).HasMaxLength(500);
        builder.Property(x => x.UnitType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountRate).HasPrecision(5, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
