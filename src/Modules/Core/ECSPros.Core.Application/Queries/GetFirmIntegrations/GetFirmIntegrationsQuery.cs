using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetFirmIntegrations;

public record GetFirmIntegrationsQuery(Guid FirmId, string? ServiceType = null) : IRequest<Result<List<FirmIntegrationDto>>>;

public record FirmIntegrationDto(
    Guid Id,
    Guid FirmId,
    Guid IntegrationServiceId,
    string ServiceCode,
    string ServiceType,
    string? Name,
    bool IsActive,
    DateTime CreatedAt
);

public class GetFirmIntegrationsQueryHandler : IRequestHandler<GetFirmIntegrationsQuery, Result<List<FirmIntegrationDto>>>
{
    private readonly ICoreDbContext _db;

    public GetFirmIntegrationsQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<FirmIntegrationDto>>> Handle(GetFirmIntegrationsQuery request, CancellationToken ct)
    {
        var query = _db.FirmIntegrations
            .Include(fi => fi.IntegrationService)
            .Where(fi => fi.FirmId == request.FirmId);

        if (!string.IsNullOrEmpty(request.ServiceType))
            query = query.Where(fi => fi.IntegrationService.ServiceType == request.ServiceType);

        var list = await query
            .OrderBy(fi => fi.IntegrationService.ServiceType)
            .Select(fi => new FirmIntegrationDto(
                fi.Id, fi.FirmId, fi.IntegrationServiceId,
                fi.IntegrationService.Code, fi.IntegrationService.ServiceType,
                fi.Name, fi.IsActive, fi.CreatedAt))
            .ToListAsync(ct);

        return Result.Success<List<FirmIntegrationDto>>(list);
    }
}
