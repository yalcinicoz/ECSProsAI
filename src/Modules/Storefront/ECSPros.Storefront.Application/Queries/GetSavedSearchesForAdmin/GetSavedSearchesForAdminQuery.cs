using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetSavedSearchesForAdmin;

/// <summary>P5: kayıtlı arama izleme (admin) — bildirim açık/kapalı filtreli, sayfalı.
/// Gönderim durumu LastNotifiedAt'tan okunur (E11/H8, günde en fazla 1).</summary>
public record GetSavedSearchesForAdminQuery(
    bool? NotifyEnabled = null,
    Guid? FirmPlatformId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<AdminSavedSearchDto>>>;

public record AdminSavedSearchDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid MemberId,
    string? Name,
    string Query,
    bool NotifyEnabled,
    DateTime? LastNotifiedAt,
    DateTime CreatedAt);

public class GetSavedSearchesForAdminQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetSavedSearchesForAdminQuery, Result<PagedResult<AdminSavedSearchDto>>>
{
    public async Task<Result<PagedResult<AdminSavedSearchDto>>> Handle(
        GetSavedSearchesForAdminQuery request, CancellationToken ct)
    {
        var q = db.SavedSearches.AsNoTracking().AsQueryable();

        if (request.NotifyEnabled.HasValue)
            q = q.Where(s => s.NotifyEnabled == request.NotifyEnabled.Value);
        if (request.FirmPlatformId.HasValue)
            q = q.Where(s => s.FirmPlatformId == request.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var aranan = request.Search.Trim().ToLower();
            q = q.Where(s =>
                s.Query.ToLower().Contains(aranan) ||
                (s.Name != null && s.Name.ToLower().Contains(aranan)));
        }

        var toplam = await q.CountAsync(ct);
        var kayitlar = await q
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new AdminSavedSearchDto(
                s.Id, s.FirmPlatformId, s.MemberId, s.Name, s.Query,
                s.NotifyEnabled, s.LastNotifiedAt, s.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AdminSavedSearchDto>(
            kayitlar, toplam, request.Page, request.PageSize));
    }
}
