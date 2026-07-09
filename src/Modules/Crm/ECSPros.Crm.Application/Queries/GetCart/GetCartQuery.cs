using ECSPros.Crm.Application.Services;
using ECSPros.Crm.Domain.Entities;
using ECSPros.Shared.Contracts;
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

// B5: gösterim alanları (ProductCode/NameI18n/ImageUrl/OptionsText) additive eklendi —
// IProductService (Catalog) üzerinden zenginleştirilir; eski istemciler etkilenmez.
public record CartItemDto(
    Guid Id,
    Guid VariantId,
    int Quantity,
    decimal AddedPrice,
    decimal LineTotal,
    bool IsAvailable,
    int AvailableQuantity,
    string? ProductCode = null,
    Dictionary<string, string>? ProductNameI18n = null,
    string? ImageUrl = null,
    string? OptionsText = null);

public class GetCartQueryHandler(ICrmDbContext db, IProductService productService)
    : IRequestHandler<GetCartQuery, Result<CartDto?>>
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

        var gosterim = await productService.GetVariantDisplayAsync(
            cart.Items.Select(i => i.VariantId).ToList(), ct);

        var items = cart.Items.Select(i =>
        {
            gosterim.TryGetValue(i.VariantId, out var g);
            return new CartItemDto(
                i.Id, i.VariantId, i.Quantity, i.AddedPrice, i.Quantity * i.AddedPrice,
                i.IsAvailable, i.AvailableQuantity,
                g?.ProductCode, g?.ProductNameI18n, g?.ImageUrl, g?.OptionsText);
        }).ToList();

        var dto = new CartDto(
            cart.Id, cart.MemberId, cart.SessionId, cart.FirmPlatformId,
            cart.CurrencyCode, items, items.Sum(i => i.LineTotal));

        return Result.Success<CartDto?>(dto);
    }
}
