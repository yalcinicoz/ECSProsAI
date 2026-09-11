using System.Reflection;
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
using Npgsql;

namespace ECSPros.Api.Tests;

[TestClass]
public class ReportShareFlowTests
{
    [TestMethod]
    public async Task GroupPermissionsAllowSharingWithoutFirm_AndUserRevocationBlocksIt()
    {
        var owner = Guid.NewGuid();
        var sources = new List<YetkiKaynagi>
        {
            new(ReportDictionary.UsePermission, false, null, YetkiKaynakTipi.Grup),
            new(ReportDictionary.SharePermission, false, null, YetkiKaynakTipi.Grup),
            new("inventory.view", false, null, YetkiKaynakTipi.Grup)
        };
        var rights = new Rights();
        rights.Users[owner] = EfektifYetkiHesabi.Hesapla(false, sources);
        var db = DispatchProxy.Create<IIamDbContext, SavedAiReportsTests.Store>();
        var store = (SavedAiReportsTests.Store)(object)db;
        var controller = new SavedAiReportsController(db, rights, new ReportAttributeCatalogStub(), null!, new ConfigurationBuilder().Build())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", owner.ToString()) }, "test")) } } };
        var definition = JsonDocument.Parse("""{"version":1,"subject":"stock","metrics":["stock.available"],"dimensions":[],"filters":[],"presentation":"table","limit":100}""").RootElement;
        var entry = JsonSerializer.SerializeToElement(new { name = "test", definition });
        var directory = new Directory();
        directory.Users[owner] = new(true, new() { [SavedAiReportsController.Prefix + 0] = entry });
        var audit = new Audit();
        Assert.IsInstanceOfType<OkObjectResult>(await controller.Sharing(0, new(entry, true), directory, audit, default));
        Assert.IsNotNull(store.Written);
        store.Written = null;
        sources.Add(new(ReportDictionary.SharePermission, false, null, YetkiKaynakTipi.KullaniciKaldir));
        rights.Users[owner] = EfektifYetkiHesabi.Hesapla(false, sources);
        Assert.IsInstanceOfType<ForbidResult>(await controller.Sharing(0, new(entry, true), directory, audit, default));
        Assert.IsNull(store.Written);
        // Losing permission to create a link does not prevent revoking it.
        Assert.IsInstanceOfType<OkObjectResult>(await controller.Sharing(0, new(entry, false), directory, audit, default));
    }

