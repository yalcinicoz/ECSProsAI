using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetFirms;

public record GetFirmsQuery(bool ActiveOnly = false) : IRequest<Result<List<FirmDto>>>;

public record FirmDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string TaxOffice,
    string TaxNumber,
    string Address,
    string Phone,
    string Email,
    bool IsMain,
    string PriceType,
    decimal? PriceMultiplier,
    bool IsActive,
    DateTime CreatedAt
);

public class GetFirmsQueryHandler : IRequestHandler<GetFirmsQuery, Result<List<FirmDto>>>
{
    private readonly ICoreDbContext _db;

    public GetFirmsQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<FirmDto>>> Handle(GetFirmsQuery request, CancellationToken ct)
    {
        var query = _db.Firms.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(f => f.IsActive);

        var firms = await query
            .OrderBy(f => f.Code)
            .Select(f => new FirmDto(f.Id, f.Code, f.NameI18n, f.TaxOffice, f.TaxNumber,
                f.Address, f.Phone, f.Email, f.IsMain, f.PriceType, f.PriceMultiplier,
                f.IsActive, f.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<List<FirmDto>>(firms);
    }
}
