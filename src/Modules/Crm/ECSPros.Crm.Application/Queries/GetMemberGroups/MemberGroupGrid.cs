using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetMemberGroups;

/// <summary>
/// Üye grupları DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /crm/member-groups</c> TÜM grupları döner ve kupon hedef seçicisi ile
/// üye formunu besler; sayfalamak onları kırar. Liste ekranı <c>/crm/member-groups/grid</c> kullanır.
/// </summary>
public static class MemberGroupGrid
{
    public static readonly GridSchema<MemberGroup> Schema = new GridSchema<MemberGroup>()
        .Text("code", g => g.Code)
        .Text("name", g => GridJson.Text(g.NameI18n, "tr"))
        .Bool("isDefault", g => g.IsDefault)
        .Bool("isWholesale", g => g.IsWholesale)
        .Bool("requiresApproval", g => g.RequiresApproval)
        .Bool("showPricesBeforeLogin", g => g.ShowPricesBeforeLogin)
        .Bool("isActive", g => g.IsActive)
        .Bool("hasMembers", g => g.Members.Any(m => !m.IsDeleted))
        .Number("minOrderAmount", g => g.MinOrderAmount)
        .Number("paymentTermsDays", g => g.PaymentTermsDays)
        .Number("sortOrder", g => g.SortOrder)
        .Number("memberCount", g => g.Members.Count(m => !m.IsDeleted))
        .Date("createdAt", g => g.CreatedAt)
        .Sort("code", g => g.Code)
        .Sort("name", g => GridJson.Text(g.NameI18n, "tr"))
        .Sort("isDefault", g => g.IsDefault)
        .Sort("isWholesale", g => g.IsWholesale)
        .Sort("requiresApproval", g => g.RequiresApproval)
        .Sort("minOrderAmount", g => g.MinOrderAmount)
        .Sort("paymentTermsDays", g => g.PaymentTermsDays)
        .Sort("memberCount", g => g.Members.Count(m => !m.IsDeleted))
        .Sort("sortOrder", g => g.SortOrder)
        .Sort("isActive", g => g.IsActive)
        .Sort("createdAt", g => g.CreatedAt)
        .DefaultSort(g => g.SortOrder)
        .TieBreaker(g => g.Id);

    public static IQueryable<MemberGroup> ApplyNamed(IQueryable<MemberGroup> query, MemberGroupFilters f)
    {
        if (f.ActiveOnly) query = query.Where(g => g.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(g => g.Code.ToLower().Contains(t)
                || GridJson.Text(g.NameI18n, "tr").ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<MemberGroup> ApplyAll(IQueryable<MemberGroup> query, MemberGroupFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

public record MemberGroupFilters(bool ActiveOnly = false, string? Search = null);

public record MemberGroupGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, bool IsDefault, bool IsWholesale,
    bool RequiresApproval, bool ShowPricesBeforeLogin, decimal? MinOrderAmount, int? PaymentTermsDays,
    bool IsActive, int SortOrder, int MemberCount, DateTime CreatedAt);

public record GetMemberGroupsGridQuery(MemberGroupFilters Filters, int Page = 1, int PageSize = 20, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<MemberGroupGridRow>>>;

public class GetMemberGroupsGridQueryHandler(ICrmDbContext db)
    : IRequestHandler<GetMemberGroupsGridQuery, Result<PagedResult<MemberGroupGridRow>>>
{
    public async Task<Result<PagedResult<MemberGroupGridRow>>> Handle(GetMemberGroupsGridQuery r, CancellationToken ct)
    {
        var q = MemberGroupGrid.ApplyAll(db.MemberGroups.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await MemberGroupGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(g => new MemberGroupGridRow(
                g.Id, g.Code, g.NameI18n, g.IsDefault, g.IsWholesale, g.RequiresApproval,
                g.ShowPricesBeforeLogin, g.MinOrderAmount, g.PaymentTermsDays, g.IsActive, g.SortOrder,
                g.Members.Count(m => !m.IsDeleted), g.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<MemberGroupGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record MemberGroupExportRow(
    string Code, string Name, bool IsDefault, bool IsWholesale, bool RequiresApproval,
    bool ShowPricesBeforeLogin, decimal? MinOrderAmount, int? PaymentTermsDays,
    bool IsActive, int SortOrder, int MemberCount, DateTime CreatedAt);

public record ExportMemberGroupsQuery(MemberGroupFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<MemberGroupExportRow>>>;

public class ExportMemberGroupsQueryHandler(ICrmDbContext db)
    : IRequestHandler<ExportMemberGroupsQuery, Result<GridExportSource<MemberGroupExportRow>>>
{
    public async Task<Result<GridExportSource<MemberGroupExportRow>>> Handle(ExportMemberGroupsQuery r, CancellationToken ct)
    {
        var q = MemberGroupGrid.ApplyAll(db.MemberGroups.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<MemberGroupExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = MemberGroupGrid.Schema.ApplySort(q, r.Grid).Select(g => new MemberGroupExportRow(
            g.Code, GridJson.Text(g.NameI18n, "tr"), g.IsDefault, g.IsWholesale, g.RequiresApproval,
            g.ShowPricesBeforeLogin, g.MinOrderAmount, g.PaymentTermsDays, g.IsActive, g.SortOrder,
            g.Members.Count(m => !m.IsDeleted), g.CreatedAt));
        return Result.Success(new GridExportSource<MemberGroupExportRow>(count, rows));
    }
}
