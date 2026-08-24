using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class LabelTemplateConfiguration : IEntityTypeConfiguration<LabelTemplate>
{
    public void Configure(EntityTypeBuilder<LabelTemplate> builder)
    {
        builder.ToTable("core_label_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.WidthMm).HasPrecision(8, 2);
        builder.Property(x => x.HeightMm).HasPrecision(8, 2);
        builder.Property(x => x.ElementsJson).HasColumnName("elements").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.TargetType, x.IsDefault });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
