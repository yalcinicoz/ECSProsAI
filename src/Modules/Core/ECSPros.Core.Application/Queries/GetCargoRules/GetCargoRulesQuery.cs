using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetCargoRules;

public record GetCargoRulesQuery(Guid FirmId) : IRequest<Result<List<CargoRuleDto>>>;

public record CargoRuleDto(
    Guid Id,
    Guid FirmId,
    Guid FirmIntegrationId,
    string? IntegrationName,
    string RuleType,
    string? PaymentType,
    Guid? NeighborhoodId,
    Guid? CityId,
    int Priority,
    bool IsActive
);

public class GetCargoRulesQueryHandler : IRequestHandler<GetCargoRulesQuery, Result<List<CargoRuleDto>>>
{
    private readonly ICoreDbContext _db;

    public GetCargoRulesQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<CargoRuleDto>>> Handle(GetCargoRulesQuery request, CancellationToken ct)
    {
        var list = await _db.CargoRules
            .Include(r => r.FirmIntegration)
            .Where(r => r.FirmId == request.FirmId)
            .OrderByDescending(r => r.Priority)
            .Select(r => new CargoRuleDto(
                r.Id, r.FirmId, r.FirmIntegrationId, r.FirmIntegration.Name,
                r.RuleType, r.PaymentType, r.NeighborhoodId, r.CityId,
                r.Priority, r.IsActive))
            .ToListAsync(ct);

        return Result.Success<List<CargoRuleDto>>(list);
    }
}
