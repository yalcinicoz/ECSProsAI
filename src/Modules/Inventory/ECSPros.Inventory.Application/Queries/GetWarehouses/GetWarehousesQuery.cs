using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Inventory.Application.Queries.GetWarehouses;

public record GetWarehousesQuery(bool ActiveOnly = true) : IRequest<Result<List<WarehouseDto>>>;

public record WarehouseDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string WarehouseType,
    string? Address,
    bool IsSellableOnline,
    bool IsActive,
    int SortOrder);
