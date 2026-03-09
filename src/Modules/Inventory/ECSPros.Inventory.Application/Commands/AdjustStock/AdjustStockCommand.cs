using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Inventory.Application.Commands.AdjustStock;

public record AdjustStockCommand(
    Guid VariantId,
    Guid WarehouseId,
    int QuantityDelta,
    string MovementType,
    string? Notes,
    Guid? CreatedBy) : IRequest<Result<Guid>>;
