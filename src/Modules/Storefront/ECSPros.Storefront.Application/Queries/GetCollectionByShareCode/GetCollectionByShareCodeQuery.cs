using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetCollectionByShareCode;

/// <summary>
/// H10: public koleksiyon sayfası — ShareCode ile TEK koleksiyon (anonim erişim).
/// Yalnız onaylı (Status=approved) VE paylaşıma açık (IsShareable) koleksiyon döner;
/// pending/rejected ya da paylaşıma kapalı koleksiyonun linki 404 olur (E6 moderasyon kapısı).
/// Her başarılı çözümde ViewCount atomik artar (link görüntülenme sayacı).
/// </summary>
public record GetCollectionByShareCodeQuery(Guid FirmPlatformId, string ShareCode)
    : IRequest<Result<PublicCollectionDto>>;

public record PublicCollectionDto(
    Guid Id,
    string Name,
    string? Description,
    int ViewCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<string> ItemCodes);

public class GetCollectionByShareCodeQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetCollectionByShareCodeQuery, Result<PublicCollectionDto>>
{
    public async Task<Result<PublicCollectionDto>> Handle(GetCollectionByShareCodeQuery request, CancellationToken ct)
    {
        var code = request.ShareCode.Trim();
        if (code.Length is 0 or > 16)
            return Result.Failure<PublicCollectionDto>("Koleksiyon bulunamadı.");

        var koleksiyon = await db.Collections
            .AsNoTracking()
            .Where(c => c.FirmPlatformId == request.FirmPlatformId
                     && c.ShareCode == code
                     && c.Status == "approved"
                     && c.IsShareable)
            .Select(c => new
            {
                c.Id, c.Name, c.Description, c.ViewCount, c.CreatedAt, c.UpdatedAt,
                ItemCodes = c.Items.Where(i => !i.IsDeleted)
                    .OrderBy(i => i.CreatedAt).Select(i => i.ProductCode).ToList(),
            })
            .FirstOrDefaultAsync(ct);

        if (koleksiyon is null)
            return Result.Failure<PublicCollectionDto>("Koleksiyon bulunamadı.");

        // Görüntülenme sayacı — atomik, entity yüklemeden (yarışta kayıp artış olmaz)
        await db.Collections
            .Where(c => c.Id == koleksiyon.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ViewCount, c => c.ViewCount + 1), ct);

        return Result.Success(new PublicCollectionDto(
            koleksiyon.Id, koleksiyon.Name, koleksiyon.Description,
            koleksiyon.ViewCount + 1, koleksiyon.CreatedAt, koleksiyon.UpdatedAt,
            koleksiyon.ItemCodes));
    }
}
