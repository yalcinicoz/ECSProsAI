using ECSPros.Inventory.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.UpdateWarehouse;

public class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, Result<bool>>
{
    private readonly IInventoryDbContext _context;

    public UpdateWarehouseCommandHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken);

        if (warehouse is null)
            return Result.Failure<bool>("Depo bulunamadı.");

        warehouse.NameI18n = request.NameI18n;
        warehouse.WarehouseType = request.WarehouseType;
        warehouse.Address = request.Address;
        warehouse.IsSellableOnline = request.IsSellableOnline;
        warehouse.ReservePriority = request.ReservePriority;
        warehouse.IsActive = request.IsActive;
        warehouse.SortOrder = request.SortOrder;
        warehouse.UpdatedAt = DateTime.UtcNow;
        warehouse.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
