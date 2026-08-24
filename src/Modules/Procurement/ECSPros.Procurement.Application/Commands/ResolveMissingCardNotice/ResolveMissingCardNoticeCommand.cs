using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.ResolveMissingCardNotice;

public record ResolveMissingCardNoticeCommand(Guid Id, Guid? ResolvedBy) : IRequest<Result<bool>>;

public class ResolveMissingCardNoticeCommandHandler(IProcurementDbContext db)
    : IRequestHandler<ResolveMissingCardNoticeCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResolveMissingCardNoticeCommand request, CancellationToken ct)
    {
        var n = await db.MissingCardNotices.FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (n is null) return Result.Failure<bool>("Bildirim bulunamadı.");
        if (n.Status == "resolved") return Result.Success(true);
        n.Status = "resolved";
        n.ResolvedAt = DateTime.UtcNow;
        n.ResolvedBy = request.ResolvedBy;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
