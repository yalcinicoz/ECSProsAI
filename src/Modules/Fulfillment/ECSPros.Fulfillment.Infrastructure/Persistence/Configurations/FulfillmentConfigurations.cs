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
        builder.Property(x => x.Barcode).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Weight).HasPrecision(10, 3);
        builder.Property(x => x.Width).HasPrecision(10, 3);
        builder.Property(x => x.Height).HasPrecision(10, 3);
        builder.Property(x => x.Length).HasPrecision(10, 3);
        builder.Property(x => x.Desi).HasPrecision(10, 3);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
