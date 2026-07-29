using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetIntegrationServices;

public record GetIntegrationServicesQuery(string? ServiceType = null) : IRequest<Result<List<IntegrationServiceDto>>>;

public record IntegrationServiceDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string ServiceType,
    bool IsAvailable,
    string? LogoUrl = null,             // H2 additive: kargo servislerinde logo
    string? TrackingUrlTemplate = null, // H2 additive: {trackingNumber} yer tutuculu takip linki
    List<PlatformSchemaField>? SettingsSchema = null, // admin form alanları (credentials/settings ayrımı)
    // F3 additive: kargo kod stratejisi alanları (yalnız ServiceType=cargo dolu olur)
    string? CargoCodeStrategy = null,
    int? CargoCodeMinLength = null,
    int? CargoCodeMaxLength = null,
    string? CargoCodeCharset = null
);

public class GetIntegrationServicesQueryHandler : IRequestHandler<GetIntegrationServicesQuery, Result<List<IntegrationServiceDto>>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public GetIntegrationServicesQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<IntegrationServiceDto>>> Handle(GetIntegrationServicesQuery request, CancellationToken ct)
    {
        var query = _db.IntegrationServices.AsQueryable();
        if (!string.IsNullOrEmpty(request.ServiceType))
            query = query.Where(s => s.ServiceType == request.ServiceType);

        var kayitlar = await query
            .OrderBy(s => s.ServiceType).ThenBy(s => s.Code)
            .Select(s => new
            {
                s.Id, s.Code, s.NameI18n, s.ServiceType, s.IsAvailable,
                s.LogoUrl, s.TrackingUrlTemplate, s.SettingsSchemaJson,
                s.CargoCodeStrategy, s.CargoCodeMinLength, s.CargoCodeMaxLength, s.CargoCodeCharset
            })
            .ToListAsync(ct);

        var list = kayitlar.Select(s => new IntegrationServiceDto(
            s.Id, s.Code, s.NameI18n, s.ServiceType, s.IsAvailable, s.LogoUrl, s.TrackingUrlTemplate,
            DeserializeSchema(s.SettingsSchemaJson),
            s.CargoCodeStrategy, s.CargoCodeMinLength, s.CargoCodeMaxLength, s.CargoCodeCharset)).ToList();

        return Result.Success<List<IntegrationServiceDto>>(list);
    }

    private static List<PlatformSchemaField>? DeserializeSchema(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            // Eski biçim ({"fields":[...]} sözlüğü) diziye çevrilemez — null döner, form
            // serbest key-value editörüne düşer (bozuk şema admin'i kilitlemesin).
            return JsonSerializer.Deserialize<List<PlatformSchemaField>>(json, _json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
