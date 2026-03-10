using ECSPros.Crm.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetCountries;

public record GetCountriesQuery(bool ActiveOnly = true) : IRequest<Result<List<CountryDto>>>;

public record CountryDto(Guid Id, string Code, Dictionary<string, string> NameI18n, string PhoneCode, bool IsActive, int SortOrder);

public class GetCountriesQueryHandler : IRequestHandler<GetCountriesQuery, Result<List<CountryDto>>>
{
    private readonly ICrmDbContext _db;

    public GetCountriesQueryHandler(ICrmDbContext db) => _db = db;

    public async Task<Result<List<CountryDto>>> Handle(GetCountriesQuery request, CancellationToken ct)
    {
        var query = _db.Countries.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(c => c.IsActive);

        var items = await query
            .OrderBy(c => c.SortOrder)
            .Select(c => new CountryDto(c.Id, c.Code, c.NameI18n, c.PhoneCode, c.IsActive, c.SortOrder))
            .ToListAsync(ct);

        return Result.Success(items);
    }
}
