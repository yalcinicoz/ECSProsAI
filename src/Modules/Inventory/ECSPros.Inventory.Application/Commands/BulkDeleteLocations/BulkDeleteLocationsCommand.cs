using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Inventory.Application.Commands.BulkDeleteLocations;

public record BulkDeleteLocationsCommand(
    Guid WarehouseId,
    string StartCode,
    string EndCode
) : IRequest<Result<BulkDeleteLocationsResult>>;

public record BulkDeleteLocationsResult(
    int DeletedCount,
    bool Blocked,
    List<BlockedLocationInfo> BlockedLocations);

public record BlockedLocationInfo(string Code, int Quantity);
