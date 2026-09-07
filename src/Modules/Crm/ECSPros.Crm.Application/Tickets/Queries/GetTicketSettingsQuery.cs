using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Tickets.Queries;

public record GetTicketSettingsQuery(bool IncludeInactive = false) : IRequest<Result<TicketSettingsDto>>;

public class GetTicketSettingsQueryHandler(ICrmDbContext db) : IRequestHandler<GetTicketSettingsQuery, Result<TicketSettingsDto>>
{
    public async Task<Result<TicketSettingsDto>> Handle(GetTicketSettingsQuery r, CancellationToken ct)
    {
        var statuses = await db.TicketStatuses.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new TicketStatusDto(s.Id, s.Code, s.Name, s.Color, s.SortOrder, s.IsHidden, s.IsResolved, s.ExemptFromDuplicateCheck, s.IsDefault)).ToListAsync(ct);
        var sq = db.TicketSubjects.AsNoTracking().AsQueryable();
        if (!r.IncludeInactive) sq = sq.Where(s => s.IsActive);
        var subjects = await sq.OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new TicketSubjectDto(s.Id, s.Name, s.Type, s.SortOrder, s.IsActive, s.RequiredFields)).ToListAsync(ct);
        return Result.Success(new TicketSettingsDto(statuses, subjects));
    }
}
