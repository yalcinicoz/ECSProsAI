using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECSPros.Api.Tests;

/// <summary>
/// DataGrid F0 — Siparişler şemasının EF/Npgsql SQL çevirisi (SALT OKUNUR). Yalnız <c>ECSPROS_TEST_DB</c> ortam değişkeni
/// (bağlantı dizesi) verildiğinde çalışır; verilmezse atlanır. Veri değiştirmez.
/// </summary>
[TestClass]
public sealed class OrderGridDbTests
{
    private static OrderDbContext? Ctx()
    {
        var cs = Environment.GetEnvironmentVariable("ECSPROS_TEST_DB");
        if (string.IsNullOrWhiteSpace(cs)) return null;
        var dsb = new NpgsqlDataSourceBuilder(cs);
        dsb.EnableDynamicJson();
        var opts = new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql(dsb.Build()).Options;
        return new OrderDbContext(opts);
    }

    [TestMethod]
    public async Task Filters_and_sorts_translate_to_sql_and_counts_are_consistent()
    {
        await using var db = Ctx();
        if (db is null) { Assert.Inconclusive("ECSPROS_TEST_DB verilmedi — DB testi atlandı."); return; }

        var named = new OrderListFilters(CreatedFrom: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var all = await OrderGrid.ApplyAll(db.Orders.AsNoTracking(), named, null).CountAsync();

        // bool tamamlayıcılık: paid=true + paid=false = tümü
        var paid = await OrderGrid.ApplyAll(db.Orders.AsNoTracking(), named, new GridRequest { Filters = { new("paid", "eq", "true") } }).CountAsync();
        var unpaid = await OrderGrid.ApplyAll(db.Orders.AsNoTracking(), named, new GridRequest { Filters = { new("paid", "eq", "false") } }).CountAsync();
        Assert.AreEqual(all, paid + unpaid);

        // enum in + nullToken, text contains/startsWith, date between (gün), number between, guid, sort — hepsi SQL'e çevrilmeli (exception yok)
        var grid = new GridRequest
        {
            Sort = "total", Dir = "desc",
            Filters =
            {
                new("status", "in", "pending,confirmed,processing,shipped,delivered,cancelled,returned"),
                new("paymentMethod", "in", "kart,none"),
                new("customer", "contains", "a"),
                new("orderNumber", "startswith", "m"),
                new("createdAt", "between", "2026-08-01,2026-12-31"),
                new("total", "between", "0;1000000"),
            },
        };
        var q = OrderGrid.Schema.ApplySort(OrderGrid.ApplyAll(db.Orders.AsNoTracking(), named, grid), grid);
        var sql = q.ToQueryString();
        StringAssert.Contains(sql.ToLowerInvariant(), "order by");
        StringAssert.Contains(sql, "\"GrandTotal\" DESC");
        StringAssert.Contains(sql, "\"Id\" DESC", "tie-breaker");
        var page = await q.Skip(0).Take(5).Select(o => new { o.OrderNumber, o.GrandTotal }).ToListAsync();
        for (var i = 1; i < page.Count; i++) Assert.IsTrue(page[i - 1].GrandTotal >= page[i].GrandTotal, "toplam azalan sıralı olmalı");

        // sekme sayaçları: status hariç sayım, listeyle tutarlı
        var counts = await OrderGrid.ApplyAll(db.Orders.AsNoTracking(), named, new GridRequest { Filters = { new("status", "in", "pending"), new("paid", "eq", "true") } }, includeStatus: false)
            .Where(o => o.Status == "pending").CountAsync();
        var liste = await OrderGrid.ApplyAll(db.Orders.AsNoTracking(), named, new GridRequest { Filters = { new("status", "in", "pending"), new("paid", "eq", "true") } }).CountAsync();
        Assert.AreEqual(liste, counts);

        // export projeksiyonu SQL'e çevrilir (ilk 3 satır)
        var rows = await OrderGrid.Schema.ApplySort(OrderGrid.ApplyAll(db.Orders.AsNoTracking(), named, null), null)
            .Select(o => new OrderExportRow(o.OrderNumber, o.ExternalOrderNumber, o.CreatedAt, o.Status, o.PaymentStatus, o.PaymentMethod,
                o.ShippingRecipientName, o.ShippingRecipientPhone, o.ShippingAddressLine, o.ShippingPostalCode, o.RequestedCargoName, o.OrderType,
                o.Subtotal, o.TotalDiscount, o.TotalExpense, o.TotalTax, o.GrandTotal, o.CurrencyCode, null, null)).Take(3).ToListAsync();   // 15.4h: not kolonları
        Assert.IsTrue(rows.Count <= 3);

        // beyaz liste: bilinmeyen alan sorgu kurulmadan reddedilir
        Assert.ThrowsExactly<GridException>(() => OrderGrid.ApplyAll(db.Orders, named, new GridRequest { Filters = { new("passwordHash", "eq", "x") } }));
    }
}
