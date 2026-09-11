using System.Net;
using System.Text;
using System.Text.Json;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using Npgsql;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class OpenAiDynamicReportContractTests
{
    [TestMethod]
    public void StockClarificationReplacesAmbiguousGroupRatherThanAddingAnotherFilter()
    {
        using var request = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("model", "stok", 
            new HashSet<string> { "reports.ai.use", "inventory.view", "catalog.products.view" }, null, "stock"));
        var instructions = request.RootElement.GetProperty("instructions").GetString()!;
        StringAssert.Contains(instructions, "REPLACES the ambiguous productGroup predicate");
        StringAssert.Contains(instructions, "Preserve all unrelated earlier columns and thresholds");
        StringAssert.Contains(instructions, "Never change eq/in to contains");
    }

    private const string Plan = """
        {"version":2,"source":"orders","from":"2026-08-01T00:00:00Z","to":"2026-09-01T00:00:00Z","predicate":null,
         "aggregate":{"dimensions":["orders.status"],"measures":["orders.count"],"sort":"orders.count","direction":"desc"}}
        """;
    private static HashSet<string> Allowed(bool returns = false) => returns
        ? ["reports.ai.use", "orders.view", "orders.returns.view"] : ["reports.ai.use", "orders.view"];
    private static string Envelope(string proposal, string status = "completed") => JsonSerializer.Serialize(new
    { status, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = proposal } } } } });
    private static string Ready(string plan = Plan) => Envelope("{\"decision\":\"ready\",\"clarification\":\"none\",\"plan\":" + plan + "}");

    [TestMethod]
    public void MetadataAndSchemaExcludeUnauthorizedReturnsAndPrivateFields()
    {
        Assert.AreEqual(0, DynamicReportMetadata.Fields(new HashSet<string> { "orders.view" }).Count);
        Assert.AreEqual(0, DynamicReportMetadata.Fields(new HashSet<string> { "reports.ai.use", "inventory.view" }).Count);
        Assert.IsFalse(DynamicReportMetadata.Fields(Allowed()).Any(f => f.Id.StartsWith("returns.")));
        Assert.IsTrue(DynamicReportMetadata.Fields(Allowed(true)).Any(f => f.Id == "returns.type"));
        using var request = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("model", "sipariş", Allowed(), null));
        var schema = request.RootElement.GetProperty("text").GetProperty("format").GetProperty("schema").GetRawText();
        Assert.IsFalse(schema.Contains("returns."));
        Assert.IsFalse(schema.Contains("orders.returns"));
        Assert.IsFalse(schema.Contains("customer.email"));
        using var permitted = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("model", "iade", Allowed(true), null));
        StringAssert.Contains(permitted.RootElement.GetProperty("text").GetRawText(), "orders.returns");
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => OpenAiDynamicReportContract.CreateRequest("model", "rapor", new HashSet<string>(), null));
    }

    [TestMethod]
    public void RequestIsStrictRecursiveAndDoesNotStoreOrExecuteTools()
    {
        using var doc = JsonDocument.Parse(OpenAiDynamicReportContract.CreateRequest("model", "sipariş", Allowed(), null));
        var root = doc.RootElement;
        Assert.IsFalse(root.GetProperty("store").GetBoolean());
        Assert.IsFalse(root.TryGetProperty("tools", out _));
        var format = root.GetProperty("text").GetProperty("format");
        Assert.IsTrue(format.GetProperty("strict").GetBoolean());
        var schema = format.GetProperty("schema");
        AssertStrictObjects(schema);
        StringAssert.Contains(schema.GetProperty("$defs").GetProperty("predicate").GetRawText(), "#/$defs/predicate");
    }

    private static void AssertStrictObjects(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "object")
            {
                Assert.IsFalse(value.GetProperty("additionalProperties").GetBoolean());
                CollectionAssert.AreEquivalent(value.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToArray(),
                    value.GetProperty("required").EnumerateArray().Select(p => p.GetString()).ToArray());
            }
            foreach (var property in value.EnumerateObject()) AssertStrictObjects(property.Value);
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) AssertStrictObjects(item);
    }

    [TestMethod]
    public void ReadyReturnsOnlyPlanAndRechecksBasicPermissions()
    {
        var result = OpenAiDynamicReportContract.ParseResponse(Ready(), Allowed());
        Assert.AreEqual("ready", result.Status);
        Assert.IsNotNull(result.DynamicPlan);
        Assert.AreEqual(2, result.DynamicPlan.Version);
        Assert.IsNull(result.Definition);
        Assert.AreEqual("denied", OpenAiDynamicReportContract.ParseResponse(Ready(), new HashSet<string>()).Status);
    }

    [TestMethod]
    [DataRow("{", "envelope_json")]
    [DataRow("{\"status\":\"completed\",\"output\":[]}", "message_count")]
    [DataRow("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"private-content\"}]}]}", "response_refusal")]
    public void DiagnosticCodesAreStructuralAndNeverEchoProviderContent(string response, string code)
    {
        var result = OpenAiDynamicReportContract.ParseResponse(response, Allowed());
        Assert.AreEqual(code, result.ErrorCode);
        Assert.AreEqual("error", result.Status);
        Assert.IsNull(result.DynamicPlan);
        Assert.IsFalse(System.Text.Json.JsonSerializer.Serialize(result).Contains("private-content"));
    }

    [TestMethod]
    [DataRow("clarify")]
    [DataRow("ready")]
    public void RecognizedClarificationDiscardsProvisionalPlanWithoutExecution(string decision)
    {
        var result = OpenAiDynamicReportContract.ParseResponse(Envelope(
            "{\"decision\":\"" + decision + "\",\"clarification\":\"order_dates\",\"plan\":" + Plan + "}"), Allowed());
        Assert.AreEqual("clarify", result.Status);
        Assert.AreEqual("order_dates", result.Clarification);
        Assert.IsNull(result.DynamicPlan);
        Assert.IsNull(result.Definition);
    }

    [TestMethod]
    [DataRow("incomplete")]
    [DataRow("failed")]
    [DataRow("queued")]
    public void IncompleteResponsesFailClosed(string status) =>
        Assert.AreEqual("error", OpenAiDynamicReportContract.ParseResponse(Envelope("{}", status), Allowed()).Status);

    [TestMethod]
    [DataRow("null")]
    [DataRow("not json")]
    [DataRow("{\"decision\":\"ready\",\"clarification\":\"none\",\"plan\":null}")]
    [DataRow("{\"decision\":\"clarify\",\"decision\":\"out_of_scope\",\"clarification\":\"none\",\"plan\":null}")]
    [DataRow("{\"decision\":\"clarify\",\"clarification\":\"order_dates\",\"plan\":null,\"answer\":\"injected\"}")]
    public void MalformedProposalHasNoUsablePlan(string proposal)
    {
        var result = OpenAiDynamicReportContract.ParseResponse(Envelope(proposal), Allowed());
        Assert.AreEqual("error", result.Status);
        Assert.IsNull(result.DynamicPlan);
    }

    [TestMethod]
    public void RefusalToolCallsDuplicateEnvelopesAndInvalidPlansFailClosed()
    {
        foreach (var response in new[] {
            """{"status":"completed","output":[{"type":"message","content":[{"type":"refusal","refusal":"no"}]}]}""",
            """{"status":"completed","output":[{"type":"function_call","name":"execute_sql"}]}""",
            Ready().Replace("\"status\":\"completed\"", "\"status\":\"failed\",\"status\":\"completed\""),
            Ready(Plan.Replace("\"version\":2", "\"version\":1")),
            Ready(Plan.Replace("\"source\":\"orders\"", "\"source\":\"personnel\"")),
            Ready(Plan.Replace("2026-09-01T00:00:00Z", "2028-09-01T00:00:00Z")) })
            Assert.AreEqual("error", OpenAiDynamicReportContract.ParseResponse(response, Allowed()).Status);
    }

    [TestMethod]
    public async Task InterpreterRoutesV2ToDynamicContractWithoutDatabaseOrRealProvider()
    {
        var handler = new Handler(Ready());
        using var http = new HttpClient(handler);
        var interpreter = new OpenAiReportInterpreter(http);
        var result = await interpreter.InterpretAsync("fake-test-key", "model", ApprovedReportPrompt.Create("sipariş raporu", true),
            Allowed(), default, subject: "orders", planVersion: 2);
        Assert.AreEqual("ready", result.Status);
        Assert.IsNotNull(result.DynamicPlan);
        Assert.AreEqual("https://api.openai.com/v1/responses", handler.Url);
        StringAssert.Contains(handler.Body!, "dynamic_report_plan");
        Assert.IsNull(http.DefaultRequestHeaders.Authorization);
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => interpreter.InterpretAsync("fake-test-key", "model",
            ApprovedReportPrompt.Create("stok raporu", true), Allowed(), default, subject: "stock", planVersion: 2));
        Assert.AreEqual(1, handler.Calls);
    }

    [TestMethod]
    public void ExecutorValidationRejectsUnknownColumnsAndCurrencyMixingWithoutConnecting()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1");
        var effective = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null });
        var executor = new OrderReportExecutor(dataSource, new Authorization(effective));
        executor.ValidateDynamicPlan(Plan, effective);
        Assert.ThrowsExactly<ArgumentException>(() => executor.ValidateDynamicPlan(Plan.Replace("orders.status", "customer.email"), effective));
        Assert.ThrowsExactly<ArgumentException>(() => executor.ValidateDynamicPlan(Plan.Replace("orders.count", "orders.amount"), effective));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => executor.ValidateDynamicPlan(Plan, EfektifYetkiler.Bos));
    }

    [TestMethod]
    public async Task EchoedMetadataHasSeparateBoundFromGeneratedProposal()
    {
        var largeEnvelope = Ready().Insert(1, "\"instructions\":\"" + new string('x', 90_000) + "\",");
        Assert.AreEqual("ready", OpenAiDynamicReportContract.ParseResponse(largeEnvelope, Allowed()).Status);
        using var http = new HttpClient(new Handler(largeEnvelope));
        var result = await new OpenAiReportInterpreter(http).InterpretAsync("fake-key", "model",
            ApprovedReportPrompt.Create("sipariş raporu", true), Allowed(), default, subject: "orders", planVersion: 2);
        Assert.AreEqual("ready", result.Status);
        var oversizedProposal = new string(' ', OpenAiResponseLimits.ProposalBytes) + "{}";
        Assert.AreEqual("error", OpenAiDynamicReportContract.ParseResponse(Envelope(oversizedProposal), Allowed()).Status);
        var oversizedEnvelope = new string(' ', OpenAiResponseLimits.EnvelopeBytes) + Ready();
        Assert.AreEqual("error", OpenAiDynamicReportContract.ParseResponse(oversizedEnvelope, Allowed()).Status);
        using var oversizedHttp = new HttpClient(new Handler(oversizedEnvelope));
        var rejected = await new OpenAiReportInterpreter(oversizedHttp).InterpretAsync("fake-key", "model",
            ApprovedReportPrompt.Create("sipariş raporu", true), Allowed(), default, subject: "orders", planVersion: 2);
        Assert.AreEqual("error", rejected.Status);
        Assert.IsNull(rejected.DynamicPlan);
    }

    private sealed class Handler(string response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Url { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class Authorization(EfektifYetkiler effective) : IEtkinYetkiServisi
    {
        public Task<EfektifYetkiler> GetirAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(effective);
        public Task GecersizKilAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrupIcinGecersizKilAsync(Guid roleId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
