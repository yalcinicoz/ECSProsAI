using ECSPros.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Inventory.Infrastructure.Persistence.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("inv_warehouses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.WarehouseType).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Locations).WithOne(x => x.Warehouse).HasForeignKey(x => x.WarehouseId);
        builder.HasMany(x => x.Stocks).WithOne(x => x.Warehouse).HasForeignKey(x => x.WarehouseId);
    }
}

public class WarehouseLocationConfiguration : IEntityTypeConfiguration<WarehouseLocation>
{
    public void Configure(EntityTypeBuilder<WarehouseLocation> builder)
    {
        builder.ToTable("inv_warehouse_locations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.LocationType).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Barcode).IsUnique();
        builder.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).IsRequired(false);
    }
}

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("inv_stocks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StockType).HasMaxLength(20).IsRequired();
        builder.Ignore(x => x.AvailableQuantity);
        builder.HasIndex(x => new { x.VariantId, x.WarehouseId, x.LocationId, x.StockType }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).IsRequired(false);
        builder.HasMany(x => x.Reservations).WithOne(x => x.Stock).HasForeignKey(x => x.StockId);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("inv_stock_movements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MovementType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.HasIndex(x => x.VariantId);
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
    }
}

public class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("inv_stock_reservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TransferRequestConfiguration : IEntityTypeConfiguration<TransferRequest>
{
    public void Configure(EntityTypeBuilder<TransferRequest> builder)
    {
        builder.ToTable("inv_transfer_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TransferType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.FromWarehouse).WithMany().HasForeignKey(x => x.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne(x => x.TransferRequest).HasForeignKey(x => x.TransferRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Trackings).WithOne(x => x.TransferRequest).HasForeignKey(x => x.TransferRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransferRequestItemConfiguration : IEntityTypeConfiguration<TransferRequestItem>
{
    public void Configure(EntityTypeBuilder<TransferRequestItem> builder)
    {
        builder.ToTable("inv_transfer_request_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class TransferTrackingConfiguration : IEntityTypeConfiguration<TransferTracking>
{
    public void Configure(EntityTypeBuilder<TransferTracking> builder)
    {
        builder.ToTable("inv_transfer_tracking");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.TransferRequestId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
