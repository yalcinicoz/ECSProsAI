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
        var allTags = await db.Products
            .AsNoTracking()
            .Where(p => p.Tags != null && p.Tags.Count > 0)
            .Select(p => p.Tags)
            .ToListAsync(ct);

        var distinct = allTags
            .SelectMany(t => t)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        return Result.Success(distinct);
    }
}
