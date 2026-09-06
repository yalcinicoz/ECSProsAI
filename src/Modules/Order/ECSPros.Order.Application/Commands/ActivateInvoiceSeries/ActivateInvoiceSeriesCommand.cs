using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.ActivateInvoiceSeries;

public record ActivateInvoiceSeriesCommand(Guid Id, Guid UserId) : IRequest<Result<bool>>;

public class ActivateInvoiceSeriesCommandHandler(IOrderDbContext db)
    : IRequestHandler<ActivateInvoiceSeriesCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ActivateInvoiceSeriesCommand request, CancellationToken ct)
    {
        var series = await db.InvoiceSeries.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (series is null) return Result.Failure<bool>("Fatura serisi bulunamadı.");
        if (series.IsActive) return Result.Failure<bool>("Seri zaten aktif.");
        series.IsActive = true;
        series.RetiredAt = null;
        series.UpdatedAt = DateTime.UtcNow;
        series.UpdatedBy = request.UserId;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
