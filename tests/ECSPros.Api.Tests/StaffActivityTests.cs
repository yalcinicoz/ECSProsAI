using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class StaffActivityTests
{
    private static DynamicReportPlan Plan => DynamicReportPlan.Parse("""
        {"version":2,"source":"staffActivities","from":"2026-09-01T00:00:00Z","to":"2026-10-01T00:00:00Z",
         "aggregate":{"dimensions":["staff.actorId","staff.actorName","staff.activity"],"measures":["staff.count"],"direction":"desc","sort":"staff.count"}}
        """);
    private static OrderDbContext Db() => new(new DbContextOptionsBuilder<OrderDbContext>()
        .UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);

    [TestMethod]
    public void StaffIdentityAndDomainRightsAreRequiredBeforeQuery()
    {
        using var db = Db();
        foreach (var rights in new[] { EfektifYetkiler.Bos,
            new(false, new() { ["reports.ai.use"] = null, ["fulfillment.view"] = null }),
            new(false, new() { ["reports.ai.use"] = null, ["iam.users.view"] = null }),
            new(false, new() { ["reports.ai.use"] = null, ["iam.users.view"] = [], ["fulfillment.view"] = null }) })
        {
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => StaffActivitySource.Query(db, Plan, rights));
            Assert.IsFalse(ReportSourceCatalog.ForPermissions(ReportSourceCatalog.ResolvePermissions(rights)).Any(s => s.Id == StaffActivitySource.Id));
        }
    }

    [TestMethod]
    public void BranchesArePermissionScopedAndNeverReadUnauthorizedDomains()
    {
        using var db = Db();
        var channel = Guid.NewGuid();
        var operation = new EfektifYetkiler(false, new() { ["reports.ai.use"] = [channel], ["iam.users.view"] = null,
            ["fulfillment.view"] = [channel] });
        var sql = StaffActivitySource.Query(db, Plan, operation).ToQueryString();
        StringAssert.Contains(sql, "ful_operation_logs");
        StringAssert.Contains(sql, "ANY (@operationChannels)");
        Assert.IsFalse(sql.Contains("ord_invoices"));
        var invoice = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["iam.users.view"] = null,
            ["orders.invoices.view"] = [] });
        sql = StaffActivitySource.Query(db, Plan, invoice).ToQueryString();
        Assert.IsFalse(sql.Contains("ful_operation_logs"));
        StringAssert.Contains(sql, "ANY (@invoiceChannels)");
        StringAssert.Contains(sql, "\"LegacyInvoiceId\" IS NULL");
        StringAssert.Contains(sql, "\"NumberSource\"='internal'");
        StringAssert.Contains(sql, "\"CreatedBy\" IS NOT NULL");
        foreach (var secret in new[] { "Password", "Email", "Phone", "Detail", "RecipientName" }) Assert.IsFalse(sql.Contains(secret));
    }

    [TestMethod]
    public void DetailAggregateFiltersAndMetadataUseTheSameFields()
    {
        using var db = Db();
        var rights = new EfektifYetkiler(true, new());
        var permissions = ReportSourceCatalog.ResolvePermissions(rights);
        var rows = StaffActivitySource.Query(db, Plan, rights);
        var aggregate = StaffActivitySource.Dictionary.Aggregates(_ => true).Build(rows, Plan.Aggregate!, permissions);
        StringAssert.Contains(aggregate.Rows.ToQueryString(), "GROUP BY");
        StringAssert.Contains(aggregate.Rows.ToQueryString(), "UNION ALL");
        var detail = StaffActivitySource.Dictionary.Details(_ => true, r => r.Id).Build(rows,
            new() { Columns = ["staff.actorId", "staff.actorName", "staff.activity", "staff.occurredAt"], Direction = "asc" }, new(), permissions);
        StringAssert.Contains(detail.Page.ToQueryString(), "LIMIT");
        Assert.AreEqual(7, DynamicReportMetadata.Fields(permissions, StaffActivitySource.Id).Count);
        var request = OpenAiDynamicReportContract.CreateRequest("configured", "bu ayki personel işlem sayılarını göster", permissions, null, StaffActivitySource.Id);
        StringAssert.Contains(request, "staff.count");
        Assert.IsFalse(request.Contains("staff.revenue"));
        Assert.ThrowsExactly<ArgumentException>(() => StaffActivitySource.Query(db, Plan with {
            Predicate = new() { Kind = "compare", Field = "staff.revenue", Operator = "gt", Values = ["0"] } }, rights));
    }
}
