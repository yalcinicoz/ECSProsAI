using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Infrastructure.Services;

/// <summary>Order şemasından paketleme bilgisi okur (salt-okunur, raw SQL) —
/// modüller arası proje referansı kurmadan.</summary>
public class OrderPackagingReader : IOrderPackagingReader
{
    private readonly FulfillmentDbContext _db;

    public OrderPackagingReader(FulfillmentDbContext db) => _db = db;

    private sealed class OrderRow
    {
        public Guid Id { get; set; }
        public Guid FirmPlatformId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class ItemRow
    {
        public Guid Id { get; set; }
        public Guid VariantId { get; set; }
        public Guid? SupplierId { get; set; }
        public int Quantity { get; set; }
    }

    public async Task<OrderPackagingInfo?> GetOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = (await _db.Database.SqlQuery<OrderRow>($"""
            SELECT "Id", "FirmPlatformId", "OrderNumber", "Status"
            FROM "order".ord_orders
            WHERE "Id" = {orderId} AND "IsDeleted" = false
            """).ToListAsync(ct)).SingleOrDefault();

        if (order is null)
            return null;

        var items = await _db.Database.SqlQuery<ItemRow>($"""
            SELECT "Id", "VariantId", "SupplierId", "Quantity"
            FROM "order".ord_order_items
            WHERE "OrderId" = {orderId} AND "IsDeleted" = false
            """).ToListAsync(ct);

        return new OrderPackagingInfo(
            order.Id, order.FirmPlatformId, order.OrderNumber, order.Status,
            items.Select(i => new OrderPackagingItem(i.Id, i.VariantId, i.SupplierId, i.Quantity)).ToList());
    }
}
