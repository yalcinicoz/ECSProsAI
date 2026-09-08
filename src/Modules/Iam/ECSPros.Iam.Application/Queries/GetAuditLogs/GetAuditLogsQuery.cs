using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Queries.GetAuditLogs;

public record GetAuditLogsQuery(
    Guid? UserId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    GridRequest? Grid = null
) : IRequest<Result<PagedResult<AuditLogDto>>>;
// Search + Grid (2026-09-08, DataGrid): global arama + beyaz listeli f.* filtreleri + sort/dir (AuditLogGrid); null → eski davranış

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string EntityType,
    Guid EntityId,
    string Action,
    string? IpAddress,
    DateTime CreatedAt,
    string? UserName = null
);

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<PagedResult<AuditLogDto>>>
{
    private readonly IIamDbContext _db;

    public GetAuditLogsQueryHandler(IIamDbContext db) => _db = db;

    public async Task<Result<PagedResult<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        // DataGrid (2026-09-08): adlandırılmış filtreler + global arama + grid filtreleri TEK yerden (AuditLogGrid).
        var query = AuditLogGrid.ApplyAll(_db.AuditLogs.AsQueryable(), new AuditLogListFilters(
            request.UserId, request.EntityType, request.EntityId, request.Action, request.From, request.To, request.Search), request.Grid);

        var total = await query.CountAsync(ct);
        var items = await AuditLogGrid.Schema.ApplySort(query, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new AuditLogDto(l.Id, l.UserId, l.EntityType, l.EntityId, l.Action, l.IpAddress, l.CreatedAt,
                _db.Users.Where(u => u.Id == l.UserId).Select(u => u.Username).FirstOrDefault()))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AuditLogDto>(items, total, request.Page, request.PageSize));
    }
}
