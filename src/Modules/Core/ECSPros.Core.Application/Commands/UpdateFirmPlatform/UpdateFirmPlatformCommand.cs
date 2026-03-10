using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateFirmPlatform;

public record UpdateFirmPlatformCommand(
    Guid Id,
    Dictionary<string, string> NameI18n,
    string? PriceType,
    decimal? PriceMultiplier,
    bool IsActive
) : IRequest<Result<bool>>;

public class UpdateFirmPlatformCommandHandler : IRequestHandler<UpdateFirmPlatformCommand, Result<bool>>
{
    private readonly ICoreDbContext _db;

    public UpdateFirmPlatformCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateFirmPlatformCommand request, CancellationToken ct)
    {
        var platform = await _db.FirmPlatforms.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (platform is null)
            return Result.Failure<bool>("Firma platformu bulunamadı.");

        platform.NameI18n = request.NameI18n;
        platform.PriceType = request.PriceType;
        platform.PriceMultiplier = request.PriceMultiplier;
        platform.IsActive = request.IsActive;
        platform.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success<bool>(true);
    }
}
