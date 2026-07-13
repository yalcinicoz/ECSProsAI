using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateIntegrationService;

/// <summary>
/// Code ve ServiceType bilinçli olarak DEĞİŞTİRİLEMEZ — kod, seed'lerin ve çözümleme
/// sorgularının (ServiceType=cargo/email/visual_search) kimliğidir; mevcut firma
/// entegrasyonları bu kimliğe bağlıdır.
/// </summary>
public record UpdateIntegrationServiceCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    bool IsAvailable,
    string? LogoUrl,
    string? TrackingUrlTemplate,
    List<PlatformSchemaField>? SettingsSchema
) : IRequest<Result<bool>>;

public class UpdateIntegrationServiceCommandHandler
    : IRequestHandler<UpdateIntegrationServiceCommand, Result<bool>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public UpdateIntegrationServiceCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateIntegrationServiceCommand request, CancellationToken ct)
    {
        var service = await _db.IntegrationServices.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (service is null)
            return Result.Failure<bool>("Entegrasyon servisi bulunamadı.");

        service.NameI18n = request.NameI18n;
        service.IsAvailable = request.IsAvailable;
        service.LogoUrl = request.LogoUrl;
        service.TrackingUrlTemplate = request.TrackingUrlTemplate;
        service.SettingsSchemaJson = request.SettingsSchema is { Count: > 0 }
            ? JsonSerializer.Serialize(request.SettingsSchema, _json)
            : null;
        service.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success<bool>(true);
    }
}
