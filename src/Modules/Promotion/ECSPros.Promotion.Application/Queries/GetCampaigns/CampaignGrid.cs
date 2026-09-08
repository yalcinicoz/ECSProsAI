using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCampaigns;

/// <summary>
/// Kampanyalar DataGrid şeması (F4). Ad (NameI18n jsonb "tr") GridJson.Text DbFunction'ıyla (jsonb_extract_path_text) sunucu tarafında
/// filtrelenir/sıralanır; kod/aktiflik/tarih/öncelik/platform/doldurma tipi beyaz listede. Global arama: kod + rozet + ad (tr).
/// </summary>
public static class CampaignGrid
{
    public static readonly string[] FillTypes = { "all", "manual", "filter", "mixed" };

    public static readonly GridSchema<Campaign> Schema = new GridSchema<Campaign>()
        .Text("code", c => c.Code)
        .Text("name", c => GridJson.Text(c.NameI18n, "tr"))
        .Text("badgeLabel", c => c.BadgeLabel)
        .Enum("fillType", c => c.FillType, FillTypes)
        .Enum("campaignTypeCode", c => c.CampaignType.Code)
        .Bool("isActive", c => c.IsActive)
        .Bool("live", c => c.IsActive && c.StartsAt <= DateTime.UtcNow && (c.EndsAt == null || c.EndsAt >= DateTime.UtcNow))
        .Date("startsAt", c => c.StartsAt)
        .Date("endsAt", c => c.EndsAt)
        .Date("createdAt", c => c.CreatedAt)
        .Number("priority", c => c.Priority)
        .Guid("firmPlatformId", c => c.FirmPlatformId)
        .Sort("code", c => c.Code)
        .Sort("name", c => GridJson.Text(c.NameI18n, "tr"))
        .Sort("priority", c => c.Priority)
        .Sort("startsAt", c => c.StartsAt)
        .Sort("endsAt", c => c.EndsAt)
        .Sort("isActive", c => c.IsActive)
        .Sort("createdAt", c => c.CreatedAt)
        .DefaultSort(c => c.Priority, desc: true)
        .TieBreaker(c => c.Id);

    public static IQueryable<Campaign> ApplyNamed(IQueryable<Campaign> query, CampaignListFilters f)
    {
        if (f.ActiveOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(c => c.IsActive && c.StartsAt <= now && (c.EndsAt == null || c.EndsAt >= now));
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(c => c.Code.ToLower().Contains(term)
                || (c.BadgeLabel != null && c.BadgeLabel.ToLower().Contains(term))
                || (GridJson.Text(c.NameI18n, "tr") != null && GridJson.Text(c.NameI18n, "tr")!.ToLower().Contains(term)));
        }
        return query;
    }

    public static IQueryable<Campaign> ApplyAll(IQueryable<Campaign> query, CampaignListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string FillLabel(string s) => s switch { "all" => "Tüm ürünler", "manual" => "Manuel", "filter" => "Filtre", "mixed" => "Karma", _ => s };
}

public record CampaignListFilters(bool ActiveOnly = false, string? Search = null);

/// <summary>Sayfalı kampanya listesi (DataGrid). Eski `GetCampaignsQuery` düz dizi döner ve aynen korunur; controller `page` parametresi varsa bunu kullanır.</summary>
public record GetCampaignsGridQuery(CampaignListFilters Filters, GridRequest Grid) : IRequest<Result<PagedResult<CampaignDto>>>;

public class GetCampaignsGridQueryHandler(IPromotionDbContext db) : IRequestHandler<GetCampaignsGridQuery, Result<PagedResult<CampaignDto>>>
{
    public async Task<Result<PagedResult<CampaignDto>>> Handle(GetCampaignsGridQuery r, CancellationToken ct)
    {
        var q = CampaignGrid.ApplyAll(db.Campaigns.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var items = await CampaignGrid.Schema.ApplySort(q, r.Grid)
            .Skip((r.Grid.Page - 1) * r.Grid.PageSize).Take(r.Grid.PageSize)
            .Select(c => new CampaignDto(
                c.Id, c.Code, c.NameI18n, c.StartsAt, c.EndsAt, c.IsActive, c.Priority, c.FillType,
                c.CampaignTypeId, c.CampaignType.Code, c.DescriptionI18n, c.Settings, c.FirmPlatformId))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<CampaignDto>(items, total, r.Grid.Page, r.Grid.PageSize));
    }
}

public record CampaignExportRow(
    string Code, Dictionary<string, string> NameI18n, string CampaignTypeCode, string FillType, DateTime StartsAt, DateTime? EndsAt, bool IsActive, int Priority,
    string? BadgeLabel, decimal? SupplierCommissionRate, decimal SupplierDiscountSharePercent, bool RequiresSupplierOptIn, DateTime CreatedAt);

public record ExportCampaignsQuery(CampaignListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<CampaignExportRow>>>;

public class ExportCampaignsQueryHandler(IPromotionDbContext db) : IRequestHandler<ExportCampaignsQuery, Result<GridExportSource<CampaignExportRow>>>
{
    public async Task<Result<GridExportSource<CampaignExportRow>>> Handle(ExportCampaignsQuery r, CancellationToken ct)
    {
        var q = CampaignGrid.ApplyAll(db.Campaigns.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<CampaignExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = CampaignGrid.Schema.ApplySort(q, r.Grid).Select(c => new CampaignExportRow(
            c.Code, c.NameI18n, c.CampaignType.Code, c.FillType, c.StartsAt, c.EndsAt, c.IsActive, c.Priority,
            c.BadgeLabel, c.SupplierCommissionRate, c.SupplierDiscountSharePercent, c.RequiresSupplierOptIn, c.CreatedAt));
        return Result.Success(new GridExportSource<CampaignExportRow>(count, rows));
    }
}
