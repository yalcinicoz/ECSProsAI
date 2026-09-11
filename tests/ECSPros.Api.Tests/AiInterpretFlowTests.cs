using System.Net;
using System.Security.Claims;
using System.Text.Json;
using ECSPros.Api.Controllers;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Api.Services.Store;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ECSPros.Api.Tests;

// Controller -> real quota service -> real interpreter/contract, fake boundary stores/HTTP only.
// No ASP.NET HTTP host, database, Redis or paid provider connection is started.
[TestClass]
public sealed class AiInterpretFlowTests
{
    [TestMethod]
    [DataRow(false, "stok")]
    [DataRow(true, "mail@example.com stok")]
    public async Task InvalidConsentOrSensitiveInputNeverLoadsSettings(bool consent, string prompt)
    {
        using var flow = new Flow();
        Assert.AreEqual(400, (await flow.Invoke(consent, prompt)).StatusCode);
        Assert.AreEqual(0, flow.Settings.Calls); Assert.AreEqual(0, flow.Http.Calls);
    }

    [TestMethod]
    public async Task SensitiveHistoryCannotBypassCurrentPromptChecks()
    {
        using var flow = new Flow();
        var response = await flow.Invoke(history: new[] { new ReportConversationTurn("mail@example.com stok", "none") });
        Assert.AreEqual(400, response.StatusCode);
        Assert.AreEqual(0, flow.Settings.Calls);
        Assert.AreEqual(0, flow.Store.Calls);
        Assert.AreEqual(0, flow.Http.Calls);
    }

    [TestMethod]
    public async Task DeniedUserNeverReachesSettings()
    {
        using var flow = new Flow(); flow.Auth.Allowed = false;
        Assert.AreEqual(403, (await flow.Invoke()).StatusCode);
        Assert.AreEqual(0, flow.Settings.Calls); Assert.AreEqual(0, flow.Store.Calls);
        Assert.AreEqual(0, flow.Http.Calls);
    }

    [TestMethod]
    public async Task ExhaustedQuotaReturnsRetryAfterWithoutProviderCall()
    {
        using var flow = new Flow(); flow.Store.Allowed = false;
        Assert.AreEqual(429, (await flow.Invoke()).StatusCode);
        Assert.AreEqual("60", flow.Controller.Response.Headers.RetryAfter.ToString());
        Assert.AreEqual(1, flow.Store.Calls); Assert.AreEqual(0, flow.Http.Calls);
    }

    [TestMethod]
    public async Task UnavailableQuotaDoesNotFallThroughToProvider()
    {
        using var flow = new Flow(); flow.Store.Unavailable = true;
        Assert.AreEqual(503, (await flow.Invoke()).StatusCode);
        Assert.AreEqual(0, flow.Http.Calls);
    }

    [TestMethod]
    public async Task ReadyProposalIsReturnedButNeverExecuted_AndAuditExcludesContent()
    {
        using var flow = new Flow();
        var response = await flow.Invoke();
        Assert.AreEqual(200, response.StatusCode);
        var json = JsonSerializer.Serialize(response.Value);
        StringAssert.Contains(json, "ready");
        Assert.IsFalse(json.Contains("fake-key"));
        Assert.AreEqual(1, flow.Http.Calls); Assert.AreEqual(1, flow.Audit.Calls);
        Assert.IsFalse(flow.Audit.Metadata.Contains("stock.quantity"));
        Assert.IsFalse(flow.Audit.Metadata.Contains("fake-key"));
        // Executor deliberately null: successful result proves interpretation did not run stock query.
    }

    [TestMethod]
    public async Task PermissionRevokedDuringProviderCallDiscardsProposal()
    {
        using var flow = new Flow(); flow.Http.AfterSend = () => flow.Auth.Allowed = false;
        Assert.AreEqual(403, (await flow.Invoke()).StatusCode);
        Assert.AreEqual(1, flow.Http.Calls); Assert.AreEqual(0, flow.Audit.Calls);
    }

    [TestMethod]
    public async Task DynamicPlanPassesCompilerWithoutExecutingReport()
    {
        using var flow = new Flow(dynamic: true);
        var result = await flow.Invoke(prompt: "Bu ay siparişleri duruma göre say");
        Assert.AreEqual(200, result.StatusCode);
        var json = JsonSerializer.Serialize(result.Value);
        StringAssert.Contains(json, "DynamicPlan");
        Assert.AreEqual(1, flow.Http.Calls); Assert.AreEqual(1, flow.Audit.Calls);
        Assert.IsFalse(flow.Audit.Metadata.Contains("orders.count"));
    }

    [TestMethod]
    public async Task DynamicUnsupportedMeasureIsRejectedBeforePreviewOrAudit()
    {
        using var flow = new Flow(dynamic: true);
        flow.Http.InvalidDynamic = true;
        Assert.AreEqual(400, (await flow.Invoke(prompt: "sipariş raporu")).StatusCode);
        Assert.AreEqual(0, flow.Audit.Calls);
    }

    [TestMethod]
    public async Task DynamicDetailPassesCompilerWithoutExecutionAndRejectsHiddenColumn()
    {
        using var flow = new Flow(dynamic: true);
        flow.Http.Detail = true;
        var result = await flow.Invoke(prompt: "Bu ay siparişleri tek tek listele");
        Assert.AreEqual(200, result.StatusCode);
        Assert.IsTrue(JsonSerializer.Serialize(result.Value).Contains("orders.orderNumber"));
        Assert.IsFalse(flow.Audit.Metadata.Contains("orders.orderNumber"));
        flow.Http.InvalidDynamic = true;
        Assert.AreEqual(400, (await flow.Invoke(prompt: "detay raporu")).StatusCode);
        Assert.AreEqual(1, flow.Audit.Calls);
    }

