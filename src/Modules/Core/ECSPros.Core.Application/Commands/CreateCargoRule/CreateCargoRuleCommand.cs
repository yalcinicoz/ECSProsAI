using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreateCargoRule;

public record CreateCargoRuleCommand(
    Guid FirmId,
    Guid FirmIntegrationId,
    string RuleType,
    string? PaymentType,
    Guid? NeighborhoodId,
    Guid? CityId,
    int Priority
) : IRequest<Result<Guid>>;

public class CreateCargoRuleCommandHandler : IRequestHandler<CreateCargoRuleCommand, Result<Guid>>
{
    private readonly ICoreDbContext _db;

    public CreateCargoRuleCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateCargoRuleCommand request, CancellationToken ct)
    {
        var integrationExists = await _db.FirmIntegrations
            .AnyAsync(fi => fi.Id == request.FirmIntegrationId && fi.FirmId == request.FirmId, ct);
        if (!integrationExists)
            return Result.Failure<Guid>("Firma entegrasyonu bulunamadı.");

        var rule = new CargoRule
        {
            Id = Guid.NewGuid(),
            FirmId = request.FirmId,
            FirmIntegrationId = request.FirmIntegrationId,
            RuleType = request.RuleType,
            PaymentType = request.PaymentType,
            NeighborhoodId = request.NeighborhoodId,
            CityId = request.CityId,
            Priority = request.Priority,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.CargoRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(rule.Id);
    }
}
