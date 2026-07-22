using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ECSPros.Integration.Application.Services;

public interface IIntegrationDbContext
{
    DbSet<IntegrationLog> IntegrationLogs { get; }
    DbSet<MarketplaceProduct> MarketplaceProducts { get; }
    DbSet<ErpVariantData> ErpVariantData { get; }
    /// <summary>Jsonb payload filtreleri gibi EF'e çevrilemeyen sorgular için (Accounts kalıbı).</summary>
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
