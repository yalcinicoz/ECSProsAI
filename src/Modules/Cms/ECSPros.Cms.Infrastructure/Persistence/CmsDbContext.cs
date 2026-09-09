using ECSPros.Cms.Application.Services;
using ECSPros.Cms.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Infrastructure.Persistence;

public class CmsDbContext : DbContext, ICmsDbContext
{
    public CmsDbContext(DbContextOptions<CmsDbContext> options) : base(options) { }

    public DbSet<PageTemplate> PageTemplates => Set<PageTemplate>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<SectionType> SectionTypes => Set<SectionType>();
    public DbSet<PageSection> PageSections => Set<PageSection>();
    public DbSet<PageSectionItem> PageSectionItems => Set<PageSectionItem>();
    public DbSet<ProductList> ProductLists => Set<ProductList>();
    public DbSet<ProductListItem> ProductListItems => Set<ProductListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // DataGrid: jsonb sözlük alanlarında filtre/sıralama (GridJson.Text → jsonb_extract_path_text,
        // yerleşik PG fonksiyonu). Şema değiştirmez, migration gerektirmez; kaydı olmayan DbContext'te
        // i18n ad üzerinden filtre/sıralama SQL'e çevrilemez (2026-09-09'da Core'da bu yaşandı).
        modelBuilder.HasDbFunction(ECSPros.Shared.Kernel.Grid.GridJson.TextMethod).HasName("jsonb_extract_path_text").IsBuiltIn();
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
