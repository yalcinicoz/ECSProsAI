using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportGridQueryTests
{
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "inventory.view" };
    public const string Recipe = """
        {"version":1,"subject":"stock","metrics":["stock.quantity","stock.reserved","stock.available"],
        "dimensions":["productCode"],"filters":[],"presentation":"table","limit":100}
        """;

    [TestMethod]
    public void FiltersEntireAggregateBeforePagingAndComputesTotalsInSameStatement()
    {
        using var command = ReportGridQuery.Create(Recipe, Permissions, [], new(Page: 2, PageSize: 25,
            Sort: "stock.quantity", Dir: "desc", Filters: [new("stock.quantity", "between", "10;20")]));
        Assert.IsFalse(command.Parameters.Contains("resultLimit"));
        Assert.AreEqual(25L, command.Parameters["gridOffset"].Value);
        StringAssert.Contains(command.CommandText, "r.c1 BETWEEN @");
        StringAssert.Contains(command.CommandText, "COUNT(*) AS total");
        StringAssert.Contains(command.CommandText, "COALESCE(SUM(c1),0) AS m0");
        StringAssert.Contains(command.CommandText, "p.c1 DESC NULLS LAST, p.c0 ASC NULLS LAST");
        StringAssert.Contains(command.CommandText, "LEFT JOIN LATERAL");
        Assert.IsTrue(command.CommandText.IndexOf("BETWEEN", StringComparison.Ordinal) < command.CommandText.IndexOf("LIMIT", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SearchIsLiteralParameterAndDoesNotReplaceRecipeScope()
    {
        var json = Recipe.Replace("\"filters\":[]", "\"filters\":[{\"field\":\"stockType\",\"operator\":\"eq\",\"values\":[\"physical\"]}]");
        const string input = "%_'; SELECT secret;--";
        using var command = ReportGridQuery.Create(json, Permissions, [], new(Search: input));
        StringAssert.Contains(command.CommandText, "= ANY(@f0)");
        StringAssert.Contains(command.CommandText, "strpos(");
        Assert.IsFalse(command.CommandText.Contains(input));
        Assert.IsTrue(command.Parameters.Cast<Npgsql.NpgsqlParameter>().Any(p => Equals(p.Value, input.ToLowerInvariant())));
    }

    [TestMethod]
    public void RejectsUnknownFieldsAndInvalidFilters()
    {
        ReportGridState[] invalid = [new(Page: 0), new(PageSize: 251), new(Dir: "DROP"), new(Sort: "secret"),
            new(Search: new string('x', 257)), new(Filters: [new("secret", "eq", "x")]),
            new(Filters: [new("stock.quantity", "between", "20;10")]),
            new(Filters: [new("stock.quantity", "eq", "1e10")]),
            new(Filters: [new("stock.quantity", "contains", "1")]),
            new(Filters: [new("productCode", "eq", "x"), new("productCode", "eq", "y")])];
        foreach (var grid in invalid)
            Assert.ThrowsExactly<ArgumentException>(() => ReportGridQuery.Create(Recipe, Permissions, [], grid));
        Assert.ThrowsExactly<ArgumentException>(() => ReportGridQuery.Create(Recipe, new HashSet<string>(), [], new()));
    }
}