    private sealed class Flow : IDisposable
    {
        public readonly Settings Settings = new();
        public readonly Store Store = new();
        public readonly Auth Auth = new();
        public readonly Handler Http = new();
        public readonly Audit Audit = new();
        public readonly AiReportsController Controller;
        private readonly HttpClient client;
        private readonly AiReportQuota quota;
        private readonly bool dynamic;
        private readonly NpgsqlDataSource source = NpgsqlDataSource.Create("Host=localhost;Database=validation_only;Username=unused");
        private readonly ServiceProvider services;
        public Flow(bool dynamic = false)
        {
            this.dynamic = dynamic; Auth.Dynamic = dynamic; Http.Dynamic = dynamic;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiReporting:NaturalLanguageEnabled"] = "true", ["AiReporting:DynamicEnabled"] = dynamic.ToString(), ["AiReporting:Quota:Scope"] = "test",
                ["AiReporting:Quota:UserPerMinute"] = "3", ["AiReporting:Quota:UserPer24Hours"] = "20",
                ["AiReporting:Quota:FirmPer24Hours"] = "100"
            }).Build();
            quota = new(config, Store); client = new(Http);
            services = new ServiceCollection().AddSingleton(new OrderReportExecutor(source, Auth)).BuildServiceProvider();
            Controller = new(config, Auth, null!, Audit, new ReportAttributeCatalogStub())
            {
                ControllerContext = new() { HttpContext = new DefaultHttpContext
                { RequestServices = services, User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", Guid.NewGuid().ToString()) }, "test")) } }
            };
        }
        public async Task<ObjectResult> Invoke(bool consent = true, string prompt = "Fiziksel stok toplamı", ReportConversationTurn[]? history = null) =>
            (ObjectResult)await Controller.Interpret(new(prompt, consent, null, history, dynamic ? "orders" : "stock", dynamic ? 2 : 1), Settings, new(client), quota, default);
        public void Dispose() { client.Dispose(); services.Dispose(); source.Dispose(); }
    }
    private sealed class Settings : IOpenAiFirmSettingsProvider
    {
        public int Calls;
        public Task<List<ReportFirmOption>> ListFirmsAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<OpenAiFirmSettings> GetAsync(Guid userId, Guid? selectedFirmId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(OpenAiFirmSettings.From(new() { FirmId = Guid.NewGuid(),
                Credentials = new() { ["apiKey"] = "fake-key" }, Settings = new() { ["model"] = "test-model" } }));
        }
    }
    private sealed class Store : IAiReportQuotaStore
    {
        public int Calls; public bool Allowed = true; public bool Unavailable;
        public Task<AiQuotaDecision> ReserveAsync(string scope, Guid userId, Guid firmId, int perMinute, int userDaily, int firmDaily, CancellationToken ct)
        { Calls++; if (Unavailable) throw new TimeoutException(); return Task.FromResult(new AiQuotaDecision(Allowed, Allowed ? 0 : 60)); }
    }
    private sealed class Auth : IEtkinYetkiServisi
    {
        public bool Allowed = true; public bool Dynamic;
        public Task<EfektifYetkiler> GetirAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(new EfektifYetkiler(false,
            Allowed ? new() { ["reports.ai.use"] = null, [Dynamic ? "orders.view" : "inventory.view"] = null } : new Dictionary<string, HashSet<Guid>?>()));
        public Task GecersizKilAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrupIcinGecersizKilAsync(Guid roleId, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Audit : IVitrinAuditLogger
    {
        public int Calls; public string Metadata = "";
        public Task LogAsync(HttpContext http, string action, string entityType, Guid entityId, object? oldValues, object? newValues,
            Guid firmPlatformId, string? title = null, CancellationToken ct = default)
        { Calls++; Metadata = JsonSerializer.Serialize(newValues); return Task.CompletedTask; }
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public Action? AfterSend; public bool Dynamic; public bool InvalidDynamic; public bool Detail;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; AfterSend?.Invoke();
            var proposal = """
                {"decision":"ready","clarification":"none","definition":{"version":1,"subject":"stock",
                "metrics":["stock.quantity"],"dimensions":[],"filters":[],"presentation":"table","limit":100}}
                """;
            if (Dynamic) proposal = """
                {"decision":"ready","clarification":"none","plan":{"version":2,"source":"orders",
                "from":"2026-09-01T00:00:00+03:00","to":"2026-10-01T00:00:00+03:00","predicate":null,
                "aggregate":{"dimensions":["orders.status"],"measures":["orders.count"],"sort":null,"direction":"asc"}}}
                """;
            if (Detail) proposal = "{\"decision\":\"ready\",\"clarification\":\"none\",\"plan\":" + DynamicDetailPlanTests.Json + "}";
            if (InvalidDynamic) proposal = proposal.Replace("orders.count", "secret.measure").Replace("orders.orderNumber", "secret.column");
            var payload = JsonSerializer.Serialize(new { status = "completed", output = new[] {
                new { type = "message", content = new[] { new { type = "output_text", text = proposal } } } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) });
        }
    }
}
