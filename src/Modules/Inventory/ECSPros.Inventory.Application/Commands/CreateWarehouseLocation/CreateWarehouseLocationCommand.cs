using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.CreateWarehouseLocation;

public record CreateWarehouseLocationCommand(
    Guid WarehouseId,
    string Code,
    string Barcode,
    string? Name,
    Guid? ParentId,
    string LocationType,
    int ReservePriority,
    int PickingOrder,
    int SortOrder
) : IRequest<Result<Guid>>;

public class CreateWarehouseLocationCommandHandler : IRequestHandler<CreateWarehouseLocationCommand, Result<Guid>>
{
    private readonly IInventoryDbContext _db;

    public CreateWarehouseLocationCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateWarehouseLocationCommand request, CancellationToken ct)
    {
        var warehouseExists = await _db.Warehouses.AnyAsync(w => w.Id == request.WarehouseId, ct);
        if (!warehouseExists)
            return Result.Failure<Guid>("Depo bulunamadı.");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.WarehouseLocations.AnyAsync(l => l.Id == request.ParentId.Value, ct);
            if (!parentExists)
                return Result.Failure<Guid>("Üst lokasyon bulunamadı.");
        }

        var codeExists = await _db.WarehouseLocations
            .AnyAsync(l => l.WarehouseId == request.WarehouseId && l.Code == request.Code, ct);
        if (codeExists)
            return Result.Failure<Guid>("Bu depoda aynı koda sahip lokasyon zaten mevcut.");

        var location = new WarehouseLocation
        {
            Id = Guid.NewGuid(),
            WarehouseId = request.WarehouseId,
            Code = request.Code,
            Barcode = request.Barcode,
            Name = request.Name,
            ParentId = request.ParentId,
            LocationType = request.LocationType,
            ReservePriority = request.ReservePriority,
            PickingOrder = request.PickingOrder,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.WarehouseLocations.Add(location);
        await _db.SaveChangesAsync(ct);

        return Result.Success(location.Id);
    }
}
