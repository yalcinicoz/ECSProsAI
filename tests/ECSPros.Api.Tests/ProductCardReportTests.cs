using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ProductCardReportTests
{
    [TestMethod]
    public void AiRelationShapeCannotCarryCompareFieldsAndCompilerStillRejectsMixedNodes()
    {
        var rights = new EfektifYetkiler(true, new());
        var permissions = ReportSourceCatalog.ResolvePermissions(rights);
        using var request = System.Text.Json.JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest(
            "configured", "ilk altı ay hareketsiz", permissions, null, ProductCardReportSource.Id));
        var branches = request.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema")
            .GetProperty("$defs").GetProperty("predicate").GetProperty("anyOf").EnumerateArray().ToArray();
        Assert.AreEqual(4, branches.Length);
        var dates = branches.Single(b => b.GetProperty("properties").GetProperty("field").TryGetProperty("enum", out var ids)
            && ids.EnumerateArray().Any(v => v.GetString() == "cardMovements.createdAt"));
        Assert.AreEqual("date-time", dates.GetProperty("properties").GetProperty("values").GetProperty("anyOf")[0]
            .GetProperty("items").GetProperty("format").GetString());
        var relation = branches.Single(b => b.GetProperty("properties").GetProperty("kind").GetProperty("enum")
            .EnumerateArray().Any(v => v.GetString() == "notExists"));
        foreach (var field in new[] { "field", "operator", "values" })
            Assert.AreEqual("null", relation.GetProperty("properties").GetProperty(field).GetProperty("type").GetString());
        Assert.IsFalse(relation.GetProperty("additionalProperties").GetBoolean());
        Assert.AreEqual(6, relation.GetProperty("required").GetArrayLength());
        using var db = Db();
        var plan = Plan with { CardWindowMonths = 6 };
        StringAssert.Contains(ProductCardReportSource.Query(db, plan, rights).ToQueryString(), "NOT EXISTS");
        Assert.ThrowsExactly<ArgumentException>(() => ProductCardReportSource.Query(db,
            plan with { Predicate = plan.Predicate! with { Field = "cardMovements.createdAt" } }, rights));
    }

    private static DynamicReportPlan Plan => DynamicReportPlan.Parse("""
        {"version":2,"source":"productCards","from":"2026-08-01T00:00:00Z","to":"2026-09-01T00:00:00Z",
         "predicate":{"kind":"notExists","relation":"cards.movements","children":[
           {"kind":"compare","field":"cardMovements.createdAt","operator":"gte","values":["2026-08-01T00:00:00Z"]}]},
         "detail":{"columns":["cards.code","cards.name","cards.createdAt"],"direction":"asc"}}
        """);
    private static OrderDbContext Db() => new(new DbContextOptionsBuilder<OrderDbContext>()
        .UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);

    [TestMethod]
    public void BothGlobalDomainsAreRequiredAndCannotBeBypassedByNotExists()
    {
        using var db = Db();
        foreach (var rights in new[] { EfektifYetkiler.Bos,
            new(false, new() { ["reports.ai.use"] = null, ["inventory.view"] = null }),
            new(false, new() { ["reports.ai.use"] = null, ["catalog.products.view"] = null }),
            new(false, new() { ["reports.ai.use"] = [], ["catalog.products.view"] = null, ["inventory.view"] = null }),
            new(false, new() { ["reports.ai.use"] = null, ["catalog.products.view"] = [], ["inventory.view"] = null }) })
        {
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => ProductCardReportSource.Query(db, Plan, rights));
            Assert.IsFalse(ReportSourceCatalog.ForPermissions(ReportSourceCatalog.ResolvePermissions(rights)).Any(s => s.Id == ProductCardReportSource.Id));
        }
    }

    [TestMethod]
    public void NoMovementUsesCatalogAntiJoinNotCurrentStockOrVariantActiveStatus()
    {
        using var db = Db();
        var rights = new EfektifYetkiler(true, new());
        var sql = ProductCardReportSource.Query(db, Plan, rights).ToQueryString();
        StringAssert.Contains(sql, "NOT EXISTS");
        StringAssert.Contains(sql, "catalog.products");
        StringAssert.Contains(sql, "inventory.inv_stock_movements");
        Assert.IsFalse(sql.Contains("inv_stocks"));
        Assert.IsFalse(sql.Contains("v.\"IsDeleted\""));
        Assert.IsFalse(sql.Contains("v.\"IsActive\""));
        Assert.IsFalse(sql.Contains("p.\"CreatedAt\" >="));
        StringAssert.Contains(sql, "CreatedAt\" >=");
        StringAssert.Contains(sql, "CreatedAt\" <");
        var permissions = ReportSourceCatalog.ResolvePermissions(rights);
        var rows = ProductCardReportSource.Query(db, Plan, rights);
        var detail = ProductCardReportSource.Dictionary.Details(_ => true, p => p.Id).Build(rows, Plan.Detail!, new(), permissions);
        StringAssert.Contains(detail.Page.ToQueryString(), "LIMIT");
        Assert.IsFalse(DynamicReportMetadata.Details(permissions, ProductCardReportSource.Id).Any(f => f.Id.StartsWith("cardMovements.")));
        var request = OpenAiDynamicReportContract.CreateRequest("configured", "geçen ay hareketi olmayan kartlar", permissions, null, ProductCardReportSource.Id);
        StringAssert.Contains(request, "cards.movements");
        StringAssert.Contains(request, "per-card relative window");
    }

    [TestMethod]
    public void MovementFieldsMustRemainInTheirRelationAndNoPredicateDoesNotReadMovements()
    {
        using var db = Db();
        var rights = new EfektifYetkiler(true, new());
        Assert.IsFalse(ProductCardReportSource.Query(db, Plan with { Predicate = null }, rights).ToQueryString().Contains("inv_stock_movements"));
        Assert.ThrowsExactly<ArgumentException>(() => ProductCardReportSource.Query(db, Plan with {
            Predicate = new() { Kind = "compare", Field = "cardMovements.quantity", Operator = "gt", Values = ["0"] } }, rights));
    }

    [TestMethod]
    public void InitialWindowIsParameterizedCohortWithCompletedCalendarWindow()
    {
        using var db = Db();
        var plan = Plan with { CardWindowMonths = 6 };
        var sql = ProductCardReportSource.Query(db, plan, new(true, new()), observedAt: DateTimeOffset.Parse("2027-03-01T00:00:00Z")).ToQueryString();
        StringAssert.Contains(sql, "make_interval(months => @");
        StringAssert.Contains(sql, "AT TIME ZONE 'Europe/Istanbul'");
        StringAssert.Contains(sql, "m.\"CreatedAt\" >= p.\"CreatedAt\"");
        StringAssert.Contains(sql, "m.\"CreatedAt\" <");
        StringAssert.Contains(sql, "NOT EXISTS");
        StringAssert.Contains(sql, "p.\"CreatedAt\" >= @");
        StringAssert.Contains(sql, "<= @");
        Assert.IsFalse(sql.Contains("inv_stocks"));
        var roundtrip = DynamicReportPlan.Parse(System.Text.Json.JsonSerializer.Serialize(plan, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }));
        Assert.AreEqual(6, roundtrip.CardWindowMonths);
    }

    [TestMethod]
    public void InvalidOrCrossSourceWindowIsRejected()
    {
        using var db = Db();
        foreach (var months in new[] { 0, -1, 13, int.MaxValue })
            Assert.ThrowsExactly<ArgumentException>(() => ProductCardReportSource.Query(db, Plan with { CardWindowMonths = months }, new(true, new())));
        Assert.ThrowsExactly<ArgumentException>(() => (Plan with { Source = "orders", CardWindowMonths = 6 }).ValidateCardWindow());
        var request = OpenAiDynamicReportContract.CreateRequest("configured", "ilk altı ay hareketsiz", ReportSourceCatalog.ResolvePermissions(new(true, new())), null, ProductCardReportSource.Id);
        StringAssert.Contains(request, "cardWindowMonths");
        StringAssert.Contains(request, "cohort dates are missing");
    }
}
