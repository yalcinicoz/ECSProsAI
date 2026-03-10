using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Commands.UpdatePackingStation;

public record UpdatePackingStationCommand(
    Guid Id,
    string? StationName,
    int SlotCount,
    bool IsObm,
    Guid? AssignedTo,
    string Status,
    Guid UpdatedBy) : IRequest<Result<bool>>;
