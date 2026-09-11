using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportDistinctCountTests
{
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "orders.view" };
    private sealed record Row(string Group, Guid? Identity, bool Hidden = false);

    [TestMethod]
    public void CountsUniqueNonNullValuesWithinAuthorizedGroups()
    {
        var id = Guid.NewGuid();
        var data = new[] { new Row("A", id), new Row("A", id), new Row("A", null),
            new Row("A", Guid.NewGuid(), true), new Row("B", id), new Row("C", null) }.AsQueryable();
        var dictionary = new ReportEntityDefinition<Row>("orders.view")
            .Text("group", "Grup", r => r.Group).Count("rows", "Kayıt")
            .DistinctCount("unique", "Tekil kimlik", r => r.Identity).Seal();
        var plan = new ReportAggregatePlan { Dimensions = new[] { "group" }, Measures = new[] { "rows", "unique" } };
        var result = dictionary.Aggregates(r => !r.Hidden).Build(data, plan, Permissions).Rows.ToArray();
        Assert.AreEqual(3m, result[0].M0);
        Assert.AreEqual(1m, result[0].M1);
        Assert.AreEqual(1m, result[1].M1);
        Assert.AreEqual(0m, result[2].M1);
        Assert.AreEqual(0, dictionary.Aggregates(r => !r.Hidden).Build(data.Where(r => false), plan, Permissions).Rows.Count());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => dictionary.Aggregates(r => true).Build(data, plan, new HashSet<string>()));
        Assert.AreEqual("metric", dictionary.Describe(Permissions).Single(f => f.Id == "unique").Kind);
    }

    [TestMethod]
    public void PostgreSqlTranslatesDistinctCountAndNullExclusionWithoutConnection()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var schema = new ReportAggregateSchema<OrderEntity>("orders.view", o => !o.IsDeleted)
            .Dimension("currency", o => o.CurrencyCode).DistinctCount("unique", o => o.PaymentMethod);
        var sql = schema.Build(db.Orders, new() { Dimensions = new[] { "currency" }, Measures = new[] { "unique" },
            Sort = "unique", Direction = "desc" }, Permissions).Rows.Take(20).ToQueryString();
        StringAssert.Contains(sql, "DISTINCT");
        StringAssert.Contains(sql, "GROUP BY");
        StringAssert.Contains(sql, "ORDER BY");
        StringAssert.Contains(sql, "LIMIT");
    }

    [TestMethod]
    public void UnsupportedTypesAndDuplicateIdsFailAtDeclaration()
    {
        var dictionary = new ReportEntityDefinition<Row>("orders.view").Count("count", "Adet");
        Assert.ThrowsExactly<ArgumentException>(() => dictionary.DistinctCount("count", "Tekil", r => r.Identity));
        Assert.ThrowsExactly<ArgumentException>(() => dictionary.DistinctCount("hidden", "Yanlış", r => r.Hidden));
    }
}
