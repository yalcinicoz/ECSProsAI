using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreateIntegrationService;

/// <summary>
/// Servis kataloğu kaydı (SMTP, kargo firması, görsel arama...). SettingsSchema, firma
/// entegrasyon formunun alanlarını tanımlar (section=credentials → şifreli, settings → jsonb).
/// </summary>
public record CreateIntegrationServiceCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    string ServiceType,
    bool IsAvailable = true,
    string? LogoUrl = null,
    string? TrackingUrlTemplate = null,
    List<PlatformSchemaField>? SettingsSchema = null,
    // F3: kargo kod stratejisi (yalnız ServiceType=cargo için anlamlı)
    string? CargoCodeStrategy = null,
    int? CargoCodeMinLength = null,
    int? CargoCodeMaxLength = null,
    string? CargoCodeCharset = null
) : IRequest<Result<Guid>>;

public class CreateIntegrationServiceCommandHandler
    : IRequestHandler<CreateIntegrationServiceCommand, Result<Guid>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public CreateIntegrationServiceCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateIntegrationServiceCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code))
            return Result.Failure<Guid>("Servis kodu boş olamaz.");
        if (string.IsNullOrWhiteSpace(request.ServiceType))
            return Result.Failure<Guid>("Servis tipi boş olamaz.");

        var exists = await _db.IntegrationServices.AnyAsync(s => s.Code == code, ct);
        if (exists)
            return Result.Failure<Guid>("Bu kodda bir servis zaten mevcut.");

        var service = new IntegrationService
        {
            Id = Guid.NewGuid(),
            Code = code,
            NameI18n = request.NameI18n,
            ServiceType = request.ServiceType.Trim().ToLowerInvariant(),
            IsAvailable = request.IsAvailable,
            LogoUrl = request.LogoUrl,
            TrackingUrlTemplate = request.TrackingUrlTemplate,
            CargoCodeStrategy = request.CargoCodeStrategy?.Trim().ToLowerInvariant(),
            CargoCodeMinLength = request.CargoCodeMinLength,
            CargoCodeMaxLength = request.CargoCodeMaxLength,
            CargoCodeCharset = request.CargoCodeCharset?.Trim().ToLowerInvariant(),
            SettingsSchemaJson = request.SettingsSchema is { Count: > 0 }
                ? JsonSerializer.Serialize(request.SettingsSchema, _json)
                : null,
            CreatedAt = DateTime.UtcNow
        };

        _db.IntegrationServices.Add(service);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(service.Id);
    }
}
