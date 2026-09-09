using ECSPros.Promotion.Application.Queries.GetCampaignTypes;

namespace ECSPros.Api.Grid;

/// <summary>Kampanya tipleri Excel kolonları (kod kilitli).</summary>
public static class CampaignTypeExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<CampaignTypeExportRow>> All = new GridExportColumn<CampaignTypeExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("scope", "Kapsam", r => CampaignTypeGrid.ScopeLabel(r.Scope)),
        new("campaignCount", "Kampanya", r => r.CampaignCount),
        new("requiresProducts", "Ürün Gerekir", r => r.RequiresProducts ? "Evet" : "Hayır"),
        new("isStackable", "Birleşebilir", r => r.IsStackable ? "Evet" : "Hayır"),
        new("handlerClass", "Handler", r => r.HandlerClass),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
    };
}
