using ECSPros.Cms.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Infrastructure.Persistence;

public class CmsDbContext : DbContext
{
    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options) { }

    public DbSet<SiteMenu> SiteMenus => Set<SiteMenu>();
    public DbSet<SiteMenuItem> SiteMenuItems => Set<SiteMenuItem>();
    public DbSet<MenuMegaPanel> MenuMegaPanels => Set<MenuMegaPanel>();
    public DbSet<MenuPanelGroup> MenuPanelGroups => Set<MenuPanelGroup>();
    public DbSet<MenuPanelItem> MenuPanelItems => Set<MenuPanelItem>();
    public DbSet<PageTemplate> PageTemplates => Set<PageTemplate>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<SectionType> SectionTypes => Set<SectionType>();
    public DbSet<PageSection> PageSections => Set<PageSection>();
    public DbSet<PageSectionItem> PageSectionItems => Set<PageSectionItem>();
    public DbSet<ProductList> ProductLists => Set<ProductList>();
    public DbSet<ProductListItem> ProductListItems => Set<ProductListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("cms");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CmsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }
}
