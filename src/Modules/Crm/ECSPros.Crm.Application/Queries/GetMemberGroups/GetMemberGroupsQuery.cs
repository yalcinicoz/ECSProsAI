using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberGroups;

public record GetMemberGroupsQuery(bool ActiveOnly = true) : IRequest<Result<List<MemberGroupDto>>>;

public record MemberGroupDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsDefault,
    bool IsWholesale,
    bool RequiresApproval,
    bool ShowPricesBeforeLogin,
    decimal? MinOrderAmount,
    int? PaymentTermsDays,
    bool IsActive,
    int SortOrder,
    int MemberCount);

public class GetMemberGroupsQueryHandler : IRequestHandler<GetMemberGroupsQuery, Result<List<MemberGroupDto>>>
{
    private readonly ICrmDbContext _db;

    public GetMemberGroupsQueryHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<List<MemberGroupDto>>> Handle(GetMemberGroupsQuery request, CancellationToken ct)
    {
        var query = _db.MemberGroups.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(g => g.IsActive);

        var items = await query
            .OrderBy(g => g.SortOrder)
            .Select(g => new MemberGroupDto(
                g.Id, g.Code, g.NameI18n,
                g.IsDefault, g.IsWholesale, g.RequiresApproval,
                g.ShowPricesBeforeLogin, g.MinOrderAmount, g.PaymentTermsDays,
                g.IsActive, g.SortOrder, g.Members.Count))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
