using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetOperationLogs;

/// <summary>OP1 (K-16): operasyon günlüğü — sipariş detayı "Operasyon Geçmişi" (OrderId ile)
/// veya görev izleme (PlanId ile). En yeni üstte, sayfalı.</summary>
public record GetOperationLogsQuery(
    Guid? OrderId = null,
    Guid? PickingPlanId = null,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<PagedResult<OperationLogDto>>>;

public record OperationLogDto(
    Guid Id,
    Guid? OrderId,
    Guid? OrderItemId,
    Guid? PickingPlanId,
    Guid? PackageId,
    string Action,
    Guid ActorId,
    DateTime At,
    Dictionary<string, object>? Detail);

public class GetOperationLogsQueryHandler(IFulfillmentDbContext db)
    : IRequestHandler<GetOperationLogsQuery, Result<PagedResult<OperationLogDto>>>
{
    public async Task<Result<PagedResult<OperationLogDto>>> Handle(GetOperationLogsQuery request, CancellationToken ct)
    {
        if (request.OrderId is null && request.PickingPlanId is null)
            return Result.Failure<PagedResult<OperationLogDto>>("OrderId veya PickingPlanId verilmeli.");

        var sorgu = db.OperationLogs.AsNoTracking();
        if (request.OrderId is { } oid) sorgu = sorgu.Where(l => l.OrderId == oid);
        if (request.PickingPlanId is { } pid) sorgu = sorgu.Where(l => l.PickingPlanId == pid);

        var toplam = await sorgu.CountAsync(ct);
        var liste = await sorgu
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new OperationLogDto(l.Id, l.OrderId, l.OrderItemId, l.PickingPlanId,
                l.PackageId, l.Action, l.ActorId, l.CreatedAt, l.Detail))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<OperationLogDto>(liste, toplam, request.Page, request.PageSize));
    }
}
