using ECSPros.Api.Services.AiReporting;
using System.Text.Json;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportDefinitionValidatorTests
{
    private const string Valid = """
        {"version":1,"subject":"stock","metrics":["stock.available"],
         "dimensions":["productCode"],"filters":[],"presentation":"table","limit":100}
        """;
    private static HashSet<string> Permissions() => new() { "reports.ai.use", "inventory.view" };

    [TestMethod]
    public void ValidRecipe_RoundTripsWithoutAi()
    {
        var first = ReportDefinitionValidator.Parse(Valid, Permissions());
        Assert.IsTrue(first.IsValid);
        var saved = JsonSerializer.Serialize(first.Definition,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.IsTrue(ReportDefinitionValidator.Parse(saved, Permissions()).IsValid);
    }

    [TestMethod]
    [DataRow("reports.ai.use")]
    [DataRow("inventory.view")]
    public void RemovingPermissionBlocksSavedRecipe(string permission)
    {
        var permissions = Permissions();
        Assert.IsTrue(ReportDefinitionValidator.Parse(Valid, permissions).IsValid);
        permissions.Remove(permission);
        Assert.IsFalse(ReportDefinitionValidator.Parse(Valid, permissions).IsValid);
        Assert.AreEqual(0, ReportDictionary.ForPermissions(permissions).Count);
    }

    [TestMethod]
    public void ExportRequiresSeparatePermission()
    {
        var permissions = Permissions();
        Assert.IsFalse(ReportDefinitionValidator.Parse(Valid, permissions, forExport: true).IsValid);
        permissions.Add(ReportDictionary.ExportPermission);
        Assert.IsTrue(ReportDefinitionValidator.Parse(Valid, permissions, forExport: true).IsValid);
    }

    [TestMethod]
    [DataRow("\"version\":1", "\"version\":2")]
    [DataRow("\"subject\":\"stock\"", "\"subject\":\"site_analysis\"")]
    [DataRow("\"subject\":\"stock\"", "\"subject\":\"sales\"")]
    [DataRow("\"stock.available\"", "\"customer.email\"")]
    [DataRow("\"stock.available\"", "\"SUM(Quantity)\"")]
    [DataRow("\"productCode\"", "\"stock.quantity\"")]
    [DataRow("\"limit\":100", "\"limit\":0")]
    [DataRow("\"limit\":100", "\"limit\":1001")]
    [DataRow("\"filters\":[]", "\"filters\":null")]
    [DataRow("\"stock.available\"", "\"stock.available\",\"stock.available\"")]
    [DataRow("\"stock.available\"", "null")]
    [DataRow("\"table\"", "\"javascript\"")]
    public void InvalidRecipeFailsClosed(string oldValue, string newValue)
    {
        var result = ReportDefinitionValidator.Parse(Valid.Replace(oldValue, newValue), Permissions());
        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Definition);
    }

    [TestMethod]
    [DataRow("\"sql\":\"SELECT * FROM users\"")]
    [DataRow("\"firmId\":\"other-firm\"")]
    [DataRow("\"isSuperAdmin\":true")]
    [DataRow("\"limit\":10")]
    public void ExtraOrDuplicatePropertiesAreRejected(string extra)
    {
        Assert.IsFalse(ReportDefinitionValidator.Parse(Valid.Replace("{", "{" + extra + ","), Permissions()).IsValid);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("{\"field\":\"customer.email\",\"operator\":\"eq\",\"values\":[\"x\"]}")]
    [DataRow("{\"field\":\"warehouseId\",\"operator\":\"eq\",\"values\":[\"invalid\"]}")]
    [DataRow("{\"field\":\"stockType\",\"operator\":\"eq\",\"values\":[\"all\"]}")]
    [DataRow("{\"field\":\"productCode\",\"operator\":\"sql\",\"values\":[\"x\"]}")]
    [DataRow("{\"field\":\"productCode\",\"operator\":\"eq\",\"values\":[\"a\",\"b\"]}")]
    [DataRow("{\"field\":\"productCode\",\"operator\":\"in\",\"values\":[null]}")]
    [DataRow("{\"field\":\"productCode\",\"field\":\"stockType\",\"operator\":\"eq\",\"values\":[\"physical\"]}")]
    public void InvalidFiltersAreRejected(string filter)
    {
        Assert.IsFalse(ReportDefinitionValidator.Parse(
            Valid.Replace("\"filters\":[]", "\"filters\":[" + filter + "]"), Permissions()).IsValid);
    }

    [TestMethod]
    public void PhysicalStockFilterAccepted_AndBarRequiresOneDimension()
    {
        var json = Valid.Replace("\"filters\":[]",
            "\"filters\":[{\"field\":\"stockType\",\"operator\":\"eq\",\"values\":[\"physical\"]}]");
        Assert.IsTrue(ReportDefinitionValidator.Parse(json.Replace("table", "bar"), Permissions()).IsValid);
        Assert.IsFalse(ReportDefinitionValidator.Parse(json.Replace("table", "bar")
            .Replace("[\"productCode\"]", "[]"), Permissions()).IsValid);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("not json")]
    [DataRow("")]
    public void MalformedInputDoesNotThrow(string json) =>
        Assert.IsFalse(ReportDefinitionValidator.Parse(json, Permissions()).IsValid);

    [TestMethod]
    public void OversizedPayloadRejected() =>
        Assert.IsFalse(ReportDefinitionValidator.Parse(new string(' ', ReportDefinitionValidator.MaxPayloadBytes) + Valid,
            Permissions()).IsValid);
}
