using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateCatalogSetting;

public record UpdateCatalogSettingCommand(string Key, string Value) : IRequest<Result<bool>>;

public class UpdateCatalogSettingCommandHandler : IRequestHandler<UpdateCatalogSettingCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;
    public UpdateCatalogSettingCommandHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateCatalogSettingCommand request, CancellationToken ct)
    {
        var setting = await _db.CatalogSettings.FirstOrDefaultAsync(s => s.Key == request.Key, ct);
        if (setting is null)
        {
            setting = new Domain.Entities.CatalogSetting { Key = request.Key, Value = string.Empty };
            _db.CatalogSettings.Add(setting);
        }

        // Barkod seri için sayısal doğrulama
        if (request.Key == "barcode_sequence")
        {
            if (!long.TryParse(request.Value, out var num) || num < 1)
                return Result.Failure<bool>("Barkod seri değeri 1 veya daha büyük bir sayı olmalıdır.");
        }

        setting.Value = request.Value.Trim();
        setting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
