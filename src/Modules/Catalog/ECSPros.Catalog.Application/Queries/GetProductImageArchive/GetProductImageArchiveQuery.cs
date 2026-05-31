using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductImageArchive;

public record GetProductImageArchiveQuery(
    Guid ProductId,
    Guid? ImageSetId,
    Guid? BatchId) : IRequest<Result<List<ArchivedBatchDto>>>;

public record ArchivedImageItem(Guid Id, string FileName, int SortOrder, bool IsProductCover, bool IsVariantCover);
public record ArchivedBatchDto(Guid BatchId, Guid ImageSetId, string ImageSetCode, DateTime? ArchivedAt, List<ArchivedImageItem> Images);

public class GetProductImageArchiveQueryHandler : IRequestHandler<GetProductImageArchiveQuery, Result<List<ArchivedBatchDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductImageArchiveQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ArchivedBatchDto>>> Handle(GetProductImageArchiveQuery request, CancellationToken ct)
    {
        var query = _db.ProductImages
            .Include(x => x.ImageSet)
            .Where(x => x.ProductId == request.ProductId && x.Status == ProductImageStatus.Archived);

        if (request.ImageSetId.HasValue)
            query = query.Where(x => x.ImageSetId == request.ImageSetId.Value);

        if (request.BatchId.HasValue)
            query = query.Where(x => x.BatchId == request.BatchId.Value);

        var images = await query.ToListAsync(ct);

        var batches = images
            .GroupBy(x => x.BatchId)
            .Select(g => new ArchivedBatchDto(
                g.Key,
                g.First().ImageSetId,
                g.First().ImageSet.Code,
                g.Max(x => x.ArchivedAt),
                g.OrderBy(x => x.SortOrder)
                    .Select(x => new ArchivedImageItem(x.Id, x.FileName, x.SortOrder, x.IsProductCover, x.IsVariantCover))
                    .ToList()))
            .OrderByDescending(x => x.ArchivedAt)
            .ToList();

        return Result<List<ArchivedBatchDto>>.Success(batches);
    }
}
