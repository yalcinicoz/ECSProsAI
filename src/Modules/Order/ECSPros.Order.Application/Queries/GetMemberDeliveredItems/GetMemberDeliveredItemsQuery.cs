using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetMemberDeliveredItems;

/// <summary>
/// A11 (2026-09-07, mobil): üyenin TESLİM EDİLMİŞ sipariş kalemleri tek sorguda — yorumlanabilirlik
/// (reviewable) ve satın alma kanıtı buradan. Önceden sipariş başına ayrı detay sorgusu (N+1) çekiliyordu.
/// En yeni MaxOrders sipariş; kalemler sipariş tarihine göre yeniden-eskiye.
/// </summary>
public record GetMemberDeliveredItemsQuery(Guid MemberId, int MaxOrders = 50)
    : IRequest<Result<List<DeliveredOrderItemDto>>>;

public record DeliveredOrderItemDto(
    Guid OrderId,
    string OrderNumber,
    DateTime OrderedAt,
    Guid OrderItemId,
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantInfo,
    int Quantity);

public class GetMemberDeliveredItemsQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetMemberDeliveredItemsQuery, Result<List<DeliveredOrderItemDto>>>
{
    public async Task<Result<List<DeliveredOrderItemDto>>> Handle(GetMemberDeliveredItemsQuery request, CancellationToken ct)
    {
        var maxOrders = Math.Clamp(request.MaxOrders, 1, 200);
        var siparisler = await db.Orders.AsNoTracking()
            .Where(o => o.MemberId == request.MemberId && o.Status == "delivered")
            .OrderByDescending(o => o.CreatedAt)
            .Take(maxOrders)
            .Select(o => new
            {
                o.Id, o.OrderNumber, o.CreatedAt,
                Items = o.Items.Select(i => new { i.Id, i.VariantId, i.Sku, i.ProductName, i.VariantInfo, i.Quantity }).ToList()
            })
            .ToListAsync(ct);

        var liste = siparisler
            .SelectMany(o => o.Items.Select(i => new DeliveredOrderItemDto(
                o.Id, o.OrderNumber, o.CreatedAt, i.Id, i.VariantId, i.Sku, i.ProductName, i.VariantInfo, i.Quantity)))
            .ToList();
        return Result.Success(liste);
    }
}
