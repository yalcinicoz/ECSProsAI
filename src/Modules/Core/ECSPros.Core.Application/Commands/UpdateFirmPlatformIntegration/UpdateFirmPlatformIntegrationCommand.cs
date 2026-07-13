using ECSPros.Core.Application.Common;
using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateFirmPlatformIntegration;

/// <summary>
/// Credentials'ta MaskedValue ("•••") gelen anahtarların saklı değeri korunur —
/// GET yanıtı maskeli döndüğünden admin değiştirmediği alanı maskeyle geri yollar.
/// </summary>
public record UpdateFirmPlatformIntegrationCommand(
    Guid Id,
    string? Name,
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings,
    bool IsActive,
    Guid? FirmPlatformId,
    DateTime? StartDate,
    DateTime? EndDate,
    string Status,
    Dictionary<string, object>? Terms
) : IRequest<Result<bool>>;

public class UpdateFirmPlatformIntegrationCommandHandler
    : IRequestHandler<UpdateFirmPlatformIntegrationCommand, Result<bool>>
{
    private readonly ICoreDbContext _db;

    public UpdateFirmPlatformIntegrationCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateFirmPlatformIntegrationCommand request, CancellationToken ct)
    {
        var integration = await _db.FirmPlatformIntegrations.FirstOrDefaultAsync(fi => fi.Id == request.Id, ct);
        if (integration is null)
            return Result.Failure<bool>("Firma entegrasyonu bulunamadı.");

        if (request.FirmPlatformId.HasValue)
        {
            var platformOk = await _db.FirmPlatforms.AnyAsync(
                p => p.Id == request.FirmPlatformId.Value && p.FirmId == integration.FirmId, ct);
            if (!platformOk)
                return Result.Failure<bool>("Platform bulunamadı veya bu firmaya ait değil.");
        }

        integration.Name = request.Name;
        integration.Credentials = CredentialsMasking.MergeMasked(request.Credentials, integration.Credentials);
        integration.Settings = request.Settings;
        integration.IsActive = request.IsActive;
        integration.FirmPlatformId = request.FirmPlatformId;
        integration.StartDate = AsUtc(request.StartDate);
        integration.EndDate = AsUtc(request.EndDate);
        integration.Status = request.Status;
        integration.Terms = request.Terms;
        integration.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success<bool>(true);
    }

    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
