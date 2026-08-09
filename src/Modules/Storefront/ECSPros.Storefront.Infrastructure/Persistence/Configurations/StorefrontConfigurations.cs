using ECSPros.Storefront.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Storefront.Infrastructure.Persistence.Configurations;

public class NavigationMenuConfiguration : IEntityTypeConfiguration<NavigationMenu>
{
    public void Configure(EntityTypeBuilder<NavigationMenu> builder)
    {
        builder.ToTable("nav_menus");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Code).HasMaxLength(50).IsRequired();
        builder.Property(m => m.MenuType).HasMaxLength(20).IsRequired();
        builder.Property(m => m.NameI18n).HasColumnType("jsonb");
        builder.HasIndex(m => new { m.FirmPlatformId, m.Code }).IsUnique();
        builder.HasQueryFilter(m => !m.IsDeleted);

        builder.HasMany(m => m.Nodes)
            .WithOne(n => n.NavigationMenu)
            .HasForeignKey(n => n.NavigationMenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NavNodeConfiguration : IEntityTypeConfiguration<NavNode>
{
    public void Configure(EntityTypeBuilder<NavNode> builder)
    {
        builder.ToTable("nav_nodes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.NodeType).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Slug).HasMaxLength(200);
        builder.Property(n => n.CustomUrl).HasMaxLength(500);
        builder.Property(n => n.ImageUrl).HasMaxLength(500);
        builder.Property(n => n.BadgeLabel).HasMaxLength(50);
        builder.Property(n => n.Icon).HasMaxLength(100);
        builder.Property(n => n.CanonicalUrl).HasMaxLength(500);
        builder.Property(n => n.OgImageUrl).HasMaxLength(500);
        builder.Property(n => n.NameOverrideI18n).HasColumnType("jsonb");
        builder.Property(n => n.SeoTitleI18n).HasColumnType("jsonb");
        builder.Property(n => n.SeoDescriptionI18n).HasColumnType("jsonb");
        builder.Property(n => n.OgTitleI18n).HasColumnType("jsonb");
        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.HasOne(n => n.Parent)
            .WithMany(n => n.Children)
            .HasForeignKey(n => n.ParentNavNodeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(n => n.ChannelCategory)
            .WithMany()
            .HasForeignKey(n => n.ChannelCategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ChannelProductConfiguration : IEntityTypeConfiguration<ChannelProduct>
{
    public void Configure(EntityTypeBuilder<ChannelProduct> builder)
    {
        builder.ToTable("channel_products");
        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.NameI18n).HasColumnType("jsonb");
        builder.Property(cp => cp.ShortDescriptionI18n).HasColumnType("jsonb");
        builder.HasIndex(cp => new { cp.FirmPlatformId, cp.ProductId }).IsUnique();
        // M2/M3 deny-set sorgusu: platformda çıkarılan (IsActive=false) / durdurulan ürünler.
        builder.HasIndex(cp => new { cp.FirmPlatformId, cp.IsActive });
        builder.HasQueryFilter(cp => !cp.IsDeleted);
    }
}

public class ChannelVariantConfiguration : IEntityTypeConfiguration<ChannelVariant>
{
    public void Configure(EntityTypeBuilder<ChannelVariant> builder)
    {
        builder.ToTable("channel_variants");
        builder.HasKey(cv => cv.Id);
        builder.Property(cv => cv.PriceType).HasMaxLength(20);
        builder.Property(cv => cv.PriceMultiplier).HasPrecision(18, 6);
        builder.Property(cv => cv.Price).HasPrecision(18, 2);
        builder.Property(cv => cv.CompareAtPrice).HasPrecision(18, 2);
        builder.HasIndex(cv => new { cv.FirmPlatformId, cv.VariantId }).IsUnique();
        builder.Property(cv => cv.Slug).HasMaxLength(255);
        // Slug → varyant çözümü routing sıcak yolu (kök /{slug} isteği).
        builder.HasIndex(cv => new { cv.FirmPlatformId, cv.Slug });
        builder.HasQueryFilter(cv => !cv.IsDeleted);
    }
}

public class ChannelCategoryConfiguration : IEntityTypeConfiguration<ChannelCategory>
{
    public void Configure(EntityTypeBuilder<ChannelCategory> builder)
    {
        builder.ToTable("channel_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Slug).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(20).IsRequired();
        builder.Property(c => c.FillType).HasMaxLength(20).IsRequired();
        builder.Property(c => c.ListingMode).HasMaxLength(20).IsRequired().HasDefaultValue("product");
        builder.Property(c => c.DisplayImageUrl).HasMaxLength(500);
        builder.Property(c => c.BadgeLabel).HasMaxLength(50);
        builder.Property(c => c.OgImageUrl).HasMaxLength(500);
        builder.Property(c => c.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.FilterDef).HasColumnType("jsonb");
        builder.Property(c => c.MetaTitleI18n).HasColumnType("jsonb");
        builder.Property(c => c.MetaDescriptionI18n).HasColumnType("jsonb");
        builder.Property(c => c.OgTitleI18n).HasColumnType("jsonb");
        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasIndex(c => new { c.FirmPlatformId, c.Slug })
            .IsUnique()
            .HasFilter("\"Slug\" IS NOT NULL AND \"Slug\" <> ''");

        builder.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(c => c.CategoryGroups)
            .WithOne(g => g.ChannelCategory)
            .HasForeignKey(g => g.ChannelCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.CategoryProducts)
            .WithOne(p => p.ChannelCategory)
            .HasForeignKey(p => p.ChannelCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChannelCategoryGroupConfiguration : IEntityTypeConfiguration<ChannelCategoryGroup>
{
    public void Configure(EntityTypeBuilder<ChannelCategoryGroup> builder)
    {
        builder.ToTable("channel_category_groups");
        builder.HasKey(g => new { g.ChannelCategoryId, g.ProductGroupId });
        builder.Property(g => g.ShowcaseProductId);
    }
}

public class ChannelCategoryProductConfiguration : IEntityTypeConfiguration<ChannelCategoryProduct>
{
    public void Configure(EntityTypeBuilder<ChannelCategoryProduct> builder)
    {
        builder.ToTable("channel_category_products");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.ChannelCategoryId, p.ProductId }).IsUnique();
    }
}

public class ChannelProductGroupConfiguration : IEntityTypeConfiguration<ChannelProductGroup>
{
    public void Configure(EntityTypeBuilder<ChannelProductGroup> builder)
    {
        builder.ToTable("channel_product_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(g => new { g.FirmPlatformId, g.ProductGroupId }).IsUnique();
        builder.HasQueryFilter(g => !g.IsDeleted);
    }
}

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.ToTable("product_reviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ProductCode).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Text).HasMaxLength(2000);
        builder.Property(r => r.Status).HasMaxLength(20).IsRequired();
        builder.Property(r => r.RejectReason).HasMaxLength(500);
        builder.Property(r => r.MemberName).HasMaxLength(100).IsRequired();
        // Üye bir ürünü bir kez değerlendirir (silinen tekrar yazılabilir — filtre IsDeleted'ı eler)
        builder.HasIndex(r => new { r.FirmPlatformId, r.MemberId, r.ProductCode });
        builder.HasIndex(r => new { r.FirmPlatformId, r.ProductCode, r.Status }); // ürün sayfası + istatistik
        builder.HasIndex(r => new { r.FirmPlatformId, r.Status });                // moderasyon kuyruğu
        builder.Property(r => r.Topic).HasMaxLength(50);
        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}

public class ProductReviewPhotoConfiguration : IEntityTypeConfiguration<ProductReviewPhoto>
{
    public void Configure(EntityTypeBuilder<ProductReviewPhoto> builder)
    {
        builder.ToTable("product_review_photos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PhotoUrl).HasMaxLength(500).IsRequired();
        builder.HasIndex(p => p.ReviewId);
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("collections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.ShareCode).HasMaxLength(16).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.ShareCode).IsUnique();
        builder.HasIndex(c => new { c.FirmPlatformId, c.MemberId });
        builder.HasIndex(c => new { c.FirmPlatformId, c.Status }); // moderasyon kuyruğu + G bloğu
        builder.HasQueryFilter(c => !c.IsDeleted);
        builder.HasMany(c => c.Items).WithOne(i => i.Collection)
            .HasForeignKey(i => i.CollectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CollectionItemConfiguration : IEntityTypeConfiguration<CollectionItem>
{
    public void Configure(EntityTypeBuilder<CollectionItem> builder)
    {
        builder.ToTable("collection_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ProductCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(i => new { i.CollectionId, i.ProductCode }).IsUnique();
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("favorites");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.ProductCode).HasMaxLength(50).IsRequired();
        // Üye bir ürünü platform başına bir kez favoriler (ekleme idempotent)
        // 2026-07-16: favori ürün+renk bazlı — aynı ürünün farklı renkleri ayrı kayıt.
        builder.HasIndex(f => new { f.FirmPlatformId, f.MemberId, f.ProductCode, f.ColorValueId }).IsUnique();
        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}

public class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
{
    public void Configure(EntityTypeBuilder<StockAlert> builder)
    {
        builder.ToTable("stock_alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Email).HasMaxLength(200);
        builder.Property(a => a.ProductCode).HasMaxLength(50);
        builder.Property(a => a.VariantInfo).HasMaxLength(200);
        builder.Property(a => a.Status).HasMaxLength(20).IsRequired();
        // Stok girişinde "bu varyantı bekleyenler" sorgusu + üyenin mükerrer kaydı guard'ı
        builder.HasIndex(a => new { a.FirmPlatformId, a.VariantId, a.Status });
        builder.HasIndex(a => new { a.MemberId, a.Status });
        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}

public class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        builder.ToTable("saved_searches");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100);
        builder.Property(s => s.Query).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Filters).HasColumnType("jsonb");
        // Üye aynı arama metnini platform başına bir kez kaydeder (mükerrer engeli komutta)
        builder.HasIndex(s => new { s.FirmPlatformId, s.MemberId, s.Query }).IsUnique();
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public class ViewedProductConfiguration : IEntityTypeConfiguration<ViewedProduct>
{
    public void Configure(EntityTypeBuilder<ViewedProduct> builder)
    {
        builder.ToTable("viewed_products");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.ProductCode).HasMaxLength(50).IsRequired();
        // Ürün başına tek kayıt (tekrar gezmede ViewedAt güncellenir)
        builder.HasIndex(v => new { v.FirmPlatformId, v.MemberId, v.ProductCode }).IsUnique();
        // Üyenin son gezdikleri sorgusu + budama
        builder.HasIndex(v => new { v.MemberId, v.ViewedAt });
        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}

public class CartRemovedItemConfiguration : IEntityTypeConfiguration<CartRemovedItem>
{
    public void Configure(EntityTypeBuilder<CartRemovedItem> builder)
    {
        builder.ToTable("cart_removed_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.CurrencyCode).HasMaxLength(8).IsRequired();
        // Üye+platform+varyant başına tek kayıt (yeniden silmede tarih tazelenir)
        builder.HasIndex(x => new { x.FirmPlatformId, x.MemberId, x.VariantId }).IsUnique();
        // Üyenin son çıkardıkları sorgusu + budama
        builder.HasIndex(x => new { x.MemberId, x.CreatedAt });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("contact_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Email).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Phone).HasMaxLength(30);
        builder.Property(m => m.Subject).HasMaxLength(150);
        builder.Property(m => m.Message).HasMaxLength(4000).IsRequired();
        builder.Property(m => m.Status).HasMaxLength(20).IsRequired();
        // Admin kuyruğu + aynı e-postadan gönderim sınırı (throttle) sorguları
        builder.HasIndex(m => new { m.FirmPlatformId, m.Status });
        builder.HasIndex(m => new { m.Email, m.CreatedAt });
        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}

public class NewsletterSubscriptionConfiguration : IEntityTypeConfiguration<NewsletterSubscription>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscription> builder)
    {
        builder.ToTable("newsletter_subscriptions");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Email).HasMaxLength(200).IsRequired();
        // E-posta platform başına bir kez (kayıt idempotent)
        builder.HasIndex(n => new { n.FirmPlatformId, n.Email }).IsUnique();
        builder.HasQueryFilter(n => !n.IsDeleted);
    }
}

public class PageBlockConfiguration : IEntityTypeConfiguration<PageBlock>
{
    public void Configure(EntityTypeBuilder<PageBlock> builder)
    {
        builder.ToTable("page_blocks");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Placement).HasMaxLength(30).IsRequired();
        builder.Property(b => b.BlockType).HasMaxLength(30).IsRequired();
        builder.Property(b => b.Template).HasMaxLength(30);
        builder.Property(b => b.TitleI18n).HasColumnType("jsonb");
        builder.Property(b => b.SubtitleI18n).HasColumnType("jsonb");
        builder.Property(b => b.RuleJson).HasColumnType("jsonb");
        builder.Property(b => b.ConfigJson).HasColumnType("jsonb");
        // Admin yerleşim editörü + Yayınla taraması bu sırayla okur
        builder.HasIndex(b => new { b.FirmPlatformId, b.Placement, b.SortOrder });
        builder.HasQueryFilter(b => !b.IsDeleted);

        builder.HasMany(b => b.Items)
            .WithOne(i => i.PageBlock)
            .HasForeignKey(i => i.PageBlockId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PageBlockItemConfiguration : IEntityTypeConfiguration<PageBlockItem>
{
    public void Configure(EntityTypeBuilder<PageBlockItem> builder)
    {
        builder.ToTable("page_block_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TitleI18n).HasColumnType("jsonb");
        builder.Property(i => i.SubtitleI18n).HasColumnType("jsonb");
        builder.Property(i => i.ButtonTextI18n).HasColumnType("jsonb");
        builder.Property(i => i.RuleJson).HasColumnType("jsonb");
        builder.Property(i => i.ConfigJson).HasColumnType("jsonb");
        builder.Property(i => i.ImageUrl).HasMaxLength(500);
        builder.Property(i => i.MobileImageUrl).HasMaxLength(500);
        builder.Property(i => i.VideoUrl).HasMaxLength(500);
        builder.Property(i => i.LinkUrl).HasMaxLength(500);
        builder.Property(i => i.BadgeLabel).HasMaxLength(50);
        builder.HasIndex(i => new { i.PageBlockId, i.SortOrder });
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public class PublishedSnapshotConfiguration : IEntityTypeConfiguration<PublishedSnapshot>
{
    public void Configure(EntityTypeBuilder<PublishedSnapshot> builder)
    {
        builder.ToTable("published_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.JsonData).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Note).HasMaxLength(500);
        builder.HasIndex(s => new { s.FirmPlatformId, s.Version }).IsUnique();
        // Canlı sitenin tek sorgusu: platformun aktif snapshot'ı
        builder.HasIndex(s => new { s.FirmPlatformId, s.IsActive });
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public class PublishLogConfiguration : IEntityTypeConfiguration<PublishLog>
{
    public void Configure(EntityTypeBuilder<PublishLog> builder)
    {
        builder.ToTable("publish_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Status).HasMaxLength(20).IsRequired();
        builder.Property(l => l.ErrorMessage).HasMaxLength(2000);
        builder.Property(l => l.Note).HasMaxLength(500);
        // Yayın Geçmişi ekranı yeniden eskiye listeler
        builder.HasIndex(l => new { l.FirmPlatformId, l.PublishedAt });
        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}

public class CardMessageConfiguration : IEntityTypeConfiguration<CardMessage>
{
    public void Configure(EntityTypeBuilder<CardMessage> builder)
    {
        builder.ToTable("card_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.MessageI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.ScopeCategoryIds).HasColumnType("jsonb");
        builder.Property(m => m.ScopeProductCodes).HasColumnType("jsonb");
        builder.Property(m => m.Icon).HasMaxLength(60);
        builder.Property(m => m.Color).HasMaxLength(20);
        builder.Property(m => m.ScopeType).HasMaxLength(20).IsRequired();
        // Site çözümlemesinin tek sorgusu: kanalın aktif mesajları
        builder.HasIndex(m => new { m.FirmPlatformId, m.IsActive });
        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
