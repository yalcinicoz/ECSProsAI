using ECSPros.Integration.Application.Adapters;
using Microsoft.Extensions.Logging;

namespace ECSPros.Integration.Infrastructure.Adapters.Marketplace;

/// <summary>
/// Amazon pazaryeri adaptörü — SP-API gerçek HTTP çağrıları (W4b).
///   ürün       → Listings Items PUT (SKU bazında oluştur/güncelle)
///   stok/fiyat → Listings Items PATCH (fulfillment_availability; fiyat SyncProduct'ın Price alanında gelir)
///   sipariş    → Orders API GET /orders/v0/orders
/// Kimlikler Core'daki FirmPlatformIntegration'dan IMarketplaceCredentialResolver ile çözülür.
/// </summary>
public class AmazonMarketplaceAdapter(
    IMarketplaceCredentialResolver resolver,
    AmazonSpApiClient client,
    ILogger<AmazonMarketplaceAdapter> logger) : IMarketplaceAdapter
{
    public string ServiceCode => "amazon";

    public async Task<MarketplaceSyncResult> SyncProductAsync(
        Guid firmIntegrationId, MarketplaceProductPayload payload, CancellationToken ct = default)
    {
        try
        {
            var (cfg, error) = await client.ResolveConfigAsync(resolver, firmIntegrationId, ct);
            if (cfg is null) return new MarketplaceSyncResult(false, null, error);

            var sku = await client.PutListingAsync(cfg, payload, ct);
            return new MarketplaceSyncResult(true, sku, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Amazon ürün senkronizasyonu başarısız: FirmIntegrationId={FirmIntegrationId}, VariantId={VariantId}",
                firmIntegrationId, payload.VariantId);
            return new MarketplaceSyncResult(false, null, ex.Message);
        }
    }

    public async Task<MarketplaceSyncResult> UpdateStockAsync(
        Guid firmIntegrationId, string externalId, int quantity, CancellationToken ct = default)
    {
        try
        {
            var (cfg, error) = await client.ResolveConfigAsync(resolver, firmIntegrationId, ct);
            if (cfg is null) return new MarketplaceSyncResult(false, externalId, error);

            await client.PatchListingAsync(cfg, externalId, price: null, quantity, ct);
            return new MarketplaceSyncResult(true, externalId, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Amazon stok güncelleme başarısız: ExternalId={ExternalId}, Quantity={Quantity}",
                externalId, quantity);
            return new MarketplaceSyncResult(false, externalId, ex.Message);
        }
    }

    public async Task<IReadOnlyList<MarketplaceOrderDto>> FetchOrdersAsync(
        Guid firmIntegrationId, DateTime? since, CancellationToken ct = default)
    {
        var (cfg, error) = await client.ResolveConfigAsync(resolver, firmIntegrationId, ct);
        if (cfg is null)
        {
            logger.LogWarning("Amazon sipariş çekilemedi: {Error} (FirmIntegrationId={FirmIntegrationId})",
                error, firmIntegrationId);
            return [];
        }

        return await client.FetchOrdersAsync(cfg, since, ct);
    }
}
