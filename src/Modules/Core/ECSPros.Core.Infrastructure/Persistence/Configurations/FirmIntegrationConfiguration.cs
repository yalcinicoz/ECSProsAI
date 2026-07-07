using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class FirmIntegrationConfiguration : IEntityTypeConfiguration<FirmIntegration>
{
    public void Configure(EntityTypeBuilder<FirmIntegration> builder)
    {
        builder.ToTable("core_firm_integrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Credentials).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Settings).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ContractNumber).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Terms).HasColumnType("jsonb");
        builder.Property(x => x.ContactName).HasMaxLength(200);
        builder.Property(x => x.ContactPhone).HasMaxLength(20);
        builder.Property(x => x.ContactEmail).HasMaxLength(200);
        builder.Property(x => x.DocumentUrl).HasMaxLength(500);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.IntegrationService)
            .WithMany(x => x.FirmIntegrations)
            .HasForeignKey(x => x.IntegrationServiceId);
    }
}
