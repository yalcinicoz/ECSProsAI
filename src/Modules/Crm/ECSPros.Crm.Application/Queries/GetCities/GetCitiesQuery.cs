using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetCities;

public record GetCitiesQuery(Guid CountryId, bool ActiveOnly = true) : IRequest<Result<List<CityDto>>>;

public record CityDto(Guid Id, Guid CountryId, string Code, Dictionary<string, string> NameI18n, bool IsActive, int SortOrder);

public class GetCitiesQueryHandler : IRequestHandler<GetCitiesQuery, Result<List<CityDto>>>
{
    private readonly ICrmDbContext _db;

    public GetCitiesQueryHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<List<CityDto>>> Handle(GetCitiesQuery request, CancellationToken ct)
    {
        var query = _db.Cities.Where(c => c.CountryId == request.CountryId);
        if (request.ActiveOnly)
            query = query.Where(c => c.IsActive);

        var items = await query
            .OrderBy(c => c.SortOrder)
            .Select(c => new CityDto(c.Id, c.CountryId, c.Code, c.NameI18n, c.IsActive, c.SortOrder))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
