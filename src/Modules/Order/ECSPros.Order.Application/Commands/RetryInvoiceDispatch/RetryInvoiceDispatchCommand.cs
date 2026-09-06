using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.RetryInvoiceDispatch;

/// <summary>Ölü/engelli gönderim işini yeniden kuyruğa alır (deneme sayacı sıfırlanır).</summary>
public record RetryInvoiceDispatchCommand(Guid DispatchId, Guid UserId) : IRequest<Result<bool>>;

public class RetryInvoiceDispatchCommandHandler(IOrderDbContext db) : IRequestHandler<RetryInvoiceDispatchCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RetryInvoiceDispatchCommand request, CancellationToken ct)
    {
        var d = await db.InvoiceDispatches.Include(x => x.Invoice).FirstOrDefaultAsync(x => x.Id == request.DispatchId, ct);
        if (d is null) return Result.Failure<bool>("Gönderim kaydı bulunamadı.");
        if (d.Status == InvoiceDispatchStatuses.Done) return Result.Failure<bool>("Tamamlanmış gönderim tekrar denenmez; gerekiyorsa faturayı yeniden gönderin.");
        d.Status = InvoiceDispatchStatuses.Pending;
        d.Attempt = 0;
        d.NextAttemptAt = DateTime.UtcNow;
        d.LastError = null;
        d.UpdatedAt = DateTime.UtcNow;
        d.UpdatedBy = request.UserId;
        if (d.Action == InvoiceDispatchActions.Send) d.Invoice.IntegratorStatus = "queued";
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
