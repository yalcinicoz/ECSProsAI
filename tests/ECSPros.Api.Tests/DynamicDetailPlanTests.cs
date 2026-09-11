using System.Text.Json;
using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class DynamicDetailPlanTests
{
    public const string Json = """
        {"version":2,"source":"orders","from":"2026-08-01T00:00:00Z","to":"2026-09-01T00:00:00Z",
        "aggregate":null,"detail":{"columns":["orders.orderNumber","orders.createdAt","orders.amount","orders.currencyCode"],"sort":"orders.createdAt","direction":"desc","top":null}}
        """;
    private static readonly HashSet<string> Allowed = new() { "reports.ai.use", "orders.view" };

    [TestMethod]
    public void EnvelopeAcceptsExactlyOneOutputAndPreservesOldAggregatePlans()
    {
        Assert.AreEqual(4, DynamicReportPlan.Parse(Json).Detail!.Columns!.Length);
        Assert.IsNull(DynamicReportPlan.Parse(DynamicReportPlanTests.Json).Detail);
        Assert.ThrowsExactly<ArgumentException>(() => DynamicReportPlan.Parse(Json.Replace("\"aggregate\":null", "\"aggregate\":{}")));
        Assert.ThrowsExactly<ArgumentException>(() => DynamicReportPlan.Parse("{\"version\":2,\"source\":\"orders\"}"));
        Assert.ThrowsExactly<ArgumentException>(() => DynamicReportPlan.Parse(Json.Replace("\"top\":null", "\"sql\":\"SELECT 1\"")));
    }

    [TestMethod]
    public void ModelSchemaUsesOnlyPermittedDetailColumnsAndNullableOutputBranches()
    {
        using var doc = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("configured", "detay", Allowed, null));
        var plan = doc.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema").GetProperty("properties")
            .GetProperty("plan").GetProperty("anyOf")[0];
        Assert.IsTrue(plan.GetProperty("required").EnumerateArray().Any(v => v.GetString() == "detail"));
        var detail = plan.GetProperty("properties").GetProperty("detail").GetProperty("anyOf")[0];
        var columns = detail.GetProperty("properties").GetProperty("columns").GetProperty("items").GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToArray();
        CollectionAssert.AreEquivalent(ReportBusinessDictionary.Orders.DescribeDetails(Allowed).Select(f => f.Id).ToArray(), columns);
        Assert.IsFalse(columns.Contains("orders.count"));
        Assert.IsFalse(columns.Contains("items.productCode"));
        Assert.AreEqual("date", ReportBusinessDictionary.Orders.DescribeDetails(Allowed).Single(f => f.Id == "orders.createdAt").DataType);
        Assert.AreEqual("number", ReportBusinessDictionary.Orders.DescribeDetails(Allowed).Single(f => f.Id == "orders.amount").DataType);
    }

    [TestMethod]
    public void ReadyDetailParsesWithoutProducingResults()
    {
        var payload = JsonSerializer.Serialize(new { status = "completed", output = new[] { new { type = "message",
            content = new[] { new { type = "output_text", text = "{\"decision\":\"ready\",\"clarification\":\"none\",\"plan\":" + Json + "}" } } } } });
        var result = OpenAiDynamicReportContract.ParseResponse(payload, Allowed);
        Assert.AreEqual("ready", result.Status);
        Assert.IsNotNull(result.DynamicPlan!.Detail);
        Assert.IsNull(result.Definition);
    }
}
