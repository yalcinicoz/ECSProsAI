using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.CreateWarehouse;

public class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Result<Guid>>
{
    private readonly IInventoryDbContext _context;

    public CreateWarehouseCommandHandler(IInventoryDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Warehouses.AnyAsync(w => w.Code == request.Code, cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"'{request.Code}' depo kodu zaten mevcut.");

        var warehouse = new Warehouse
        {
            Code = request.Code,
            NameI18n = request.NameI18n,
            WarehouseType = request.WarehouseType,
            Address = request.Address,
            IsSellableOnline = request.IsSellableOnline,
            ReservePriority = request.ReservePriority,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(warehouse.Id);
    }
}
