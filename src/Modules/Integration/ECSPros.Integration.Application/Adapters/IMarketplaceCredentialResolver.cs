namespace ECSPros.Integration.Application.Adapters;

/// <summary>
/// Pazaryeri sözleşmesinden (Core'daki FirmPlatformIntegration) çözülmüş kimlik + ayarlar.
/// Adapter'lar Core modülüne referans vermeden bu arayüz üzerinden erişir; somut
/// implementasyon Api projesinde ICoreDbContext ile sağlanır. Singleton adapter'lar
/// tarafından tüketildiği için implementasyon scope-factory kullanarak scoped DbContext'i
/// güvenle çözer (captive dependency oluşmaz).
/// </summary>
public sealed record MarketplaceCredentials(
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings);

public interface IMarketplaceCredentialResolver
{
    Task<MarketplaceCredentials?> ResolveAsync(Guid firmIntegrationId, CancellationToken ct);
}
