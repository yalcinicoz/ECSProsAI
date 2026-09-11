using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Order.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class DynamicStockDetailTests
{
    private static readonly EfektifYetkiler All = new(true, new());
    private static readonly ReportField[] Attributes = [
        ReportAttributeCatalog.Field(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Ortam", "environment"),
        ReportAttributeCatalog.Field(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Renk", "color"),
        ReportAttributeCatalog.Field(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Beden", "size") ];
    public static DynamicReportPlan Plan(IReadOnlyList<ReportField> fields) => new() {
        Version = 2, Source = "stock", StockGrain = "variant",
        Detail = new() { Columns = ["productCode", "barcode", fields[1].Id, fields[2].Id, "productCreatedAt", "stock.quantity", "stockType"], Direction = "asc" },
        Predicate = new() { Kind = "all", Children = [
            new() { Kind = "compare", Field = "stock.quantity", Operator = "gte", Values = ["5"] },
            new() { Kind = "compare", Field = fields[0].Id, Operator = "eq", Values = ["Tesettür"] } ] } };

    [TestMethod]
    public void DetailedRequestCompilesWithoutLosingColumnsOrNumericThreshold()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);
        var plan = Plan(Attributes);
        var dictionary = DynamicStockSource.Dictionary(Attributes);
        var rows = DynamicStockSource.Query(db, plan, All, Attributes);
        var detail = dictionary.Details(_ => true, r => r.Id).Build(rows, plan.Detail!, new(), ReportSourceCatalog.ResolvePermissions(All));
        var sql = detail.Page.ToQueryString();
        StringAssert.Contains(sql, "GROUP BY");
        StringAssert.Contains(sql, "LATERAL");
        StringAssert.Contains(sql, "Barcode");
        StringAssert.Contains(sql, "ProductCreatedAt");
        StringAssert.Contains(sql, ">=");
        CollectionAssert.AreEqual(plan.Detail!.Columns!, detail.Columns.ToArray());
        var dated = plan with { Predicate = new() { Kind = "compare", Field = "productCreatedAt", Operator = "gte", Values = ["2026-01-01T00:00:00Z"] } };
        StringAssert.Contains(DynamicStockSource.Query(db, dated, All, Attributes).ToQueryString(), "ProductCreatedAt");
    }

    [TestMethod]
    public void MultiValueMembershipDoesNotMatchSubstringAndThresholdIncludesFive()
    {
        var plan = Plan(Attributes);
        var rows = new[] {
            new DynamicStockRow { Quantity = 5, SearchValues = ["[\"günlük\", \"tesettür\"]"], Labels = ["Günlük / Tesettür"] },
            new DynamicStockRow { Quantity = 4, SearchValues = ["[\"tesettür\"]"] },
            new DynamicStockRow { Quantity = 50, SearchValues = ["[\"tesettür değil\"]"] },
            new DynamicStockRow { Quantity = 50, SearchValues = [null] } }.AsQueryable();
        var selected = DynamicStockSource.Dictionary(Attributes).Predicates(_ => true).Apply(rows, plan.Predicate!, ReportSourceCatalog.ResolvePermissions(All)).ToList();
        Assert.AreEqual(1, selected.Count); Assert.AreEqual(5m, selected[0].Quantity);
    }

    [TestMethod]
    public void ScopePermissionsAndUnknownFieldsFailClosed()
    {
        using var db = new OrderDbContext(new DbContextOptionsBuilder<OrderDbContext>().UseNpgsql("Host=localhost;Database=not_opened;Username=unused").Options);
        var plan = Plan(Attributes);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => DynamicStockSource.Query(db, plan,
            new(false, new() { ["reports.ai.use"] = [Guid.NewGuid()], ["inventory.view"] = null }), Attributes));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => DynamicStockSource.Query(db, plan,
            new(false, new() { ["reports.ai.use"] = null, ["inventory.view"] = null }), Attributes));
        Assert.ThrowsExactly<ArgumentException>(() => DynamicStockSource.Query(db, plan with { Detail = plan.Detail! with { Columns = ["barcode", "password"] } }, All, Attributes));
        Assert.ThrowsExactly<ArgumentException>(() => DynamicStockSource.Query(db, plan with { Detail = plan.Detail! with { Columns = ["warehouseId"] } }, All, Attributes));
        Assert.ThrowsExactly<ArgumentException>(() => DynamicStockSource.Query(db, plan with { Detail = plan.Detail! with { Columns = ["stock.quantity"] } }, All, Attributes));
        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.AreEqual("variant", DynamicReportPlan.Parse(json).StockGrain);
        Assert.ThrowsExactly<ArgumentException>(() => DynamicReportPlan.Parse(json.Replace("\"from\":null", "\"from\":\"2026-01-01T00:00:00Z\"")));
    }

    [TestMethod]
    public void AiReceivesDetailFieldsNumericOperatorsAndCurrentStockScope()
    {
        var request = OpenAiDynamicReportContract.CreateRequest("configured-model", "barkod renk beden açılış tarihi stok adedi en az5", ReportSourceCatalog.ResolvePermissions(All), null, "stock", Attributes);
        using var json = JsonDocument.Parse(request);
        var schema = json.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema").GetRawText();
        StringAssert.Contains(schema, "barcode"); StringAssert.Contains(schema, "productCreatedAt");
        StringAssert.Contains(schema, "gte"); StringAssert.Contains(schema, "stockGrain"); StringAssert.Contains(schema, Attributes[0].Id);
        Assert.AreEqual("configured-model", json.RootElement.GetProperty("model").GetString());
    }
}
