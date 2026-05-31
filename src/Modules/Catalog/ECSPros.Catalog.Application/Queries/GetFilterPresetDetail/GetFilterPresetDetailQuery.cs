using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetFilterPresetDetail;

public record GetFilterPresetDetailQuery(Guid Id) : IRequest<Result<FilterPresetDetailDto>>;

public record FilterPresetDetailDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? Description,
    Dictionary<string, object> FilterDef,
    bool IsActive,
    int SortOrder);

public class GetFilterPresetDetailQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetFilterPresetDetailQuery, Result<FilterPresetDetailDto>>
{
    public async Task<Result<FilterPresetDetailDto>> Handle(GetFilterPresetDetailQuery request, CancellationToken ct)
    {
        var fp = await db.FilterPresets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (fp is null) return Result.Failure<FilterPresetDetailDto>("Filtre şablonu bulunamadı.");

        return Result.Success(new FilterPresetDetailDto(
            fp.Id, fp.Code, fp.NameI18n, fp.Description, fp.FilterDef, fp.IsActive, fp.SortOrder));
    }
}
