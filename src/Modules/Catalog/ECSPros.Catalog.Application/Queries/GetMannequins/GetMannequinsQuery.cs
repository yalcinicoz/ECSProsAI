using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetMannequins;

public record GetMannequinsQuery(bool ActiveOnly = true) : IRequest<Result<List<MannequinDto>>>;

public record MannequinDto(
    Guid Id,
    string? Code,
    string FirstName,
    string? LastName,
    string? Gender,
    int? HeightCm,
    int? WeightKg,
    int? ChestCm,
    int? WaistCm,
    int? HipCm,
    string? DefaultWornSize,
    bool IsActive,
    string? Notes);

public class GetMannequinsQueryHandler : IRequestHandler<GetMannequinsQuery, Result<List<MannequinDto>>>
{
    private readonly ICatalogDbContext _db;

    public GetMannequinsQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<List<MannequinDto>>> Handle(GetMannequinsQuery request, CancellationToken ct)
    {
        var query = _db.Mannequins.AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(x => x.IsActive);

        var mannequins = await query
            .OrderBy(x => x.FirstName)
            .Select(x => new MannequinDto(
                x.Id, x.Code, x.FirstName, x.LastName, x.Gender,
                x.HeightCm, x.WeightKg, x.ChestCm, x.WaistCm, x.HipCm,
                x.DefaultWornSize, x.IsActive, x.Notes))
            .ToListAsync(ct);

        return Result<List<MannequinDto>>.Success(mannequins);
    }
}
