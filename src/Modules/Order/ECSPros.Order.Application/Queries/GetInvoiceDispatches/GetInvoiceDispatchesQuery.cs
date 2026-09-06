using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoiceDispatches;

/// <summary>Gönderim kuyruğu (panel: Faturalar → Gönderim Kuyruğu; fatura detayı zaman çizelgesi).</summary>
public record GetInvoiceDispatchesQuery(string? Status = null, Guid? InvoiceId = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<InvoiceDispatchDto>>>;

public record InvoiceDispatchDto(
    Guid Id, Guid InvoiceId, string InvoiceNumber, string InvoiceType, Guid OrderId,
    string Action, string Status, string? ProviderCode, int Attempt, int MaxAttempts,
    DateTime? NextAttemptAt, DateTime? LastAttemptAt, DateTime? CompletedAt, string? LastError,
    Dictionary<string, object>? ResponseSnapshot, DateTime CreatedAt);

public class GetInvoiceDispatchesQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetInvoiceDispatchesQuery, Result<PagedResult<InvoiceDispatchDto>>>
{
    public async Task<Result<PagedResult<InvoiceDispatchDto>>> Handle(GetInvoiceDispatchesQuery request, CancellationToken ct)
    {
        var q = db.InvoiceDispatches.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(d => d.Status == request.Status);
        if (request.InvoiceId is not null) q = q.Where(d => d.InvoiceId == request.InvoiceId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(d => d.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(d => new InvoiceDispatchDto(
                d.Id, d.InvoiceId, d.Invoice.InvoiceNumber, d.Invoice.InvoiceType, d.Invoice.OrderId,
                d.Action, d.Status, d.ProviderCode, d.Attempt, d.MaxAttempts,
                d.NextAttemptAt, d.LastAttemptAt, d.CompletedAt, d.LastError, d.ResponseSnapshot, d.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<InvoiceDispatchDto>(items, total, request.Page, request.PageSize));
    }
}
