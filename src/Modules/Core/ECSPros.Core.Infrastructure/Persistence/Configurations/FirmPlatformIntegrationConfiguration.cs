using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class FirmPlatformIntegrationConfiguration : IEntityTypeConfiguration<FirmPlatformIntegration>
{
    public void Configure(EntityTypeBuilder<FirmPlatformIntegration> builder)
    {
        builder.ToTable("core_firm_platform_integrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200);
        // Credentials eşlemesi (Data Protection ile şifreli text) CoreDbContext.OnModelCreating'de —
        // IDataProtector DI'dan geldiği için parametresiz configuration'da yapılamaz.
        builder.Property(x => x.Settings).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Terms).HasColumnType("jsonb");
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasIndex(x => new { x.FirmId, x.FirmPlatformId });

        builder.HasOne(x => x.IntegrationService)
            .WithMany(x => x.PlatformIntegrations)
            .HasForeignKey(x => x.IntegrationServiceId);

        // null → firma geneli (tüm platformlar); dolu → yalnız o platform.
        builder.HasOne(x => x.FirmPlatform)
            .WithMany()
            .HasForeignKey(x => x.FirmPlatformId);
    }
}
