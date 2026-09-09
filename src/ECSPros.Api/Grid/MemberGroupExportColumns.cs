using ECSPros.Crm.Application.Queries.GetMemberGroups;

namespace ECSPros.Api.Grid;

/// <summary>Üye grupları Excel kolonları (kod kilitli).</summary>
public static class MemberGroupExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<MemberGroupExportRow>> All = new GridExportColumn<MemberGroupExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("memberCount", "Üye Sayısı", r => r.MemberCount),
        new("isWholesale", "Toptan", r => r.IsWholesale ? "Evet" : "Hayır"),
        new("isDefault", "Varsayılan", r => r.IsDefault ? "Evet" : "Hayır"),
        new("requiresApproval", "Onay Gerekir", r => r.RequiresApproval ? "Evet" : "Hayır"),
        new("showPricesBeforeLogin", "Girişsiz Fiyat", r => r.ShowPricesBeforeLogin ? "Evet" : "Hayır"),
        new("minOrderAmount", "Min. Sipariş", r => r.MinOrderAmount),
        new("paymentTermsDays", "Vade (gün)", r => r.PaymentTermsDays),
        new("sortOrder", "Sıra", r => r.SortOrder),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
