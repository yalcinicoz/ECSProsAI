using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class FirmPlatformConfiguration : IEntityTypeConfiguration<FirmPlatform>
{
    public void Configure(EntityTypeBuilder<FirmPlatform> builder)
    {
        builder.ToTable("core_firm_platforms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Credentials).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Settings).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PriceType).HasMaxLength(20);
        builder.Property(x => x.PriceMultiplier).HasPrecision(18, 6);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.PlatformType)
            .WithMany(x => x.FirmPlatforms)
            .HasForeignKey(x => x.PlatformTypeId);
    }
}
