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
