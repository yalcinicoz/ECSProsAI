using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetStoreCatalog;

public record GetStoreCatalogQuery(Guid FirmPlatformId) : IRequest<Result<StoreCatalogDto>>;

public record StoreCatalogDto(
    List<StoreCategoryDto> Categories);

public record StoreCategoryDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    int SortOrder,
    List<StoreCategoryDto> Children);

public class GetStoreCatalogQueryHandler(ICatalogDbContext db) : IRequestHandler<GetStoreCatalogQuery, Result<StoreCatalogDto>>
{
    public async Task<Result<StoreCatalogDto>> Handle(GetStoreCatalogQuery request, CancellationToken ct)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .ToListAsync(ct);

        var roots = BuildTree(categories, null);
        return Result.Success(new StoreCatalogDto(roots));
    }

    private static List<StoreCategoryDto> BuildTree(
        List<ECSPros.Catalog.Domain.Entities.Category> all, Guid? parentId)
    {
        return all
            .Where(c => c.ParentId == parentId)
            .Select(c => new StoreCategoryDto(
                c.Id, c.Code, c.NameI18n, c.ParentId, c.SortOrder,
                BuildTree(all, c.Id)))
            .ToList();
    }
}
