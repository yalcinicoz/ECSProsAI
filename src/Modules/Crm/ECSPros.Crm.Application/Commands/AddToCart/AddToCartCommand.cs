using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.AddToCart;

// B12: EnforceStock — platformun "stok kontrolü" anahtarı açıkken API katmanı true geçer;
// kapalıyken (varsayılan — bugünkü veri durumu) stok hiç sorgulanmaz, her şey satılabilir.
public record AddToCartCommand(
    Guid FirmPlatformId,
    Guid VariantId,
    int Quantity,
    decimal Price,
    string CurrencyCode,
    Guid? MemberId = null,
    string? SessionId = null,
    bool EnforceStock = false) : IRequest<Result<Guid>>;

public class AddToCartCommandHandler(ICrmDbContext db, IStockService stockService)
    : IRequestHandler<AddToCartCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddToCartCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<Guid>("Miktar sıfırdan büyük olmalıdır.");

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
            existing.AddedPrice = request.Price;
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
                AddedPrice = request.Price,
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
