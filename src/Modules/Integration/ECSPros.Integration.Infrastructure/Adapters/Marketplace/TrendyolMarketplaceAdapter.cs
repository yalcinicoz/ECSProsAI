using ECSPros.Integration.Application.Adapters;
using Microsoft.Extensions.Logging;

namespace ECSPros.Integration.Infrastructure.Adapters.Marketplace;

/// <summary>
/// Trendyol pazaryeri adaptörü.
/// Production'da gerçek Trendyol Seller API endpoint'lerine bağlanır.
/// Bu implementasyon HTTP altyapısını kurar; stub response döner.
/// </summary>
public class TrendyolMarketplaceAdapter(
    IHttpClientFactory httpClientFactory,
    ILogger<TrendyolMarketplaceAdapter> logger) : IMarketplaceAdapter
{
    public string ServiceCode => "trendyol";

    public async Task<MarketplaceSyncResult> SyncProductAsync(
        Guid firmIntegrationId, MarketplaceProductPayload payload, CancellationToken ct = default)
    {
        logger.LogInformation("Trendyol ürün senkronizasyonu: FirmIntegrationId={FirmIntegrationId}, VariantId={VariantId}",
            firmIntegrationId, payload.VariantId);

        // TODO: Gerçek Trendyol API entegrasyonu — credentials'ı FirmIntegration.Credentials'dan çek
        // var client = httpClientFactory.CreateClient("Trendyol");
        // var response = await client.PostAsJsonAsync("/sapigw/suppliers/{supplierId}/v2/products", trendyolPayload, ct);

        await Task.Delay(100, ct); // simulate HTTP call
        var externalId = $"TY-{payload.VariantId:N}";
        return new MarketplaceSyncResult(true, externalId, null);
    }

    public async Task<MarketplaceSyncResult> UpdateStockAsync(
        Guid firmIntegrationId, string externalId, int quantity, CancellationToken ct = default)
    {
        logger.LogInformation("Trendyol stok güncelleme: ExternalId={ExternalId}, Quantity={Quantity}",
            externalId, quantity);

        await Task.Delay(50, ct);
        return new MarketplaceSyncResult(true, externalId, null);
    }

    public async Task<IReadOnlyList<MarketplaceOrderDto>> FetchOrdersAsync(
        Guid firmIntegrationId, DateTime? since, CancellationToken ct = default)
    {
        logger.LogInformation("Trendyol sipariş çekme: FirmIntegrationId={FirmIntegrationId}, Since={Since}",
            firmIntegrationId, since);

        await Task.Delay(200, ct);
        return [];
    }
}
