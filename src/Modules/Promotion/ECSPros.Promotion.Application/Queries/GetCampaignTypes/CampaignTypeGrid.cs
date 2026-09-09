using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCampaignTypes;

/// <summary>
/// Kampanya tipleri DataGrid şeması (2026-09-09).
///
/// ★ AYRI UÇ: mevcut <c>GET /promotion/campaign-types</c> TAM liste döner (ayar şemasıyla) ve kampanya
/// listesi + kampanya detayı ekranlarının kaynağıdır; sayfalamak onları kırar. Liste ekranı sayfalı
/// <c>/promotion/campaign-types/grid</c> kullanır — satırda ayar şeması YOK, yalnız alan SAYISI.
///
/// <para><c>handlerClass</c> koda ait teknik alandır: filtrede var (aramada işe yarar) ama panelden
/// düzenlenmez. <c>kullanimda</c>: bu tipte tanımlı kampanyası olan tip — silmeden önce bakılır.</para>
/// </summary>
public static class CampaignTypeGrid
{
    public static readonly string[] Scopes = { "product", "cart", "shipping" };

    public static readonly GridSchema<CampaignType> Schema = new GridSchema<CampaignType>()
        .Text("code", t => t.Code)
        .Text("name", t => GridJson.Text(t.NameI18n, "tr"))
        .Text("description", t => GridJson.Text(t.DescriptionI18n, "tr"))
        .Text("handlerClass", t => t.HandlerClass)
        .Enum("scope", t => t.Scope, Scopes)
        .Bool("requiresProducts", t => t.RequiresProducts)
        .Bool("productPriceDisplay", t => t.ProductPriceDisplay)
        .Bool("isStackable", t => t.IsStackable)
        .Bool("isActive", t => t.IsActive)
        .Bool("kullanimda", t => t.Campaigns.Any(c => !c.IsDeleted))
        .Number("sortOrder", t => t.SortOrder)
        .Number("campaignCount", t => t.Campaigns.Count(c => !c.IsDeleted))
        .Date("createdAt", t => t.CreatedAt)
        .Sort("code", t => t.Code)
        .Sort("name", t => GridJson.Text(t.NameI18n, "tr"))
        .Sort("scope", t => t.Scope)
        .Sort("handlerClass", t => t.HandlerClass)
        .Sort("requiresProducts", t => t.RequiresProducts)
        .Sort("isStackable", t => t.IsStackable)
        .Sort("isActive", t => t.IsActive)
        .Sort("sortOrder", t => t.SortOrder)
        .Sort("campaignCount", t => t.Campaigns.Count(c => !c.IsDeleted))
        .Sort("createdAt", t => t.CreatedAt)
        .DefaultSort(t => t.SortOrder, desc: false)
        .TieBreaker(t => t.Code);

    public static IQueryable<CampaignType> ApplyNamed(IQueryable<CampaignType> query, CampaignTypeFilters f)
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

    public static IQueryable<CampaignType> ApplyAll(IQueryable<CampaignType> query, CampaignTypeFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string ScopeLabel(string s) => s switch
    {
        "product" => "Ürün", "cart" => "Sepet", "shipping" => "Kargo", _ => s,
    };
}

public record CampaignTypeFilters(bool ActiveOnly = false, string? Search = null);

public record CampaignTypeGridRow(
    Guid Id, string Code, Dictionary<string, string> NameI18n, Dictionary<string, string>? DescriptionI18n,
    string Scope, string HandlerClass, bool RequiresProducts, bool ProductPriceDisplay, bool IsStackable,
    bool IsActive, int SortOrder, int CampaignCount, int SettingsFieldCount);

public record GetCampaignTypesGridQuery(CampaignTypeFilters Filters, int Page = 1, int PageSize = 30, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<CampaignTypeGridRow>>>;

public class GetCampaignTypesGridQueryHandler(IPromotionDbContext db)
    : IRequestHandler<GetCampaignTypesGridQuery, Result<PagedResult<CampaignTypeGridRow>>>
{
    public async Task<Result<PagedResult<CampaignTypeGridRow>>> Handle(GetCampaignTypesGridQuery r, CancellationToken ct)
    {
        var q = CampaignTypeGrid.ApplyAll(db.CampaignTypes.AsNoTracking(), r.Filters, r.Grid);
        var total = await q.CountAsync(ct);
        var satirlar = await CampaignTypeGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            // Ayar şeması satırda GÖNDERİLMEZ (ağır jsonb) — yalnız alan sayısı bilgi olarak taşınır.
            .Select(t => new CampaignTypeGridRow(
                t.Id, t.Code, t.NameI18n, t.DescriptionI18n, t.Scope, t.HandlerClass,
                t.RequiresProducts, t.ProductPriceDisplay, t.IsStackable, t.IsActive, t.SortOrder,
                t.Campaigns.Count(c => !c.IsDeleted),
                t.SettingsSchema == null ? 0 : t.SettingsSchema.Count))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<CampaignTypeGridRow>(satirlar, total, r.Page, r.PageSize));
    }
}

public record CampaignTypeExportRow(
    string Code, string Name, string Scope, string HandlerClass, bool RequiresProducts,
    bool IsStackable, bool IsActive, int SortOrder, int CampaignCount);

public record ExportCampaignTypesQuery(CampaignTypeFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<CampaignTypeExportRow>>>;

public class ExportCampaignTypesQueryHandler(IPromotionDbContext db)
    : IRequestHandler<ExportCampaignTypesQuery, Result<GridExportSource<CampaignTypeExportRow>>>
{
    public async Task<Result<GridExportSource<CampaignTypeExportRow>>> Handle(ExportCampaignTypesQuery r, CancellationToken ct)
    {
        var q = CampaignTypeGrid.ApplyAll(db.CampaignTypes.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<CampaignTypeExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = CampaignTypeGrid.Schema.ApplySort(q, r.Grid).Select(t => new CampaignTypeExportRow(
            t.Code, GridJson.Text(t.NameI18n, "tr") ?? t.Code, t.Scope, t.HandlerClass,
            t.RequiresProducts, t.IsStackable, t.IsActive, t.SortOrder, t.Campaigns.Count(c => !c.IsDeleted)));
        return Result.Success(new GridExportSource<CampaignTypeExportRow>(count, rows));
    }
}
