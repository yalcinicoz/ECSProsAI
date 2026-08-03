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

// Kampanya alanları (2026-08-03, additive): CampaignDiscount sepet-seviyesi indirim
// (buy_x_get_y/min_cart — ödenecek tutardan düşülür), Campaigns uygulanan kampanya özetleri.
// Ürün-bazlı kampanya fiyatı kalemde CampaignUnitPrice olarak döner (görsel çizik fiyat için).
public record CartDto(
    Guid Id,
    Guid? MemberId,
    string? SessionId,
    Guid FirmPlatformId,
    string CurrencyCode,
    List<CartItemDto> Items,
    decimal Subtotal,
    decimal CampaignDiscount = 0m,
    List<AppliedCampaign>? Campaigns = null);

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
    string? OptionsText = null,
    decimal? CampaignUnitPrice = null);

public class GetCartQueryHandler(
    ICrmDbContext db,
    IProductService productService,
    IProductCampaignResolver campaignResolver)
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

        // Kampanya çözümü (2026-08-03): checkout'la (F4) AYNI servis — ürün-bazlı kampanya
        // birim fiyata (CampaignUnitPrice), sepet-seviyesi (buy_x_get_y/min_cart) indirime.
        // Çözüm hatası sepeti düşürmez — kampanyasız görünümle devam edilir.
        var kampanya = new CartCampaignResult(new Dictionary<Guid, decimal>(), 0m, []);
        try
        {
            var kalemler = cart.Items
                .Where(i => gosterim.TryGetValue(i.VariantId, out var g) && g.ProductId != Guid.Empty)
                .Select(i => new CartCampaignItem(
                    i.VariantId, gosterim[i.VariantId].ProductId, i.Quantity, i.AddedPrice))
                .ToList();
            if (kalemler.Count > 0)
                kampanya = await campaignResolver.ResolveCartAsync(cart.FirmPlatformId, kalemler, ct);
        }
        catch { /* kampanya çözülemedi — sepet kampanyasız döner */ }

        var items = cart.Items.Select(i =>
        {
            gosterim.TryGetValue(i.VariantId, out var g);
            var kampanyaFiyat = kampanya.ItemUnitPrices.TryGetValue(i.VariantId, out var kf)
                ? (decimal?)kf : null;
            return new CartItemDto(
                i.Id, i.VariantId, i.Quantity, i.AddedPrice, i.Quantity * i.AddedPrice,
                i.IsAvailable, i.AvailableQuantity,
                g?.ProductCode, g?.ProductNameI18n, g?.ImageUrl, g?.OptionsText,
                kampanyaFiyat);
        }).ToList();

        var dto = new CartDto(
            cart.Id, cart.MemberId, cart.SessionId, cart.FirmPlatformId,
            cart.CurrencyCode, items, items.Sum(i => i.LineTotal),
            kampanya.CartDiscount,
            kampanya.Applied.Count > 0 ? kampanya.Applied : null);

        return Result.Success<CartDto?>(dto);
    }
}
