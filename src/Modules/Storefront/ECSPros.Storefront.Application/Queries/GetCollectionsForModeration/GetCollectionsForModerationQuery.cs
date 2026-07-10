using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetCollectionsForModeration;

/// <summary>E6: admin moderasyon kuyruğu — durum filtreli, sayfalı.</summary>
public record GetCollectionsForModerationQuery(
    string? Status = "pending",
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ModerationCollectionDto>>>;

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
        var q = db.Collections.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(c => c.Status == request.Status);

        var toplam = await q.CountAsync(ct);
        var kayitlar = await q
            .OrderBy(c => c.CreatedAt)
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
