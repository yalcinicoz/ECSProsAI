using System.Net;
using System.Text;
using System.Text.Json;
using ECSPros.Api.Services.AiReporting;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class OpenAiReportContractTests
{
    private static HashSet<string> Permissions() => new() { "reports.ai.use", "inventory.view" };
    private const string Recipe = """
        {"version":1,"subject":"stock","metrics":["stock.quantity"],"dimensions":[],
        "filters":[],"presentation":"table","limit":1000}
        """;

    [TestMethod]
    public void InstructionsDefineExplicitTurkishScopeAndPreserveAmbiguity()
    {
        using var doc = JsonDocument.Parse(OpenAiReportContract.CreateRequest("configured-model",
            "Fiziksel stokların genel toplamını tablo olarak göster", Permissions()));
        var instructions = doc.RootElement.GetProperty("instructions").GetString()!;
        StringAssert.Contains(instructions, "stockType eq physical");
        StringAssert.Contains(instructions, "stockType eq virtual");
        StringAssert.Contains(instructions, "dimensions: []");
        StringAssert.Contains(instructions, "Do not ask again");
        StringAssert.Contains(instructions, "karar vermedim");
        Assert.AreEqual("Fiziksel stokların genel toplamını tablo olarak göster",
            doc.RootElement.GetProperty("input")[0].GetProperty("content").GetString());
    }
    private static string Envelope(string proposal, string status = "completed") => JsonSerializer.Serialize(new
    {
        status, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = proposal } } } }
    });

    [TestMethod]
    public void RequestUsesStrictAuthorizedSchema_NoToolsOrStoredResponse()
    {
        using var doc = JsonDocument.Parse(OpenAiReportContract.CreateRequest("configured-model", "Fiziksel stok genel toplam", Permissions()));
        var root = doc.RootElement;
        Assert.IsFalse(root.GetProperty("store").GetBoolean());
        Assert.IsFalse(root.TryGetProperty("tools", out _));
        var format = root.GetProperty("text").GetProperty("format");
        Assert.AreEqual("json_schema", format.GetProperty("type").GetString());
        Assert.IsTrue(format.GetProperty("strict").GetBoolean());
        Assert.IsFalse(format.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OpenAiReportContract.CreateRequest("model", "stok", new HashSet<string>()));
    }

    [TestMethod]
    public void ReadyRecipeIsValidated_AndPermissionIsRechecked()
    {
        var response = Envelope("{\"decision\":\"ready\",\"clarification\":\"none\",\"definition\":" + Recipe + "}");
        Assert.AreEqual("ready", OpenAiReportContract.ParseResponse(response, Permissions()).Status);
        Assert.AreEqual("denied", OpenAiReportContract.ParseResponse(response, new HashSet<string>()).Status);
        Assert.AreEqual("error", OpenAiReportContract.ParseResponse(response.Replace("stock.quantity", "customer.email"), Permissions()).Status);
    }

    [TestMethod]
    [DataRow("incomplete")]
    [DataRow("failed")]
    [DataRow("queued")]
    public void IncompleteResponseCannotBecomeReport(string status)
    {
        Assert.AreEqual("error", OpenAiReportContract.ParseResponse(Envelope("{}", status), Permissions()).Status);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("null")]
    [DataRow("not json")]
    [DataRow("{\"decision\":\"ready\",\"clarification\":\"none\",\"definition\":null}")]
    [DataRow("{\"decision\":\"clarify\",\"clarification\":\"stock_type\",\"definition\":null,\"answer\":\"general chat\"}")]
    public void InvalidPayloadReturnsNoDefinition(string payload)
    {
        var result = OpenAiReportContract.ParseResponse(Envelope(payload), Permissions());
        Assert.AreEqual("error", result.Status);
        Assert.IsNull(result.Definition);
    }

    [TestMethod]
    public void ClarificationAndOutOfScopeUseLocalMessagesOnly()
    {
        var clarify = OpenAiReportContract.ParseResponse(Envelope("""
            {"decision":"clarify","clarification":"stock_type","definition":null}
            """), Permissions());
        Assert.AreEqual("clarify", clarify.Status);
        Assert.IsNull(clarify.Definition);
        var outside = OpenAiReportContract.ParseResponse(Envelope("""
            {"decision":"out_of_scope","clarification":"none","definition":null}
            """), Permissions());
        Assert.AreEqual("out_of_scope", outside.Status);
    }

    [TestMethod]
    public void RefusalAndDuplicatePropertiesFailClosed()
    {
        var refusal = """
            {"status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"no"}]}]}
            """;
        Assert.AreEqual("error", OpenAiReportContract.ParseResponse(refusal, Permissions()).Status);
        var duplicate = Envelope("""
            {"decision":"clarify","decision":"out_of_scope","clarification":"none","definition":null}
            """);
        Assert.AreEqual("error", OpenAiReportContract.ParseResponse(duplicate, Permissions()).Status);
    }

    [TestMethod]
    public async Task TransportUsesFixedEndpoint_PerRequestKey_AndHidesProviderErrors()
    {
        var handler = new FakeHandler();
        using var http = new HttpClient(handler);
        var interpreter = new OpenAiReportInterpreter(http);
        var result = await interpreter.InterpretAsync("fake-test-key", "configured-model", ApprovedReportPrompt.Create("stok", true), Permissions(), default);
        Assert.AreEqual("provider_error", result.Status);
        Assert.IsFalse(result.Message.Contains("sensitive"));
        Assert.AreEqual("https://api.openai.com/v1/responses", handler.Url);
        Assert.AreEqual("Bearer fake-test-key", handler.Authorization);
        Assert.IsNull(http.DefaultRequestHeaders.Authorization);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            Authorization = request.Headers.Authorization!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            { Content = new StringContent("sensitive provider details", Encoding.UTF8) });
        }
    }
}
