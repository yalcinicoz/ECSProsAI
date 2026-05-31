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
    Guid? FilterPresetId,
    FilterPresetInfoDto? FilterPreset,
    Dictionary<string, object>? FilterRules,
    bool IsActive,
    int SortOrder);

public record FilterPresetInfoDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Description,
    Dictionary<string, object> FilterDef);

public class GetCategoryDetailQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetCategoryDetailQuery, Result<CategoryDetailDto>>
{
    public async Task<Result<CategoryDetailDto>> Handle(GetCategoryDetailQuery request, CancellationToken ct)
    {
        var cat = await db.Categories
            .AsNoTracking()
            .Include(c => c.FilterPreset)
            .FirstOrDefaultAsync(c => c.Id == request.Id, ct);

        if (cat is null) return Result.Failure<CategoryDetailDto>("Kategori bulunamadı.");

        var presetDto = cat.FilterPreset is null ? null : new FilterPresetInfoDto(
            cat.FilterPreset.Id, cat.FilterPreset.Code, cat.FilterPreset.NameI18n,
            cat.FilterPreset.Description, cat.FilterPreset.FilterDef);

        return Result.Success(new CategoryDetailDto(
            cat.Id, cat.Code, cat.NameI18n,
            cat.ParentId, cat.FillType,
            cat.FilterPresetId, presetDto, cat.FilterRules,
            cat.IsActive, cat.SortOrder));
    }
}
