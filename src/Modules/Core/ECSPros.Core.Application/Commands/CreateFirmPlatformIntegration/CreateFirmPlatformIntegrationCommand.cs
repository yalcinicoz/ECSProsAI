using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreateFirmPlatformIntegration;

/// <summary>
/// FirmPlatformId null → firma geneli (tüm platformlar); dolu → yalnız o platforma özel.
/// Credentials DB'ye şifreli yazılır (Infrastructure converter).
/// </summary>
public record CreateFirmPlatformIntegrationCommand(
    Guid FirmId,
    Guid IntegrationServiceId,
    string? Name,
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings,
    Guid? FirmPlatformId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string Status = "draft",
    Dictionary<string, object>? Terms = null
) : IRequest<Result<Guid>>;

public class CreateFirmPlatformIntegrationCommandHandler
    : IRequestHandler<CreateFirmPlatformIntegrationCommand, Result<Guid>>
{
    private readonly ICoreDbContext _db;

    public CreateFirmPlatformIntegrationCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateFirmPlatformIntegrationCommand request, CancellationToken ct)
    {
        var firmExists = await _db.Firms.AnyAsync(f => f.Id == request.FirmId, ct);
        if (!firmExists)
            return Result.Failure<Guid>("Firma bulunamadı.");

        var serviceExists = await _db.IntegrationServices.AnyAsync(s => s.Id == request.IntegrationServiceId, ct);
        if (!serviceExists)
            return Result.Failure<Guid>("Entegrasyon servisi bulunamadı.");

        if (request.FirmPlatformId.HasValue)
        {
            var platformOk = await _db.FirmPlatforms.AnyAsync(
                p => p.Id == request.FirmPlatformId.Value && p.FirmId == request.FirmId, ct);
            if (!platformOk)
                return Result.Failure<Guid>("Platform bulunamadı veya bu firmaya ait değil.");
        }

        var integration = new FirmPlatformIntegration
        {
            Id = Guid.NewGuid(),
            FirmId = request.FirmId,
            IntegrationServiceId = request.IntegrationServiceId,
            FirmPlatformId = request.FirmPlatformId,
            Name = request.Name,
            Credentials = request.Credentials,
            Settings = request.Settings,
            IsActive = true,
            StartDate = AsUtc(request.StartDate),
            EndDate = AsUtc(request.EndDate),
            Status = request.Status,
            Terms = request.Terms,
            CreatedAt = DateTime.UtcNow
        };

        _db.FirmPlatformIntegrations.Add(integration);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(integration.Id);
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
