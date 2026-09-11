using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class DynamicReportPlanTests
{
    public const string Json = """
        {"version":2,"source":"orders","from":"2026-08-01T00:00:00+03:00","to":"2026-09-01T00:00:00+03:00",
         "aggregate":{"dimensions":["status"],"measures":["count"],"sort":"count","direction":"desc"}}
        """;
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "orders.view" };
    private static ReportAggregateSchema<OrderEntity> Schema() => new ReportAggregateSchema<OrderEntity>("orders.view", o => !o.IsDeleted)
        .Dimension("status", o => o.Status).Count("count");

    [TestMethod]
    public void StrictEnvelopeRejectsUnknownDuplicateUnsupportedAndInvalidPeriods()
    {
        Assert.AreEqual(2, DynamicReportPlan.Parse(Json).Version);
        foreach (var bad in new[] { Json.Replace("\"version\":2", "\"version\":1"), Json.Replace("\"version\":2", "\"version\":2,\"version\":2"),
            Json.Replace("\"source\":\"orders\"", "\"source\":\"unknownSource\""), Json.Replace("+03:00", ""),
            Json.Replace("2026-09-01", "2028-09-01"), Json.Replace("\"direction\":\"desc\"", "\"sql\":\"SELECT 1\"") })
            Assert.ThrowsExactly<ArgumentException>(() => DynamicReportPlan.Parse(bad));
    }

    [TestMethod]
    public void GridFiltersAllGroupsBeforePagingAndKeepsCountOnEmptyPage()
    {
        var rows = Enumerable.Range(1, 12).Select(i => new OrderEntity { Status = i < 10 ? "large" : "small" }).AsQueryable();
        var plan = DynamicReportPlan.Parse(Json).Aggregate!;
        var query = Schema().Build(rows, plan, Permissions);
        var filtered = ReportAggregateGrid.Apply(query, plan, new(PageSize: 1, Filters: new[] { new ReportGridFilter("count", "gte", "4") }));
        Assert.AreEqual(1, filtered.Count());
        Assert.AreEqual(9m, filtered.First().M0);
        Assert.AreEqual(0, filtered.Skip(1).Take(1).Count());
        Assert.AreEqual(1, ReportAggregateGrid.Apply(query, plan, new(Search: "LARGE")).Count());
        Assert.ThrowsExactly<ArgumentException>(() => ReportAggregateGrid.Apply(query, plan, new(Sort: "secret")));
        Assert.ThrowsExactly<ArgumentException>(() => ReportAggregateGrid.Apply(query, plan, new(PageSize: 251)));
        var totalPlan = new ReportAggregatePlan { Dimensions = Array.Empty<string>(), Measures = new[] { "count" } };
        Assert.ThrowsExactly<ArgumentException>(() => ReportAggregateGrid.Apply(Schema().Build(rows, totalPlan, Permissions), totalPlan, new(Search: "12")));
    }

    [TestMethod]
    public void PostAggregateFilterSortAndPageTranslateToPostgreSql()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var plan = DynamicReportPlan.Parse(Json).Aggregate!;
        var rows = ReportAggregateGrid.Apply(Schema().Build(db.Orders, plan, Permissions), plan,
            new(Search: "paid", Sort: "count", Dir: "desc", Filters: new[] { new ReportGridFilter("count", "gte", "2") }));
        var sql = rows.Skip(1).Take(25).ToQueryString();
        StringAssert.Contains(sql, "GROUP BY");
        StringAssert.Contains(sql, "ORDER BY");
        StringAssert.Contains(sql, "LIMIT");
        StringAssert.Contains(sql, "count(");
    }
}
