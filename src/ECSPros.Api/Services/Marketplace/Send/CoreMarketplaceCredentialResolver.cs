using ECSPros.Core.Application.Services;
using ECSPros.Integration.Application.Adapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECSPros.Api.Services.Marketplace.Send;

/// <summary>
/// Pazaryeri kimlik çözümleyicisi (singleton-safe): Core'daki FirmPlatformIntegration
/// kaydını scoped ICoreDbContext ile okur. Integration.Infrastructure'daki singleton
/// adapter'lar tarafından kullanılır; scoped DbContext'i scope factory ile çözer.
/// </summary>
public sealed class CoreMarketplaceCredentialResolver(IServiceScopeFactory scopeFactory)
    : IMarketplaceCredentialResolver
{
    public async Task<MarketplaceCredentials?> ResolveAsync(Guid firmIntegrationId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var coreDb = scope.ServiceProvider.GetRequiredService<ICoreDbContext>();

        var fi = await coreDb.FirmPlatformIntegrations.AsNoTracking()
            .Where(x => x.Id == firmIntegrationId && x.IsActive)
            .Select(x => new { x.Credentials, x.Settings })
            .FirstOrDefaultAsync(ct);

        return fi is null ? null : new MarketplaceCredentials(fi.Credentials, fi.Settings);
    }
}
