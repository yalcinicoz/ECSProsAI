using ECSPros.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Procurement.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.Status });
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.PurchaseOrder).HasForeignKey(x => x.PurchaseOrderId);
    }
}

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("purchase_order_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ModelText).HasMaxLength(300);
        builder.Property(x => x.ColorText).HasMaxLength(100);
        builder.Property(x => x.SizeText).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.VariantId);
        // Sorgular Include(p => p.Items.Where(!IsDeleted)) kullanır; parent filtresiyle uyum için filtre eşlenir.
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ReceiptBatchConfiguration : IEntityTypeConfiguration<ReceiptBatch>
{
    public void Configure(EntityTypeBuilder<ReceiptBatch> builder)
    {
        builder.ToTable("receipt_batches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DeliveryNoteNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.Status });
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.ReceiptBatch).HasForeignKey(x => x.ReceiptBatchId);
        builder.HasMany(x => x.PurchaseOrders).WithOne(x => x.ReceiptBatch).HasForeignKey(x => x.ReceiptBatchId);
    }
}

public class ReceiptBatchItemConfiguration : IEntityTypeConfiguration<ReceiptBatchItem>
{
    public void Configure(EntityTypeBuilder<ReceiptBatchItem> builder)
    {
        builder.ToTable("receipt_batch_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DescriptionText).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.HasIndex(x => x.ReceiptBatchId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ReceiptBatchPurchaseOrderConfiguration : IEntityTypeConfiguration<ReceiptBatchPurchaseOrder>
{
    public void Configure(EntityTypeBuilder<ReceiptBatchPurchaseOrder> builder)
    {
        builder.ToTable("receipt_batch_purchase_orders");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ReceiptBatchId, x.PurchaseOrderId }).IsUnique();
        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
