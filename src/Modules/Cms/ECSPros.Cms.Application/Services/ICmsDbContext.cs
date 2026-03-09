using ECSPros.Cms.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Cms.Application.Services;

public interface ICmsDbContext
{
    DbSet<SiteMenu> SiteMenus { get; }
    DbSet<SiteMenuItem> SiteMenuItems { get; }
    DbSet<Page> Pages { get; }
    DbSet<PageTemplate> PageTemplates { get; }
    DbSet<PageSection> PageSections { get; }
    DbSet<ProductList> ProductLists { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
