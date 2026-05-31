using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductVideos;

public record GetProductVideosQuery(
    Guid ProductId,
    Guid? ImageSetId) : IRequest<Result<List<ProductVideoDto>>>;

public record ProductVideoDto(
    Guid Id,
    Guid ProductId,
    Guid ImageSetId,
    string ImageSetCode,
    string FileName,
    string? ThumbnailFileName,
    int SortOrder,
    string Status,
    Guid BatchId);

public class GetProductVideosQueryHandler : IRequestHandler<GetProductVideosQuery, Result<List<ProductVideoDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductVideosQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ProductVideoDto>>> Handle(GetProductVideosQuery request, CancellationToken ct)
    {
        var query = _db.ProductVideos
            .Include(x => x.ImageSet)
            .Where(x => x.ProductId == request.ProductId && x.Status == ProductImageStatus.Active);

        if (request.ImageSetId.HasValue)
            query = query.Where(x => x.ImageSetId == request.ImageSetId.Value);

        var videos = await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new ProductVideoDto(
                x.Id,
                x.ProductId,
                x.ImageSetId,
                x.ImageSet.Code,
                x.FileName,
                x.ThumbnailFileName,
                x.SortOrder,
                x.Status.ToString(),
                x.BatchId))
            .ToListAsync(ct);

        return Result<List<ProductVideoDto>>.Success(videos);
    }
}
