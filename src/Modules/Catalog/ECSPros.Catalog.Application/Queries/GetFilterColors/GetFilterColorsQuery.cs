using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetFilterColors;

public record GetFilterColorsQuery : IRequest<Result<List<FilterColorDto>>>;

public record FilterColorDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string? HexCode,
    int SortOrder,
    bool IsActive
);

public class GetFilterColorsQueryHandler : IRequestHandler<GetFilterColorsQuery, Result<List<FilterColorDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetFilterColorsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<FilterColorDto>>> Handle(GetFilterColorsQuery request, CancellationToken cancellationToken)
    {
        var colors = await _db.FilterColors
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .Select(c => new FilterColorDto(c.Id, c.Code, c.NameI18n, c.HexCode, c.SortOrder, c.IsActive))
            .ToListAsync(cancellationToken);

        return Result<List<FilterColorDto>>.Success(colors);
    }
}
