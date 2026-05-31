using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetImageSets;

public record GetImageSetsQuery(bool ActiveOnly = true) : IRequest<Result<List<ImageSetDto>>>;

public record ImageSetDto(
    Guid Id,
    string Code,
    string Name,
    bool IsDefault,
    Guid? FallbackSetId,
    string? FallbackSetName,
    int SortPriority,
    bool IsActive);

public class GetImageSetsQueryHandler : IRequestHandler<GetImageSetsQuery, Result<List<ImageSetDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetImageSetsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ImageSetDto>>> Handle(GetImageSetsQuery request, CancellationToken ct)
    {
        var query = _db.ImageSets
            .Include(x => x.FallbackSet)
            .AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(x => x.IsActive);

        var sets = await query
            .OrderBy(x => x.SortPriority)
            .Select(x => new ImageSetDto(
                x.Id,
                x.Code,
                x.Name,
                x.IsDefault,
                x.FallbackSetId,
                x.FallbackSet != null ? x.FallbackSet.Name : null,
                x.SortPriority,
                x.IsActive))
            .ToListAsync(ct);

        return Result<List<ImageSetDto>>.Success(sets);
    }
}
