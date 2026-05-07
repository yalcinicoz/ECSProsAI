using System.Text.Json;
using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdatePlatformType;

public record UpdatePlatformTypeCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    bool IsMarketplace,
    bool IsActive,
    List<PlatformSchemaField>? SettingsSchema = null
) : IRequest<Result<Unit>>;

public class UpdatePlatformTypeCommandHandler : IRequestHandler<UpdatePlatformTypeCommand, Result<Unit>>
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ICoreDbContext _db;

    public UpdatePlatformTypeCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Unit>> Handle(UpdatePlatformTypeCommand request, CancellationToken ct)
    {
        var exists = await _db.PlatformTypes.AnyAsync(p => p.Id == request.Id, ct);
        if (!exists)
            return Result.Failure<Unit>("Platform tipi bulunamadı.");

        var schemaJson = request.SettingsSchema is { Count: > 0 }
            ? JsonSerializer.Serialize(request.SettingsSchema, _json)
            : null;

        var nameJson = JsonSerializer.Serialize(request.NameI18n, _json);

        await _db.UpdatePlatformTypeRawAsync(
            request.Id, nameJson, request.IsMarketplace, request.IsActive, schemaJson, ct);

        return Result.Success<Unit>(Unit.Value);
    }
}
