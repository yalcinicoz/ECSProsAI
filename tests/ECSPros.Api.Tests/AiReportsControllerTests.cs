using System.Security.Claims;
using System.Text.Json;
using ECSPros.Api.Controllers;
using ECSPros.Api.Services.AiReporting;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class AiReportsControllerTests
{
    private static AiReportsController Controller(bool allowed, bool enabled = false,
        EfektifYetkiler? permissions = null, IReportAttributeCatalog? catalog = null, bool dynamicEnabled = false)
    {
        var effective = new EfektifYetkiler(false, allowed ? new()
        {
            ["reports.ai.use"] = null, ["inventory.view"] = null
        } : new Dictionary<string, HashSet<Guid>?>());
        var controller = new AiReportsController(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AiReporting:Enabled"] = enabled.ToString(),
                ["AiReporting:DynamicEnabled"] = dynamicEnabled.ToString() }).Build(),
            new Authorization(permissions ?? effective), null!, null!, catalog ?? new ReportAttributeCatalogStub());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                { new Claim("sub", Guid.NewGuid().ToString()) }, "test"))
            }
        };
        return controller;
    }

    [TestMethod]
    public async Task DisabledByDefault_DoesNotCallDatabaseOrAudit()
    {
        using var json = JsonDocument.Parse("{}");
        var result = (ObjectResult)await Controller(true).Run(json.RootElement, default);
        Assert.AreEqual(503, result.StatusCode);
        var interpretation = (ObjectResult)await Controller(true).Interpret(
            new InterpretReportRequest("stok", true, null), null!, null!, null!, default);
        Assert.AreEqual(503, interpretation.StatusCode);
    }

    [TestMethod]
    public async Task ExportWithoutDedicatedPermissionNeverExecutesOrCreatesFile()
    {
        var controller = Controller(true, enabled: true);
        var recipe = JsonDocument.Parse("{}").RootElement;
        Assert.AreEqual(403, ((ObjectResult)await controller.Export(new GridReportRequest(recipe, new()), default)).StatusCode);
    }

    [TestMethod]
    public async Task NoInventoryPermission_DoesNotRevealCatalogOrRun()
    {
        var controller = Controller(false);
        Assert.AreEqual(403, ((ObjectResult)await controller.Catalog(default)).StatusCode);
        Assert.AreEqual(403, ((ObjectResult)await controller.Firms(null!, default)).StatusCode);
        using var json = JsonDocument.Parse("{}");
        Assert.AreEqual(403, ((ObjectResult)await controller.Run(json.RootElement, default)).StatusCode);
    }

    [TestMethod]
    public async Task GridRetainsPermissionAndRolloutGates_RejectsInvalidInputBeforeExecutor()
    {
        using var json = JsonDocument.Parse(ReportGridQueryTests.Recipe);
        var request = new GridReportRequest(json.RootElement, new ReportGridState());
        Assert.AreEqual(403, ((ObjectResult)await Controller(false, true).Grid(request, default)).StatusCode);
        Assert.AreEqual(503, ((ObjectResult)await Controller(true).Grid(request, default)).StatusCode);
        Assert.AreEqual(400, ((ObjectResult)await Controller(true, true).Grid(
            request with { Grid = new ReportGridState(Sort: "secret") }, default)).StatusCode);
        Assert.AreEqual(400, ((ObjectResult)await Controller(true, true).Grid(
            request with { Definition = default }, default)).StatusCode);
    }

    [TestMethod]
    public async Task MovementCatalogRequiresDynamicFlagAndInventoryPermission()
    {
        async Task<JsonElement> Data(AiReportsController controller) => JsonSerializer.SerializeToElement(((OkObjectResult)await controller.Catalog(default)).Value).GetProperty("data");
        var disabled = await Data(Controller(true));
        Assert.IsFalse(disabled.GetProperty("subjects").EnumerateArray().Any(s => s.GetString() == MovementReportSource.Id));
        var enabled = await Data(Controller(true, dynamicEnabled: true));
        Assert.IsTrue(enabled.GetProperty("dynamicSources").TryGetProperty(MovementReportSource.Id, out var metadata));
        Assert.IsTrue(metadata.GetProperty("fields").GetArrayLength() > 0);
        var orderOnly = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.view"] = null });
        var restricted = await Data(Controller(true, permissions: orderOnly, dynamicEnabled: true));
        Assert.IsFalse(restricted.GetProperty("dynamicSources").TryGetProperty(MovementReportSource.Id, out _));
    }

    [TestMethod]
    public async Task CatalogReportsActualCapabilities()
    {
        var result = (OkObjectResult)await Controller(true).Catalog(default);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var data = json.RootElement.GetProperty("data");
        Assert.IsFalse(data.GetProperty("enabled").GetBoolean());
        Assert.IsFalse(data.GetProperty("naturalLanguageEnabled").GetBoolean());
        Assert.AreEqual(6, data.GetProperty("fields").GetArrayLength());
    }

    [TestMethod]
    public async Task CustomerCatalogRequiresGlobalCrmAndDynamicFlag()
    {
        var permission = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["crm.members.view"] = null });
        var disabled = JsonSerializer.SerializeToElement(((OkObjectResult)await Controller(true, permissions: permission).Catalog(default)).Value).GetProperty("data");
        Assert.AreEqual(0, disabled.GetProperty("subjects").GetArrayLength());
        var enabled = JsonSerializer.SerializeToElement(((OkObjectResult)await Controller(true, permissions: permission, dynamicEnabled: true).Catalog(default)).Value).GetProperty("data");
        Assert.AreEqual("customers", enabled.GetProperty("subjects").EnumerateArray().Single().GetString());
        var fields = enabled.GetProperty("dynamicSources").GetProperty("customers").GetProperty("fields").GetRawText();
        StringAssert.Contains(fields, "customers.firstName");
        Assert.IsFalse(fields.Contains("customers.orderCount"));
        Assert.IsFalse(fields.Contains("customers.returnCount"));
    }

    [TestMethod]
    public async Task ReturnOnlyCatalogRequiresDynamicFlagAndDoesNotExposeOtherSources()
    {
        var permission = new EfektifYetkiler(false, new() { ["reports.ai.use"] = null, ["orders.returns.view"] = null });
        var disabled = JsonSerializer.SerializeToElement(((OkObjectResult)await Controller(true, permissions: permission).Catalog(default)).Value).GetProperty("data");
        Assert.AreEqual(0, disabled.GetProperty("subjects").GetArrayLength());
        var enabled = JsonSerializer.SerializeToElement(((OkObjectResult)await Controller(true, permissions: permission, dynamicEnabled: true).Catalog(default)).Value).GetProperty("data");
        Assert.AreEqual("returns", enabled.GetProperty("subjects").EnumerateArray().Single().GetString());
        Assert.IsTrue(enabled.GetProperty("dynamicSources").TryGetProperty("returns", out _));
        Assert.IsFalse(enabled.GetProperty("dynamicSources").TryGetProperty("orders", out _));
    }

    [TestMethod]
    public async Task ScopedReportPermissionReachesOnlyOrderCatalog()
    {
        var channel = Guid.NewGuid();
        var permissions = new EfektifYetkiler(false, new()
        { ["reports.ai.use"] = new() { channel }, ["orders.view"] = new() { channel }, ["inventory.view"] = null });
        var result = (OkObjectResult)await Controller(true, permissions: permissions).Catalog(default);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var data = json.RootElement.GetProperty("data");
        Assert.AreEqual("orders", data.GetProperty("subjects").EnumerateArray().Single().GetString());
        Assert.IsTrue(data.GetProperty("fields").EnumerateArray().All(f => f.GetProperty("Id").GetString()!.StartsWith("orders.")));
    }

    [TestMethod]
    public async Task CorruptAttributeMetadataReturnsControlledServiceError()
    {
        var result = (ObjectResult)await Controller(true, catalog: new InvalidCatalog()).Catalog(default);
        Assert.AreEqual(503, result.StatusCode);
    }

    [TestMethod]
    public async Task DynamicPlanRetainsIndependentRolloutGateAndRejectsWrongSourcePermission()
    {
        using var document = JsonDocument.Parse(DynamicReportPlanTests.Json);
        var request = new GridReportRequest(document.RootElement, new());
        Assert.AreEqual(503, ((ObjectResult)await Controller(true, true).Grid(request, default)).StatusCode);
        Assert.AreEqual(403, ((ObjectResult)await Controller(true, true, dynamicEnabled: true).Grid(request, default)).StatusCode);
        Assert.AreEqual(400, ((ObjectResult)await Controller(true, true, dynamicEnabled: true).Run(document.RootElement, default)).StatusCode);
    }

    private sealed class InvalidCatalog : IReportAttributeCatalog
    {
        public Task<IReadOnlyList<ReportField>> LoadAsync(IReadOnlySet<string> permissions, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReportField>>(new[] { ReportDictionary.Fields[0] });
    }

    private sealed class Authorization(EfektifYetkiler permissions) : IEtkinYetkiServisi
    {
        public Task<EfektifYetkiler> GetirAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(permissions);
        public Task GecersizKilAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrupIcinGecersizKilAsync(Guid roleId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
