using ECSPros.Promotion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Promotion.Infrastructure.Persistence.Configurations;

public class CampaignTypeConfiguration : IEntityTypeConfiguration<CampaignType>
{
    public void Configure(EntityTypeBuilder<CampaignType> builder)
    {
        // Tip = definition katmanı (platformdan bağımsız). definition şemasına konur.
        builder.ToTable("campaign_types", "definition");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.HandlerClass).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SettingsSchema).HasColumnType("jsonb");
        builder.Property(x => x.Scope).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Campaigns).WithOne(x => x.CampaignType).HasForeignKey(x => x.CampaignTypeId);
    }
}

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("prm_campaigns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.BadgeLabel).HasMaxLength(60);
        builder.Property(x => x.Settings).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.FillType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FilterDef).HasColumnType("jsonb");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.FirmPlatformId, x.IsActive });
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Products).WithOne(x => x.Campaign).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Exclusions).WithOne(x => x.Campaign).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Platforms).WithOne(x => x.Campaign).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CampaignProductConfiguration : IEntityTypeConfiguration<CampaignProduct>
{
    public void Configure(EntityTypeBuilder<CampaignProduct> builder)
    {
        builder.ToTable("prm_campaign_products");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AddedType).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => new { x.CampaignId, x.ProductId, x.VariantId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

// P3a (2026-08-11): kampanya × satıcı katılımı (opt-in + ürün seçimi)
public class CampaignSupplierParticipationConfiguration : IEntityTypeConfiguration<CampaignSupplierParticipation>
{
    public void Configure(EntityTypeBuilder<CampaignSupplierParticipation> builder)
    {
        builder.ToTable("prm_campaign_supplier_participations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductIds).HasColumnType("jsonb");
        builder.HasOne(x => x.Campaign).WithMany()
            .HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.CampaignId, x.SupplierAccountId }).IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CampaignExclusionConfiguration : IEntityTypeConfiguration<CampaignExclusion>
{
    public void Configure(EntityTypeBuilder<CampaignExclusion> builder)
    {
        builder.ToTable("prm_campaign_exclusions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CampaignId, x.ProductId, x.VariantId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CampaignPlatformConfiguration : IEntityTypeConfiguration<CampaignPlatform>
{
    public void Configure(EntityTypeBuilder<CampaignPlatform> builder)
    {
        builder.ToTable("prm_campaign_platforms");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.CampaignId, x.FirmPlatformId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("prm_coupons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CouponType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DiscountValue).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MinimumCartTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Usages).WithOne(x => x.Coupon).HasForeignKey(x => x.CouponId);
    }
}

public class CouponUsageConfiguration : IEntityTypeConfiguration<CouponUsage>
{
    public void Configure(EntityTypeBuilder<CouponUsage> builder)
    {
        builder.ToTable("prm_coupon_usages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2).IsRequired();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

// ── Şans oyunları (docs/BACKEND_OYUNLAR.md, 2026-09-11) ─────────────────────────────────────
public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("prm_games");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TitleI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SubtitleI18n).HasColumnType("jsonb");
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.RulesTextI18n).HasColumnType("jsonb");
        builder.Property(x => x.CtaLabel).HasMaxLength(60);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.ThemeColor).HasMaxLength(9);
        builder.Property(x => x.AccentColor).HasMaxLength(9);
        builder.Property(x => x.LimitPeriod).HasMaxLength(10).IsRequired();
        foreach (var p in new[] { nameof(Game.LabelAvailable), nameof(Game.LabelCooldown), nameof(Game.LabelExhausted),
                 nameof(Game.LabelLoginRequired), nameof(Game.LabelEnded), nameof(Game.WinMessage), nameof(Game.LoseMessage) })
            builder.Property(p).HasMaxLength(200).IsRequired();
        builder.Property(x => x.WinSubMessage).HasMaxLength(500).IsRequired();
        builder.Property(x => x.LoseSubMessage).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => new { x.FirmPlatformId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Prizes).WithOne(x => x.Game).HasForeignKey(x => x.GameId);
        builder.HasMany(x => x.Plays).WithOne(x => x.Game).HasForeignKey(x => x.GameId);
    }
}

public class GamePrizeConfiguration : IEntityTypeConfiguration<GamePrize>
{
    public void Configure(EntityTypeBuilder<GamePrize> builder)
    {
        builder.ToTable("prm_game_prizes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ShortLabel).HasMaxLength(30);
        builder.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(9);
        builder.Property(x => x.IconUrl).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.CouponType).HasMaxLength(20);
        builder.Property(x => x.CouponValue).HasPrecision(18, 2);
        builder.Property(x => x.MinimumCartTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.GameId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class GamePlayConfiguration : IEntityTypeConfiguration<GamePlay>
{
    public void Configure(EntityTypeBuilder<GamePlay> builder)
    {
        builder.ToTable("prm_game_plays");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PeriodKey).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CouponCode).HasMaxLength(100);
        builder.HasIndex(x => new { x.GameId, x.MemberId, x.PeriodKey });
        builder.HasIndex(x => new { x.MemberId, x.PlayedAt });
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasOne(x => x.Prize).WithMany().HasForeignKey(x => x.PrizeId).OnDelete(DeleteBehavior.SetNull);
    }
}
