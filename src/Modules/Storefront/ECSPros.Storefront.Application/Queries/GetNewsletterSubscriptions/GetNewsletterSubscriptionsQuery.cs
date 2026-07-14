using ECSPros.Shared.Kernel.Common;
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
    int PageSize = 20) : IRequest<Result<PagedResult<NewsletterSubscriptionDto>>>;

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
        var q = db.NewsletterSubscriptions.AsNoTracking().AsQueryable();

        if (request.IsActive.HasValue)
            q = q.Where(n => n.IsActive == request.IsActive.Value);
        if (request.FirmPlatformId.HasValue)
            q = q.Where(n => n.FirmPlatformId == request.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var aranan = request.Search.Trim().ToLower();
            q = q.Where(n => n.Email.ToLower().Contains(aranan));
        }

        var toplam = await q.CountAsync(ct);
        var kayitlar = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NewsletterSubscriptionDto(
                n.Id, n.FirmPlatformId, n.Email, n.MemberId, n.IsActive, n.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<NewsletterSubscriptionDto>(
            kayitlar, toplam, request.Page, request.PageSize));
    }
}
