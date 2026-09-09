using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
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
    int PageSize = 20,
    // Y3 (K2): kullanıcının görebileceği kanallar; null = kısıt yok, boş = hiçbir kayıt.
    IReadOnlyCollection<Guid>? KanalKisiti = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<AdminSavedSearchDto>>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (SavedSearchGrid.Schema)

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
        // Y3 kanal kapsamı + adlandırılmış + grid filtreleri TEK yerden.
        var q = SavedSearchGrid.ApplyAll(
            db.SavedSearches.AsNoTracking(),
            new SavedSearchFilters(request.NotifyEnabled, request.FirmPlatformId, request.Search),
            request.Grid,
            request.KanalKisiti ?? request.Grid?.KanalKisiti);

        var toplam = await q.CountAsync(ct);
        var kayitlar = await SavedSearchGrid.Schema.ApplySort(q, request.Grid)
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
