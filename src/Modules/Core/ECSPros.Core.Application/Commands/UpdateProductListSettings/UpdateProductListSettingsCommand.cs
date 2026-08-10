using ECSPros.Core.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateProductListSettings;

/// <summary>
/// Ürün listesi ayarları (2026-08-10): sitede gösterilecek sıralama seçenekleri —
/// FirmPlatform.Settings jsonb'sine YALNIZ "productList" anahtarı merge edilir
/// (productCard kalıbı; ChannelsPage tam-replace davranışından etkilenmez).
/// SortOptions: kod → görünür mü. "default" kapatılamaz; bilinmeyen kod reddedilir.
/// Panel Storefront → Ürün Kartı → Sıralama sekmesinden yazılır.
/// </summary>
public record UpdateProductListSettingsCommand(
    Guid FirmPlatformId,
    Dictionary<string, bool> SortOptions) : IRequest<Result<bool>>;

public class UpdateProductListSettingsCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpdateProductListSettingsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateProductListSettingsCommand request, CancellationToken ct)
    {
        var gecerliKodlar = ProductSortCatalog.Tumu.Select(s => s.Kod).ToHashSet();
        if (request.SortOptions.Keys.Any(k => !gecerliKodlar.Contains(k)))
            return Result.Failure<bool>("Bilinmeyen sıralama kodu.");

        var platform = await db.FirmPlatforms.FirstOrDefaultAsync(p => p.Id == request.FirmPlatformId, ct);
        if (platform is null) return Result.Failure<bool>("Platform bulunamadı.");

        // Tüm kodlar açık/kapalı olarak eksiksiz yazılır ki site tarafı tutarlı okusun;
        // "default" her zaman true.
        var sortOptions = ProductSortCatalog.Tumu.ToDictionary(
            s => s.Kod,
            s => (object)(s.Kod == "default" || request.SortOptions.GetValueOrDefault(s.Kod, true)));

        var settings = platform.Settings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(platform.Settings);
        settings["productList"] = new Dictionary<string, object> { ["sortOptions"] = sortOptions };
        platform.Settings = settings;
        platform.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
