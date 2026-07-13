using ECSPros.Core.Application.Common;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetFirmPlatformIntegrations;

public record GetFirmPlatformIntegrationsQuery(Guid FirmId, string? ServiceType = null)
    : IRequest<Result<List<FirmPlatformIntegrationDto>>>;

/// <summary>Credentials maskeli döner (anahtarlar görünür, değerler "•••") — gerçek
/// değerler yalnız DB'de (şifreli) yaşar, admin'e geri inmez.</summary>
public record FirmPlatformIntegrationDto(
    Guid Id,
    Guid FirmId,
    Guid IntegrationServiceId,
    string ServiceCode,
    Dictionary<string, string> ServiceNameI18n,
    string ServiceType,
    Guid? FirmPlatformId,
    Dictionary<string, string>? FirmPlatformNameI18n,
    string? Name,
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? StartDate,
    DateTime? EndDate,
    string Status,
    Dictionary<string, object>? Terms
);

public class GetFirmPlatformIntegrationsQueryHandler
    : IRequestHandler<GetFirmPlatformIntegrationsQuery, Result<List<FirmPlatformIntegrationDto>>>
{
    private readonly ICoreDbContext _db;

    public GetFirmPlatformIntegrationsQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<FirmPlatformIntegrationDto>>> Handle(
        GetFirmPlatformIntegrationsQuery request, CancellationToken ct)
    {
        var query = _db.FirmPlatformIntegrations
            .Include(fi => fi.IntegrationService)
            .Where(fi => fi.FirmId == request.FirmId);

        if (!string.IsNullOrEmpty(request.ServiceType))
            query = query.Where(fi => fi.IntegrationService.ServiceType == request.ServiceType);

        var list = await query
            .OrderBy(fi => fi.IntegrationService.ServiceType)
            .Select(fi => new FirmPlatformIntegrationDto(
                fi.Id, fi.FirmId, fi.IntegrationServiceId,
                fi.IntegrationService.Code, fi.IntegrationService.NameI18n, fi.IntegrationService.ServiceType,
                fi.FirmPlatformId,
                fi.FirmPlatform != null ? fi.FirmPlatform.NameI18n : null,
                fi.Name, fi.Credentials, fi.Settings, fi.IsActive, fi.CreatedAt,
                fi.StartDate, fi.EndDate, fi.Status, fi.Terms))
            .ToListAsync(ct);

        return Result.Success<List<FirmPlatformIntegrationDto>>(
            list.Select(dto => dto with { Credentials = CredentialsMasking.Mask(dto.Credentials) }).ToList());
    }
}
