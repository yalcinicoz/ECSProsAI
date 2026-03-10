using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.CreatePackingStation;

public class CreatePackingStationCommandHandler : IRequestHandler<CreatePackingStationCommand, Result<Guid>>
{
    private readonly IFulfillmentDbContext _context;

    public CreatePackingStationCommandHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreatePackingStationCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.PackingStations
            .AnyAsync(s => s.StationCode == request.StationCode, cancellationToken);

        if (exists)
            return Result.Failure<Guid>($"'{request.StationCode}' istasyon kodu zaten mevcut.");

        var station = new PackingStation
        {
            WarehouseId = request.WarehouseId,
            StationCode = request.StationCode,
            Barcode = request.Barcode,
            StationName = request.StationName,
            SlotCount = request.SlotCount,
            IsObm = request.IsObm,
            Status = "active"
        };

        _context.PackingStations.Add(station);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(station.Id);
    }
}
