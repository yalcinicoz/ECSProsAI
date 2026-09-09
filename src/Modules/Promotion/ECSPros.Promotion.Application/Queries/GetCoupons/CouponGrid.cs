using ECSPros.Promotion.Application.Services;
using ECSPros.Promotion.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCoupons;

/// <summary>
/// Kuponlar DataGrid şeması (2026-09-09, "tüm tablolar orders gibi olsun"): beyaz listeli filtre/sıralama +
/// mevcut adlandırılmış filtreler (isActive/memberId/memberGroupId) + global arama (kod).
///
/// <para><c>hedef</c> alanı KuponHedefKurali'nın üç durumunu tek enum'a indirir — panelde "kime açık"
/// kolonu bununla süzülür: <c>all</c> (herkes) | <c>member</c> (kişiye özel) | <c>group</c> (üye grubu).</para>
/// <para><c>live</c>: bugün geçerli olan kupon (aktif + tarih aralığında) — kampanya şemasındaki kalıbın aynısı.</para>
/// </summary>
public static class CouponGrid
{
    public static readonly string[] CouponTypes = { "percentage", "amount", "free_shipping" };
    public static readonly string[] Targets = { "all", "member", "group" };

    public static readonly GridSchema<Coupon> Schema = new GridSchema<Coupon>()
        .Text("code", c => c.Code)
        .Text("name", c => GridJson.Text(c.NameI18n, "tr"))
        .Enum("couponType", c => c.CouponType, CouponTypes)
        .Enum("hedef", c => c.MemberId != null ? "member" : c.MemberGroupId != null ? "group" : "all", Targets)
        .Number("discountValue", c => c.DiscountValue)
        .Number("usageCount", c => c.UsageCount)
        .Number("usageLimitTotal", c => c.UsageLimitTotal)
        .Number("minimumCartTotal", c => c.MinimumCartTotal)
        .Bool("isActive", c => c.IsActive)
        .Bool("live", c => c.IsActive && c.StartsAt <= DateTime.UtcNow && (c.EndsAt == null || c.EndsAt >= DateTime.UtcNow))
        .Bool("firstOrderOnly", c => c.ValidForFirstOrderOnly)
        .Bool("used", c => c.UsageCount > 0)
        .Date("startsAt", c => c.StartsAt)
        .Date("endsAt", c => c.EndsAt)
        .Date("createdAt", c => c.CreatedAt)
        .Guid("memberId", c => c.MemberId)
        .Guid("memberGroupId", c => c.MemberGroupId)
        .Guid("campaignId", c => c.CampaignId)
        .Sort("code", c => c.Code)
        .Sort("name", c => GridJson.Text(c.NameI18n, "tr"))
        .Sort("couponType", c => c.CouponType)
        .Sort("hedef", c => c.MemberId != null ? "member" : c.MemberGroupId != null ? "group" : "all")
        .Sort("discountValue", c => c.DiscountValue)
        .Sort("usageCount", c => c.UsageCount)
        .Sort("usageLimitTotal", c => c.UsageLimitTotal)
        .Sort("minimumCartTotal", c => c.MinimumCartTotal)
        .Sort("isActive", c => c.IsActive)
        .Sort("firstOrderOnly", c => c.ValidForFirstOrderOnly)
        .Sort("startsAt", c => c.StartsAt)
        .Sort("endsAt", c => c.EndsAt)
        .Sort("createdAt", c => c.CreatedAt)
        .DefaultSort(c => c.CreatedAt, desc: true)
        .TieBreaker(c => c.Id);

    /// <summary>Eski istemciler/derin linkler bozulmasın diye adlandırılmış filtreler korunur.</summary>
    public static IQueryable<Coupon> ApplyNamed(IQueryable<Coupon> query, CouponListFilters f)
    {
        if (f.IsActive.HasValue) query = query.Where(c => c.IsActive == f.IsActive.Value);
        if (f.MemberId.HasValue) query = query.Where(c => c.MemberId == f.MemberId.Value);
        if (f.MemberGroupId.HasValue) query = query.Where(c => c.MemberGroupId == f.MemberGroupId.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(c => c.Code.ToLower().Contains(term));
        }
        return query;
    }

    public static IQueryable<Coupon> ApplyAll(IQueryable<Coupon> query, CouponListFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string TypeLabel(string s) => s switch
    {
        "percentage" => "Yüzde", "amount" => "Tutar", "free_shipping" => "Ücretsiz Kargo", _ => s,
    };

    public static string TargetLabel(string s) => s switch
    {
        "all" => "Herkes", "member" => "Kişiye Özel", "group" => "Üye Grubu", _ => s,
    };
}

public record CouponListFilters(
    string? Search = null, bool? IsActive = null, Guid? MemberId = null, Guid? MemberGroupId = null);

public record CouponExportRow(
    string Code, string Name, string CouponType, string Hedef, decimal DiscountValue,
    int UsageCount, int? UsageLimitTotal, int? UsageLimitPerMember, decimal? MinimumCartTotal,
    bool ValidForFirstOrderOnly, bool IsActive, DateTime StartsAt, DateTime? EndsAt, DateTime CreatedAt);

public record ExportCouponsQuery(CouponListFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<CouponExportRow>>>;

public class ExportCouponsQueryHandler(IPromotionDbContext db)
    : IRequestHandler<ExportCouponsQuery, Result<GridExportSource<CouponExportRow>>>
{
    public async Task<Result<GridExportSource<CouponExportRow>>> Handle(ExportCouponsQuery r, CancellationToken ct)
    {
        var q = CouponGrid.ApplyAll(db.Coupons.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<CouponExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = CouponGrid.Schema.ApplySort(q, r.Grid).Select(c => new CouponExportRow(
            c.Code, GridJson.Text(c.NameI18n, "tr"), c.CouponType,
            c.MemberId != null ? "member" : c.MemberGroupId != null ? "group" : "all",
            c.DiscountValue, c.UsageCount, c.UsageLimitTotal, c.UsageLimitPerMember, c.MinimumCartTotal,
            c.ValidForFirstOrderOnly, c.IsActive, c.StartsAt, c.EndsAt, c.CreatedAt));
        return Result.Success(new GridExportSource<CouponExportRow>(count, rows));
    }
}
