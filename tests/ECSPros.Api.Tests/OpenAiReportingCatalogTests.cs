using ECSPros.Api.Extensions;
using System.Text.Json;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class OpenAiReportingCatalogTests
{
    [TestMethod]
    public void ApiKeyUsesEncryptedCredentialSection_NotSettings()
    {
        var schema = OpenAiReportingCatalog.CreateSchema();
        var key = schema.Single(x => x.Key == "apiKey");
        Assert.AreEqual("credentials", key.Section);
        Assert.AreEqual("password", key.Type);
        Assert.IsTrue(key.Required);
        Assert.AreEqual("settings", schema.Single(x => x.Key == "model").Section);
        Assert.IsTrue(schema.Single(x => x.Key == "model").Required);
        Assert.IsFalse(schema.Any(x => x.Key == "apiUrl"));
    }

    [TestMethod]
    public void SchemaSerializesToExistingDynamicFormContract()
    {
        var json = JsonSerializer.Serialize(OpenAiReportingCatalog.CreateSchema(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var document = JsonDocument.Parse(json);
        var field = document.RootElement[0];
        Assert.AreEqual("apiKey", field.GetProperty("key").GetString());
        Assert.AreEqual("credentials", field.GetProperty("section").GetString());
        Assert.IsTrue(field.GetProperty("required").GetBoolean());
        Assert.IsFalse(string.IsNullOrWhiteSpace(field.GetProperty("labelI18n").GetProperty("tr").GetString()));
    }
}
