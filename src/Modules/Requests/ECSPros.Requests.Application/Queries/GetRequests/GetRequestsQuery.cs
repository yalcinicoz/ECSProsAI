using ECSPros.Requests.Application.Services;
using ECSPros.Shared.Kernel.Common;
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
    string? Search = null) : IRequest<Result<RequestListResponse>>;

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
        var sorgu = db.ProjectRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status))
            sorgu = sorgu.Where(r => r.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Category))
            sorgu = sorgu.Where(r => r.Category == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Priority))
            sorgu = sorgu.Where(r => r.Priority == request.Priority);
        if (request.AssignedTo is { } atanan)
            sorgu = sorgu.Where(r => r.AssignedTo == atanan);
        if (request.RequestedBy is { } eden)
            sorgu = sorgu.Where(r => r.RequestedBy == eden);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var arama = request.Search.Trim().ToLowerInvariant();
            sorgu = sorgu.Where(r =>
                r.Title.ToLower().Contains(arama) ||
                r.Code.ToLower().Contains(arama) ||
                r.Description.ToLower().Contains(arama));
        }

        var toplam = await sorgu.CountAsync(ct);
        var sayfa = Math.Max(1, request.Page);
        var boyut = Math.Clamp(request.PageSize, 1, 100);

        var kayitlar = await sorgu
            .OrderByDescending(r => r.CreatedAt)
            .Skip((sayfa - 1) * boyut)
            .Take(boyut)
            .Select(r => new RequestListDto(
                r.Id, r.Code, r.Title, r.Category, r.Priority, r.Status,
                r.RequestedByName, r.AssignedToName, r.DueDate, r.CreatedAt, r.CompletedAt,
                r.Activities.Count(a => a.ActivityType == "comment")))
            .ToListAsync(ct);

        // Sekme sayaçları: durum filtresi HARİÇ diğer filtrelerle (sekmeler arası tutarlılık)
        var sayacSorgu = db.ProjectRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Category))
            sayacSorgu = sayacSorgu.Where(r => r.Category == request.Category);
        if (!string.IsNullOrWhiteSpace(request.Priority))
            sayacSorgu = sayacSorgu.Where(r => r.Priority == request.Priority);
        if (request.AssignedTo is { } a2)
            sayacSorgu = sayacSorgu.Where(r => r.AssignedTo == a2);
        if (request.RequestedBy is { } e2)
            sayacSorgu = sayacSorgu.Where(r => r.RequestedBy == e2);
        var sayaclar = await sayacSorgu
            .GroupBy(r => r.Status)
            .Select(g => new { Durum = g.Key, Adet = g.Count() })
            .ToDictionaryAsync(x => x.Durum, x => x.Adet, ct);

        return Result.Success(new RequestListResponse(
            new PagedResult<RequestListDto>(kayitlar, toplam, sayfa, boyut), sayaclar));
    }
}
