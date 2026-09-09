using ECSPros.Accounts.Application.Services;
using ECSPros.Accounts.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Accounts.Application.Queries.GetAccountGroups;

/// <summary>
/// Cari grupları DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /accounts/groups</c> TÜM grupları döner ve cari listesinin grup
/// süzgecini besler; sayfalamak onu kırar. Liste ekranı sayfalı <c>/accounts/groups/grid</c> kullanır.
/// </summary>
public static class AccountGroupGrid
{
    public static readonly string[] GroupTypes = { "supplier", "customer", "both" };

    public static readonly GridSchema<CurrentAccountGroup> Schema = new GridSchema<CurrentAccountGroup>()
        .Text("code", g => g.Code)
        .Text("name", g => g.Name)
        .Text("description", g => g.Description)
        .Enum("groupType", g => g.GroupType, GroupTypes)
        .Bool("isActive", g => g.IsActive)
        .Bool("hasAccounts", g => g.Accounts.Any(a => !a.IsDeleted))
        .Number("sortOrder", g => g.SortOrder)
        .Number("accountCount", g => g.Accounts.Count(a => !a.IsDeleted))
        .Date("createdAt", g => g.CreatedAt)
        .Sort("code", g => g.Code)
        .Sort("name", g => g.Name)
        .Sort("description", g => g.Description)
        .Sort("groupType", g => g.GroupType)
        .Sort("isActive", g => g.IsActive)
        .Sort("sortOrder", g => g.SortOrder)
        .Sort("accountCount", g => g.Accounts.Count(a => !a.IsDeleted))
        .Sort("createdAt", g => g.CreatedAt)
        .DefaultSort(g => g.SortOrder)
        .TieBreaker(g => g.Id);

    public static IQueryable<CurrentAccountGroup> ApplyNamed(IQueryable<CurrentAccountGroup> query, AccountGroupFilters f)
    {
        if (f.ActiveOnly) query = query.Where(g => g.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(g => g.Code.ToLower().Contains(t) || g.Name.ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<CurrentAccountGroup> ApplyAll(IQueryable<CurrentAccountGroup> query, AccountGroupFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string TypeLabel(string s) => s switch
    {
        "supplier" => "Tedarikçi", "customer" => "Müşteri", "both" => "Her İkisi", _ => s,
    };
}

public record AccountGroupFilters(bool ActiveOnly = false, string? Search = null);

public record AccountGroupGridRow(
    Guid Id, string Code, string Name, string GroupType, string? Description,
    bool IsActive, int SortOrder, int AccountCount, DateTime CreatedAt);

public record GetAccountGroupsGridQuery(AccountGroupFilters Filters, int Page = 1, int PageSize = 20, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<AccountGroupGridRow>>>;

public class GetAccountGroupsGridQueryHandler(IAccountsDbContext db)
    : IRequestHandler<GetAccountGroupsGridQuery, Result<PagedResult<AccountGroupGridRow>>>
{
    public async Task<Result<PagedResult<AccountGroupGridRow>>> Handle(GetAccountGroupsGridQuery r, CancellationToken ct)
    {
        var q = AccountGroupGrid.ApplyAll(db.AccountGroups.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await AccountGroupGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(g => new AccountGroupGridRow(
                g.Id, g.Code, g.Name, g.GroupType, g.Description, g.IsActive, g.SortOrder,
                g.Accounts.Count(a => !a.IsDeleted), g.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<AccountGroupGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record AccountGroupExportRow(
    string Code, string Name, string GroupType, string? Description, bool IsActive, int SortOrder, int AccountCount, DateTime CreatedAt);

public record ExportAccountGroupsQuery(AccountGroupFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<AccountGroupExportRow>>>;

public class ExportAccountGroupsQueryHandler(IAccountsDbContext db)
    : IRequestHandler<ExportAccountGroupsQuery, Result<GridExportSource<AccountGroupExportRow>>>
{
    public async Task<Result<GridExportSource<AccountGroupExportRow>>> Handle(ExportAccountGroupsQuery r, CancellationToken ct)
    {
        var q = AccountGroupGrid.ApplyAll(db.AccountGroups.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<AccountGroupExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = AccountGroupGrid.Schema.ApplySort(q, r.Grid).Select(g => new AccountGroupExportRow(
            g.Code, g.Name, g.GroupType, g.Description, g.IsActive, g.SortOrder,
            g.Accounts.Count(a => !a.IsDeleted), g.CreatedAt));
        return Result.Success(new GridExportSource<AccountGroupExportRow>(count, rows));
    }
}
