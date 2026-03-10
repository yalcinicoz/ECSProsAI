using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.UpdateWarehouseLocation;

public record UpdateWarehouseLocationCommand(
    Guid Id,
    string? Name,
    string LocationType,
    int ReservePriority,
    int PickingOrder,
    int SortOrder,
    bool IsActive
) : IRequest<Result<bool>>;

public class UpdateWarehouseLocationCommandHandler : IRequestHandler<UpdateWarehouseLocationCommand, Result<bool>>
{
    private readonly IInventoryDbContext _db;

    public UpdateWarehouseLocationCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateWarehouseLocationCommand request, CancellationToken ct)
    {
        var location = await _db.WarehouseLocations.FirstOrDefaultAsync(l => l.Id == request.Id, ct);
        if (location is null)
            return Result.Failure<bool>("Lokasyon bulunamadı.");

        location.Name = request.Name;
        location.LocationType = request.LocationType;
        location.ReservePriority = request.ReservePriority;
        location.PickingOrder = request.PickingOrder;
        location.SortOrder = request.SortOrder;
        location.IsActive = request.IsActive;
        location.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
