using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class UiTranslationConfiguration : IEntityTypeConfiguration<UiTranslation>
{
    public void Configure(EntityTypeBuilder<UiTranslation> builder)
    {
        builder.ToTable("core_ui_translations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Namespace).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Lang).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();

        builder.HasIndex(x => new { x.Namespace, x.Key, x.Lang }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
