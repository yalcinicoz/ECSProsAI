using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetCollectionsForModeration;

/// <summary>E6: admin moderasyon kuyruğu — durum filtreli, sayfalı.</summary>
public record GetCollectionsForModerationQuery(
    string? Status = "pending",
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    // Y3 (K2): kullanıcının görebileceği kanallar; null = kısıt yok, boş = hiçbir kayıt.
    IReadOnlyCollection<Guid>? KanalKisiti = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<ModerationCollectionDto>>>;
    // Search/Grid (2026-09-09, DataGrid): global arama + beyaz listeli f.* filtreleri + sort/dir

public record ModerationCollectionDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid MemberId,
    string Name,
    string? Description,
    bool IsPublic,
    bool IsShareable,
    string Status,
    bool IsQuickSave,
    int ItemCount,
    DateTime CreatedAt,
    DateTime? ModeratedAt);

public class GetCollectionsForModerationQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetCollectionsForModerationQuery, Result<PagedResult<ModerationCollectionDto>>>
{
    public async Task<Result<PagedResult<ModerationCollectionDto>>> Handle(
        GetCollectionsForModerationQuery request, CancellationToken ct)
    {
        // Y3 kanal kapsamı + adlandırılmış + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var q = CollectionModerationGrid.ApplyAll(
            db.Collections.AsNoTracking(),
            new CollectionModerationFilters(request.Status, request.Search),
            request.Grid,
            request.KanalKisiti ?? request.Grid?.KanalKisiti);

        var toplam = await q.CountAsync(ct);
        // Sıralama şemadan (varsayılan: en ESKİ önce — moderasyon kuyruğu sırası).
        var kayitlar = await CollectionModerationGrid.Schema.ApplySort(q, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ModerationCollectionDto(
                c.Id, c.FirmPlatformId, c.MemberId, c.Name, c.Description,
                c.IsPublic, c.IsShareable, c.Status, c.IsQuickSave,
                c.Items.Count(i => !i.IsDeleted), c.CreatedAt, c.ModeratedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ModerationCollectionDto>(
            kayitlar, toplam, request.Page, request.PageSize));
    }
}
