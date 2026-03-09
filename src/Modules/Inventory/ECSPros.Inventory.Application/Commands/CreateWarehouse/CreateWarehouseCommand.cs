using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Inventory.Application.Commands.CreateWarehouse;

public record CreateWarehouseCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string WarehouseType,
    string? Address,
    bool IsSellableOnline,
    int ReservePriority,
    int SortOrder) : IRequest<Result<Guid>>;
