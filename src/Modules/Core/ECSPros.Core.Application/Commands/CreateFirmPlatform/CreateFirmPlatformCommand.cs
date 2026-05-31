using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.CreateFirmPlatform;

public record CreateFirmPlatformCommand(
    Guid FirmId,
    Guid PlatformTypeId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier,
    Dictionary<string, object> Credentials,
    Dictionary<string, object> Settings
) : IRequest<Result<Guid>>;

public class CreateFirmPlatformCommandHandler : IRequestHandler<CreateFirmPlatformCommand, Result<Guid>>
{
    private readonly ICoreDbContext _db;

    public CreateFirmPlatformCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateFirmPlatformCommand request, CancellationToken ct)
    {
        var firmExists = await _db.Firms.AnyAsync(f => f.Id == request.FirmId, ct);
        if (!firmExists)
            return Result.Failure<Guid>("Firma bulunamadı.");

        var codeExists = await _db.FirmPlatforms.AnyAsync(p => p.FirmId == request.FirmId && p.Code == request.Code.Trim().ToLowerInvariant(), ct);
        if (codeExists)
            return Result.Failure<Guid>("Bu kodda bir satış kanalı zaten mevcut.");

        var platform = new FirmPlatform
        {
            Id              = Guid.NewGuid(),
            FirmId          = request.FirmId,
            PlatformTypeId  = request.PlatformTypeId,
            Code            = request.Code.Trim().ToLowerInvariant(),
            NameI18n        = request.NameI18n,
            PriceType       = request.PriceType,
            PriceMultiplier = request.PriceMultiplier,
            Credentials     = request.Credentials,
            Settings        = request.Settings,
            IsActive        = true,
            CreatedAt       = DateTime.UtcNow
        };

        _db.FirmPlatforms.Add(platform);
        await _db.SaveChangesAsync(ct);

        return Result.Success<Guid>(platform.Id);
    }
}
