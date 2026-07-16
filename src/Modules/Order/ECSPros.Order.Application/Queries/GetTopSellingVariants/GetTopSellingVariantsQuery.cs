using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetTopSellingVariants;

/// <summary>
/// G3: "çok satanlar" ürün kaynağı — son N gündeki geçerli siparişlerin (iptal hariç;
/// pending henüz satış sayılmaz) kalem adetlerini varyant bazında toplar. Varyant→ürün
/// çözümü çağıranın işidir (kalemler snapshot, ürün kodu taşımaz — API katmanı
/// IProductService ile koda çevirir, E7/E8 deseni).
/// </summary>
public record GetTopSellingVariantsQuery(
    Guid FirmPlatformId,
    int Days = 90,
    int Limit = 50) : IRequest<Result<List<TopSellingVariantDto>>>;

public record TopSellingVariantDto(Guid VariantId, int TotalQuantity);

public class GetTopSellingVariantsQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetTopSellingVariantsQuery, Result<List<TopSellingVariantDto>>>
{
    private static readonly string[] SoldStatuses = ["confirmed", "processing", "shipped", "delivered"];

    public async Task<Result<List<TopSellingVariantDto>>> Handle(GetTopSellingVariantsQuery request, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, request.Days));

        // EF, GroupBy sonrası record constructor projeksiyonunu SQL'e çeviremiyor
        // (ilk canlı kullanımda yakalandı) — anonim projeksiyonla çekilip bellekte map'lenir.
        var items = await db.OrderItems
            .AsNoTracking()
            .Where(i => db.Orders.Any(o => o.Id == i.OrderId
                && o.FirmPlatformId == request.FirmPlatformId
                && SoldStatuses.Contains(o.Status)
                && o.CreatedAt >= cutoff))
            .GroupBy(i => i.VariantId)
            .Select(g => new { VariantId = g.Key, TotalQuantity = g.Sum(i => i.Quantity) })
            .OrderByDescending(x => x.TotalQuantity)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .ToListAsync(ct);

        return Result.Success(items.Select(x => new TopSellingVariantDto(x.VariantId, x.TotalQuantity)).ToList());
    }
}
