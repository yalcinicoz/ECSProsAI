using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportTopLimitTests
{
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "orders.view" };
    private static ReportAggregateSchema<OrderEntity> Schema() => new ReportAggregateSchema<OrderEntity>("orders.view", o => !o.IsDeleted)
        .Dimension("status", o => o.Status).Count("count");
    private static ReportAggregatePlan Plan(int? top = 2) => new() { Dimensions = new[] { "status" }, Measures = new[] { "count" }, Sort = "count", Direction = "desc", Top = top };
    private static IQueryable<OrderEntity> Data() => new[] { "B", "B", "A", "A", "C" }
        .Select(s => new OrderEntity { Status = s }).Concat(Enumerable.Range(0, 10).Select(_ => new OrderEntity { Status = "hidden", IsDeleted = true })).AsQueryable();

    [TestMethod]
    public void TopUsesAllAuthorizedGroupsAndStableTiesBeforeTablePaging()
    {
        var query = Schema().Build(Data(), Plan(), Permissions);
        CollectionAssert.AreEqual(new[] { "A", "B" }, query.Rows.Select(r => r.D0).ToArray());
        var table = ReportAggregateGrid.Apply(query, Plan(), new(PageSize: 1, Sort: "status", Dir: "desc"));
        Assert.AreEqual(2, table.Count());
        Assert.AreEqual("B", table.Take(1).Single().D0);
        Assert.AreEqual("A", table.Skip(1).Take(1).Single().D0);
        Assert.AreEqual(0, table.Skip(2).Take(1).Count());
        Assert.AreEqual(3, Schema().Build(Data(), Plan(null), Permissions).Rows.Count());
        Assert.AreEqual("C", Schema().Build(Data(), Plan(1) with { Direction = "asc" }, Permissions).Rows.Single().D0);
    }

    [TestMethod]
    public void TableFiltersDoNotRefillTopSetOrChangeRanking()
    {
        var query = Schema().Build(Data(), Plan(), Permissions);
        Assert.AreEqual(0, ReportAggregateGrid.Apply(query, Plan(), new(Search: "C")).Count());
        Assert.AreEqual(1, ReportAggregateGrid.Apply(query, Plan(), new(Search: "B")).Count());
        Assert.AreEqual(0, ReportAggregateGrid.Apply(query, Plan(), new(Filters: new[] { new ReportGridFilter("count", "lt", "2") })).Count());
    }

    [TestMethod]
    public void InvalidLimitsAndAmbiguousRankingAreRejectedWithoutClamping()
    {
        foreach (var plan in new[] { Plan(0), Plan(-1), Plan(1001), Plan() with { Sort = null }, Plan() with { Dimensions = Array.Empty<string>() } })
            Assert.ThrowsExactly<ArgumentException>(() => Schema().Build(Data(), plan, Permissions));
        foreach (var invalid in new[] { "0", "1001", "1.5", "\"20\"" })
            Assert.ThrowsExactly<ArgumentException>(() => DynamicReportPlan.Parse(DynamicReportPlanTests.Json.Replace("\"direction\":\"desc\"", "\"direction\":\"desc\",\"top\":" + invalid)));
        Assert.IsNull(DynamicReportPlan.Parse(DynamicReportPlanTests.Json).Aggregate!.Top);
        Assert.AreEqual(3, Schema().Build(Data(), Plan(1000), Permissions).Rows.Count());
        Assert.AreEqual(0, Schema().Build(Data().Where(_ => false), Plan(), Permissions).Rows.Count());
    }

    [TestMethod]
    public void PostgreSqlKeepsTopLimitInsideTableFilterAndPage()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var query = Schema().Build(db.Orders, Plan(), Permissions);
        var table = ReportAggregateGrid.Apply(query, Plan(), new(Search: "paid", Sort: "status", Dir: "asc"));
        var sql = table.Skip(1).Take(1).ToQueryString();
        StringAssert.Contains(sql, "GROUP BY");
        Assert.IsTrue(sql.IndexOf("LIMIT", StringComparison.Ordinal) < sql.LastIndexOf("WHERE", StringComparison.Ordinal), sql);
        Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(sql, @"\bLIMIT\b").Count, sql);
    }

    [TestMethod]
    public void AiSchemaRequiresNullableTopAndKeepsConfiguredModel()
    {
        using var doc = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("configured-model", "en fazla sipariş olan ilk 2 durum", Permissions, null));
        var aggregate = doc.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema")
            .GetProperty("properties").GetProperty("plan").GetProperty("anyOf")[0].GetProperty("properties").GetProperty("aggregate").GetProperty("anyOf")[0];
        Assert.IsTrue(aggregate.GetProperty("required").EnumerateArray().Any(v => v.GetString() == "top"));
        Assert.AreEqual("integer", aggregate.GetProperty("properties").GetProperty("top").GetProperty("anyOf")[0].GetProperty("type").GetString());
        Assert.AreEqual("null", aggregate.GetProperty("properties").GetProperty("top").GetProperty("anyOf")[1].GetProperty("type").GetString());
        Assert.AreEqual("configured-model", doc.RootElement.GetProperty("model").GetString());
    }
}
