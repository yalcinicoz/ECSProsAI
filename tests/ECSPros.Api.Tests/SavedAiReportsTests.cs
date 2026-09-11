using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using ECSPros.Api.Controllers;
using ECSPros.Iam.Application.Services;
using ECSPros.Shared.Kernel.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ECSPros.Api.Tests;

[TestClass]
public class SavedAiReportsTests
{
    private const string Recipe = """{"version":1,"subject":"stock","metrics":["stock.available"],"dimensions":["productCode"],"filters":[],"presentation":"table","limit":100}""";
    public class Store : DispatchProxy
    {
        public object?[]? Written;
        public int Affected = 1;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.IsTrue(method!.Name is "WritePreferenceAsync" or "CompareExchangePreferenceAsync");
            Written = args;
            return Task.FromResult(Affected);
        }
    }
    private sealed class Authorization(bool allowed) : IEtkinYetkiServisi
    {
        public Task<EfektifYetkiler> GetirAsync(Guid id, CancellationToken ct = default) => Task.FromResult(new EfektifYetkiler(false,
            allowed ? new() { ["reports.ai.use"] = null, ["inventory.view"] = null } : new Dictionary<string, HashSet<Guid>?>()));
        public Task GecersizKilAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task GrupIcinGecersizKilAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
    private static (SavedAiReportsController Controller, Store Store, Guid User) Create(bool allowed = true)
    {
        var db = DispatchProxy.Create<IIamDbContext, Store>();
        var uid = Guid.NewGuid();
        var controller = new SavedAiReportsController(db, new Authorization(allowed), new ReportAttributeCatalogStub(), null!, new ConfigurationBuilder().Build());
        controller.ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", uid.ToString()) }, "test")) } };
        return (controller, (Store)(object)db, uid);
    }
    [TestMethod]
    public async Task SavesOnlyValidatedRecipeForClaimOwner_InsertOnly()
    {
        var (controller, store, user) = Create();
        var result = await controller.Save(2, new(" Test ", JsonDocument.Parse(Recipe).RootElement), default);
        Assert.IsInstanceOfType<OkObjectResult>(result);
        Assert.AreEqual(user, store.Written![0]);
        Assert.AreEqual("reports.ai.saved.2", store.Written[1]);
        Assert.AreEqual(true, store.Written[3]);
        var value = JsonDocument.Parse((string)store.Written[2]!);
        Assert.AreEqual("Test", value.RootElement.GetProperty("name").GetString());
        Assert.IsFalse(value.RootElement.TryGetProperty("rows", out _));
        store.Affected = 0;
        Assert.IsInstanceOfType<ConflictObjectResult>(await controller.Save(2, new("Test", JsonDocument.Parse(Recipe).RootElement), default));
    }
    [TestMethod]
    public async Task InvalidSlotRecipeOrPermissionNeverWrites()
    {
        foreach (var slot in new[] { -1, 20 })
        {
            var (controller, store, _) = Create();
            Assert.IsInstanceOfType<BadRequestObjectResult>(await controller.Save(slot, new("Test", JsonDocument.Parse(Recipe).RootElement), default));
            Assert.IsNull(store.Written);
        }
        var denied = Create(false);
        Assert.IsInstanceOfType<ForbidResult>(await denied.Controller.Save(0, new("Test", JsonDocument.Parse(Recipe).RootElement), default));
        Assert.IsNull(denied.Store.Written);
        var invalid = Create();
        Assert.IsInstanceOfType<BadRequestObjectResult>(await invalid.Controller.Save(0, new("Test", JsonDocument.Parse("{}").RootElement), default));
        Assert.IsNull(invalid.Store.Written);
    }

    [TestMethod]
    public async Task UpdateAndRemoveRequireExpectedValueAndOwner_ConflictsPreserveRecord()
    {
        var (controller, store, user) = Create();
        var definition = JsonDocument.Parse(Recipe).RootElement;
        var expected = JsonSerializer.SerializeToElement(new { name = "old", definition });
        Assert.IsInstanceOfType<OkObjectResult>(await controller.Update(1, new("new", definition, expected), default));
        Assert.AreEqual(user, store.Written![0]);
        Assert.AreEqual(expected.GetRawText(), store.Written[2]);
        Assert.IsFalse(((string)store.Written[3]!).Contains("shareToken"));
        store.Affected = 0;
        Assert.IsInstanceOfType<ConflictObjectResult>(await controller.Remove(1, new(expected), default));
        Assert.AreEqual(user, store.Written![0]);
        Assert.IsNull(store.Written[3]);
        var denied = Create(false);
        Assert.IsInstanceOfType<ForbidResult>(await denied.Controller.Remove(1, new(expected), default));
        Assert.IsNull(denied.Store.Written);
    }
}
