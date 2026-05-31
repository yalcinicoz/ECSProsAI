using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductImageSetMappings;

public record GetProductImageSetMappingsQuery(Guid ProductId) : IRequest<Result<List<ProductImageSetMappingDto>>>;

public record ProductImageSetMappingDto(Guid Id, Guid ForSetId, string ForSetName, Guid UseSetId, string UseSetName);

public class GetProductImageSetMappingsQueryHandler
    : IRequestHandler<GetProductImageSetMappingsQuery, Result<List<ProductImageSetMappingDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetProductImageSetMappingsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<ProductImageSetMappingDto>>> Handle(GetProductImageSetMappingsQuery request, CancellationToken ct)
    {
        var mappings = await _db.ProductImageSetMappings
            .Include(x => x.ForSet)
            .Include(x => x.UseSet)
            .Where(x => x.ProductId == request.ProductId)
            .Select(x => new ProductImageSetMappingDto(x.Id, x.ForSetId, x.ForSet.Name, x.UseSetId, x.UseSet.Name))
            .ToListAsync(ct);

        return Result<List<ProductImageSetMappingDto>>.Success(mappings);
    }
}
