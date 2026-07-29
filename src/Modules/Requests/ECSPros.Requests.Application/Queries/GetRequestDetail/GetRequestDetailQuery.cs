using ECSPros.Requests.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Requests.Application.Queries.GetRequestDetail;

public record GetRequestDetailQuery(Guid Id) : IRequest<Result<RequestDetailDto>>;

public record RequestDetailDto(
    Guid Id,
    string Code,
    string Title,
    string Description,
    string Category,
    string Priority,
    string Status,
    Guid RequestedBy,
    string RequestedByName,
    Guid? AssignedTo,
    string? AssignedToName,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CompletedAt,
    List<RequestActivityDto> Activities);

public record RequestActivityDto(
    Guid Id,
    string ActivityType,
    string? Comment,
    string? OldValue,
    string? NewValue,
    string UserName,
    List<string> Attachments,
    DateTime CreatedAt);

public class GetRequestDetailQueryHandler(IRequestsDbContext db)
    : IRequestHandler<GetRequestDetailQuery, Result<RequestDetailDto>>
{
    public async Task<Result<RequestDetailDto>> Handle(GetRequestDetailQuery request, CancellationToken ct)
    {
        var talep = await db.ProjectRequests.AsNoTracking()
            .Where(r => r.Id == request.Id)
            .Select(r => new RequestDetailDto(
                r.Id, r.Code, r.Title, r.Description, r.Category, r.Priority, r.Status,
                r.RequestedBy, r.RequestedByName, r.AssignedTo, r.AssignedToName,
                r.DueDate, r.CreatedAt, r.UpdatedAt, r.CompletedAt,
                r.Activities
                    .OrderBy(a => a.CreatedAt)
                    .Select(a => new RequestActivityDto(
                        a.Id, a.ActivityType, a.Comment, a.OldValue, a.NewValue,
                        a.UserName, a.Attachments, a.CreatedAt))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return talep is null
            ? Result.Failure<RequestDetailDto>("Talep bulunamadı.")
            : Result.Success(talep);
    }
}
