using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Crm.Application.Queries.GetCart;

public record GetCartQuery(
    Guid? CartId = null,
    Guid? MemberId = null,
    string? SessionId = null,
    Guid? FirmPlatformId = null) : IRequest<Result<CartDto?>>;

public record CartDto(
    Guid Id,
    Guid? MemberId,
    string? SessionId,
    Guid FirmPlatformId,
    string CurrencyCode,
    List<CartItemDto> Items,
    decimal Subtotal);

public record CartItemDto(
    Guid Id,
    Guid VariantId,
    int Quantity,
    decimal AddedPrice,
    decimal LineTotal,
    bool IsAvailable,
    int AvailableQuantity);

public class GetCartQueryHandler(ICrmDbContext db) : IRequestHandler<GetCartQuery, Result<CartDto?>>
{
    public async Task<Result<CartDto?>> Handle(GetCartQuery request, CancellationToken ct)
    {
        var q = db.Carts.Include(c => c.Items).AsNoTracking();

        Cart? cart = null;
        if (request.CartId.HasValue)
            cart = await q.FirstOrDefaultAsync(c => c.Id == request.CartId.Value, ct);
        else if (request.MemberId.HasValue && request.FirmPlatformId.HasValue)
            cart = await q.FirstOrDefaultAsync(c => c.MemberId == request.MemberId.Value && c.FirmPlatformId == request.FirmPlatformId.Value, ct);
        else if (request.SessionId != null && request.FirmPlatformId.HasValue)
            cart = await q.FirstOrDefaultAsync(c => c.SessionId == request.SessionId && c.FirmPlatformId == request.FirmPlatformId.Value, ct);

        if (cart is null) return Result.Success<CartDto?>(null);

        var items = cart.Items.Select(i => new CartItemDto(
            i.Id, i.VariantId, i.Quantity, i.AddedPrice, i.Quantity * i.AddedPrice,
            i.IsAvailable, i.AvailableQuantity)).ToList();

        var dto = new CartDto(
            cart.Id, cart.MemberId, cart.SessionId, cart.FirmPlatformId,
            cart.CurrencyCode, items, items.Sum(i => i.LineTotal));

        return Result.Success<CartDto?>(dto);
    }
}
