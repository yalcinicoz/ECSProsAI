using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class OrderReportSourceTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly OrderReportScope Scope = new(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
    private static EfektifYetkiler Allowed(HashSet<Guid>? orders = null, HashSet<Guid>? reports = null) => new(false,
        new() { [Permissions.OrdersView] = orders, [ReportDictionary.UsePermission] = reports });
    private static OrderEntity Order(int day, Guid channel, string currency = "TRY", string status = "delivered") => new()
    {
        Id = Guid.NewGuid(), OrderNumber = $"ORDER-{day}", CreatedAt = Scope.From.UtcDateTime.AddDays(day),
        FirmPlatformId = channel, CurrencyCode = currency, Status = status, GrandTotal = 100m,
        PaymentStatus = "paid", ShippingRecipientName = "Hidden Customer", ShippingRecipientPhone = "5550000000"
    };

    [TestMethod]
    public void PermissionIntersectionCannotBeOverriddenByClientScope()
    {
        var data = new[] { Order(1, A), Order(2, B) }.AsQueryable();
        var source = OrderReportSource.Create(data, Allowed(new() { A, B }, new() { A }), Scope,
            new GridRequest { KanalKisiti = new[] { B } });
        Assert.AreEqual(A, source.Details().Single().FirmPlatformId);
        Assert.AreEqual(0, OrderReportSource.Create(data, Allowed(new() { A }, new() { B }), Scope).Details().Count());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OrderReportSource.Create(data, EfektifYetkiler.Bos, Scope));
    }

    [TestMethod]
    public void DateSoftDeleteAndRequestedChannelsOnlyNarrowScope()
    {
        var deleted = Order(3, A); deleted.IsDeleted = true;
        var data = new[] { Order(0, A), Order(1, B), Order(31, A), Order(-1, A), deleted }.AsQueryable();
        var source = OrderReportSource.Create(data, Allowed(), Scope with { Channels = new[] { A } });
        Assert.AreEqual(1, source.Details().Count());
        Assert.AreEqual(0, OrderReportSource.Create(data, Allowed(), Scope with { Channels = Array.Empty<Guid>() }).Details().Count());
    }

    [TestMethod]
    public void SummaryUsesAllFilteredOrders_NotOnlyCurrentPage_AndKeepsCurrenciesSeparate()
    {
        var data = new[] { Order(1, A), Order(2, A, status: "cancelled"), Order(3, B, "EUR", "returned") }.AsQueryable();
        var source = OrderReportSource.Create(data, Allowed(), Scope, new GridRequest { PageSize = 1 });
        Assert.AreEqual(1, source.Details().Count());
        var totals = source.Summary().ToList();
        Assert.AreEqual(2, totals.Count);
        Assert.AreEqual(2L, totals.Single(r => r.CurrencyCode == "TRY").OrderCount);
        Assert.AreEqual(200m, totals.Single(r => r.CurrencyCode == "TRY").OrderAmount);
        Assert.AreEqual(3, source.Summary("status", "firmPlatformId").Count());
        var cancelled = OrderReportSource.Create(data, Allowed(), Scope,
            new GridRequest { Filters = { new("status", "eq", "cancelled") } });
        Assert.AreEqual(1L, cancelled.Summary().Single().OrderCount);
    }

    [TestMethod]
    public void HiddenFieldsCannotBeSearchedFilteredSortedOrGrouped()
    {
        var data = new[] { Order(1, A) }.AsQueryable();
        Assert.AreEqual(0, OrderReportSource.Create(data, Allowed(), Scope, new GridRequest { Search = "Hidden Customer" }).Details().Count());
        foreach (var field in new[] { "customer", "phone", "memberId", "notes", "secret" })
        {
            Assert.ThrowsExactly<ArgumentException>(() => OrderReportSource.Create(data, Allowed(), Scope, new GridRequest { Filters = { new(field, "eq", "x") } }));
            Assert.ThrowsExactly<ArgumentException>(() => OrderReportSource.Create(data, Allowed(), Scope, new GridRequest { Sort = field }));
            Assert.ThrowsExactly<ArgumentException>(() => OrderReportSource.Create(data, Allowed(), Scope).Summary(field));
        }
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OrderReportSource.Create(data, Allowed(), Scope, new GridRequest { Filters = { new("barcode", "eq", "123") } }));
    }

    [TestMethod]
    public void RejectsInvalidRangesAndPaging()
    {
        var data = Array.Empty<OrderEntity>().AsQueryable();
        Assert.ThrowsExactly<ArgumentException>(() => OrderReportSource.Create(data, Allowed(), Scope with { To = Scope.From }));
        Assert.ThrowsExactly<ArgumentException>(() => OrderReportSource.Create(data, Allowed(), Scope with { To = Scope.From.AddDays(367) }));
        Assert.ThrowsExactly<ArgumentException>(() => OrderReportSource.Create(data, Allowed(), Scope, new GridRequest { PageSize = 251 }));
        Assert.ThrowsExactly<ArgumentException>(() => OrderReportSource.Create(data, Allowed(), Scope, new GridRequest { Filters = { new("status", "eq", "paid"), new("status", "eq", "paid") } }));
    }

    [TestMethod]
    public void DetailAndDynamicSummaryTranslateToPostgresWithoutConnectionOrPiiProjection()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=offline;Username=unused").Options);
        var source = OrderReportSource.Create(db.Orders, Allowed(new() { A }), Scope,
            new GridRequest { Page = 2, PageSize = 25, Search = "ORDER", Filters = { new("status", "in", "delivered,returned") } });
        var details = source.Details().ToQueryString();
        var summary = source.Summary("status", "paymentStatus", "firmPlatformId", "orderType").ToQueryString();
        StringAssert.Contains(details, "LIMIT"); StringAssert.Contains(details, "OFFSET");
        StringAssert.Contains(summary, "GROUP BY"); StringAssert.Contains(summary, "CurrencyCode");
        Assert.IsFalse(summary.Contains("LIMIT"));
        foreach (var sql in new[] { details, summary, source.Summary().ToQueryString() })
        {
            StringAssert.Contains(sql, "FirmPlatformId"); StringAssert.Contains(sql, "IsDeleted");
            Assert.IsFalse(sql.Contains("ShippingRecipient"));
            Assert.IsFalse(sql.Contains("JOIN"));
        }
    }
}
