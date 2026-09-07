using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductTags;

public record GetProductTagsQuery() : IRequest<Result<List<string>>>;

public class GetProductTagsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductTagsQuery, Result<List<string>>>
{
    public async Task<Result<List<string>>> Handle(GetProductTagsQuery request, CancellationToken ct)
    {
        // Tags jsonb sütunu: `Tags.Count > 0` Npgsql'de cardinality(jsonb)'ye çevrilip 42883 veriyordu (2026-09-07 canlı log).
        // Boşluk filtresi bellekte yapılır; sütun küçük (etiket listesi), satır sayısı ürün sayısı kadar.
        var allTags = await db.Products
            .AsNoTracking()
            .Where(p => p.Tags != null)
            .Select(p => p.Tags)
            .ToListAsync(ct);

        var distinct = allTags
            .Where(t => t != null && t.Count > 0)
            .SelectMany(t => t)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        return Result.Success(distinct);
    }
}
