using ECSPros.Integration.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Integration.Application.Queries.GetIntegrationLogs;

public record GetIntegrationLogsQuery(
    Guid? FirmIntegrationId = null,
    string? ServiceType = null,
    string? OperationType = null,
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50,
    List<Guid>? FirmIntegrationIds = null,
    /// <summary>DataGrid (2026-09-08): verilirse sayfa/sıralama/filtre/arama buradan (IntegrationLogGrid.Schema); Page/PageSize yok sayılır.</summary>
    GridRequest? Grid = null) : IRequest<Result<PagedResult<IntegrationLogDto>>>;

public record IntegrationLogDto(
    Guid Id,
    Guid FirmIntegrationId,
    string ServiceType,
    string OperationType,
    string Status,
    string? ErrorMessage,
    int DurationMs,
    Guid? ReferenceId,
    string? ReferenceType,
    DateTime CreatedAt);

public class GetIntegrationLogsQueryHandler(IIntegrationDbContext db)
    : IRequestHandler<GetIntegrationLogsQuery, Result<PagedResult<IntegrationLogDto>>>
{
    public async Task<Result<PagedResult<IntegrationLogDto>>> Handle(
        GetIntegrationLogsQuery request, CancellationToken ct)
    {
        // DataGrid (2026-09-08): adlandırılmış filtreler + beyaz listeli grid filtre/sıralama (IntegrationLogGrid); Grid yoksa eski davranış.
        var q = IntegrationLogGrid.ApplyAll(db.IntegrationLogs.AsNoTracking(), new IntegrationLogListFilters(
            request.FirmIntegrationId, request.ServiceType, request.OperationType, request.Status, request.From, request.To,
            request.FirmIntegrationIds, request.Grid?.Search), request.Grid);
        var page = request.Grid?.Page ?? request.Page;
        var pageSize = request.Grid?.PageSize ?? request.PageSize;

        var total = await q.CountAsync(ct);
        var items = await IntegrationLogGrid.Schema.ApplySort(q, request.Grid)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new IntegrationLogDto(
                x.Id, x.FirmIntegrationId, x.ServiceType, x.OperationType,
                x.Status, x.ErrorMessage, x.DurationMs, x.ReferenceId, x.ReferenceType, x.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<IntegrationLogDto>(items, total, page, pageSize));
    }
}
