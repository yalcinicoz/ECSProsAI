using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetDistricts;

public record GetDistrictsQuery(Guid CityId, bool ActiveOnly = true) : IRequest<Result<List<DistrictDto>>>;

public record DistrictDto(Guid Id, Guid CityId, string Code, Dictionary<string, string> NameI18n, bool IsActive, int SortOrder);

public class GetDistrictsQueryHandler : IRequestHandler<GetDistrictsQuery, Result<List<DistrictDto>>>
{
    private readonly ICrmDbContext _db;

    public GetDistrictsQueryHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<List<DistrictDto>>> Handle(GetDistrictsQuery request, CancellationToken ct)
    {
        var query = _db.Districts.Where(d => d.CityId == request.CityId);
        if (request.ActiveOnly)
            query = query.Where(d => d.IsActive);

        var items = await query
            .OrderBy(d => d.SortOrder)
            .Select(d => new DistrictDto(d.Id, d.CityId, d.Code, d.NameI18n, d.IsActive, d.SortOrder))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
