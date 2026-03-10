using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.ScanItem;

public record ScanItemCommand(Guid PickingPlanId, string Barcode, Guid ScannedBy) : IRequest<Result<ScanItemResult>>;

public record ScanItemResult(Guid BinId, int BinNumber, string? OrderCode, string Message);

public class ScanItemCommandHandler : IRequestHandler<ScanItemCommand, Result<ScanItemResult>>
{
    private readonly IFulfillmentDbContext _db;

    public ScanItemCommandHandler(IFulfillmentDbContext db) => _db = db;

    public async Task<Result<ScanItemResult>> Handle(ScanItemCommand request, CancellationToken ct)
    {
        var plan = await _db.PickingPlans
            .Include(p => p.Bins)
            .FirstOrDefaultAsync(p => p.Id == request.PickingPlanId, ct);

        if (plan is null)
            return Result.Failure<ScanItemResult>("Picking planı bulunamadı.");

        if (plan.Status != "picking")
            return Result.Failure<ScanItemResult>($"Plan '{plan.Status}' durumunda — tarama yapılamaz.");

        // Find a filling or empty bin to assign — simple round-robin logic
        var bin = plan.Bins.FirstOrDefault(b => b.Status == "filling")
                  ?? plan.Bins.FirstOrDefault(b => b.Status == "empty");

        if (bin is null)
            return Result.Failure<ScanItemResult>("Uygun kutu bulunamadı. Tüm kutular dolu.");

        if (bin.Status == "empty")
        {
            bin.Status = "filling";
            bin.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(new ScanItemResult(
            bin.Id, bin.BinNumber, null,
            $"Ürün {request.Barcode} kutu #{bin.BinNumber}'e atandı."));
    }
}
