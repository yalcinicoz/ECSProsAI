using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportDetailTests
{
    private static readonly HashSet<string> Allowed = new() { "reports.ai.use", "orders.view" };
    private static ReportDetailSchema<OrderEntity> Schema() => ReportBusinessDictionary.Orders.Details(o => !o.IsDeleted, o => o.Id);
    private static ReportDetailPlan Plan() => new() { Columns = new[] { "orders.orderNumber", "orders.status" }, Sort = "orders.orderNumber" };
    private static IQueryable<OrderEntity> Data() => new[] {
        new OrderEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), OrderNumber = "B", Status = "pending", GrandTotal = 20, CurrencyCode = "TRY" },
        new OrderEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), OrderNumber = "A", Status = "paid", GrandTotal = 10, CurrencyCode = "TRY" },
        new OrderEntity { OrderNumber = "C", Status = "paid", IsDeleted = true } }.AsQueryable();

    [TestMethod]
    public void ExportReturnsMoreThanOneGridPageWithoutChangingRegularLimit()
    {
        var data = Enumerable.Range(0, 501).Select(i => new OrderEntity
            { Id = Guid.NewGuid(), OrderNumber = i.ToString("D4"), Status = "paid" }).AsQueryable();
        Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(data, Plan(), new(PageSize: 501), Allowed));
        var exported = Schema().Build(data, Plan(), ReportGridState.ForExport(new()), Allowed);
        Assert.AreEqual(501, exported.Page.Count());
        Assert.AreEqual(501, exported.Filtered.Count());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => Schema().Build(data, Plan(), ReportGridState.ForExport(new()), new HashSet<string>()));
    }

    [TestMethod]
    public void SelectedColumnsKeepOrderAndFiltersCountBeforePaging()
    {
        var plan = Plan();
        var query = Schema().Build(Data(), plan, new(PageSize: 1), Allowed);
        plan.Columns![0] = "secret";
        CollectionAssert.AreEqual(new object[] { "A", "paid" }, query.Page.Single());
        Assert.AreEqual("orders.orderNumber", query.Columns[0]);
        Assert.AreEqual(2, query.Filtered.Count());
        var filtered = Schema().Build(Data(), Plan(), new(Page: 2, PageSize: 1, Search: "PAID"), Allowed);
        Assert.AreEqual(1, filtered.Filtered.Count());
        Assert.AreEqual(0, filtered.Page.Count());
        Assert.AreEqual("B", Schema().Build(Data(), Plan(), new(Filters: new[] { new ReportGridFilter("orders.status", "eq", "pending") }), Allowed).Page.Single()[0]);
    }

    [TestMethod]
    public void UnsupportedAndHiddenColumnsSortsFiltersAndPermissionsAreRejected()
    {
        foreach (var columns in new[] { Array.Empty<string>(), new[] { "orders.count" }, new[] { "orders.averageAmount" },
            new[] { "orders.shippingRecipientPhone" }, new[] { "orders.status", "orders.status" }, new[] { "orders.amount" } })
            Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(Data(), Plan() with { Columns = columns, Sort = null }, new(), Allowed));
        Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(Data(), Plan(), new(Sort: "orders.amount"), Allowed));
        Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(Data(), Plan(), new(Filters: new[] { new ReportGridFilter("orders.amount", "gte", "1") }), Allowed));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => Schema().Build(Data(), Plan(), new(), new HashSet<string>()));
        Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(Data(), Plan(), new(PageSize: 251), Allowed));
        Assert.IsFalse(ReportBusinessDictionary.Orders.DescribeDetails(Allowed).Any(f => f.Id == "orders.count"));
        Assert.AreEqual(0, ReportBusinessDictionary.Orders.DescribeDetails(new HashSet<string>()).Count);
    }

    [TestMethod]
    public void TopMembershipAndStableKeySurviveTableSortAndFilter()
    {
        var plan = Plan() with { Top = 1 };
        Assert.AreEqual(0, Schema().Build(Data(), plan, new(Search: "B"), Allowed).Filtered.Count());
        Assert.AreEqual("A", Schema().Build(Data(), plan, new(Dir: "desc"), Allowed).Page.Single()[0]);
        var tiePlan = new ReportDetailPlan { Columns = new[] { "orders.currencyCode", "orders.orderNumber" }, Sort = "orders.currencyCode" };
        Assert.AreEqual("A", Schema().Build(Data(), tiePlan, new(PageSize: 1), Allowed).Page.Single()[1]);
        Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(Data(), plan with { Top = 1001 }, new(), Allowed));
    }

    [TestMethod]
    public void PostgreSqlSelectsOnlyRequestedValuesAndPagesOnServer()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var plan = new ReportDetailPlan { Columns = new[] { "orders.orderNumber", "orders.createdAt", "orders.amount", "orders.currencyCode" }, Sort = "orders.amount" };
        var query = Schema().Build(db.Orders, plan, new(Page: 2, PageSize: 5, Filters: new[] { new ReportGridFilter("orders.amount", "gte", "10") }), Allowed);
        var sql = query.Page.ToQueryString();
        StringAssert.Contains(sql, "LIMIT");
        StringAssert.Contains(sql, "OFFSET");
        StringAssert.Contains(sql, "ORDER BY");
        StringAssert.Contains(sql, "GrandTotal");
        Assert.IsFalse(sql.Contains("ShippingRecipient"));
        Assert.IsFalse(sql.Contains("PaymentMethod"));
        var data = Schema().Build(Data(), plan, new(), Allowed).Page.First();
        Assert.IsInstanceOfType<DateTime>(data[1]);
        Assert.AreEqual(10m, data[2]);
    }

    [TestMethod]
    public void OrderSourceRechecksCurrentChannelScopeWithoutExecuting()
    {
        var channel = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var data = Data().ToArray();
        foreach (var item in data) { item.FirmPlatformId = channel; item.CreatedAt = from.UtcDateTime; }
        var all = new EfektifYetkiler(true, new());
        var source = OrderReportSource.Create(data.AsQueryable(), all, new(from, from.AddMonths(1)));
        var denied = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = new() });
        Assert.AreEqual(0, source.DynamicDetails(Plan(), new(), denied).Filtered.Count());
        Assert.AreEqual(2, source.DynamicDetails(Plan(), new(), all).Filtered.Count());
    }
}
