using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.UpdateBinStatus;

public class UpdateBinStatusCommandHandler : IRequestHandler<UpdateBinStatusCommand, Result<bool>>
{
    private readonly IFulfillmentDbContext _context;

    public UpdateBinStatusCommandHandler(IFulfillmentDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateBinStatusCommand request, CancellationToken cancellationToken)
    {
        var validStatuses = new[] { "empty", "filling", "ready" };
        if (!validStatuses.Contains(request.Status))
            return Result.Failure<bool>($"Geçersiz bin durumu. Geçerli değerler: {string.Join(", ", validStatuses)}");

        var bin = await _context.SortingBins
            .FirstOrDefaultAsync(b => b.Id == request.BinId, cancellationToken);

        if (bin is null)
            return Result.Failure<bool>("Sorting bin bulunamadı.");

        bin.Status = request.Status;
        bin.UpdatedAt = DateTime.UtcNow;
        bin.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
