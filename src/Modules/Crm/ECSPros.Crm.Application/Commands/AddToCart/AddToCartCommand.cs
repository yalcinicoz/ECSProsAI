using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.AddToCart;

// B12: EnforceStock — platformun "stok kontrolü" anahtarı açıkken API katmanı true geçer;
// kapalıyken (varsayılan — bugünkü veri durumu) stok hiç sorgulanmaz, her şey satılabilir.
//
// ★ M1 (2026-09-09, mobil isteği): <b>Price artık YOK SAYILIR</b> — fiyatı sunucu belirler.
// Neden: istemci fiyatına güvenmek iki soruna yol açıyordu. (1) Mobil, B9 sözleşmesine uyup
// listedeki KAMPANYALI fiyatı gönderiyordu; sepet kampanyayı bu fiyatın üstüne bir kez daha
// uygulayınca indirim ÇİFT sayılıyordu. (2) İstemci teoride istediği fiyatı yazabiliyordu
// (para kaybı yoktu — checkout zaten sunucuda hesaplıyor — ama sepet yanlış tutar gösteriyordu).
// Alan sözleşmede KALDI (eski istemciler gönderiyor), yalnız kullanılmıyor.
public record AddToCartCommand(
    Guid FirmPlatformId,
    Guid VariantId,
    int Quantity,
    decimal Price,
    string CurrencyCode,
    Guid? MemberId = null,
    string? SessionId = null,
    bool EnforceStock = false) : IRequest<Result<Guid>>;

public class AddToCartCommandHandler(
    ICrmDbContext db,
    IStockService stockService,
    IProductService productService,
    IChannelPricingService pricingService)
    : IRequestHandler<AddToCartCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddToCartCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<Guid>("Miktar sıfırdan büyük olmalıdır.");

        // ★ Fiyatı SUNUCU belirler (checkout ile AYNI kaynak: kanal fiyatı → varyantın taban fiyatı).
        // Kampanya BURADA uygulanmaz; sepet GET'i kampanyayı bu taban fiyatın üzerine bir kez uygular.
        var varyant = await productService.GetVariantAsync(request.VariantId, ct);
        if (varyant is null || !varyant.IsActive)
            return Result.Failure<Guid>("Ürün şu an satışa kapalı.");

        var kanalFiyatlar = await pricingService.GetActiveVariantPricesAsync(
            request.FirmPlatformId, [request.VariantId], ct);
        var sunucuFiyat = kanalFiyatlar.TryGetValue(request.VariantId, out var kf) && kf.Price is > 0
            ? kf.Price!.Value
            : varyant.BasePrice;
        if (sunucuFiyat <= 0)
            return Result.Failure<Guid>("Ürün fiyatı doğrulanamadı; ürün sepete eklenemedi.");

        if (request.EnforceStock
            && !await stockService.HasSufficientStockAsync(request.VariantId, request.Quantity, null, ct))
            return Result.Failure<Guid>("Bu ürün tükendi veya istenen adet stokta yok.");

        Cart? cart = null;
        if (request.MemberId.HasValue)
            cart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.MemberId == request.MemberId.Value && c.FirmPlatformId == request.FirmPlatformId, ct);
        else if (request.SessionId != null)
            cart = await db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == request.SessionId && c.FirmPlatformId == request.FirmPlatformId, ct);

        if (cart is null)
        {
            cart = new Cart
            {
                MemberId = request.MemberId,
                SessionId = request.SessionId,
                FirmPlatformId = request.FirmPlatformId,
                CurrencyCode = request.CurrencyCode
            };
            db.Carts.Add(cart);
        }

        var existing = cart.Items.FirstOrDefault(i => i.VariantId == request.VariantId);
        if (existing is not null)
        {
            existing.Quantity += request.Quantity;
            existing.AddedPrice = sunucuFiyat;
            existing.AddedAt = DateTime.UtcNow;
        }
        else
        {
            // DbSet üzerinden eklenmeli: izlenen cart'ın koleksiyonuna Id'si baştan atanmış
            // (BaseEntity Guid.NewGuid()) yeni satır eklenince DetectChanges bunu Added değil
            // Modified sayıyor — var olmayan satıra UPDATE gidip DbUpdateConcurrencyException
            // fırlatıyordu (mevcut sepete ikinci farklı ürün hep 500 dönüyordu).
            db.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                VariantId = request.VariantId,
                Quantity = request.Quantity,
                AddedPrice = sunucuFiyat,
                AddedAt = DateTime.UtcNow,
                IsAvailable = true,
                AvailableQuantity = 999,
                LastCheckedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(cart.Id);
    }
}
