using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Inventory.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class MovementReportTests
{
    private static readonly EfektifYetkiler Effective = new(false, new() { ["reports.ai.use"] = null, ["inventory.view"] = null });
    private static readonly HashSet<string> Allowed = ["reports.ai.use", "inventory.view"];
    private const string Json = """
        {"version":2,"source":"stockMovements","from":"2026-09-01T00:00:00Z","to":"2026-10-01T00:00:00Z",
        "aggregate":{"dimensions":["movements.type"],"measures":["movements.count","movements.quantity"],"sort":"movements.count","direction":"desc","top":20}}
        """;
    [TestMethod]
    public void SourcesShareOneProcessBudget()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
        var stock = typeof(StockReportExecutor).GetField("Slots", flags)!.GetValue(null);
        Assert.AreSame(stock, typeof(OrderReportExecutor).GetField("Slots", flags)!.GetValue(null));
        Assert.AreSame(stock, typeof(MovementReportExecutor).GetField("Slots", flags)!.GetValue(null));
    }
    [TestMethod]
    public void ScopeAndQuantityPreserveRecordedMeaning()
    {
        var plan = DynamicReportPlan.Parse(Json);
        var start = plan.Scope().From.UtcDateTime;
        var rows = new[] {
            new StockMovement { CreatedAt = start, MovementType = "transfer", Quantity = 7 },
            new StockMovement { CreatedAt = start.AddDays(1), MovementType = "transfer", Quantity = 3 },
            new StockMovement { CreatedAt = start.AddTicks(-1), MovementType = "transfer", Quantity = 99 },
            new StockMovement { CreatedAt = plan.Scope().To.UtcDateTime, MovementType = "transfer", Quantity = 99 }
        }.AsQueryable();
        var selected = MovementReportSource.Apply(rows, plan, Effective);
        var result = MovementReportSource.Dictionary.Aggregates(_ => true).Build(selected, plan.Aggregate!, Allowed).Rows.Single();
        Assert.AreEqual(2m, result.M0);
        Assert.AreEqual(10m, result.M1);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => MovementReportSource.Apply(rows, plan, EfektifYetkiler.Bos));
        var scoped = new EfektifYetkiler(false, new() { ["reports.ai.use"] = [Guid.NewGuid()], ["inventory.view"] = null });
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => MovementReportSource.Apply(rows, plan, scoped));
        Assert.ThrowsExactly<ArgumentException>(() => MovementReportSource.Apply(rows, plan with { Predicate = new() { Kind = "compare", Field = "movements.notes", Operator = "eq", Values = ["private"] } }, Effective));
    }
    [TestMethod]
    public void AggregateAndDetailTranslateWithoutOpeningDatabase()
    {
        using var db = new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);
        var plan = DynamicReportPlan.Parse(Json);
        var rows = MovementReportSource.Apply(db.StockMovements, plan, Effective);
        var sql = MovementReportSource.Dictionary.Aggregates(_ => true).Build(rows, plan.Aggregate!, Allowed).Rows.ToQueryString();
        StringAssert.Contains(sql, "GROUP BY"); StringAssert.Contains(sql, "LIMIT"); StringAssert.Contains(sql, "CreatedAt");
        var detail = MovementReportSource.Dictionary.Details(_ => true, m => m.Id).Build(rows,
            new() { Columns = ["movements.createdAt", "movements.quantity", "movements.fromWarehouseId"], Sort = "movements.createdAt", Direction = "desc" }, new(), Allowed);
        StringAssert.Contains(detail.Page.ToQueryString(), "LIMIT");
    }
    [TestMethod]
    public void AiContractIsSourceSpecificAndRejectsSourceSwitch()
    {
        using var request = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("configured", "hareket raporu", Allowed, null, MovementReportSource.Id));
        var schema = request.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema");
        Assert.IsFalse(schema.GetRawText().Contains("orders."));
        var branches = schema.GetProperty("$defs").GetProperty("predicate").GetProperty("anyOf").EnumerateArray().ToArray();
        Assert.AreEqual(3, branches.Length);
        foreach (var branch in branches)
        {
            var predicate = branch.GetProperty("properties");
            Assert.AreEqual("null", predicate.GetProperty("relation").GetProperty("type").GetString());
            Assert.IsFalse(predicate.GetProperty("kind").GetRawText().Contains("exists"));
        }
        var text = "{\"decision\":\"ready\",\"clarification\":\"none\",\"plan\":" + Json + "}";
        var response = JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } });
        Assert.AreEqual("ready", OpenAiDynamicReportContract.ParseResponse(response, Allowed, MovementReportSource.Id).Status);
        var both = new HashSet<string>(Allowed) { "orders.view" };
        Assert.AreEqual("error", OpenAiDynamicReportContract.ParseResponse(response, both, "orders").Status);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OpenAiDynamicReportContract.CreateRequest("configured", "rapor", new HashSet<string> { "reports.ai.use", "orders.view" }, null, MovementReportSource.Id));
    }
}
