using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetPlatformTypes;

/// <summary>
/// Platform tipleri DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /core/platform-types</c> TAM liste + ayar şeması + yetenekler döner ve
/// Kanallar, Firma detayı, Pazaryerleri ekranlarının kaynağıdır; sayfalamak onları kırar. Liste ekranı
/// sayfalı <c>/core/platform-types/grid</c> kullanır — satırda ŞEMA/YETENEK JSON'u YOK.
///
/// <para><c>SettingsSchemaJson</c> ve <c>CapabilitiesJson</c> ham JSON metin kolonlarıdır (jsonb değil);
/// içeriklerine göre filtre/sıralama YOK — yalnız "şeması var mı" bayrağı bildirildi. Şema ayrıntısı
/// satır tıklanınca tam uçtan çekilir (ağır jsonb'yi satırdan çıkarma kalıbı).</para>
/// </summary>
public static class PlatformTypeGrid
{
    public static readonly GridSchema<PlatformType> Schema = new GridSchema<PlatformType>()
        .Text("code", t => t.Code)
        .Text("name", t => GridJson.Text(t.NameI18n, "tr"))
        .Bool("isMarketplace", t => t.IsMarketplace)
        .Bool("isActive", t => t.IsActive)
        .Bool("hasSchema", t => t.SettingsSchemaJson != null && t.SettingsSchemaJson != "")
        .Bool("kullanimda", t => t.FirmPlatforms.Any(p => !p.IsDeleted))
        .Number("channelCount", t => t.FirmPlatforms.Count(p => !p.IsDeleted))
        .Date("createdAt", t => t.CreatedAt)
        .Sort("code", t => t.Code)
        .Sort("name", t => GridJson.Text(t.NameI18n, "tr"))
        .Sort("isMarketplace", t => t.IsMarketplace)
        .Sort("isActive", t => t.IsActive)
        .Sort("channelCount", t => t.FirmPlatforms.Count(p => !p.IsDeleted))
        .Sort("createdAt", t => t.CreatedAt)
        .DefaultSort(t => t.Code, desc: false)
        .TieBreaker(t => t.Id);

    public static IQueryable<PlatformType> ApplyNamed(IQueryable<PlatformType> query, PlatformTypeFilters f)
    {
        if (f.ActiveOnly) query = query.Where(t => t.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToLower();
            query = query.Where(t => t.Code.ToLower().Contains(s)
                || GridJson.Text(t.NameI18n, "tr")!.ToLower().Contains(s));
        }
        return query;
    }

    public static IQueryable<PlatformType> ApplyAll(IQueryable<PlatformType> query, PlatformTypeFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

public record PlatformTypeFilters(bool ActiveOnly = false, string? Search = null);

public record PlatformTypeGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, bool IsMarketplace, bool IsActive,
    bool HasSchema, int ChannelCount, DateTime CreatedAt);

public record GetPlatformTypesGridQuery(PlatformTypeFilters Filters, int Page = 1, int PageSize = 30, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<PlatformTypeGridRow>>>;

public class GetPlatformTypesGridQueryHandler(ICoreDbContext db)
    : IRequestHandler<GetPlatformTypesGridQuery, Result<PagedResult<PlatformTypeGridRow>>>
{
    public async Task<Result<PagedResult<PlatformTypeGridRow>>> Handle(GetPlatformTypesGridQuery r, CancellationToken ct)
    {
        var q = PlatformTypeGrid.ApplyAll(db.PlatformTypes.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await PlatformTypeGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(t => new PlatformTypeGridRow(
                t.Id, t.Code, t.NameI18n, t.IsMarketplace, t.IsActive,
                t.SettingsSchemaJson != null && t.SettingsSchemaJson != "",
                t.FirmPlatforms.Count(p => !p.IsDeleted), t.CreatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<PlatformTypeGridRow>(items, total, r.Page, r.PageSize));
    }
}

public record PlatformTypeExportRow(
    string Code, string Name, bool IsMarketplace, bool IsActive, bool HasSchema, int ChannelCount, DateTime CreatedAt);

public record ExportPlatformTypesQuery(PlatformTypeFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<PlatformTypeExportRow>>>;

public class ExportPlatformTypesQueryHandler(ICoreDbContext db)
    : IRequestHandler<ExportPlatformTypesQuery, Result<GridExportSource<PlatformTypeExportRow>>>
{
    public async Task<Result<GridExportSource<PlatformTypeExportRow>>> Handle(ExportPlatformTypesQuery r, CancellationToken ct)
    {
        var q = PlatformTypeGrid.ApplyAll(db.PlatformTypes.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<PlatformTypeExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = PlatformTypeGrid.Schema.ApplySort(q, r.Grid).Select(t => new PlatformTypeExportRow(
            t.Code, GridJson.Text(t.NameI18n, "tr") ?? t.Code, t.IsMarketplace, t.IsActive,
            t.SettingsSchemaJson != null && t.SettingsSchemaJson != "",
            t.FirmPlatforms.Count(p => !p.IsDeleted), t.CreatedAt));
        return Result.Success(new GridExportSource<PlatformTypeExportRow>(count, rows));
    }
}
