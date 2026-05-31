using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetCatalogSettings;

public record GetCatalogSettingsQuery : IRequest<Result<List<CatalogSettingDto>>>;

public record CatalogSettingDto(string Key, string Value);

public class GetCatalogSettingsQueryHandler : IRequestHandler<GetCatalogSettingsQuery, Result<List<CatalogSettingDto>>>
{
    private readonly ICatalogDbContext _db;
    public GetCatalogSettingsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<CatalogSettingDto>>> Handle(GetCatalogSettingsQuery request, CancellationToken ct)
    {
        var settings = await _db.CatalogSettings
            .Select(s => new CatalogSettingDto(s.Key, s.Value))
            .ToListAsync(ct);
        return Result.Success(settings);
    }
}
