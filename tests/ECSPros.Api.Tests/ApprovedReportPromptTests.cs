using System.Text.Json;
using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ApprovedReportPromptTests
{
    [TestMethod]
    public void ConsentIsRequired_AndPromptIsNotSerialized()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ApprovedReportPrompt.Create("Fiziksel stok", false));
        var prompt = ApprovedReportPrompt.Create("Fiziksel stok", true);
        Assert.IsFalse(JsonSerializer.Serialize(prompt).Contains("Fiziksel"));
        Assert.IsFalse(prompt.ToString().Contains("Fiziksel"));
    }

    [TestMethod]
    [DataRow("mail@example.com stokları")]
    [DataRow("+90 555 123 45 67")]
    [DataRow("12345678901")]
    [DataRow("4111 1111 1111 1111")]
    [DataRow("TR12 3456 7890 1234 5678 9012 34")]
    [DataRow("sk-proj-fakecredential123")]
    [DataRow("şifre: gizli")]
    [DataRow("api_key=test")]
    [DataRow("-----BEGIN PRIVATE KEY-----")]
    [DataRow("https://example.com/private")]
    [DataRow("stok\u200Braporu")]
    [DataRow("１２３４５６７８９０１")]
    public void ObviousSensitiveInputsAreBlocked(string text)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() => ApprovedReportPrompt.Create(text, true));
        Assert.IsFalse(exception.Message.Contains(text));
    }

    [TestMethod]
    [DataRow("P-00023146 fiziksel stok genel toplam")]
    [DataRow("9İ60142.0001 ürününün fiziksel stoğu")]
    [DataRow("Fiziksel stokları depoya göre göster")]
    public void OrdinaryStockRequestsRemainUnchanged(string text) =>
        Assert.AreEqual(text, ApprovedReportPrompt.Create(text, true).Value);
}
