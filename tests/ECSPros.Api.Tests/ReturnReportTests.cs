using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Domain.Entities;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReturnReportTests
{
    private static readonly Guid Channel = Guid.NewGuid();
    private static readonly HashSet<string> Allowed = ["reports.ai.use", "orders.returns.view"];
    private static readonly EfektifYetkiler Effective = new(false, new() { ["reports.ai.use"] = [Channel], ["orders.returns.view"] = null });
    private const string Json = """
        {"version":2,"source":"returns","from":"2026-09-01T00:00:00Z","to":"2026-10-01T00:00:00Z",
        "aggregate":{"dimensions":["returns.currencyCode"],"measures":["returns.count","returns.amount"],"direction":"asc"}}
        """;

    [TestMethod]
    public void ReturnDateAndChannelScopeExcludeDeletedAndOtherChannels()
    {
        var plan = DynamicReportPlan.Parse(Json);
        var from = plan.Scope().From.UtcDateTime;
        Return Row(Guid channel, DateTime date, decimal amount) => new() { CreatedAt = date, RefundAmount = amount,
            Order = new() { FirmPlatformId = channel, CurrencyCode = "TRY", CreatedAt = from.AddYears(-2) } };
        var deleted = Row(Channel, from, 999); deleted.IsDeleted = true;
        var deletedOrder = Row(Channel, from, 999); deletedOrder.Order.IsDeleted = true;
        var rows = new[] { Row(Channel, from, 10), Row(Channel, from.AddDays(1), 20),
            Row(Guid.NewGuid(), from, 999), Row(Channel, plan.Scope().To.UtcDateTime, 999), deleted, deletedOrder }.AsQueryable();
        var filtered = ReturnReportSource.Apply(rows, plan, Effective);
        var result = ReturnReportSource.Dictionary.Aggregates(_ => true).Build(filtered, plan.Aggregate!, Allowed).Rows.Single();
        Assert.AreEqual(2m, result.M0); Assert.AreEqual(30m, result.M1);
        var empty = new EfektifYetkiler(false, new() { ["reports.ai.use"] = [Channel], ["orders.returns.view"] = [Guid.NewGuid()] });
        Assert.AreEqual(0, ReturnReportSource.Apply(rows, plan, empty).Count());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => ReturnReportSource.Apply(rows, plan, EfektifYetkiler.Bos));
        Assert.AreEqual(0, ReturnReportSource.Apply(rows, plan, new(false, new() { ["reports.ai.use"] = null, ["orders.returns.view"] = [] })).Count());
    }

    [TestMethod]
    public void TranslationCurrencyAndHiddenFieldGuardsWorkWithoutDatabase()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);
        var plan = DynamicReportPlan.Parse(Json);
        var rows = ReturnReportSource.Apply(db.Returns, plan, Effective);
        var sql = ReturnReportSource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate!, Allowed).Rows.ToQueryString();
        StringAssert.Contains(sql, "GROUP BY"); StringAssert.Contains(sql, "FirmPlatformId");
        var detail = ReturnReportSource.Dictionary.Details(_ => true, r => r.Id).Build(rows,
            new() { Columns = ["returns.orderNumber", "returns.amount", "returns.currencyCode"], Direction = "asc" }, ReportGridState.ForExport(new()), Allowed);
        StringAssert.Contains(detail.Page.ToQueryString(), "LIMIT");
        Assert.ThrowsExactly<ArgumentException>(() => ReturnReportSource.Dictionary.Aggregates(_ => true).Build(rows,
            plan.Aggregate! with { Dimensions = [] }, Allowed));
        Assert.ThrowsExactly<ArgumentException>(() => ReturnReportSource.Apply(rows, plan with { Predicate = new() {
            Kind = "compare", Field = "returns.customerNotes", Operator = "contains", Values = ["private"] } }, Effective));
        Assert.IsFalse(sql.Contains("CustomerNotes"));
    }

    [TestMethod]
    public void SourceAndAiSchemaWorkForReturnOnlyPermission()
    {
        CollectionAssert.AreEqual(new[] { "returns" }, ReportSourceCatalog.ForPermissions(Allowed).Select(s => s.Id).ToArray());
        Assert.IsTrue(ReportDictionary.CanReport(Allowed));
        using var request = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("configured", "iade raporu", Allowed, null, "returns"));
        var schema = request.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema").GetRawText();
        StringAssert.Contains(schema, "returns.createdAt"); Assert.IsFalse(schema.Contains("orders."));
        Assert.IsFalse(schema.Contains("customerNotes"));
        var text = "{\"decision\":\"ready\",\"clarification\":\"none\",\"plan\":" + Json + "}";
        var response = JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } });
        Assert.AreEqual("ready", OpenAiDynamicReportContract.ParseResponse(response, Allowed, "returns").Status);
    }
}
