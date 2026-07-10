using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberCollections;

/// <summary>E6: üyenin koleksiyonları (Kaydedilenler dahil) — son güncellenen önce.</summary>
public record GetMemberCollectionsQuery(Guid FirmPlatformId, Guid MemberId)
    : IRequest<Result<List<MemberCollectionDto>>>;

public record MemberCollectionDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsPublic,
    bool IsShareable,
    string ShareCode,
    string Status,
    int ViewCount,
    bool IsQuickSave,
    DateTime? UpdatedAt,
    DateTime CreatedAt,
    List<string> ItemCodes);

public class GetMemberCollectionsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberCollectionsQuery, Result<List<MemberCollectionDto>>>
{
    public async Task<Result<List<MemberCollectionDto>>> Handle(GetMemberCollectionsQuery request, CancellationToken ct)
    {
        var koleksiyonlar = await db.Collections
            .Where(c => c.FirmPlatformId == request.FirmPlatformId && c.MemberId == request.MemberId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new MemberCollectionDto(
                c.Id, c.Name, c.Description, c.IsPublic, c.IsShareable, c.ShareCode,
                c.Status, c.ViewCount, c.IsQuickSave, c.UpdatedAt, c.CreatedAt,
                c.Items.Where(i => !i.IsDeleted)
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => i.ProductCode).ToList()))
            .ToListAsync(ct);

        return Result.Success(koleksiyonlar);
    }
}
