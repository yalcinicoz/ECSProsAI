using ECSPros.Fulfillment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Fulfillment.Infrastructure.Persistence.Configurations;

public class PickingPlanConfiguration : IEntityTypeConfiguration<PickingPlan>
{
    public void Configure(EntityTypeBuilder<PickingPlan> builder)
    {
        builder.ToTable("ful_picking_plans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlanNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PlanType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.Ignore(x => x.DomainEvents);
        builder.HasMany(x => x.Bins).WithOne(x => x.PickingPlan).HasForeignKey(x => x.PickingPlanId);
    }
}

public class SortingBinConfiguration : IEntityTypeConfiguration<SortingBin>
{
    public void Configure(EntityTypeBuilder<SortingBin> builder)
    {
        builder.ToTable("ful_sorting_bins");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PackingStationConfiguration : IEntityTypeConfiguration<PackingStation>
{
    public void Configure(EntityTypeBuilder<PackingStation> builder)
    {
        builder.ToTable("ful_packing_stations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StationCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StationName).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("ful_packages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PackageNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Barcode).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CargoIntegrationCode).HasMaxLength(100);
        builder.Property(x => x.CargoIntegrationCodeSource).HasMaxLength(10);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(10, 3);
        builder.Property(x => x.Width).HasPrecision(10, 3);
        builder.Property(x => x.Height).HasPrecision(10, 3);
        builder.Property(x => x.Length).HasPrecision(10, 3);
        builder.Property(x => x.Desi).HasPrecision(10, 3);
        // Paket kimliği kanal bazında unique (karar 2026-07-19)
        builder.HasIndex(x => new { x.FirmPlatformId, x.PackageNumber }).IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => x.OrderId);
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Package).HasForeignKey(x => x.PackageId);
    }
}

public class PackageItemConfiguration : IEntityTypeConfiguration<PackageItem>
{
    public void Configure(EntityTypeBuilder<PackageItem> builder)
    {
        builder.ToTable("ful_package_items");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrderItemId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PackageNumberSeriesConfiguration : IEntityTypeConfiguration<PackageNumberSeries>
{
    public void Configure(EntityTypeBuilder<PackageNumberSeries> builder)
    {
        builder.ToTable("ful_package_number_series");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Prefix).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => x.FirmPlatformId).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PackageCodeHistoryConfiguration : IEntityTypeConfiguration<PackageCodeHistory>
{
    public void Configure(EntityTypeBuilder<PackageCodeHistory> builder)
    {
        builder.ToTable("ful_package_code_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OldPackageNumber).HasMaxLength(30);
        builder.Property(x => x.OldCargoIntegrationCode).HasMaxLength(100);
        builder.Property(x => x.ChangeType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.PackageId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PickingPlanLineConfiguration : IEntityTypeConfiguration<PickingPlanLine>
{
    public void Configure(EntityTypeBuilder<PickingPlanLine> builder)
    {
        builder.ToTable("ful_picking_plan_lines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.VariantBarcode).HasMaxLength(60);
        builder.Property(l => l.OrderNumber).HasMaxLength(30);
        builder.Property(l => l.DisplayName).HasMaxLength(300);
        builder.Property(l => l.Sku).HasMaxLength(60);
        builder.Property(l => l.SourceBinCode).HasMaxLength(60);
        builder.Property(l => l.PickedBinCode).HasMaxLength(60);
        builder.Property(l => l.Status).HasMaxLength(20).IsRequired();
        // Görev detayı rota sıralı okur; personel kendi satırlarını çeker
        builder.HasIndex(l => new { l.PickingPlanId, l.RouteOrder });
        builder.HasIndex(l => new { l.PickingPlanId, l.AssignedTo });
        builder.HasIndex(l => l.OrderId);
        builder.HasQueryFilter(l => !l.IsDeleted);

        builder.HasOne(l => l.PickingPlan)
            .WithMany()
            .HasForeignKey(l => l.PickingPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OperationProfileConfiguration : IEntityTypeConfiguration<OperationProfile>
{
    public void Configure(EntityTypeBuilder<OperationProfile> builder)
    {
        builder.ToTable("ful_operation_profiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CargoNotifyAt).HasMaxLength(20).IsRequired();
        builder.HasIndex(p => p.FirmId).IsUnique();
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class OperationLogConfiguration : IEntityTypeConfiguration<OperationLog>
{
    public void Configure(EntityTypeBuilder<OperationLog> builder)
    {
        builder.ToTable("ful_operation_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Action).HasMaxLength(40).IsRequired();
        builder.Property(l => l.Detail).HasColumnType("jsonb");
        // Sipariş geçmişi ve görev izleme sorguları
        builder.HasIndex(l => new { l.OrderId, l.CreatedAt });
        builder.HasIndex(l => new { l.PickingPlanId, l.CreatedAt });
        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}

public class SortingBoxConfiguration : IEntityTypeConfiguration<SortingBox>
{
    public void Configure(EntityTypeBuilder<SortingBox> builder)
    {
        builder.ToTable("ful_sorting_boxes");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Status).HasMaxLength(20).IsRequired();
        // Koli duvarı görev bazlı listeler; numara+jenerasyon oturumu tekilleştirir
        builder.HasIndex(b => new { b.PickingPlanId, b.BoxNumber, b.Generation }).IsUnique();
        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}

public class PackingDeskConfiguration : IEntityTypeConfiguration<PackingDesk>
{
    public void Configure(EntityTypeBuilder<PackingDesk> builder)
    {
        builder.ToTable("ful_packing_desks");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Status).HasMaxLength(20).IsRequired();
        // Açık masalar arasında numara tekil (en küçük boş numara ataması)
        builder.HasIndex(d => new { d.PickingPlanId, d.DeskNumber, d.Status });
        builder.HasIndex(d => d.SortingBoxId);
        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

public class CargoNotifyOutboxConfiguration : IEntityTypeConfiguration<CargoNotifyOutbox>
{
    public void Configure(EntityTypeBuilder<CargoNotifyOutbox> builder)
    {
        builder.ToTable("ful_cargo_notify_outbox");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired();
        builder.Property(o => o.CargoName).HasMaxLength(100);
        builder.Property(o => o.LastError).HasMaxLength(2000);
        // Worker pending'leri çeker; yönlendirme ekranı taşıyıcı bazlı gruplar
        builder.HasIndex(o => new { o.Status, o.NextAttemptAt });
        builder.HasIndex(o => o.PackageId).IsUnique();
        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
