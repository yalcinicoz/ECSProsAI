using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.ScanToBin;

public record ScanToBinCommand(Guid BinId, string Barcode, Guid ScannedBy) : IRequest<Result<bool>>;

public class ScanToBinCommandHandler : IRequestHandler<ScanToBinCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _db;

    public ScanToBinCommandHandler(IFulfillmentDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(ScanToBinCommand request, CancellationToken ct)
    {
        var bin = await _db.SortingBins.FirstOrDefaultAsync(b => b.Id == request.BinId, ct);
        if (bin is null)
            return Result.Failure<bool>("Kutu bulunamadı.");

        if (bin.Status == "ready")
            return Result.Failure<bool>("Bu kutu zaten hazır durumda.");

        bin.Status = "filling";
        bin.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
