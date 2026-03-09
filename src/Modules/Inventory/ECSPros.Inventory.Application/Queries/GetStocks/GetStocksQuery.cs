using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Inventory.Application.Queries.GetStocks;

public record GetStocksQuery(
    Guid? WarehouseId = null,
    Guid? VariantId = null,
    bool AvailableOnly = false) : IRequest<Result<List<StockDto>>>;

public record StockDto(
    Guid Id,
    Guid VariantId,
    Guid WarehouseId,
    string StockType,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity);
