using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class NotificationTypeConfiguration : IEntityTypeConfiguration<NotificationType>
{
    public void Configure(EntityTypeBuilder<NotificationType> builder)
    {
        builder.ToTable("core_notification_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DefaultChannels).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Templates)
            .WithOne(x => x.NotificationType)
            .HasForeignKey(x => x.NotificationTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
