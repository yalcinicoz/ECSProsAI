using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.UpdatePackingStation;

public class UpdatePackingStationCommandHandler : IRequestHandler<UpdatePackingStationCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _context;

    public UpdatePackingStationCommandHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdatePackingStationCommand request, CancellationToken cancellationToken)
    {
        var station = await _context.PackingStations
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (station is null)
            return Result.Failure<bool>("Paketleme istasyonu bulunamadı.");

        station.StationName = request.StationName;
        station.SlotCount = request.SlotCount;
        station.IsObm = request.IsObm;
        station.AssignedTo = request.AssignedTo;
        station.Status = request.Status;
        station.UpdatedAt = DateTime.UtcNow;
        station.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
