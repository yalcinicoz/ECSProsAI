using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Fulfillment.Application.Queries.GetPackingStations;

public record GetPackingStationsQuery(
    Guid? WarehouseId,
    bool ActiveOnly = true) : IRequest<Result<List<PackingStationDto>>>;

public record PackingStationDto(
    Guid Id,
    Guid WarehouseId,
    string StationCode,
    string? StationName,
    int SlotCount,
    bool IsObm,
    string Status);
