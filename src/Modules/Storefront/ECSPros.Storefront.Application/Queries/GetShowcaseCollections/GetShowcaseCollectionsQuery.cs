using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetShowcaseCollections;

/// <summary>
/// G3: "Koleksiyonlar" bloğunun kaynağı — YALNIZ onaylı (approved) + herkese açık
/// (IsPublic) üye koleksiyonları (spec şartı: gizli/pasif/moderasyonsuz asla vitrine
/// çıkmaz; hızlı-kaydet koleksiyonları IsPublic=false doğduğundan kendiliğinden elenir).
/// ShareCodes verilirse manuel seçim modu: sıra verilen listeye göre korunur.
/// Üye adı çözümü çağıranın işidir (MemberId döneriz; CRM'e buradan gidilmez).
/// </summary>
public record GetShowcaseCollectionsQuery(
    Guid FirmPlatformId,
    int Limit = 10,
    string? Sort = null,               // "popular" (ViewCount) | null (en yeni)
    List<string>? ShareCodes = null) : IRequest<Result<List<ShowcaseCollectionDto>>>;

public record ShowcaseCollectionDto(
    Guid Id,
    Guid MemberId,
    string Name,
    string? Description,
    string ShareCode,
    int ViewCount,
    int ItemCount,
    List<string> PreviewProductCodes,  // kart kolajı için ilk 4 ürün kodu
    DateTime CreatedAt);

public class GetShowcaseCollectionsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetShowcaseCollectionsQuery, Result<List<ShowcaseCollectionDto>>>
{
    public async Task<Result<List<ShowcaseCollectionDto>>> Handle(GetShowcaseCollectionsQuery request, CancellationToken ct)
    {
        var q = db.Collections
            .AsNoTracking()
            .Where(c => c.FirmPlatformId == request.FirmPlatformId
                     && c.Status == "approved" && c.IsPublic);

        if (request.ShareCodes is { Count: > 0 } secilenler)
            q = q.Where(c => secilenler.Contains(c.ShareCode));

        q = request.Sort == "popular"
            ? q.OrderByDescending(c => c.ViewCount).ThenByDescending(c => c.CreatedAt)
            : q.OrderByDescending(c => c.CreatedAt);

        var collections = await q
            .Take(Math.Clamp(request.Limit, 1, 50))
            .Select(c => new
            {
                c.Id, c.MemberId, c.Name, c.Description, c.ShareCode, c.ViewCount, c.CreatedAt,
                ItemCount = c.Items.Count(i => !i.IsDeleted),
                PreviewCodes = c.Items.Where(i => !i.IsDeleted)
                    .OrderBy(i => i.CreatedAt).Select(i => i.ProductCode).Take(4).ToList(),
            })
            .ToListAsync(ct);

        var items = collections.Select(c => new ShowcaseCollectionDto(
            c.Id, c.MemberId, c.Name, c.Description, c.ShareCode,
            c.ViewCount, c.ItemCount, c.PreviewCodes, c.CreatedAt)).ToList();

        // Manuel seçimde config'teki sıra korunur
        if (request.ShareCodes is { Count: > 0 } sira)
            items = items.OrderBy(i => sira.IndexOf(i.ShareCode)).ToList();

        return Result.Success(items);
    }
}
