using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
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
    Dictionary<string, string> PlatformTypeNameI18n,
    bool PlatformTypeIsMarketplace,
    List<PlatformSchemaField>? SettingsSchema,
    string Code,
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier,
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings,
    bool IsActive,
    DateTime CreatedAt
);

public class GetFirmPlatformsQueryHandler : IRequestHandler<GetFirmPlatformsQuery, Result<List<FirmPlatformDto>>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public GetFirmPlatformsQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<FirmPlatformDto>>> Handle(GetFirmPlatformsQuery request, CancellationToken ct)
    {
        var list = await _db.FirmPlatforms
            .Include(fp => fp.PlatformType)
            .Where(fp => fp.FirmId == request.FirmId)
            .OrderBy(fp => fp.Code)
            .ToListAsync(ct);

        return Result.Success(list.Select(fp => new FirmPlatformDto(
            fp.Id, fp.FirmId,
            fp.PlatformTypeId, fp.PlatformType.Code, fp.PlatformType.NameI18n, fp.PlatformType.IsMarketplace,
            string.IsNullOrEmpty(fp.PlatformType.SettingsSchemaJson)
                ? null
                : JsonSerializer.Deserialize<List<PlatformSchemaField>>(fp.PlatformType.SettingsSchemaJson, _json),
            fp.Code, fp.NameI18n, fp.PriceType, fp.PriceMultiplier,
            fp.Credentials, fp.Settings,
            fp.IsActive, fp.CreatedAt
        )).ToList());
    }
}
