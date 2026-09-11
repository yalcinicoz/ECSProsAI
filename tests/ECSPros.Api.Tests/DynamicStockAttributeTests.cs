using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using NpgsqlTypes;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class DynamicStockAttributeTests
{
    private static readonly ReportField Gender = ReportAttributeCatalog.Field(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Cinsiyet", "cinsiyet");
    private static readonly ReportField Season = ReportAttributeCatalog.Field(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Sezon", "sezon");
    private static readonly HashSet<string> Permissions = new() { "reports.ai.use", "inventory.view", "catalog.products.view" };
    private static readonly ReportField[] Attributes = { Gender, Season };
    internal static string Recipe(string[] dimensions, ReportFilter[]? filters = null) => JsonSerializer.Serialize(new ReportDefinition
    {
        Version = 1, Subject = "stock", Metrics = new[] { "stock.quantity", "stock.reserved", "stock.available" },
        Dimensions = dimensions, Filters = filters ?? Array.Empty<ReportFilter>(), Presentation = "table", Limit = 1000
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [TestMethod]
    public void ActiveCatalogDefinitionsAreAvailableWithoutHardcodedGenderBranch()
    {
        var custom = ReportAttributeCatalog.Field(Guid.NewGuid(), "Yeni seçenek", "yeni_tanim");
        Assert.IsTrue(ReportDefinitionValidator.Parse(Recipe(new[] { custom.Id }), Permissions,
            attributes: new[] { custom }).IsValid);
        Assert.IsFalse(ReportDefinitionValidator.Parse(Recipe(new[] { custom.Id }), Permissions,
            attributes: Attributes).IsValid, "Removed or unknown type must fail, not drop grouping.");
        using var command = StockReportQuery.Create(Recipe(new[] { custom.Id }), Permissions, new[] { custom });
        Assert.AreEqual(NpgsqlDbType.Uuid, command.Parameters["type_a0"].NpgsqlDbType);
    }

    [TestMethod]
    public void CatalogPermissionIsRequiredForGroupingAndFiltering_NotOnlyDisplay()
    {
        var limited = new HashSet<string> { "reports.ai.use", "inventory.view" };
        Assert.AreEqual(6, ReportDictionary.ForPermissions(limited, Attributes).Count);
        Assert.IsFalse(ReportDefinitionValidator.Parse(Recipe(new[] { Gender.Id }), limited, attributes: Attributes).IsValid);
        Assert.IsFalse(ReportDefinitionValidator.Parse(Recipe(Array.Empty<string>(), new[] {
            new ReportFilter { Field = Gender.Id, Operator = "eq", Values = new[] { "Kadın" } }
        }), limited, attributes: Attributes).IsValid);
        Assert.AreEqual(0, ReportDictionary.ForPermissions(new HashSet<string> { "reports.ai.use", "catalog.products.view" }, Attributes).Count);
        var scoped = StockReportExecutor.ResolvePermissions(new ECSPros.Shared.Kernel.Authorization.EfektifYetkiler(false, new()
        {
            ["reports.ai.use"] = null, ["inventory.view"] = null,
            ["catalog.products.view"] = new() { Guid.NewGuid() }
        }));
        Assert.IsFalse(scoped.Contains("catalog.products.view"));
    }

    [TestMethod]
    public void QueryUsesOneRowLateralAggregatesAndVariantPrecedence()
    {
        using var command = StockReportQuery.Create(Recipe(new[] { Gender.Id, Season.Id, "stockType" }), Permissions, Attributes);
        StringAssert.Contains(command.CommandText, "LEFT JOIN LATERAL");
        StringAssert.Contains(command.CommandText, "array_agg(DISTINCT");
        StringAssert.Contains(command.CommandText, "AND NOT EXISTS");
        StringAssert.Contains(command.CommandText, "definition.attribute_values");
        StringAssert.Contains(command.CommandText, "NOT av.\"IsDeleted\" AND av.\"IsActive\"");
        StringAssert.Contains(command.CommandText, "GROUP BY a0.labels, a1.labels");
        Assert.IsFalse(command.CommandText.Contains(Gender.Id));
    }

    [TestMethod]
    public void AttributeNamesAndSqlLikeFilterValuesRemainParameters()
    {
        var value = "Kadın'; SELECT pg_sleep(5);--";
        using var command = StockReportQuery.Create(Recipe(new[] { Gender.Id }, new[] {
            new ReportFilter { Field = Gender.Id, Operator = "eq", Values = new[] { value } }
        }), Permissions, Attributes);
        Assert.IsFalse(command.CommandText.Contains(value));
        Assert.AreEqual(value.ToLower(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")),
            ((string[])command.Parameters["f0"].Value!)[0]);
        StringAssert.Contains(command.CommandText, "EXISTS (SELECT 1 FROM unnest(a0.labels)");
    }

    [TestMethod]
    public void ModelSchemaAndParserUseSameCurrentAttributeDictionary()
    {
        using var request = JsonDocument.Parse(OpenAiReportContract.CreateRequest("test-model",
            "cinsiyet bazlı stok raporu istiyorum", Permissions, Attributes));
        var instructions = request.RootElement.GetProperty("instructions").GetString()!;
        StringAssert.Contains(instructions, Gender.Id);
        StringAssert.Contains(instructions, "stockType as an extra grouping dimension");
        var recipe = Recipe(new[] { Gender.Id, "stockType" });
        var envelope = JsonSerializer.Serialize(new { status = "completed", output = new[] {
            new { type = "message", content = new[] { new { type = "output_text", text =
                "{\"decision\":\"ready\",\"clarification\":\"none\",\"definition\":" + recipe + "}" } } } } });
        Assert.AreEqual("ready", OpenAiReportContract.ParseResponse(envelope, Permissions, Attributes).Status);
        Assert.AreEqual("error", OpenAiReportContract.ParseResponse(envelope, Permissions).Status);
    }
}
