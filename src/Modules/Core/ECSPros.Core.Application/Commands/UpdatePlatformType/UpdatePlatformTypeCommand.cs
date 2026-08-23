using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Contracts.Channels;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdatePlatformType;

public record UpdatePlatformTypeCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    bool IsMarketplace,
    bool IsActive,
    List<PlatformSchemaField>? SettingsSchema = null,
    ChannelCapabilities? Capabilities = null
) : IRequest<Result<Unit>>;

public class UpdatePlatformTypeCommandHandler : IRequestHandler<UpdatePlatformTypeCommand, Result<Unit>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public UpdatePlatformTypeCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdatePlatformTypeCommand request, CancellationToken ct)
    {
        var entity = await _db.PlatformTypes
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (entity == null)
            return Result.Failure<Unit>("Platform tipi bulunamadı.");

        entity.NameI18n = new Dictionary<string, string>(request.NameI18n);
        if (request.Capabilities is not null)
        {
            entity.CapabilitiesJson = request.Capabilities.ToJson();
            entity.IsMarketplace = request.Capabilities.PushListing;   // K1: türetilmiş
        }
        else
        {
            entity.IsMarketplace = request.IsMarketplace;
            if (string.IsNullOrEmpty(entity.CapabilitiesJson))
                entity.CapabilitiesJson = ChannelCapabilities.DefaultsFor(entity.Code, request.IsMarketplace).ToJson();
        }
        entity.IsActive = request.IsActive;
        entity.SettingsSchemaJson = request.SettingsSchema is { Count: > 0 }
            ? JsonSerializer.Serialize(request.SettingsSchema, _json)
            : null;

        await _db.SaveChangesAsync(ct);
        return Result.Success<Unit>(Unit.Value);
    }
}
