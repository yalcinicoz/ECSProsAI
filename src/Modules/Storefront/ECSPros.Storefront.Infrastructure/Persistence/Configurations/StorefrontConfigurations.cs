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
        builder.HasIndex(cp => new { cp.FirmPlatformId, cp.ProductId }).IsUnique();
        builder.HasQueryFilter(cp => !cp.IsDeleted);
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
