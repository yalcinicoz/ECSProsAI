using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Shared.Kernel.Grid;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class OrderReportRecipeTests
{
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "orders.view" };
    public const string Json = """
        {"version":1,"subject":"orders","metrics":["orders.count","orders.amount"],
        "dimensions":["orders.status","orders.currencyCode"],
        "filters":[{"field":"orders.createdAt","operator":"between","values":["2026-08-01T00:00:00+03:00","2026-09-01T00:00:00+03:00"]}],
        "presentation":"table","limit":1000}
        """;
    [TestMethod]
    public void OrderOnlyPermissionSupportsOrdersButNotStock()
    {
        var parsed = ReportDefinitionValidator.Parse(Json, Permissions);
        Assert.IsTrue(parsed.IsValid, parsed.Error);
        var plan = OrderReportRecipe.Parse(parsed.Definition!, Permissions);
        Assert.IsFalse(plan.Details);
        Assert.AreEqual(new DateTime(2026, 7, 31, 21, 0, 0, DateTimeKind.Utc), plan.Scope.From.UtcDateTime);
        Assert.IsFalse(ReportDefinitionValidator.Parse(Json, new HashSet<string> { "reports.ai.use", "inventory.view" }).IsValid);
        Assert.AreEqual(0, ReportDictionary.ForPermissions(Permissions).Count);
    }
    [TestMethod]
    public void MissingCurrencyDatesAndHiddenFieldsFailClosed()
    {
        foreach (var json in new[] { Json.Replace(",\"orders.currencyCode\"", ""), Json.Replace("+03:00", ""),
            Json.Replace("2026-09-01", "2028-09-01"), Json.Replace("orders.status", "customer.phone"),
            Json.Replace("orders.amount", "profit"), Json.Replace("\"orders.status\"", "\"orders.paymentMethod\"") })
            Assert.IsFalse(ReportDefinitionValidator.Parse(json, Permissions).IsValid, json);
    }
    [TestMethod]
    public void ModelSchemaIsSubjectScoped_AndOtherSubjectResponseIsRejected()
    {
        using var request = JsonDocument.Parse(OpenAiReportContract.CreateRequest("configured-model", "geçen ay sipariş özeti", Permissions, subject: "orders"));
        var format = request.RootElement.GetProperty("text").GetProperty("format");
        Assert.IsTrue(format.GetProperty("strict").GetBoolean());
        Assert.IsFalse(format.GetRawText().Contains("stock.quantity"));
        var response = JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message", content = new[] {
            new { type = "output_text", text = "{\"decision\":\"ready\",\"clarification\":\"none\",\"definition\":" + Json + "}" } } } } });
        Assert.AreEqual("ready", OpenAiReportContract.ParseResponse(response, Permissions, subject: "orders").Status);
        Assert.AreEqual("denied", OpenAiReportContract.ParseResponse(response, Permissions).Status);
        Assert.IsTrue(request.RootElement.GetProperty("instructions").GetString()!.Contains("Current business date:"));
    }
    [TestMethod]
    public void SummaryFiltersSortAndCurrencyTotalsTranslateAfterAggregation()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql("Host=localhost;Database=offline;Username=unused").Options);
        var definition = ReportDefinitionValidator.Parse(Json, Permissions).Definition!;
        var plan = OrderReportRecipe.Parse(definition, Permissions);
        var auth = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = new() { Guid.NewGuid() } });
        var basis = OrderReportSource.Create(db.Orders, auth, plan.Scope, plan.BaseFilters);
        var request = OrderReportExecutor.ValidateGrid(definition, new(Sort: "orders.amount", Filters: [new("orders.amount", "gt", "100")]), false);
        var q = OrderReportExecutor.SummarySchema.ApplyFilters(basis.Summary("status"), request);
        StringAssert.Contains(OrderReportExecutor.SummarySchema.ApplySort(q, request).Skip(25).Take(25).ToQueryString(), "LIMIT");
        var totals = q.GroupBy(r => r.CurrencyCode).Select(g => new ReportCurrencyTotal(g.Key, g.Sum(r => r.OrderCount), g.Sum(r => r.OrderAmount))).ToQueryString();
        StringAssert.Contains(totals, "GROUP BY");
        Assert.IsFalse(totals.Contains("LIMIT"));
        Assert.ThrowsExactly<ArgumentException>(() => OrderReportExecutor.ValidateGrid(definition, new(Filters: [new("orders.orderType", "eq", "retail")]), false));
    }
    [TestMethod]
    public void OrderClarificationHistoryKeepsDateAndLayoutAnswers()
    {
        var current = ApprovedReportPrompt.Create("detay listesi", true);
        var history = ReportConversation.Create([new("sipariş raporu", "order_dates"), new("geçen ay", "order_layout")], current, true);
        using var request = JsonDocument.Parse(OpenAiReportContract.CreateRequest("configured-model", current.Value, Permissions, conversation: history, subject: "orders"));
        Assert.AreEqual(5, request.RootElement.GetProperty("input").GetArrayLength());
    }
}
