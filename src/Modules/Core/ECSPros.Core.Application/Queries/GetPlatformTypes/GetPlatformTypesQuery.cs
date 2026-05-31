using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetPlatformTypes;

public record GetPlatformTypesQuery(bool ActiveOnly = true) : IRequest<Result<List<PlatformTypeDto>>>;

public record PlatformTypeDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsMarketplace,
    bool IsActive,
    List<PlatformSchemaField>? SettingsSchema
);

public class GetPlatformTypesQueryHandler : IRequestHandler<GetPlatformTypesQuery, Result<List<PlatformTypeDto>>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public GetPlatformTypesQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<PlatformTypeDto>>> Handle(GetPlatformTypesQuery request, CancellationToken ct)
    {
        var query = _db.PlatformTypes.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(p => p.IsActive);

        var list = await query.OrderBy(p => p.Code).ToListAsync(ct);

        return Result.Success(list.Select(p => new PlatformTypeDto(
            p.Id, p.Code, p.NameI18n, p.IsMarketplace, p.IsActive,
            string.IsNullOrEmpty(p.SettingsSchemaJson)
                ? null
                : JsonSerializer.Deserialize<List<PlatformSchemaField>>(p.SettingsSchemaJson, _json)
        )).ToList());
    }
}
