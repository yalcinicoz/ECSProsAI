using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetFirms;

/// <summary>
/// Firmalar DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /core/firms</c> TÜM firmaları döner ve birçok ekranın firma seçicisini
/// besler (kanal ürünleri, hediye kartı, iletişim mesajları…); sayfalamak onları kırar. Liste ekranı
/// sayfalı <c>/core/firms/grid</c> kullanır.
/// </summary>
public static class FirmGrid
{
    public static readonly GridSchema<Firm> Schema = new GridSchema<Firm>()
        .Text("code", f => f.Code)
        .Text("name", f => GridJson.Text(f.NameI18n, "tr"))
        .Text("taxNumber", f => f.TaxNumber)
        .Text("taxOffice", f => f.TaxOffice)
        .Text("phone", f => f.Phone)
        .Text("email", f => f.Email)
        .Text("address", f => f.Address)
        .Bool("isMain", f => f.IsMain)
        .Bool("isActive", f => f.IsActive)
        .Number("platformCount", f => f.FirmPlatforms.Count(p => !p.IsDeleted))
        .Date("createdAt", f => f.CreatedAt)
        .Sort("code", f => f.Code)
        .Sort("name", f => GridJson.Text(f.NameI18n, "tr"))
        .Sort("taxNumber", f => f.TaxNumber)
        .Sort("taxOffice", f => f.TaxOffice)
        .Sort("phone", f => f.Phone)
        .Sort("email", f => f.Email)
        .Sort("address", f => f.Address)
        .Sort("isMain", f => f.IsMain)
        .Sort("isActive", f => f.IsActive)
        .Sort("platformCount", f => f.FirmPlatforms.Count(p => !p.IsDeleted))
        .Sort("createdAt", f => f.CreatedAt)
        .DefaultSort(f => f.Code, desc: false)
        .TieBreaker(f => f.Id);

    public static IQueryable<Firm> ApplyNamed(IQueryable<Firm> query, FirmFilters f)
    {
        if (f.ActiveOnly) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(t)
                || GridJson.Text(x.NameI18n, "tr").ToLower().Contains(t)
                || x.TaxNumber.Contains(t));
        }
        return query;
    }

    public static IQueryable<Firm> ApplyAll(IQueryable<Firm> query, FirmFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

public record FirmFilters(bool ActiveOnly = false, string? Search = null);

public record FirmGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, string TaxOffice, string TaxNumber,
    string Address, string Phone, string Email, bool IsMain, bool IsActive, int PlatformCount, DateTime CreatedAt);

public record GetFirmsGridQuery(FirmFilters Filters, int Page = 1, int PageSize = 20, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<FirmGridRow>>>;

public class GetFirmsGridQueryHandler(ICoreDbContext db)
    : IRequestHandler<GetFirmsGridQuery, Result<PagedResult<FirmGridRow>>>
{
    public async Task<Result<PagedResult<FirmGridRow>>> Handle(GetFirmsGridQuery r, CancellationToken ct)
    {
        var q = FirmGrid.ApplyAll(db.Firms.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await FirmGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(f => new FirmGridRow(
                f.Id, f.Code, f.NameI18n, f.TaxOffice, f.TaxNumber, f.Address, f.Phone, f.Email,
                f.IsMain, f.IsActive, f.FirmPlatforms.Count(p => !p.IsDeleted), f.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<FirmGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record FirmExportRow(
    string Code, string Name, string TaxOffice, string TaxNumber, string Address,
    string Phone, string Email, bool IsMain, bool IsActive, int PlatformCount, DateTime CreatedAt);

public record ExportFirmsQuery(FirmFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<FirmExportRow>>>;

public class ExportFirmsQueryHandler(ICoreDbContext db)
    : IRequestHandler<ExportFirmsQuery, Result<GridExportSource<FirmExportRow>>>
{
    public async Task<Result<GridExportSource<FirmExportRow>>> Handle(ExportFirmsQuery r, CancellationToken ct)
    {
        var q = FirmGrid.ApplyAll(db.Firms.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<FirmExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = FirmGrid.Schema.ApplySort(q, r.Grid).Select(f => new FirmExportRow(
            f.Code, GridJson.Text(f.NameI18n, "tr"), f.TaxOffice, f.TaxNumber, f.Address,
            f.Phone, f.Email, f.IsMain, f.IsActive, f.FirmPlatforms.Count(p => !p.IsDeleted), f.CreatedAt));
        return Result.Success(new GridExportSource<FirmExportRow>(count, rows));
    }
}
