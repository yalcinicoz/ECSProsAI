using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.CreatePackingStation;

public record CreatePackingStationCommand(
    Guid WarehouseId,
    string StationCode,
    string Barcode,
    string? StationName,
    int SlotCount,
    bool IsObm) : IRequest<Result<Guid>>;
