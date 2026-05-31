using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Queries.GetCurrentAccounts;

public record GetCurrentAccountsQuery(
    string? AccountType, Guid? GroupId, bool? IsActive, string? Search,
    int Page = 1, int PageSize = 30) : IRequest<Result<PagedResult<CurrentAccountDto>>>;

public record CurrentAccountDto(
    Guid Id, string Code, string Title, string AccountType,
    Guid? GroupId, string? GroupName,
    string? TaxNumber, string? ContactName, string? Phone, string? Email,
    string? City, string? Country, decimal CreditLimit, string Currency,
    bool IsActive, DateTime CreatedAt);

public class GetCurrentAccountsQueryHandler : IRequestHandler<GetCurrentAccountsQuery, Result<PagedResult<CurrentAccountDto>>>
{
    private readonly IAccountsDbContext _db;
    public GetCurrentAccountsQueryHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result<PagedResult<CurrentAccountDto>>> Handle(GetCurrentAccountsQuery request, CancellationToken ct)
    {
        var query = _db.CurrentAccounts.Include(a => a.Group).AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.AccountType)) query = query.Where(a => a.AccountType == request.AccountType);
        if (request.GroupId.HasValue) query = query.Where(a => a.GroupId == request.GroupId);
        if (request.IsActive.HasValue) query = query.Where(a => a.IsActive == request.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(s) || a.Code.ToLower().Contains(s)
                || (a.TaxNumber != null && a.TaxNumber.Contains(s))
                || (a.Email != null && a.Email.ToLower().Contains(s)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(a => a.Title)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(a => new CurrentAccountDto(a.Id, a.Code, a.Title, a.AccountType,
                a.GroupId, a.Group != null ? a.Group.Name : null,
                a.TaxNumber, a.ContactName, a.Phone, a.Email,
                a.City, a.Country, a.CreditLimit, a.Currency, a.IsActive, a.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<CurrentAccountDto>(items, total, request.Page, request.PageSize));
    }
}
