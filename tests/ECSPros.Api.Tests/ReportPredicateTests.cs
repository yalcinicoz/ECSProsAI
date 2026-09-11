using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Domain.Entities;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportPredicateTests
{
    private static readonly HashSet<string> Allowed = new() { "reports.ai.use", "orders.view", "orders.returns.view" };
    private static ReportPredicate Compare(string field, string op, params string[] values) =>
        new() { Kind = "compare", Field = field, Operator = op, Values = values };
    private static ReportPredicate Group(string kind, params ReportPredicate[] children) => new() { Kind = kind, Children = children };
    private static ReportPredicate Related(string kind, string relation, ReportPredicate child) => new() { Kind = kind, Relation = relation, Children = new[] { child } };
    private static ReportPredicateSchema<OrderEntity> Schema() => new ReportPredicateSchema<OrderEntity>("orders.view", o => !o.IsDeleted)
        .Field("number", o => o.OrderNumber).Field("amount", o => o.GrandTotal).Field("date", o => o.CreatedAt);

    [TestMethod]
    public void BooleanCombinationsCannotNegateMandatoryScope()
    {
        var rows = new[] { new OrderEntity { OrderNumber = "A", GrandTotal = 10 },
            new OrderEntity { OrderNumber = "B", GrandTotal = 20 }, new OrderEntity { OrderNumber = "C", IsDeleted = true } }.AsQueryable();
        var predicate = Group("any", Compare("amount", "gte", "20"), Group("not", Compare("number", "eq", "B")));
        Assert.AreEqual(2, Schema().Apply(rows, predicate, Allowed).Count());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => Schema().Apply(rows, predicate, new HashSet<string> { "reports.ai.use" }));
    }

    [TestMethod]
    public void RelatedRowsUseExistsWithoutMultiplyingOrderAmount_AndRespectChildGuard()
    {
        var order = new OrderEntity { Id = Guid.NewGuid(), GrandTotal = 100 };
        var payments = new[] { new OrderPayment { OrderId = order.Id, Status = "paid" }, new OrderPayment { OrderId = order.Id, Status = "paid" },
            new OrderPayment { OrderId = order.Id, Status = "failed", IsDeleted = true } }.AsQueryable();
        var schema = Schema().Relation("payments", payments, o => o.Id, p => p.OrderId,
            new ReportPredicateSchema<OrderPayment>("orders.view", p => !p.IsDeleted).Field("status", p => p.Status));
        var rows = new[] { order }.AsQueryable();
        Assert.AreEqual(100m, schema.Apply(rows, Related("exists", "payments", Compare("status", "eq", "paid")), Allowed).Sum(o => o.GrandTotal));
        Assert.AreEqual(1, schema.Apply(rows, Related("notExists", "payments", Compare("status", "eq", "failed")), Allowed).Count());
    }

    [TestMethod]
    public void InvalidPayloadFieldsTypesAndComplexityFailBeforeExecution()
    {
        foreach (var json in new[] { "null", "{\"kind\":\"all\",\"kind\":\"any\"}", "{\"sql\":\"SELECT 1\"}" })
            Assert.ThrowsExactly<ArgumentException>(() => ReportPredicate.Parse(json));
        var rows = Array.Empty<OrderEntity>().AsQueryable();
        foreach (var predicate in new[] { Compare("hidden", "eq", "A"), Compare("amount", "eq", "1 OR 1=1"),
            Compare("date", "between", "2026-08-01", "2026-09-01"), Compare("amount", "between", "20", "10"),
            Compare("number", "sql", "A"), Group("any"), Compare("amount", "contains", "1"),
            Related("exists", "unknown", Compare("amount", "eq", "1")) })
            Assert.ThrowsExactly<ArgumentException>(() => Schema().Apply(rows, predicate, Allowed));
        var deep = Compare("number", "eq", "A");
        for (var i = 0; i < 8; i++) deep = Group("not", deep);
        Assert.ThrowsExactly<ArgumentException>(() => Schema().Apply(rows, deep, Allowed));
    }

    [TestMethod]
    public void DateRangesAreOffsetAwareAndEndExclusive()
    {
        var rows = new[] { new OrderEntity { CreatedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
            new OrderEntity { CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc) } }.AsQueryable();
        Assert.AreEqual(1, Schema().Apply(rows, Compare("date", "between", "2026-08-01T03:00:00+03:00", "2026-09-01T03:00:00+03:00"), Allowed).Count());
    }

    [TestMethod]
    public void RealOrderAdapterTranslatesRelationsAndLiteralsToSql_WithoutDatabaseConnection()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var channel = Guid.NewGuid();
        var effective = new EfektifYetkiler(false, new()
        { ["reports.ai.use"] = new() { channel }, ["orders.view"] = null, ["orders.returns.view"] = new() { channel } });
        var predicate = Group("all", Compare("orders.orderNumber", "eq", "literal' OR 1=1 --"),
            Related("exists", "orders.payments", Compare("payments.status", "eq", "paid")),
            Related("notExists", "orders.returns", Compare("returns.status", "eq", "received")));
        var sql = OrderReportPredicates.Apply(db.Orders, predicate, effective, db).ToQueryString();
        Assert.IsTrue(sql.Contains("EXISTS"));
        Assert.IsTrue(sql.Contains("NOT EXISTS"));
        Assert.IsTrue(sql.Contains("@__"));
        Assert.IsFalse(sql[sql.IndexOf("SELECT", StringComparison.Ordinal)..].Contains("literal' OR 1=1 --"));
        var noReturns = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null });
        Assert.ThrowsExactly<ArgumentException>(() => OrderReportPredicates.Apply(db.Orders, predicate, noReturns, db));
    }

    [TestMethod]
    public void ExistingOrderSourceAppliesDynamicConditionsBeforeSummaryAndPaging()
    {
        var channel = Guid.NewGuid();
        var effective = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null });
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = Enumerable.Range(1, 30).Select(i => new OrderEntity
        { FirmPlatformId = channel, CreatedAt = from.UtcDateTime.AddDays(1), OrderNumber = $"O-{i}", GrandTotal = i, CurrencyCode = "TRY" }).AsQueryable();
        var source = OrderReportSource.Create(rows, effective, new(from, from.AddMonths(1)),
            new ECSPros.Shared.Kernel.Grid.GridRequest { PageSize = 1 }, predicate: Compare("orders.amount", "gte", "20"));
        Assert.AreEqual(1, source.Details().Count());
        Assert.AreEqual(11L, source.Summary().Single().OrderCount);
        Assert.AreEqual(275m, source.Summary().Single().OrderAmount);
    }

    [TestMethod]
    public void ChildPermissionIsRequiredEvenInsideNegation()
    {
        var schema = Schema().Relation("returns", Array.Empty<Return>().AsQueryable(), o => o.Id, r => r.OrderId,
            new ReportPredicateSchema<Return>("orders.returns.view", r => !r.IsDeleted).Field("status", r => r.Status));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => schema.Apply(Array.Empty<OrderEntity>().AsQueryable(),
            Related("notExists", "returns", Compare("status", "eq", "received")), new HashSet<string> { "reports.ai.use", "orders.view" }));
    }
}