    [TestMethod]
    public async Task StockDetailSaveShareAndRevokedRightsNeverExecuteOrExposeResults()
    {
        var owner = Guid.NewGuid(); var viewer = Guid.NewGuid();
        var rights = new Rights(); rights.Users[owner] = Permissions(true); rights.Users[viewer] = Permissions();
        await using var source = new NpgsqlDataSourceBuilder("Host=localhost;Database=not_opened;Username=unused").Build();
        var executor = new OrderReportExecutor(source, rights);
        var db = DispatchProxy.Create<IIamDbContext, SavedAiReportsTests.Store>();
        var store = (SavedAiReportsTests.Store)(object)db;
        SavedAiReportsController Controller(Guid user) => new(db, rights, new ReportAttributeCatalogStub(), executor,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AiReporting:DynamicEnabled"] = "true" }).Build())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.ToString()) }, "test")) } } };
        var definition = JsonDocument.Parse("""{"version":2,"source":"stock","stockGrain":"variant","detail":{"columns":["stockType","stock.quantity"],"direction":"asc"},"predicate":{"kind":"compare","field":"stock.quantity","operator":"gte","values":["5"]}}""").RootElement;
        Assert.IsInstanceOfType<OkObjectResult>(await Controller(owner).Save(0, new("Stok", definition), default));
        var saved = JsonDocument.Parse((string)store.Written![2]!).RootElement;
        Assert.AreEqual(definition.GetRawText(), saved.GetProperty("definition").GetRawText());
        Assert.IsFalse(saved.TryGetProperty("rows", out _));
        var token = ReportSharingPolicy.NewToken();
        var directory = new Directory();
        directory.Users[owner] = new(true, new() { [SavedAiReportsController.Prefix + 0] = JsonSerializer.SerializeToElement(new { name = "Stok", definition, shareToken = token }) });
        directory.Users[viewer] = new(true, null);
        var audit = new Audit(); var request = new OpenSharedAiReport(owner, 0, token);
        Assert.IsInstanceOfType<OkObjectResult>(await Controller(viewer).Shared(request, directory, audit, default));
        rights.Users[viewer] = EfektifYetkiler.Bos;
        Assert.IsInstanceOfType<NotFoundResult>(await Controller(viewer).Shared(request, directory, audit, default));
        rights.Users[viewer] = Permissions();
        directory.Users[viewer] = new(false, null);
        Assert.IsInstanceOfType<NotFoundResult>(await Controller(viewer).Shared(request, directory, audit, default));
        Assert.IsFalse(string.Join("", audit.Metadata).Contains(token));
    }

    private sealed class Directory : IReportShareDirectory
    {
        public Dictionary<Guid, ReportShareUser> Users = new();
        public Task<ReportShareUser?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Users.GetValueOrDefault(id));
    }
    private sealed class Rights : IEtkinYetkiServisi
    {
        public Dictionary<Guid, EfektifYetkiler> Users = new();
        public Task<EfektifYetkiler> GetirAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Users.GetValueOrDefault(id) ?? EfektifYetkiler.Bos);
        public Task GecersizKilAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrupIcinGecersizKilAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Audit : IVitrinAuditLogger
    {
        public List<string> Metadata = new();
        public Task LogAsync(HttpContext http, string action, string entityType, Guid entityId, object? oldValues,
            object? newValues, Guid firmPlatformId, string? title = null, CancellationToken ct = default)
        { Metadata.Add(JsonSerializer.Serialize(newValues)); return Task.CompletedTask; }
    }
    private static EfektifYetkiler Permissions(bool share = false) => new(false, share
        ? new() { [ReportDictionary.UsePermission] = null, [ReportDictionary.SharePermission] = null, ["inventory.view"] = null }
        : new() { [ReportDictionary.UsePermission] = null, ["inventory.view"] = null });
    [TestMethod]
    public async Task MovementRelativeRecipeSurvivesSaveAndShareButCannotBypassRolloutOrRights()
    {
        var owner = Guid.NewGuid(); var viewer = Guid.NewGuid();
        var rights = new Rights(); rights.Users[owner] = Permissions(true); rights.Users[viewer] = Permissions();
        await using var source = new NpgsqlDataSourceBuilder("Host=localhost;Database=not_opened;Username=unused").Build();
        var executor = new OrderReportExecutor(source, rights);
        var db = DispatchProxy.Create<IIamDbContext, SavedAiReportsTests.Store>();
        var store = (SavedAiReportsTests.Store)(object)db;
        SavedAiReportsController Controller(Guid user, bool enabled) => new(db, rights, new ReportAttributeCatalogStub(), executor,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AiReporting:DynamicEnabled"] = enabled.ToString() }).Build())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", user.ToString()) }, "test")) } } };
        var definition = JsonDocument.Parse("""{"version":2,"source":"stockMovements","period":"lastMonth","from":"2026-08-01T00:00:00Z","to":"2026-09-01T00:00:00Z","detail":{"columns":["movements.createdAt","movements.quantity"],"direction":"asc"}}""").RootElement;
        Assert.IsInstanceOfType<OkObjectResult>(await Controller(owner, true).Save(0, new("Hareket", definition), default));
        var saved = JsonDocument.Parse((string)store.Written![2]!).RootElement;
        Assert.AreEqual("lastMonth", saved.GetProperty("definition").GetProperty("period").GetString());
        store.Written = null;
        Assert.IsInstanceOfType<BadRequestObjectResult>(await Controller(owner, false).Save(0, new("Hareket", definition), default));
        Assert.IsNull(store.Written);
        var token = ReportSharingPolicy.NewToken();
        var directory = new Directory();
        directory.Users[owner] = new(true, new() { [SavedAiReportsController.Prefix + 0] = JsonSerializer.SerializeToElement(new { name = "Hareket", definition, shareToken = token }) });
        directory.Users[viewer] = new(true, null);
        var audit = new Audit(); var request = new OpenSharedAiReport(owner, 0, token);
        var response = (OkObjectResult)await Controller(viewer, true).Shared(request, directory, audit, default);
        var result = JsonSerializer.SerializeToElement(response.Value).GetProperty("data").GetProperty("definition");
        Assert.AreEqual("lastMonth", result.GetProperty("period").GetString());
        Assert.AreEqual("stockMovements", result.GetProperty("source").GetString());
        Assert.IsInstanceOfType<NotFoundResult>(await Controller(viewer, false).Shared(request, directory, audit, default));
        rights.Users[viewer] = new(false, new() { [ReportDictionary.UsePermission] = [Guid.NewGuid()], ["inventory.view"] = null });
        Assert.IsInstanceOfType<NotFoundResult>(await Controller(viewer, true).Shared(request, directory, audit, default));
        Assert.IsNull(store.Written);
        Assert.AreEqual(1, audit.Metadata.Count);
    }
    [TestMethod]
    public async Task SharedRecipeRechecksBothUsersAndNeverReturnsTokenOrRows()
    {
        var owner = Guid.NewGuid(); var viewer = Guid.NewGuid();
        var token = ReportSharingPolicy.NewToken();
        var definition = JsonDocument.Parse("""{"version":1,"subject":"stock","metrics":["stock.available"],"dimensions":["productCode"],"filters":[],"presentation":"table","limit":100}""").RootElement;
        var entry = JsonSerializer.SerializeToElement(new { name = "test", definition, shareToken = token });
        var directory = new Directory();
        directory.Users[owner] = new(true, new() { [SavedAiReportsController.Prefix + 0] = entry });
        directory.Users[viewer] = new(true, null);
        var rights = new Rights(); rights.Users[owner] = Permissions(true); rights.Users[viewer] = Permissions();
        var audit = new Audit();
        var db = DispatchProxy.Create<IIamDbContext, SavedAiReportsTests.Store>();
        var controller = new SavedAiReportsController(db, rights, new ReportAttributeCatalogStub(), null!, new ConfigurationBuilder().Build());
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", viewer.ToString()) }, "test")) } };
        var request = new OpenSharedAiReport(owner, 0, token);
        var response = (OkObjectResult)await controller.Shared(request, directory, audit, default);
        var json = JsonSerializer.Serialize(response.Value);
        StringAssert.Contains(json, "definition");
        Assert.IsFalse(json.Contains(token)); Assert.IsFalse(json.Contains("rows"));
        Assert.AreEqual(1, audit.Metadata.Count);
        Assert.IsFalse(audit.Metadata[0].Contains(token));
        Assert.IsNull(((SavedAiReportsTests.Store)(object)db).Written);
        Assert.IsInstanceOfType<NotFoundResult>(await controller.Shared(request with { Token = ReportSharingPolicy.NewToken() }, directory, audit, default));
        directory.Users[viewer] = new(false, null);
        Assert.IsInstanceOfType<NotFoundResult>(await controller.Shared(request, directory, audit, default));
        directory.Users[viewer] = new(true, null);
        rights.Users[owner] = Permissions();
        Assert.IsInstanceOfType<NotFoundResult>(await controller.Shared(request, directory, audit, default));
        rights.Users[owner] = Permissions(true); rights.Users[viewer] = EfektifYetkiler.Bos;
        Assert.IsInstanceOfType<NotFoundResult>(await controller.Shared(request, directory, audit, default));
        Assert.AreEqual(1, audit.Metadata.Count);
    }
}
