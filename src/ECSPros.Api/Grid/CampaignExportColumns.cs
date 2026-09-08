using ECSPros.Promotion.Application.Queries.GetCampaigns;

namespace ECSPros.Api.Grid;

/// <summary>Kampanyalar Excel kolonları (kod kilitli; ad jsonb'den "tr").</summary>
public static class CampaignExportColumns
{
    private static object? Tarih(DateTime? d) => d.HasValue ? GridExportWriter.ToIstanbul(d.Value) : null;

    public static readonly IReadOnlyList<GridExportColumn<CampaignExportRow>> All = new GridExportColumn<CampaignExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.NameI18n.TryGetValue("tr", out var tr) ? tr : r.NameI18n.Values.FirstOrDefault()),
        new("campaignTypeCode", "Tip", r => r.CampaignTypeCode),
        new("fillType", "Kapsam", r => CampaignGrid.FillLabel(r.FillType)),
        new("startsAt", "Başlangıç", r => GridExportWriter.ToIstanbul(r.StartsAt)),
        new("endsAt", "Bitiş", r => Tarih(r.EndsAt)),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("priority", "Öncelik", r => r.Priority),
        new("badgeLabel", "Rozet", r => r.BadgeLabel),
        new("supplierCommissionRate", "Tedarikçi Komisyon %", r => r.SupplierCommissionRate),
        new("supplierDiscountSharePercent", "Tedarikçi İndirim Payı %", r => r.SupplierDiscountSharePercent),
        new("requiresSupplierOptIn", "Tedarikçi Onayı", r => r.RequiresSupplierOptIn ? "Gerekli" : "Gerekmez"),
        new("createdAt", "Kayıt Tarihi", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
