using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Core.Infrastructure.Persistence.Configurations;

public class ExpenseTypeConfiguration : IEntityTypeConfiguration<ExpenseType>
{
    public void Configure(EntityTypeBuilder<ExpenseType> builder)
    {
        builder.ToTable("core_expense_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DefaultTaxRate).HasPrecision(5, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CargoRuleConfiguration : IEntityTypeConfiguration<CargoRule>
{
    public void Configure(EntityTypeBuilder<CargoRule> builder)
    {
        builder.ToTable("core_cargo_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RuleType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentType).HasMaxLength(20);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.FirmIntegration)
            .WithMany()
            .HasForeignKey(x => x.FirmIntegrationId);
    }
}

public class OrderStatusConfiguration : IEntityTypeConfiguration<OrderStatus>
{
    public void Configure(EntityTypeBuilder<OrderStatus> builder)
    {
        builder.ToTable("core_order_statuses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Color).HasMaxLength(20);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class OrderItemStatusConfiguration : IEntityTypeConfiguration<OrderItemStatus>
{
    public void Configure(EntityTypeBuilder<OrderItemStatus> builder)
    {
        builder.ToTable("core_order_item_statuses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Color).HasMaxLength(20);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("core_payment_methods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ReturnReasonConfiguration : IEntityTypeConfiguration<ReturnReason>
{
    public void Configure(EntityTypeBuilder<ReturnReason> builder)
    {
        builder.ToTable("core_return_reasons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class FirmNotificationSettingConfiguration : IEntityTypeConfiguration<FirmNotificationSetting>
{
    public void Configure(EntityTypeBuilder<FirmNotificationSetting> builder)
    {
        builder.ToTable("core_firm_notification_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channels).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.FirmId, x.NotificationTypeId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.NotificationType)
            .WithMany(x => x.FirmSettings)
            .HasForeignKey(x => x.NotificationTypeId);
    }
}
