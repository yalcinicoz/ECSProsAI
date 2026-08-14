using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.UpdateProductCardSettings;

/// <summary>
/// Ürün Kartı F1 (2026-08-09): kanal bazlı kart elementi aç/kapat ayarları —
/// FirmPlatform.Settings jsonb'sine YALNIZ "productCard" anahtarını merge eder
/// (diğer ayarlar korunur; ChannelsPage'in tam-replace davranışından etkilenmez,
/// o sayfa şema-dışı anahtarları geri yazar). Panel Storefront → Ürün Kartı ekranından yazılır.
/// </summary>
/// <summary>F2: değişken alan ayarı (1/2/3) — kaynaklar + rotasyon önceliği.</summary>
public record ProductCardAreaSetting(
    bool Enabled = true,
    bool Campaigns = false,
    bool Messages = true,
    bool MessagesFirst = true,
    // Sosyal kanıt (2026-08-10, yalnız Alan 3'te anlamlı): sepet/favori/görüntülenme sayaç satırları
    bool ShowCartCount = false,
    bool ShowFavoriteCount = false,
    bool ShowViewCount = false);

public record UpdateProductCardSettingsCommand(
    Guid FirmPlatformId,
    bool VideoBadge,
    bool SponsorBadge,
    bool ColorBadge,
    bool GalleryDots,
    bool FavoriteButton,
    bool CollectionButton,
    bool Rating,
    bool DiscountRow,
    bool CampaignPriceRow,
    Dictionary<string, ProductCardAreaSetting>? Areas,
    // Kartta sepete ekle (2026-08-14): desktop hover beden paneli + mobil sepet ikonu — varsayılan açık
    bool CartButton = true,
    // Görsel hover efekti (2026-08-14): "gallery" (varsayılan, yatay gezinme) | "zoom" (yakınlaştırma)
    string? HoverEffect = null,
    // Benzer ürünler ikonu (2026-08-14): görsel arama sonuç sayfasına gider — varsayılan açık
    bool SimilarButton = true) : IRequest<Result<bool>>;

public class UpdateProductCardSettingsCommandHandler(ICoreDbContext db)
    : IRequestHandler<UpdateProductCardSettingsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateProductCardSettingsCommand request, CancellationToken ct)
    {
        var alanlar = request.Areas ?? new Dictionary<string, ProductCardAreaSetting>();
        if (alanlar.Keys.Any(k => k is not ("1" or "2" or "3")))
            return Result.Failure<bool>("Alan anahtarları 1/2/3 olmalı.");

        var platform = await db.FirmPlatforms.FirstOrDefaultAsync(p => p.Id == request.FirmPlatformId, ct);
        if (platform is null) return Result.Failure<bool>("Platform bulunamadı.");

        var settings = platform.Settings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(platform.Settings);
        settings["productCard"] = new Dictionary<string, object>
        {
            ["videoBadge"] = request.VideoBadge,
            ["sponsorBadge"] = request.SponsorBadge,
            ["colorBadge"] = request.ColorBadge,
            ["galleryDots"] = request.GalleryDots,
            ["favoriteButton"] = request.FavoriteButton,
            ["collectionButton"] = request.CollectionButton,
            ["rating"] = request.Rating,
            ["discountRow"] = request.DiscountRow,
            ["campaignPriceRow"] = request.CampaignPriceRow,
            ["cartButton"] = request.CartButton,
            ["similarButton"] = request.SimilarButton,
            ["hoverEffect"] = request.HoverEffect == "zoom" ? "zoom" : "gallery",
            // F2: üç değişken alanın kaynak/öncelik ayarı — eksik alan varsayılanla yazılır
            // (1: kampanyalar açık; 2/3: yalnız mesajlar) ki site tarafı tutarlı okusun.
            ["areas"] = new Dictionary<string, object>
            {
                ["1"] = AlanJson(alanlar.GetValueOrDefault("1") ?? new ProductCardAreaSetting(Campaigns: true)),
                ["2"] = AlanJson(alanlar.GetValueOrDefault("2") ?? new ProductCardAreaSetting()),
                ["3"] = AlanJson(alanlar.GetValueOrDefault("3") ?? new ProductCardAreaSetting())
            }
        };
        platform.Settings = settings;
        platform.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    private static Dictionary<string, object> AlanJson(ProductCardAreaSetting a) => new()
    {
        ["enabled"] = a.Enabled,
        ["campaigns"] = a.Campaigns,
        ["messages"] = a.Messages,
        ["messagesFirst"] = a.MessagesFirst,
        ["showCartCount"] = a.ShowCartCount,
        ["showFavoriteCount"] = a.ShowFavoriteCount,
        ["showViewCount"] = a.ShowViewCount
    };
}
