namespace ECSPros.Integration.Application.Adapters;

public interface IMarketplaceAdapter
{
    string ServiceCode { get; }
    Task<MarketplaceSyncResult> SyncProductAsync(Guid firmIntegrationId, MarketplaceProductPayload payload, CancellationToken ct = default);
    Task<MarketplaceSyncResult> UpdateStockAsync(Guid firmIntegrationId, string externalId, int quantity, CancellationToken ct = default);
    Task<IReadOnlyList<MarketplaceOrderDto>> FetchOrdersAsync(Guid firmIntegrationId, DateTime? since, CancellationToken ct = default);
}

public record MarketplaceProductPayload(
    Guid VariantId,
    string Barcode,
    string Title,
    string Description,
    decimal Price,
    int StockQuantity,
    Dictionary<string, string>? Attributes = null);

public record MarketplaceSyncResult(bool Success, string? ExternalId, string? ErrorMessage);

public record MarketplaceOrderDto(
    string ExternalOrderId,
    string CustomerName,
    string CustomerPhone,
    string Address,
    decimal TotalAmount,
    string CurrencyCode,
    DateTime OrderDate,
    List<MarketplaceOrderLineDto> Lines);

public record MarketplaceOrderLineDto(
    string ExternalProductId,
    string Barcode,
    int Quantity,
    decimal UnitPrice);
