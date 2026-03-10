using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Commands.AddToCart;

public record AddToCartCommand(
    Guid FirmPlatformId,
    Guid VariantId,
    int Quantity,
    decimal Price,
    string CurrencyCode,
    Guid? MemberId = null,
    string? SessionId = null) : IRequest<Result<Guid>>;

public class AddToCartCommandHandler(ICrmDbContext db) : IRequestHandler<AddToCartCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddToCartCommand request, CancellationToken ct)
    {
        if (request.Quantity <= 0) return Result.Failure<Guid>("Miktar sıfırdan büyük olmalıdır.");

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
            cart.Items.Add(new CartItem
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
