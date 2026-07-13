using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class IntegrationServiceConfiguration : IEntityTypeConfiguration<IntegrationService>
{
    public void Configure(EntityTypeBuilder<IntegrationService> builder)
    {
        builder.ToTable("core_integration_services");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ServiceType).HasMaxLength(50).IsRequired();
        // Kolon adı DB'de SettingsSchema kalır — Json soneki CLR tarafının detayı.
        builder.Property(x => x.SettingsSchemaJson).HasColumnType("jsonb").HasColumnName("SettingsSchema");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
