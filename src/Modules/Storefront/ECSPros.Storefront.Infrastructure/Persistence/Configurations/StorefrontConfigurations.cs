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
