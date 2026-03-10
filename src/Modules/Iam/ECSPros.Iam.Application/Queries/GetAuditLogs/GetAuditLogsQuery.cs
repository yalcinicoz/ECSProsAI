using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Common;
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
    int PageSize = 20
) : IRequest<Result<PagedResult<AuditLogDto>>>;

public record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string EntityType,
    Guid EntityId,
    string Action,
    string? IpAddress,
    DateTime CreatedAt
);

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<PagedResult<AuditLogDto>>>
{
    private readonly IIamDbContext _db;

    public GetAuditLogsQueryHandler(IIamDbContext db) => _db = db;

    public async Task<Result<PagedResult<AuditLogDto>>> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (request.UserId.HasValue)
            query = query.Where(l => l.UserId == request.UserId);
        if (!string.IsNullOrEmpty(request.EntityType))
            query = query.Where(l => l.EntityType == request.EntityType);
        if (request.EntityId.HasValue)
            query = query.Where(l => l.EntityId == request.EntityId);
        if (!string.IsNullOrEmpty(request.Action))
            query = query.Where(l => l.Action == request.Action);
        if (request.From.HasValue)
            query = query.Where(l => l.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(l => l.CreatedAt <= request.To.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new AuditLogDto(l.Id, l.UserId, l.EntityType, l.EntityId, l.Action, l.IpAddress, l.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AuditLogDto>(items, total, request.Page, request.PageSize));
    }
}
