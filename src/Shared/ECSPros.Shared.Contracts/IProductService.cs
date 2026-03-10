namespace ECSPros.Shared.Contracts;

public interface IProductService
{
    Task<ProductInfo?> GetVariantAsync(Guid variantId, CancellationToken ct = default);
    Task<bool> VariantExistsAsync(Guid variantId, CancellationToken ct = default);
}

public record ProductInfo(
    Guid VariantId,
    string Sku,
    string ProductName,
    decimal BasePrice,
    bool IsActive);
