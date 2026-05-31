using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductVideoArchive;

public record GetProductVideoArchiveQuery(
    Guid ProductId,
    Guid? ImageSetId,
    Guid? BatchId) : IRequest<Result<List<ArchivedVideoBatchDto>>>;

public record ArchivedVideoItem(Guid Id, string FileName, string? ThumbnailFileName, int SortOrder);
public record ArchivedVideoBatchDto(Guid BatchId, Guid ImageSetId, string ImageSetCode, DateTime? ArchivedAt, List<ArchivedVideoItem> Videos);

public class GetProductVideoArchiveQueryHandler : IRequestHandler<GetProductVideoArchiveQuery, Result<List<ArchivedVideoBatchDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductVideoArchiveQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ArchivedVideoBatchDto>>> Handle(GetProductVideoArchiveQuery request, CancellationToken ct)
    {
        var query = _db.ProductVideos
            .Include(x => x.ImageSet)
            .Where(x => x.ProductId == request.ProductId && x.Status == ProductImageStatus.Archived);

        if (request.ImageSetId.HasValue)
            query = query.Where(x => x.ImageSetId == request.ImageSetId.Value);

        if (request.BatchId.HasValue)
            query = query.Where(x => x.BatchId == request.BatchId.Value);

        var videos = await query.ToListAsync(ct);

        var batches = videos
            .GroupBy(x => x.BatchId)
            .Select(g => new ArchivedVideoBatchDto(
                g.Key,
                g.First().ImageSetId,
                g.First().ImageSet.Code,
                g.Max(x => x.ArchivedAt),
                g.OrderBy(x => x.SortOrder)
                    .Select(x => new ArchivedVideoItem(x.Id, x.FileName, x.ThumbnailFileName, x.SortOrder))
                    .ToList()))
            .OrderByDescending(x => x.ArchivedAt)
            .ToList();

        return Result<List<ArchivedVideoBatchDto>>.Success(batches);
    }
}
