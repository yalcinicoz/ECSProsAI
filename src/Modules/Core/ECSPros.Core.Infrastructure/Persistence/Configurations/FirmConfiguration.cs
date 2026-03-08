using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class FirmConfiguration : IEntityTypeConfiguration<Firm>
{
    public void Configure(EntityTypeBuilder<Firm> builder)
    {
        builder.ToTable("core_firms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.TaxOffice).HasMaxLength(200);
        builder.Property(x => x.TaxNumber).HasMaxLength(20);
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.PriceType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PriceMultiplier).HasPrecision(18, 6);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.FirmPlatforms)
            .WithOne(x => x.Firm)
            .HasForeignKey(x => x.FirmId);

        builder.HasOne(x => x.InvoiceIntegrator)
            .WithMany()
            .HasForeignKey(x => x.InvoiceIntegratorId)
            .IsRequired(false);

        builder.HasMany(x => x.FirmIntegrations)
            .WithOne(x => x.Firm)
            .HasForeignKey(x => x.FirmId);

        builder.HasMany(x => x.CargoRules)
            .WithOne(x => x.Firm)
            .HasForeignKey(x => x.FirmId);

        builder.HasMany(x => x.NotificationSettings)
            .WithOne(x => x.Firm)
            .HasForeignKey(x => x.FirmId);
    }
}
