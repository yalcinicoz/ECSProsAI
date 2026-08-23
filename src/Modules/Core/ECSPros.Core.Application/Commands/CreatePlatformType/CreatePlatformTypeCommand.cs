using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Contracts.Channels;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreatePlatformType;

public record CreatePlatformTypeCommand(
    string Code,
    Dictionary<string, string> NameI18n,
    bool IsMarketplace,
    List<PlatformSchemaField>? SettingsSchema = null,
    ChannelCapabilities? Capabilities = null
) : IRequest<Result<Guid>>;

public class CreatePlatformTypeCommandHandler : IRequestHandler<CreatePlatformTypeCommand, Result<Guid>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public CreatePlatformTypeCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreatePlatformTypeCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToLowerInvariant();

        var exists = await _db.PlatformTypes.AnyAsync(p => p.Code == code, ct);
        if (exists)
            return Result.Failure<Guid>("Bu kodda bir platform tipi zaten mevcut.");

        // K1: yetenek seti verilmişse IsMarketplace türetilir (pushListing); verilmemişse eski bayraktan varsayılan üretilir.
        var caps = request.Capabilities ?? ChannelCapabilities.DefaultsFor(code, request.IsMarketplace);
        var platformType = new PlatformType
        {
            Id                 = Guid.NewGuid(),
            Code               = code,
            NameI18n           = request.NameI18n,
            IsMarketplace      = caps.PushListing,
            CapabilitiesJson   = caps.ToJson(),
            SettingsSchemaJson = request.SettingsSchema is { Count: > 0 }
                                     ? JsonSerializer.Serialize(request.SettingsSchema, _json)
                                     : null,
            IsActive           = true,
            CreatedAt          = DateTime.UtcNow,
        };

        _db.PlatformTypes.Add(platformType);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(platformType.Id);
    }
}
