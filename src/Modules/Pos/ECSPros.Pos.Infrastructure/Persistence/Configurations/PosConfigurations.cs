using ECSPros.Pos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Pos.Infrastructure.Persistence.Configurations;

public class PosRegisterConfiguration : IEntityTypeConfiguration<PosRegister>
{
    public void Configure(EntityTypeBuilder<PosRegister> builder)
    {
        builder.ToTable("pos_registers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReceiptPrefix).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Sessions).WithOne(x => x.Register).HasForeignKey(x => x.RegisterId);
        builder.HasMany(x => x.QuickProducts).WithOne(x => x.Register).HasForeignKey(x => x.RegisterId).IsRequired(false);
    }
}

public class PosSessionConfiguration : IEntityTypeConfiguration<PosSession>
{
    public void Configure(EntityTypeBuilder<PosSession> builder)
    {
        builder.ToTable("pos_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SessionNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OpeningCash).HasPrecision(18, 2);
        builder.Property(x => x.ClosingCash).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedCash).HasPrecision(18, 2);
        builder.Property(x => x.CashDifference).HasPrecision(18, 2);
        builder.HasIndex(x => x.SessionNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Transactions).WithOne(x => x.Session).HasForeignKey(x => x.SessionId);
        builder.HasMany(x => x.Sales).WithOne(x => x.Session).HasForeignKey(x => x.SessionId);
    }
}

public class PosSessionTransactionConfiguration : IEntityTypeConfiguration<PosSessionTransaction>
{
    public void Configure(EntityTypeBuilder<PosSessionTransaction> builder)
    {
        builder.ToTable("pos_session_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(20);
    }
}

public class PosQuickProductConfiguration : IEntityTypeConfiguration<PosQuickProduct>
{
    public void Configure(EntityTypeBuilder<PosQuickProduct> builder)
    {
        builder.ToTable("pos_quick_products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ButtonText).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ButtonColor).HasMaxLength(20);
        builder.Property(x => x.Category).HasMaxLength(50);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PosSaleConfiguration : IEntityTypeConfiguration<PosSale>
{
    public void Configure(EntityTypeBuilder<PosSale> builder)
    {
        builder.ToTable("pos_sales");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SaleNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TotalDiscount).HasPrecision(18, 2);
        builder.Property(x => x.TotalTax).HasPrecision(18, 2);
        builder.Property(x => x.GrandTotal).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => x.SaleNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Sale).HasForeignKey(x => x.SaleId);
        builder.HasMany(x => x.Payments).WithOne(x => x.Sale).HasForeignKey(x => x.SaleId);
        // DomainEvents alanı persist edilmez
        builder.Ignore(x => x.DomainEvents);
    }
}

public class PosSaleItemConfiguration : IEntityTypeConfiguration<PosSaleItem>
{
    public void Configure(EntityTypeBuilder<PosSaleItem> builder)
    {
        builder.ToTable("pos_sale_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Barcode).HasMaxLength(50);
        builder.Property(x => x.ProductName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PosSalePaymentConfiguration : IEntityTypeConfiguration<PosSalePayment>
{
    public void Configure(EntityTypeBuilder<PosSalePayment> builder)
    {
        builder.ToTable("pos_sale_payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.TenderedAmount).HasPrecision(18, 2);
        builder.Property(x => x.ChangeAmount).HasPrecision(18, 2);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
