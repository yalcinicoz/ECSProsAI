using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.UpdateCatalogSetting;

public record UpdateCatalogSettingCommand(string Key, string Value) : IRequest<Result<bool>>;

public class UpdateCatalogSettingCommandHandler : IRequestHandler<UpdateCatalogSettingCommand, Result<bool>>
{
    private readonly ICatalogDbContext _db;
    private readonly ICatalogSettingSecretProtector _secretProtector;

    public UpdateCatalogSettingCommandHandler(
        ICatalogDbContext db,
        ICatalogSettingSecretProtector secretProtector)
    {
        _db = db;
        _secretProtector = secretProtector;
    }

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

        var value = request.Value.Trim();
        if (_secretProtector.IsSecret(request.Key))
        {
            // Panelden maskeli değer geri gelirse saklı secret değiştirilmez.
            if (value == ICatalogSettingSecretProtector.MaskedValue)
                return Result.Success(true);
            value = _secretProtector.Protect(value);
        }

        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
