using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetCategories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, Result<List<CategoryDto>>>
{
    private readonly ICatalogDbContext _context;

    public GetCategoriesQueryHandler(ICatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Categories.AsQueryable();

        if (request.ParentId.HasValue)
            query = query.Where(x => x.ParentId == request.ParentId);
        else
            query = query.Where(x => x.ParentId == null);

        if (request.ActiveOnly)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new CategoryDto(x.Id, x.Code, x.NameI18n, x.ParentId, x.IsActive, x.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
