using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportAggregateTests
{
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "orders.view" };
    private static ReportAggregateSchema<OrderEntity> Schema() => new ReportAggregateSchema<OrderEntity>("orders.view", o => !o.IsDeleted)
        .Dimension("status", o => o.Status).Dimension("currency", o => o.CurrencyCode)
        .Count("count").Sum("amount", o => o.GrandTotal, "currency");
    private static IQueryable<OrderEntity> Data() => new[]
    {
        new OrderEntity { Status = "paid", CurrencyCode = "TRY", GrandTotal = 10 },
        new OrderEntity { Status = "paid", CurrencyCode = "TRY", GrandTotal = 20 },
        new OrderEntity { Status = "cancelled", CurrencyCode = "EUR", GrandTotal = 30 },
        new OrderEntity { Status = "paid", CurrencyCode = "TRY", GrandTotal = 90, IsDeleted = true }
    }.AsQueryable();

    [TestMethod]
    public void CombinationsAreDynamic_CurrencyAndSoftDeleteRemainCorrect()
    {
        var result = Schema().Build(Data(), new() { Dimensions = new[] { "currency", "status" },
            Measures = new[] { "amount", "count" }, Sort = "count", Direction = "desc" }, Permissions);
        var rows = result.Rows.ToArray();
        Assert.AreEqual(2, rows.Length);
        Assert.AreEqual("TRY", rows[0].D0);
        Assert.AreEqual(30m, rows[0].M0);
        Assert.AreEqual(2m, rows[0].M1);
        Assert.AreEqual(3m, Schema().Build(Data(), new() { Dimensions = Array.Empty<string>(), Measures = new[] { "count" } }, Permissions).Rows.Single().M0);
    }

    [TestMethod]
    public void HiddenFieldsMissingCurrencyAndPermissionsAreRejected()
    {
        foreach (var plan in new[]
        {
            new ReportAggregatePlan { Dimensions = new[] { "status" }, Measures = new[] { "amount" } },
            new ReportAggregatePlan { Dimensions = new[] { "customer" }, Measures = new[] { "count" } },
            new ReportAggregatePlan { Dimensions = new[] { "status", "status" }, Measures = new[] { "count" } },
            new ReportAggregatePlan { Dimensions = new[] { "status" }, Measures = new[] { "count" }, Sort = "secret" }
        }) Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(Data(), plan, Permissions));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => Schema().Build(Data(), new(), new HashSet<string>()));
    }

    [TestMethod]
    public void PostgreSqlTranslatesGroupingSortingAndPagingWithoutOpeningConnection()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var result = Schema().Build(db.Orders, new() { Dimensions = new[] { "currency", "status" },
            Measures = new[] { "amount", "count" }, Sort = "amount", Direction = "desc" }, Permissions);
        var sql = result.Rows.Skip(1).Take(10).ToQueryString();
        StringAssert.Contains(sql, "GROUP BY");
        StringAssert.Contains(sql, "sum(");
        StringAssert.Contains(sql, "ORDER BY");
        StringAssert.Contains(sql, "LIMIT");
    }

    [TestMethod]
    public void OrderAdapterAggregatesBeforePagingAndRechecksChannelPermission()
    {
        var channel = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var data = Data().ToArray();
        foreach (var o in data) { o.FirmPlatformId = channel; o.CreatedAt = from.UtcDateTime.AddDays(1); }
        var all = new EfektifYetkiler(true, new());
        var source = OrderReportSource.Create(data.AsQueryable(), all, new(from, from.AddMonths(1)));
        var plan = new ReportAggregatePlan { Dimensions = new[] { "orders.currencyCode" }, Measures = new[] { "orders.count" } };
        Assert.AreEqual(3m, source.DynamicSummary(plan, all).Rows.Sum(r => r.M0));
        var deniedChannel = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = new() });
        Assert.AreEqual(0, source.DynamicSummary(plan, deniedChannel).Rows.Count());
    }

    [TestMethod]
    public void AverageMinMaxUseFullGroup_AndTranslateWithoutClientEvaluation()
    {
        var schema = Schema().Average("average", o => o.GrandTotal, "currency")
            .Minimum("minimum", o => o.GrandTotal, "currency").Maximum("maximum", o => o.GrandTotal, "currency");
        var plan = new ReportAggregatePlan { Dimensions = new[] { "currency" }, Measures = new[] { "average", "minimum", "maximum" } };
        var row = schema.Build(Data(), plan, Permissions).Rows.Single(r => r.D0 == "TRY");
        Assert.AreEqual(15m, row.M0);
        Assert.AreEqual(10m, row.M1);
        Assert.AreEqual(20m, row.M2);
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var sql = schema.Build(db.Orders, plan, Permissions).Rows.ToQueryString();
        StringAssert.Contains(sql, "avg(");
        StringAssert.Contains(sql, "min(");
        StringAssert.Contains(sql, "max(");
    }

    [TestMethod]
    public void EmptyInputIsEmpty_ColumnOrderIsSnapshot_AndEqualMeasuresHaveStableKeys()
    {
        var dimensions = new[] { "currency" };
        var plan = new ReportAggregatePlan { Dimensions = dimensions, Measures = new[] { "amount" }, Sort = "amount", Direction = "desc" };
        var result = Schema().Build(Data(), plan, Permissions);
        dimensions[0] = "status";
        Assert.AreEqual("currency", result.Columns[0]);
        CollectionAssert.AreEqual(new[] { "EUR", "TRY" }, result.Rows.Select(r => r.D0).ToArray());
        var count = new ReportAggregatePlan { Dimensions = Array.Empty<string>(), Measures = new[] { "count" } };
        Assert.AreEqual(0, Schema().Build(Array.Empty<OrderEntity>().AsQueryable(), count, Permissions).Rows.Count());
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        StringAssert.Contains(Schema().Build(db.Orders, count, Permissions).Rows.ToQueryString(), "count(");
    }
}
