using ECSPros.Promotion.Application.Queries.GetCoupons;

namespace ECSPros.Api.Grid;

/// <summary>Kuponlar Excel kolonları (kod kilitli).</summary>
public static class CouponExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<CouponExportRow>> All = new GridExportColumn<CouponExportRow>[]
    {
        new("code", "Kod", r => r.Code, Locked: true),
        new("name", "Ad", r => r.Name),
        new("couponType", "Tip", r => CouponGrid.TypeLabel(r.CouponType)),
        new("discountValue", "İndirim", r => r.DiscountValue),
        new("hedef", "Kime Açık", r => CouponGrid.TargetLabel(r.Hedef)),
        new("usageCount", "Kullanım", r => r.UsageCount),
        new("usageLimitTotal", "Toplam Limit", r => r.UsageLimitTotal),
        new("usageLimitPerMember", "Üye Başına Limit", r => r.UsageLimitPerMember),
        new("minimumCartTotal", "Alt Sepet Tutarı", r => r.MinimumCartTotal),
        new("firstOrderOnly", "Yalnız İlk Sipariş", r => r.ValidForFirstOrderOnly ? "Evet" : "Hayır"),
        new("isActive", "Aktif", r => r.IsActive ? "Evet" : "Hayır"),
        new("startsAt", "Başlangıç", r => GridExportWriter.ToIstanbul(r.StartsAt)),
        new("endsAt", "Bitiş", r => r.EndsAt.HasValue ? GridExportWriter.ToIstanbul(r.EndsAt.Value) : null),
        new("createdAt", "Oluşturma", r => GridExportWriter.ToIstanbul(r.CreatedAt)),
    };
}
