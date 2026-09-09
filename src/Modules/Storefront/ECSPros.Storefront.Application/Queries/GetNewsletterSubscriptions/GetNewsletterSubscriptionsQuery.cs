using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetNewsletterSubscriptions;

/// <summary>P5 (P0 bulgusu): bülten aboneleri listesi (admin) — aktiflik/platform
/// filtreli, sayfalı. Gönderim/kampanya entegrasyonu ileri iş; bu liste kaynaktır.</summary>
public record GetNewsletterSubscriptionsQuery(
    bool? IsActive = null,
    Guid? FirmPlatformId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    // Y3 (K2): kullanıcının görebileceği kanallar; null = kısıt yok, boş = hiçbir kayıt.
    IReadOnlyCollection<Guid>? KanalKisiti = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<NewsletterSubscriptionDto>>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (NewsletterSubscriptionGrid.Schema)

public record NewsletterSubscriptionDto(
    Guid Id,
    Guid FirmPlatformId,
    string Email,
    Guid? MemberId,
    bool IsActive,
    DateTime CreatedAt);

public class GetNewsletterSubscriptionsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetNewsletterSubscriptionsQuery, Result<PagedResult<NewsletterSubscriptionDto>>>
{
    public async Task<Result<PagedResult<NewsletterSubscriptionDto>>> Handle(
        GetNewsletterSubscriptionsQuery request, CancellationToken ct)
    {
        // Y3 kanal kapsamı + adlandırılmış + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var q = NewsletterSubscriptionGrid.ApplyAll(
            db.NewsletterSubscriptions.AsNoTracking(),
            new NewsletterFilters(request.IsActive, request.FirmPlatformId, request.Search),
            request.Grid,
            request.KanalKisiti ?? request.Grid?.KanalKisiti);

        var toplam = await q.CountAsync(ct);
        var kayitlar = await NewsletterSubscriptionGrid.Schema.ApplySort(q, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NewsletterSubscriptionDto(
                n.Id, n.FirmPlatformId, n.Email, n.MemberId, n.IsActive, n.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<NewsletterSubscriptionDto>(
            kayitlar, toplam, request.Page, request.PageSize));
    }
}
