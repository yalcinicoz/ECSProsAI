using ECSPros.Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Cms.Infrastructure.Persistence.Configurations;

public class SiteMenuConfiguration : IEntityTypeConfiguration<SiteMenu>
{
    public void Configure(EntityTypeBuilder<SiteMenu> builder)
    {
        builder.ToTable("cms_site_menus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.MenuType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DisplayStyle).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.FirmPlatformId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Menu).HasForeignKey(x => x.MenuId);
    }
}

public class SiteMenuItemConfiguration : IEntityTypeConfiguration<SiteMenuItem>
{
    public void Configure(EntityTypeBuilder<SiteMenuItem> builder)
    {
        builder.ToTable("cms_site_menu_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ItemType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(30);
        builder.Property(x => x.CustomUrl).HasMaxLength(500);
        builder.Property(x => x.Icon).HasMaxLength(100);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).IsRequired(false);
        builder.HasOne(x => x.MegaPanel).WithOne(x => x.MenuItem).HasForeignKey<MenuMegaPanel>(x => x.MenuItemId);
    }
}

public class MenuMegaPanelConfiguration : IEntityTypeConfiguration<MenuMegaPanel>
{
    public void Configure(EntityTypeBuilder<MenuMegaPanel> builder)
    {
        builder.ToTable("cms_menu_mega_panels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.LayoutType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.BackgroundColor).HasMaxLength(20);
        builder.Property(x => x.BackgroundImageUrl).HasMaxLength(500);
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Groups).WithOne(x => x.MegaPanel).HasForeignKey(x => x.MegaPanelId);
    }
}

public class MenuPanelGroupConfiguration : IEntityTypeConfiguration<MenuPanelGroup>
{
    public void Configure(EntityTypeBuilder<MenuPanelGroup> builder)
    {
        builder.ToTable("cms_menu_panel_groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameI18n).HasColumnType("jsonb");
        builder.Property(x => x.TitleStyle).HasColumnType("jsonb");
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.PanelGroup).HasForeignKey(x => x.PanelGroupId);
    }
}

public class MenuPanelItemConfiguration : IEntityTypeConfiguration<MenuPanelItem>
{
    public void Configure(EntityTypeBuilder<MenuPanelItem> builder)
    {
        builder.ToTable("cms_menu_panel_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.ItemType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.TargetType).HasMaxLength(30);
        builder.Property(x => x.CustomUrl).HasMaxLength(500);
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.ImagePosition).HasMaxLength(20);
        builder.Property(x => x.BadgeText).HasMaxLength(50);
        builder.Property(x => x.BadgeColor).HasMaxLength(20);
        builder.Property(x => x.GenderFilter).HasMaxLength(10);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class PageTemplateConfiguration : IEntityTypeConfiguration<PageTemplate>
{
    public void Configure(EntityTypeBuilder<PageTemplate> builder)
    {
        builder.ToTable("cms_page_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.TemplateType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DefaultLayout).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Pages).WithOne(x => x.Template).HasForeignKey(x => x.TemplateId);
    }
}

public class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("cms_pages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SlugI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PageType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.TargetGender).HasMaxLength(10);
        builder.Property(x => x.MetaTitleI18n).HasColumnType("jsonb");
        builder.Property(x => x.MetaDescriptionI18n).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.FirmPlatformId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Sections).WithOne(x => x.Page).HasForeignKey(x => x.PageId);
    }
}

public class SectionTypeConfiguration : IEntityTypeConfiguration<SectionType>
{
    public void Configure(EntityTypeBuilder<SectionType> builder)
    {
        builder.ToTable("cms_section_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.SettingsSchema).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Sections).WithOne(x => x.SectionType).HasForeignKey(x => x.SectionTypeId);
    }
}

public class PageSectionConfiguration : IEntityTypeConfiguration<PageSection>
{
    public void Configure(EntityTypeBuilder<PageSection> builder)
    {
        builder.ToTable("cms_page_sections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.TitleI18n).HasColumnType("jsonb");
        builder.Property(x => x.SubtitleI18n).HasColumnType("jsonb");
        builder.Property(x => x.Settings).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.LayoutSettings).HasColumnType("jsonb");
        builder.Property(x => x.BackgroundColor).HasMaxLength(20);
        builder.Property(x => x.BackgroundImageUrl).HasMaxLength(500);
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.Section).HasForeignKey(x => x.SectionId);
    }
}

public class PageSectionItemConfiguration : IEntityTypeConfiguration<PageSectionItem>
{
    public void Configure(EntityTypeBuilder<PageSectionItem> builder)
    {
        builder.ToTable("cms_page_section_items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ItemType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.TitleI18n).HasColumnType("jsonb");
        builder.Property(x => x.SubtitleI18n).HasColumnType("jsonb");
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.ImageAltI18n).HasColumnType("jsonb");
        builder.Property(x => x.MobileImageUrl).HasMaxLength(500);
        builder.Property(x => x.VideoUrl).HasMaxLength(500);
        builder.Property(x => x.LinkType).HasMaxLength(20);
        builder.Property(x => x.LinkUrl).HasMaxLength(500);
        builder.Property(x => x.ButtonTextI18n).HasColumnType("jsonb");
        builder.Property(x => x.ButtonStyle).HasMaxLength(20);
        builder.Property(x => x.CustomHtmlI18n).HasColumnType("jsonb");
        builder.Property(x => x.BadgeTextI18n).HasColumnType("jsonb");
        builder.Property(x => x.BadgeColor).HasMaxLength(20);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProductListConfiguration : IEntityTypeConfiguration<ProductList>
{
    public void Configure(EntityTypeBuilder<ProductList> builder)
    {
        builder.ToTable("cms_product_lists");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ListType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FilterRules).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.FirmPlatformId, x.Code }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasMany(x => x.Items).WithOne(x => x.ProductList).HasForeignKey(x => x.ProductListId);
    }
}

public class ProductListItemConfiguration : IEntityTypeConfiguration<ProductListItem>
{
    public void Configure(EntityTypeBuilder<ProductListItem> builder)
    {
        builder.ToTable("cms_product_list_items");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
