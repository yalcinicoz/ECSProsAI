using ECSPros.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Finance.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("fin_suppliers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TaxOffice).HasMaxLength(200);
        builder.Property(x => x.TaxNumber).HasMaxLength(30);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Address).HasColumnType("text");
        builder.Property(x => x.ContactPerson).HasMaxLength(200);
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable("fin_supplier_invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TotalTax).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Items).WithOne(x => x.Invoice).HasForeignKey(x => x.InvoiceId);
        builder.HasMany(x => x.Deliveries).WithOne().HasForeignKey(x => x.InvoiceId);
    }
}

public class SupplierInvoiceItemConfiguration : IEntityTypeConfiguration<SupplierInvoiceItem>
{
    public void Configure(EntityTypeBuilder<SupplierInvoiceItem> builder)
    {
        builder.ToTable("fin_supplier_invoice_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DiscountRate).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TaxRate).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Total).HasPrecision(18, 2).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierDeliveryConfiguration : IEntityTypeConfiguration<SupplierDelivery>
{
    public void Configure(EntityTypeBuilder<SupplierDelivery> builder)
    {
        builder.ToTable("fin_supplier_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeliveryNoteNumber).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Items).WithOne(x => x.Delivery).HasForeignKey(x => x.DeliveryId);
    }
}

public class SupplierDeliveryItemConfiguration : IEntityTypeConfiguration<SupplierDeliveryItem>
{
    public void Configure(EntityTypeBuilder<SupplierDeliveryItem> builder)
    {
        builder.ToTable("fin_supplier_delivery_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierTransactionConfiguration : IEntityTypeConfiguration<SupplierTransaction>
{
    public void Configure(EntityTypeBuilder<SupplierTransaction> builder)
    {
        builder.ToTable("fin_supplier_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Debit).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Credit).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable("fin_supplier_payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.PaymentType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Details).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierReturnConfiguration : IEntityTypeConfiguration<SupplierReturn>
{
    public void Configure(EntityTypeBuilder<SupplierReturn> builder)
    {
        builder.ToTable("fin_supplier_returns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReturnNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.Property(x => x.Subtotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TotalTax).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TrackingNumber).HasMaxLength(100);
        builder.HasIndex(x => x.ReturnNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Items).WithOne(x => x.Return).HasForeignKey(x => x.ReturnId);
    }
}

public class SupplierReturnItemConfiguration : IEntityTypeConfiguration<SupplierReturnItem>
{
    public void Configure(EntityTypeBuilder<SupplierReturnItem> builder)
    {
        builder.ToTable("fin_supplier_return_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TaxRate).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Notes).HasColumnType("text");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierPriceHistoryConfiguration : IEntityTypeConfiguration<SupplierPriceHistory>
{
    public void Configure(EntityTypeBuilder<SupplierPriceHistory> builder)
    {
        builder.ToTable("fin_supplier_price_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OldPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.NewPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ChangeReason).HasColumnType("text");
    }
}
