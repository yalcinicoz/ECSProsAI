using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Queries.GetAccountGroups;

public record GetAccountGroupsQuery(bool ActiveOnly = false) : IRequest<Result<List<AccountGroupDto>>>;

public record AccountGroupDto(
    Guid Id, string Code, string Name, string GroupType,
    string? Description, bool IsActive, int SortOrder, int AccountCount);

public class GetAccountGroupsQueryHandler : IRequestHandler<GetAccountGroupsQuery, Result<List<AccountGroupDto>>>
{
    private readonly IAccountsDbContext _db;
    public GetAccountGroupsQueryHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result<List<AccountGroupDto>>> Handle(GetAccountGroupsQuery request, CancellationToken ct)
    {
        var query = _db.AccountGroups.AsQueryable();
        if (request.ActiveOnly) query = query.Where(g => g.IsActive);
        var items = await query.OrderBy(g => g.SortOrder).ThenBy(g => g.Name)
            .Select(g => new AccountGroupDto(g.Id, g.Code, g.Name, g.GroupType, g.Description,
                g.IsActive, g.SortOrder, g.Accounts.Count(a => !a.IsDeleted)))
            .ToListAsync(ct);
        return Result.Success(items);
    }
}
