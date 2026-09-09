using ECSPros.Requests.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Queries.GetRequests;

public record GetRequestsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? Category = null,
    string? Priority = null,
    Guid? AssignedTo = null,
    Guid? RequestedBy = null,
    string? Search = null,
    GridRequest? Grid = null) : IRequest<Result<RequestListResponse>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (RequestGrid.Schema); null → eski davranış

public record RequestListResponse(
    PagedResult<RequestListDto> Requests,
    Dictionary<string, int> StatusCounts);

public record RequestListDto(
    Guid Id,
    string Code,
    string Title,
    string Category,
    string Priority,
    string Status,
    string RequestedByName,
    string? AssignedToName,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int CommentCount);

public class GetRequestsQueryHandler(IRequestsDbContext db)
    : IRequestHandler<GetRequestsQuery, Result<RequestListResponse>>
{
    public async Task<Result<RequestListResponse>> Handle(GetRequestsQuery request, CancellationToken ct)
    {
        // Adlandırılmış + grid filtreleri TEK yerden (liste, sekme sayaçları ve Excel aynı modeli kullanır).
        var filtreler = new RequestListFilters(
            request.Status, request.Category, request.Priority, request.AssignedTo, request.RequestedBy, request.Search);
        var sorgu = RequestGrid.ApplyAll(db.ProjectRequests.AsNoTracking(), filtreler, request.Grid);

        var toplam = await sorgu.CountAsync(ct);
        var sayfa = Math.Max(1, request.Page);
        var boyut = Math.Clamp(request.PageSize, 1, 100);

        var kayitlar = await RequestGrid.Schema.ApplySort(sorgu, request.Grid)
            .Skip((sayfa - 1) * boyut)
            .Take(boyut)
            .Select(r => new RequestListDto(
                r.Id, r.Code, r.Title, r.Category, r.Priority, r.Status,
                r.RequestedByName, r.AssignedToName, r.DueDate, r.CreatedAt, r.CompletedAt,
                r.Activities.Count(a => a.ActivityType == "comment")))
            .ToListAsync(ct);

        // Sekme sayaçları: durum filtresi HARİÇ diğer filtrelerle (sekmeler arası tutarlılık)
        var sayacSorgu = RequestGrid.ApplyAll(
            db.ProjectRequests.AsNoTracking(), filtreler, request.Grid, includeStatus: false);
        var sayaclar = await sayacSorgu
            .GroupBy(r => r.Status)
            .Select(g => new { Durum = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Durum, x => x.Adet, ct);

        return Result.Success(new RequestListResponse(
            new PagedResult<RequestListDto>(kayitlar, toplam, sayfa, boyut), sayaclar));
    }
}
