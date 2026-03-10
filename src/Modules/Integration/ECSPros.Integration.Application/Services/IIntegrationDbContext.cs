using ECSPros.Integration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Integration.Application.Services;

public interface IIntegrationDbContext
{
    DbSet<IntegrationLog> IntegrationLogs { get; }
    DbSet<MarketplaceProduct> MarketplaceProducts { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
