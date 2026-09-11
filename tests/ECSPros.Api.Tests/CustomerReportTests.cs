using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class CustomerReportTests
{
    private static readonly EfektifYetkiler All = new(true, new());
    private static readonly DynamicReportPlan Plan = DynamicReportPlan.Parse("""
        {"version":2,"source":"customers","from":"2026-03-01T00:00:00Z","to":"2026-09-01T00:00:00Z",
         "predicate":{"kind":"compare","field":"customers.returnCount","operator":"gt","values":["0"]},
         "detail":{"columns":["customers.id","customers.firstName","customers.lastName","customers.returnCount"],"sort":"customers.returnCount","direction":"desc","top":20}}
        """);
    private static OrderDbContext Context() => new(new DbContextOptionsBuilder<OrderDbContext>()
        .UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);

    private static ReportPredicate ActivityPredicate() => ReportPredicate.Parse("""
        {"kind":"exists","relation":"customers.orders","children":[
          {"kind":"all","children":[
            {"kind":"compare","field":"orders.status","operator":"eq","values":["delivered"]},
            {"kind":"exists","relation":"orders.items","children":[
              {"kind":"compare","field":"items.productCode","operator":"eq","values":["P-TEST"]}]}]}]}
        """);

    [TestMethod]
    public void CustomerActivityUsesCorrelatedExistsWithPeriodAndChannelGuard()
    {
        using var db = Context();
        var channel = Guid.NewGuid();
        var rights = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null,
            ["orders.view"] = [channel], ["catalog.products.view"] = null });
        var rows = CustomerReportSource.Query(db, Plan with { Predicate = ActivityPredicate() }, rights);
        var sql = rows.ToQueryString();
        StringAssert.Contains(sql, "EXISTS");
        StringAssert.Contains(sql, "MemberId");
        StringAssert.Contains(sql, "FirmPlatformId");
        StringAssert.Contains(sql, channel.ToString());
        StringAssert.Contains(sql, "CreatedAt\" >=");
        StringAssert.Contains(sql, "CreatedAt\" <");
        StringAssert.Contains(sql, "delivered");
        StringAssert.Contains(sql, "P-TEST");
        var detail = CustomerReportSource.Dictionary.Details(_ => true, r => r.Id).Build(rows,
            new() { Columns = ["customers.id", "customers.firstName", "customers.orderCount"], Direction = "asc" },
            new(), ReportSourceCatalog.ResolvePermissions(rights));
        StringAssert.Contains(detail.Page.ToQueryString(), "LIMIT");
        var negative = CustomerReportSource.Query(db, Plan with { Predicate = ActivityPredicate() with { Kind = "notExists" } }, rights);
        StringAssert.Contains(negative.ToQueryString(), "NOT EXISTS");
    }

    [TestMethod]
    public void RelatedFieldsCannotBecomeCustomerColumnsOrEscapePermissions()
    {
        using var db = Context();
        var crm = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null });
        Assert.ThrowsExactly<ArgumentException>(() => CustomerReportSource.Query(db, Plan with { Predicate = ActivityPredicate() }, crm));
        var noCatalog = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null, ["orders.view"] = null });
        Assert.ThrowsExactly<ArgumentException>(() => CustomerReportSource.Query(db, Plan with { Predicate = ActivityPredicate() }, noCatalog));
        var permissions = ReportSourceCatalog.ResolvePermissions(All);
        var fields = DynamicReportMetadata.Fields(permissions, "customers");
        Assert.IsTrue(fields.Any(f => f.Id == "customers.orders" && f.Kind == "relation"));
        Assert.IsTrue(fields.Any(f => f.Id == "orders.status" && f.Kind == "filter"));
        Assert.IsFalse(DynamicReportMetadata.Details(permissions, "customers").Any(f => f.Id.StartsWith("orders.")));
        Assert.IsFalse(DynamicReportMetadata.Fields(ReportSourceCatalog.ResolvePermissions(crm), "customers")
            .Any(f => f.Kind == "relation" || f.Id.StartsWith("orders.")));
        Assert.ThrowsExactly<ArgumentException>(() => CustomerReportSource.Query(db,
            Plan with { Predicate = new() { Kind = "compare", Field = "orders.status", Operator = "eq", Values = ["delivered"] } }, All));
    }

    [TestMethod]
    public void CustomerScopeIsGlobalButActivityHasSeparateChannelPermissions()
    {
        using var db = Context();
        foreach (var effective in new[] { EfektifYetkiler.Bos,
            new(false, new() { ["reports.ai.use"] = [], ["crm.members.view"] = null }),
            new(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = [Guid.NewGuid()] }) })
        {
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => CustomerReportSource.Query(db, Plan, effective));
            Assert.IsFalse(ReportSourceCatalog.ForPermissions(ReportSourceCatalog.ResolvePermissions(effective)).Any(s => s.Id == "customers"));
        }
        var limited = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null,
            ["orders.view"] = [Guid.NewGuid()], ["orders.returns.view"] = [] });
        var sql = CustomerReportSource.Query(db, Plan, limited).ToQueryString();
        StringAssert.Contains(sql, "ANY (@orderChannels)");
        StringAssert.Contains(sql, "ANY (@returnChannels)");
        Assert.IsTrue(ReportSourceCatalog.ForPermissions(ReportSourceCatalog.ResolvePermissions(limited)).Any(s => s.Id == "customers"));
    }

    [TestMethod]
    public void UnauthorizedActivityFieldsAreNotQueriedOrAdvertised()
    {
        using var db = Context();
        var crm = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null });
        var permissions = ReportSourceCatalog.ResolvePermissions(crm);
        var fields = DynamicReportMetadata.Fields(permissions, "customers");
        Assert.IsFalse(fields.Any(f => f.Id is "customers.orderCount" or "customers.returnCount"));
        var rows = CustomerReportSource.Query(db, Plan with { Predicate = null }, crm);
        var sql = rows.ToQueryString();
        Assert.IsFalse(sql.Contains("ord_orders")); Assert.IsFalse(sql.Contains("ord_returns"));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => CustomerReportSource.Dictionary.Details(_ => true, r => r.Id)
            .Build(rows, Plan.Detail!, new(), permissions));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => CustomerReportSource.Query(db, Plan, crm));
        foreach (var secret in new[] { "Email", "Phone", "Password", "IdentityNumber", "Consents" })
            Assert.IsFalse(sql.Contains(secret));
    }

    [TestMethod]
    public void DetailAggregateAndAiUseOneDictionaryAndDoNotRestrictCustomerRegistration()
    {
        using var db = Context();
        var permissions = ReportSourceCatalog.ResolvePermissions(All);
        var rows = CustomerReportSource.Query(db, Plan, All);
        var detail = CustomerReportSource.Dictionary.Details(_ => true, r => r.Id).Build(rows, Plan.Detail!, new(), permissions);
        var sql = detail.Page.ToQueryString();
        StringAssert.Contains(sql, "r.\"CreatedAt\" >= @from");
        StringAssert.Contains(sql, "o.\"CreatedAt\" >= @from");
        Assert.IsFalse(sql.Contains("m.\"CreatedAt\" >="));
        StringAssert.Contains(sql, "GROUP BY o.\"MemberId\"");
        StringAssert.Contains(sql, "LIMIT");
        StringAssert.Contains(sql, "AnonymizedAt");
        var aggregate = CustomerReportSource.Dictionary.Aggregates(_ => true).Build(rows,
            new() { Dimensions = ["customers.active"], Measures = ["customers.count", "customers.returnCount"], Direction = "asc" }, permissions);
        StringAssert.Contains(aggregate.Rows.ToQueryString(), "GROUP BY");
        var request = OpenAiDynamicReportContract.CreateRequest("configured", "son altı ay iade yapan müşteriler", permissions, null, "customers");
        StringAssert.Contains(request, "customers.returnCount");
        Assert.IsFalse(request.Contains("customers.email"));
        var old = new CustomerReportRow { Id = Guid.NewGuid(), CreatedAt = new DateTime(2020, 1, 1), ReturnCount = 2 };
        Assert.AreEqual(1, CustomerReportSource.Dictionary.Predicates(_ => true).Apply(new[] { old }.AsQueryable(), Plan.Predicate!, permissions).Count());
    }
}
