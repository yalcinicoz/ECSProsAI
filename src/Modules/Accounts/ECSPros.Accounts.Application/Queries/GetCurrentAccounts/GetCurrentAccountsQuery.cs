using ECSPros.Accounts.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace ECSPros.Accounts.Application.Queries.GetCurrentAccounts;

public record GetCurrentAccountsQuery(
    string? AccountType, Guid? GroupId, bool? IsActive, string? Search,
    int Page = 1, int PageSize = 30, string? OwnerType = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<CurrentAccountDto>>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (CurrentAccountGrid.Schema)

public record CurrentAccountDto(
    Guid Id, string Code, string Title, string AccountType, string SupplierKind,
    Guid? GroupId, string? GroupName,
    string? TaxNumber, string? ContactName, string? Phone, string? Email,
    string? City, string? Country, decimal CreditLimit, string Currency,
    bool IsActive, DateTime CreatedAt, string OwnerType, Guid? OwnerId);

public class GetCurrentAccountsQueryHandler : IRequestHandler<GetCurrentAccountsQuery, Result<PagedResult<CurrentAccountDto>>>
{
    private readonly IAccountsDbContext _db;
    public GetCurrentAccountsQueryHandler(IAccountsDbContext db) => _db = db;
    public async Task<Result<PagedResult<CurrentAccountDto>>> Handle(GetCurrentAccountsQuery request, CancellationToken ct)
    {
        // Adlandırılmış + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var query = CurrentAccountGrid.ApplyAll(
            _db.CurrentAccounts.Include(a => a.Group),
            new CurrentAccountFilters(request.AccountType, request.GroupId, request.IsActive, request.Search, request.OwnerType),
            request.Grid);
        var total = await query.CountAsync(ct);
        var items = await CurrentAccountGrid.Schema.ApplySort(query, request.Grid)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(a => new CurrentAccountDto(a.Id, a.Code, a.Title, a.AccountType, a.SupplierKind,
                a.GroupId, a.Group != null ? a.Group.Name : null,
                a.TaxNumber, a.ContactName, a.Phone, a.Email,
                a.City, a.Country, a.CreditLimit, a.Currency, a.IsActive, a.CreatedAt, a.OwnerType, a.OwnerId))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<CurrentAccountDto>(items, total, request.Page, request.PageSize));
    }
}
