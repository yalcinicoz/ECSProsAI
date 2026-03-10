using ECSPros.Integration.Application.Services;
using ECSPros.Shared.Kernel.Common;
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
    int PageSize = 50) : IRequest<Result<PagedResult<IntegrationLogDto>>>;

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
        var q = db.IntegrationLogs.AsNoTracking();

        if (request.FirmIntegrationId.HasValue)
            q = q.Where(x => x.FirmIntegrationId == request.FirmIntegrationId.Value);
        if (!string.IsNullOrWhiteSpace(request.ServiceType))
            q = q.Where(x => x.ServiceType == request.ServiceType);
        if (!string.IsNullOrWhiteSpace(request.OperationType))
            q = q.Where(x => x.OperationType == request.OperationType);
        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(x => x.Status == request.Status);
        if (request.From.HasValue)
            q = q.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            q = q.Where(x => x.CreatedAt <= request.To.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new IntegrationLogDto(
                x.Id, x.FirmIntegrationId, x.ServiceType, x.OperationType,
                x.Status, x.ErrorMessage, x.DurationMs, x.ReferenceId, x.ReferenceType, x.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<IntegrationLogDto>(items, total, request.Page, request.PageSize));
    }
}
