using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetCategoryDetail;

public record GetCategoryDetailQuery(Guid Id) : IRequest<Result<CategoryDetailDto>>;

public record CategoryDetailDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    Guid? ParentId,
    string FillType,
    Dictionary<string, object>? FilterRules,
    bool IsActive,
    int SortOrder);

public class GetCategoryDetailQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetCategoryDetailQuery, Result<CategoryDetailDto>>
{
    public async Task<Result<CategoryDetailDto>> Handle(GetCategoryDetailQuery request, CancellationToken ct)
    {
        var cat = await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (cat is null) return Result.Failure<CategoryDetailDto>("Kategori bulunamadı.");

        return Result.Success(new CategoryDetailDto(
            cat.Id, cat.Code, cat.NameI18n,
            cat.ParentId, cat.FillType, cat.FilterRules,
            cat.IsActive, cat.SortOrder));
    }
}
