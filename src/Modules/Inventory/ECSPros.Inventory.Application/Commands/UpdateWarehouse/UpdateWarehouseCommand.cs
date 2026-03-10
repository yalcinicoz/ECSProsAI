using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Inventory.Application.Commands.UpdateWarehouse;

public record UpdateWarehouseCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string WarehouseType,
    string? Address,
    bool IsSellableOnline,
    int ReservePriority,
    bool IsActive,
    int SortOrder,
    Guid UpdatedBy) : IRequest<Result<bool>>;
