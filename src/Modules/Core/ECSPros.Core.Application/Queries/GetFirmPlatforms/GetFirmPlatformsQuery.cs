using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetFirmPlatforms;

public record GetFirmPlatformsQuery(Guid FirmId) : IRequest<Result<List<FirmPlatformDto>>>;

public record FirmPlatformDto(
    Guid Id,
    Guid FirmId,
    Guid PlatformTypeId,
    string PlatformTypeCode,
    string Code,
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier,
    bool IsActive,
    DateTime CreatedAt
);

public class GetFirmPlatformsQueryHandler : IRequestHandler<GetFirmPlatformsQuery, Result<List<FirmPlatformDto>>>
{
    private readonly ICoreDbContext _db;

    public GetFirmPlatformsQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<FirmPlatformDto>>> Handle(GetFirmPlatformsQuery request, CancellationToken ct)
    {
        var list = await _db.FirmPlatforms
            .Include(fp => fp.PlatformType)
            .Where(fp => fp.FirmId == request.FirmId)
            .OrderBy(fp => fp.Code)
            .Select(fp => new FirmPlatformDto(
                fp.Id, fp.FirmId, fp.PlatformTypeId, fp.PlatformType.Code,
                fp.Code, fp.NameI18n, fp.PriceType, fp.PriceMultiplier,
                fp.IsActive, fp.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<List<FirmPlatformDto>>(list);
    }
}
