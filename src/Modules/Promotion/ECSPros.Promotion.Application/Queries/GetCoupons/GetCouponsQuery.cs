using ECSPros.Promotion.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Promotion.Application.Queries.GetCoupons;

// P3: admin kupon listesi — tanımlar bugüne dek yalnız API/SQL'den giriliyordu
public record GetCouponsQuery(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 20,
    Guid? MemberId = null,
    Guid? MemberGroupId = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<CouponDto>>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (CouponGrid.Schema); null → eski davranış

public record CouponDto(
    Guid Id,
    Guid? CampaignId,
    Guid? MemberId,
    string Code,
    Dictionary<string, string> NameI18n,
    string CouponType,
    decimal DiscountValue,
    int? UsageLimitTotal,
    int? UsageLimitPerMember,
    int UsageCount,
    decimal? MinimumCartTotal,
    bool ValidForFirstOrderOnly,
    Guid? MemberGroupId,
    DateTime StartsAt,
    DateTime? EndsAt,
    bool IsActive,
    DateTime CreatedAt,
    string? MemberName = null);   // kişiye özel kuponda listede gösterilecek üye adı (CRM'den)

public class GetCouponsQueryHandler(IPromotionDbContext db, IMemberService memberService)
    : IRequestHandler<GetCouponsQuery, Result<PagedResult<CouponDto>>>
{
    public async Task<Result<PagedResult<CouponDto>>> Handle(GetCouponsQuery request, CancellationToken ct)
    {
        // Adlandırılmış filtreler + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var query = CouponGrid.ApplyAll(
            db.Coupons.AsNoTracking(),
            new CouponListFilters(request.Search, request.IsActive, request.MemberId, request.MemberGroupId),
            request.Grid);

        var totalCount = await query.CountAsync(ct);

        var items = await CouponGrid.Schema.ApplySort(query, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CouponDto(
                c.Id, c.CampaignId, c.MemberId, c.Code, c.NameI18n, c.CouponType,
                c.DiscountValue, c.UsageLimitTotal, c.UsageLimitPerMember, c.UsageCount,
                c.MinimumCartTotal, c.ValidForFirstOrderOnly, c.MemberGroupId,
                c.StartsAt, c.EndsAt, c.IsActive, c.CreatedAt, null))
            .ToListAsync(ct);

        // Kişiye özel kuponların üye adı (sayfa başına en çok PageSize kadar, yalnız hedefli olanlar)
        var uyeAdlari = new Dictionary<Guid, string>();
        foreach (var uyeId in items.Where(i => i.MemberId.HasValue).Select(i => i.MemberId!.Value).Distinct())
        {
            var uye = await memberService.GetMemberAsync(uyeId, ct);
            if (uye is not null) uyeAdlari[uyeId] = uye.FullName;
        }

        if (uyeAdlari.Count > 0)
            items = items
                .Select(i => i.MemberId.HasValue && uyeAdlari.TryGetValue(i.MemberId.Value, out var ad)
                    ? i with { MemberName = ad }
                    : i)
                .ToList();

        return Result.Success(new PagedResult<CouponDto>(items, totalCount, request.Page, request.PageSize));
    }
}
