using System.Text.Json;
using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportBusinessDictionaryTests
{
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "orders.view", "orders.returns.view" };
    private sealed record Example(string Category, decimal Amount);

    [TestMethod]
    public void OneFieldRegistrationSuppliesDictionaryFilterAndGroupingWithoutNewReportCode()
    {
        var definition = new ReportEntityDefinition<Example>("orders.view")
            .Text("category", "Kategori", e => e.Category)
            .Measure("amount", "Tutar", e => e.Amount, "sum", filterable: true).Seal();
        Assert.AreEqual(2, definition.Describe(Permissions).Count);
        var rows = new[] { new Example("A", 5), new Example("A", 15), new Example("B", 30) }.AsQueryable();
        var filtered = definition.Predicates(e => true).Apply(rows,
            new() { Kind = "compare", Field = "category", Operator = "eq", Values = new[] { "A" } }, Permissions);
        var aggregate = definition.Aggregates(e => true).Build(filtered,
            new() { Dimensions = new[] { "category" }, Measures = new[] { "amount" } }, Permissions);
        Assert.AreEqual(20m, aggregate.Rows.Single().M0);
    }

    [TestMethod]
    public void SchemaEnumsMatchDictionaryInsteadOfSeparateFieldLists()
    {
        var fields = DynamicReportMetadata.Fields(Permissions);
        using var json = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("configured-model", "sipariş özeti", Permissions, null));
        var schema = json.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema");
        var branches = schema.GetProperty("$defs").GetProperty("predicate").GetProperty("anyOf").EnumerateArray().ToArray();
        var filterIds = branches.Where(b => b.GetProperty("properties").GetProperty("kind").GetProperty("enum")[0].GetString() == "compare")
            .SelectMany(b => b.GetProperty("properties").GetProperty("field").GetProperty("enum").EnumerateArray()).Select(v => v.GetString()).ToArray();
        CollectionAssert.AreEquivalent(fields.Where(f => f.Operators.Count > 0).Select(f => f.Id).ToArray(), filterIds);
        var relation = branches.Single(b => b.GetProperty("properties").GetProperty("kind").GetProperty("enum")[0].GetString() == "exists").GetProperty("properties");
        var relationIds = relation.GetProperty("relation").GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToArray();
        CollectionAssert.AreEquivalent(fields.Where(f => f.Kind == "relation").Select(f => f.Id).ToArray(), relationIds);
        Assert.IsFalse(fields.Single(f => f.Id == "payments.methodId").Operators.Contains("contains"));
        Assert.IsFalse(fields.Single(f => f.Id == "orders.status").Operators.Contains("gt"));
    }

    [TestMethod]
    public void DefinitionsCannotMutateAfterSealOrPublishWithoutPermission()
    {
        var definition = new ReportEntityDefinition<Example>("orders.view").Text("category", "Kategori", e => e.Category);
        Assert.ThrowsExactly<InvalidOperationException>(() => definition.Describe(Permissions));
        Assert.ThrowsExactly<ArgumentException>(() => definition.Text("category", "Tekrar", e => e.Category));
        definition.Seal();
        Assert.ThrowsExactly<InvalidOperationException>(() => definition.Count("count", "Adet"));
        Assert.AreEqual(0, definition.Describe(new HashSet<string> { "reports.ai.use" }).Count);
        Assert.ThrowsExactly<ArgumentException>(() => new ReportEntityDefinition<Example>("orders.view")
            .Measure("average", "Ortalama", e => e.Amount, "average", filterable: true));
    }

    [TestMethod]
    public void RelatedMetadataDoesNotLeakWithoutChildPermission()
    {
        var limited = new HashSet<string> { "reports.ai.use", "orders.view" };
        Assert.AreEqual(0, ReportBusinessDictionary.OrderReturns.Describe(limited).Count);
        Assert.IsFalse(DynamicReportMetadata.Fields(limited).Any(f => f.Id.Contains("returns")));
        Assert.IsTrue(ReportBusinessDictionary.OrderReturns.Describe(Permissions).Any(f => f.Kind == "relation"));
    }
}
