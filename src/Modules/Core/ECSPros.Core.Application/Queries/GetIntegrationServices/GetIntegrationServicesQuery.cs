using ECSPros.Core.Application.Services;
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
    bool IsAvailable
);

public class GetIntegrationServicesQueryHandler : IRequestHandler<GetIntegrationServicesQuery, Result<List<IntegrationServiceDto>>>
{
    private readonly ICoreDbContext _db;

    public GetIntegrationServicesQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<IntegrationServiceDto>>> Handle(GetIntegrationServicesQuery request, CancellationToken ct)
    {
        var query = _db.IntegrationServices.AsQueryable();
        if (!string.IsNullOrEmpty(request.ServiceType))
            query = query.Where(s => s.ServiceType == request.ServiceType);

        var list = await query
            .OrderBy(s => s.ServiceType).ThenBy(s => s.Code)
            .Select(s => new IntegrationServiceDto(s.Id, s.Code, s.NameI18n, s.ServiceType, s.IsAvailable))
            .ToListAsync(ct);

        return Result.Success<List<IntegrationServiceDto>>(list);
    }
}
