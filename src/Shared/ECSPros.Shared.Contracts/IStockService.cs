namespace ECSPros.Shared.Contracts;

public interface IStockService
{
    Task<int> GetAvailableStockAsync(Guid variantId, Guid? warehouseId = null, CancellationToken ct = default);
    Task<bool> HasSufficientStockAsync(Guid variantId, int quantity, Guid? warehouseId = null, CancellationToken ct = default);
}
