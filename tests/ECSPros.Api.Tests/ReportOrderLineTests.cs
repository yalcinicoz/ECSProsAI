using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderEntity = ECSPros.Order.Domain.Entities.Order;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportOrderLineTests
{
    private static readonly HashSet<string> Allowed = new() { "reports.ai.use", "orders.view", "catalog.products.view" };
    private static ReportPredicate Compare(string field, string op, params string[] values) =>
        new() { Kind = "compare", Field = field, Operator = op, Values = values };
    private static ReportPredicate Items(params ReportPredicate[] conditions) => new()
    { Kind = "exists", Relation = "orders.items", Children = new[] { new ReportPredicate { Kind = "all", Children = conditions } } };

    [TestMethod]
    public void SameLineConditionsDoNotCombineDifferentProductsOrMultiplyAmounts()
    {
        var channel = Guid.NewGuid();
        var a = new OrderEntity { Id = Guid.NewGuid(), FirmPlatformId = channel, GrandTotal = 100 };
        var b = new OrderEntity { Id = Guid.NewGuid(), FirmPlatformId = channel, GrandTotal = 200 };
        var other = new OrderEntity { Id = Guid.NewGuid(), FirmPlatformId = Guid.NewGuid(), GrandTotal = 900 };
        var lines = new[] { new OrderReportLine { OrderId = a.Id, ProductCode = "P1", Quantity = 1 },
            new OrderReportLine { OrderId = a.Id, ProductCode = "P2", Quantity = 10 },
            new OrderReportLine { OrderId = b.Id, ProductCode = "P1", Quantity = 3 },
            new OrderReportLine { OrderId = b.Id, ProductCode = "P1", Quantity = 4 },
            new OrderReportLine { OrderId = other.Id, ProductCode = "P1", Quantity = 3 } }.AsQueryable();
        var schema = ReportBusinessDictionary.Orders.Predicates(o => !o.IsDeleted && o.FirmPlatformId == channel);
        ReportBusinessDictionary.OrderItems.Bind(schema, lines, i => !i.IsDeleted, Allowed);
        var rows = schema.Apply(new[] { a, b, other }.AsQueryable(), Items(Compare("items.productCode", "eq", "P1"), Compare("items.quantity", "gte", "2")), Allowed);
        Assert.AreEqual(1, rows.Count());
        Assert.AreEqual(200m, rows.Sum(o => o.GrandTotal));
    }

    [TestMethod]
    public void DeletedLinesAndMissingCatalogDoNotBecomeSkuFallbackMatches()
    {
        var order = new OrderEntity { Id = Guid.NewGuid() };
        var lines = new[] { new OrderReportLine { OrderId = order.Id, ProductCode = "P1", IsDeleted = true },
            new OrderReportLine { OrderId = order.Id, Sku = "P1", ProductCode = null, Barcode = null } }.AsQueryable();
        var schema = ReportBusinessDictionary.Orders.Predicates(o => !o.IsDeleted);
        ReportBusinessDictionary.OrderItems.Bind(schema, lines, i => !i.IsDeleted, Allowed);
        var rows = new[] { order }.AsQueryable();
        Assert.AreEqual(0, schema.Apply(rows, Items(Compare("items.productCode", "eq", "P1")), Allowed).Count());
        Assert.AreEqual(1, schema.Apply(rows, Items(Compare("items.sku", "eq", "P1")), Allowed).Count());
        Assert.AreEqual(1, schema.Apply(rows, Items(Compare("items.productCode", "eq", "P1")) with { Kind = "notExists" }, Allowed).Count());
        Assert.ThrowsExactly<ArgumentException>(() => schema.Apply(rows, Compare("items.sku", "eq", "P1"), Allowed));
    }

    [TestMethod]
    public void ReadProjectionComposesAsParameterizedExistsWithoutMigrationEntity()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var channel = Guid.NewGuid();
        var effective = new EfektifYetkiler(false, new() { ["reports.ai.use"] = new() { channel }, ["orders.view"] = null, ["catalog.products.view"] = null });
        var query = OrderReportPredicates.Apply(db.Orders, Items(Compare("items.productCode", "eq", "P1' OR 1=1 --"), Compare("items.barcode", "eq", "8690")), effective, db);
        var sql = query.ToQueryString();
        StringAssert.Contains(sql, "EXISTS");
        StringAssert.Contains(sql, "catalog.product_variants");
        StringAssert.Contains(sql, "catalog.products");
        StringAssert.Contains(sql, "FirmPlatformId");
        StringAssert.Contains(sql, "NOT v.\"IsDeleted\"");
        Assert.IsFalse(sql[sql.IndexOf("SELECT", StringComparison.Ordinal)..].Contains("P1' OR 1=1 --"));
        Assert.IsNull(db.Model.FindEntityType(typeof(OrderReportLine)));
    }

    [TestMethod]
    public void DictionaryExposesOnlyAuthorizedFilterFieldsNotItemSalesMetrics()
    {
        var fields = DynamicReportMetadata.Fields(Allowed);
        Assert.IsTrue(fields.Any(f => f.Id == "orders.items" && f.Kind == "relation"));
        Assert.IsTrue(fields.Any(f => f.Id == "items.barcode" && f.Kind == "filter"));
        Assert.IsFalse(fields.Any(f => f.Id.StartsWith("items.") && f.Kind == "metric"));
        Assert.IsFalse(DynamicReportMetadata.Fields(new HashSet<string> { "reports.ai.use" }).Any(f => f.Id.StartsWith("items.")));
        Assert.ThrowsExactly<ArgumentException>(() => ReportBusinessDictionary.Orders.Aggregates(o => true).Build(Array.Empty<OrderEntity>().AsQueryable(),
            new() { Dimensions = new[] { "items.productCode" }, Measures = new[] { "orders.count" } }, Allowed));
    }

    [TestMethod]
    public void MissingOrScopedCatalogPermissionHidesFieldsAndRejectsPositiveAndNegativeConditions()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var orderOnly = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null });
        var scoped = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null,
            ["catalog.products.view"] = new() { Guid.NewGuid() } });
        var empty = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null,
            ["catalog.products.view"] = new() });
        foreach (var effective in new[] { orderOnly, scoped, empty })
        {
            var permissions = ReportSourceCatalog.ResolvePermissions(effective);
            Assert.IsFalse(DynamicReportMetadata.Fields(permissions).Any(f => f.Id == "orders.items" || f.Id.StartsWith("items.")));
            var positive = Items(Compare("items.productCode", "eq", "P1"));
            foreach (var predicate in new[] { positive, positive with { Kind = "notExists" },
                new ReportPredicate { Kind = "not", Children = new[] { positive } },
                new ReportPredicate { Kind = "any", Children = new[] { Compare("orders.status", "eq", "pending"), positive } } })
                Assert.ThrowsExactly<ArgumentException>(() => OrderReportPredicates.Apply(db.Orders, predicate, effective, db));
            // Ordinary order reporting remains available without catalog permission.
            StringAssert.Contains(OrderReportPredicates.Apply(db.Orders, Compare("orders.status", "eq", "pending"), effective, db).ToQueryString(), "SELECT");
        }
    }

    [TestMethod]
    public void CatalogPermissionAloneCannotAuthorizeOrdersAndRevocationRejectsPreviousPlan()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test").Options);
        var predicate = Items(Compare("items.barcode", "eq", "8690"));
        var catalogOnly = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["catalog.products.view"] = null });
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OrderReportPredicates.Apply(db.Orders, predicate, catalogOnly, db));
        var allowed = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null, ["catalog.products.view"] = null });
        StringAssert.Contains(OrderReportPredicates.Apply(db.Orders, predicate, allowed, db).ToQueryString(), "EXISTS");
        var revoked = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null });
        Assert.ThrowsExactly<ArgumentException>(() => OrderReportPredicates.Apply(db.Orders, predicate, revoked, db));
    }
}
