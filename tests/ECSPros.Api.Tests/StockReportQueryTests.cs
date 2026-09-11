using ECSPros.Api.Services.AiReporting;
using NpgsqlTypes;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class StockReportQueryTests
{
    private static HashSet<string> Permissions() => new() { "reports.ai.use", "inventory.view" };
    private const string Recipe = """
        {"version":1,"subject":"stock","metrics":["stock.quantity","stock.reserved","stock.available"],
        "dimensions":["productCode"],"filters":[],"presentation":"table","limit":100}
        """;

    [TestMethod]
    public void ChannelLimitedPermissionCannotBecomeGlobalStockAccess()
    {
        var effective = new EfektifYetkiler(false, new Dictionary<string, HashSet<Guid>?>
        {
            ["reports.ai.use"] = null,
            ["inventory.view"] = new() { Guid.NewGuid() }
        });
        Assert.ThrowsExactly<ArgumentException>(() => StockReportQuery.Create(Recipe,
            StockReportExecutor.ResolvePermissions(effective)));
        Assert.IsTrue(StockReportExecutor.ResolvePermissions(new EfektifYetkiler(true, new()))
            .Contains("inventory.view"));
    }

    [TestMethod]
    public void AllDictionaryFieldsHaveFixedExpressions()
    {
        foreach (var field in ReportDictionary.Fields)
            Assert.IsFalse(string.IsNullOrWhiteSpace(StockReportQuery.Expression(field.Id)));
        Assert.ThrowsExactly<ArgumentException>(() => StockReportQuery.Expression("SELECT 1"));
    }

    [TestMethod]
    public void ProductFilterIsParameter_NotSql()
    {
        const string value = "x'; DROP TABLE users;--";
        var json = Recipe.Replace("\"filters\":[]",
            "\"filters\":[{\"field\":\"productCode\",\"operator\":\"eq\",\"values\":[\"" + value + "\"]}]");
        using var command = StockReportQuery.Create(json, Permissions());
        Assert.IsFalse(command.CommandText.Contains(value));
        StringAssert.Contains(command.CommandText, "= ANY(@f0)");
        Assert.AreEqual(value, ((string[])command.Parameters["f0"].Value!)[0]);
        Assert.AreEqual(NpgsqlDbType.Array | NpgsqlDbType.Text, command.Parameters["f0"].NpgsqlDbType);
    }

    [TestMethod]
    public void WarehouseFilterUsesTypedGuidArray()
    {
        var id = Guid.NewGuid();
        var json = Recipe.Replace("\"filters\":[]",
            "\"filters\":[{\"field\":\"warehouseId\",\"operator\":\"eq\",\"values\":[\"" + id + "\"]}]");
        using var command = StockReportQuery.Create(json, Permissions());
        Assert.AreEqual(NpgsqlDbType.Array | NpgsqlDbType.Uuid, command.Parameters["f0"].NpgsqlDbType);
        Assert.AreEqual(id, ((Guid[])command.Parameters["f0"].Value!)[0]);
        Assert.IsFalse(command.CommandText.Contains(id.ToString()));
    }

    [TestMethod]
    public void QueryPreservesOrphanStocks_AvoidsFanOut_AndDetectsTruncation()
    {
        using var command = StockReportQuery.Create(Recipe, Permissions());
        StringAssert.Contains(command.CommandText, "LEFT JOIN catalog.product_variants");
        StringAssert.Contains(command.CommandText, "NOT s.\"IsDeleted\"");
        StringAssert.Contains(command.CommandText, "NOT v.\"IsDeleted\"");
        StringAssert.Contains(command.CommandText, "NOT p.\"IsDeleted\"");
        Assert.IsFalse(command.CommandText.Contains("reservations"));
        Assert.AreEqual(101, command.Parameters["resultLimit"].Value);
        StringAssert.Contains(command.CommandText, "ORDER BY p.\"Code\" NULLS LAST");
    }

    [TestMethod]
    public void TotalsDoNotNeedCatalogJoin_AndCastBeforeSubtraction()
    {
        using var command = StockReportQuery.Create(Recipe.Replace("[\"productCode\"]", "[]"), Permissions());
        Assert.IsFalse(command.CommandText.Contains("JOIN"));
        StringAssert.Contains(command.CommandText, "s.\"Quantity\"::bigint - s.\"ReservedQuantity\"::bigint");
        Assert.IsFalse(command.CommandText.Contains("GREATEST"));
    }

    [TestMethod]
    public void CompilerRevalidatesPermissionsAndRecipe()
    {
        Assert.ThrowsExactly<ArgumentException>(() => StockReportQuery.Create(Recipe, new HashSet<string>()));
        Assert.ThrowsExactly<ArgumentException>(() => StockReportQuery.Create(
            Recipe.Replace("stock.quantity", "SUM(secret)"), Permissions()));
    }
}
