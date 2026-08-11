using ECSPros.Accounts.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Accounts.Infrastructure.Persistence.Configurations;

// P3a (2026-08-11): satıcı sözleşmesi + komisyon oran katmanları + hakediş satırları

public class SupplierContractConfiguration : IEntityTypeConfiguration<SupplierContract>
{
    public void Configure(EntityTypeBuilder<SupplierContract> b)
    {
        b.ToTable("supplier_contracts");
        b.HasKey(c => c.Id);
        b.Property(c => c.PayoutPeriod).HasMaxLength(20).IsRequired().HasDefaultValue("weekly");
        b.Property(c => c.CargoMode).HasMaxLength(30).IsRequired().HasDefaultValue("platform_contract");
        b.Property(c => c.TurnoverPeriodType).HasMaxLength(20).IsRequired().HasDefaultValue("monthly");
        b.Property(c => c.Notes).HasMaxLength(1000);
        b.HasIndex(c => c.CurrentAccountId).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public class SupplierGroupRateConfiguration : IEntityTypeConfiguration<SupplierGroupRate>
{
    public void Configure(EntityTypeBuilder<SupplierGroupRate> b)
    {
        b.ToTable("supplier_group_rates");
        b.HasKey(r => r.Id);
        b.Property(r => r.RatePercent).HasColumnType("numeric(5,2)");
        b.HasOne(r => r.Contract).WithMany(c => c.GroupRates)
            .HasForeignKey(r => r.ContractId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(r => new { r.ContractId, r.ProductGroupId }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public class SupplierProductRateConfiguration : IEntityTypeConfiguration<SupplierProductRate>
{
    public void Configure(EntityTypeBuilder<SupplierProductRate> b)
    {
        b.ToTable("supplier_product_rates");
        b.HasKey(r => r.Id);
        b.Property(r => r.RatePercent).HasColumnType("numeric(5,2)");
        b.HasOne(r => r.Contract).WithMany(c => c.ProductRates)
            .HasForeignKey(r => r.ContractId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(r => new { r.ContractId, r.ProductId }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public class SupplierTurnoverTierConfiguration : IEntityTypeConfiguration<SupplierTurnoverTier>
{
    public void Configure(EntityTypeBuilder<SupplierTurnoverTier> b)
    {
        b.ToTable("supplier_turnover_tiers");
        b.HasKey(t => t.Id);
        b.Property(t => t.MinTurnover).HasColumnType("numeric(18,2)");
        b.Property(t => t.RateAdjustmentPercent).HasColumnType("numeric(5,2)");
        b.HasOne(t => t.Contract).WithMany(c => c.TurnoverTiers)
            .HasForeignKey(t => t.ContractId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(t => new { t.ContractId, t.MinTurnover }).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public class CommissionGroupRateConfiguration : IEntityTypeConfiguration<CommissionGroupRate>
{
    public void Configure(EntityTypeBuilder<CommissionGroupRate> b)
    {
        b.ToTable("commission_group_rates");
        b.HasKey(r => r.Id);
        b.Property(r => r.RatePercent).HasColumnType("numeric(5,2)");
        b.HasIndex(r => r.ProductGroupId).IsUnique().HasFilter("\"IsDeleted\" = false");
    }
}

public class SettlementLineConfiguration : IEntityTypeConfiguration<SettlementLine>
{
    public void Configure(EntityTypeBuilder<SettlementLine> b)
    {
        b.ToTable("settlement_lines");
        b.HasKey(l => l.Id);
        b.Property(l => l.OrderNumber).HasMaxLength(50).IsRequired();
        b.Property(l => l.Sku).HasMaxLength(100).IsRequired();
        b.Property(l => l.ProductName).HasMaxLength(500);
        b.Property(l => l.GrossAmount).HasColumnType("numeric(18,2)");
        b.Property(l => l.CommissionRate).HasColumnType("numeric(5,2)");
        b.Property(l => l.CommissionLayer).HasMaxLength(50).IsRequired();
        b.Property(l => l.CommissionAmount).HasColumnType("numeric(18,2)");
        b.Property(l => l.CampaignDiscountShareAmount).HasColumnType("numeric(18,2)");
        b.Property(l => l.NetAmount).HasColumnType("numeric(18,2)");
        b.Property(l => l.Status).HasMaxLength(20).IsRequired().HasDefaultValue("pending");
        b.Property(l => l.Description).HasMaxLength(500);
        // Kalem başına tek hakediş satırı (ters satırlar ReversalOfId dolu olduğundan muaf)
        b.HasIndex(l => l.OrderItemId).IsUnique()
            .HasFilter("\"IsDeleted\" = false AND \"ReversalOfId\" IS NULL");
        b.HasIndex(l => new { l.SupplierAccountId, l.Status });
        b.HasIndex(l => new { l.Status, l.EligibleAt });
    }
}
