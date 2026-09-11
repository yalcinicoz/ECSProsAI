using ECSPros.Api.Services.AiReporting;
using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReportSourceCatalogTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly HashSet<string> All = new()
    { "reports.ai.use", "inventory.view", "orders.view", "catalog.products.view" };

    [TestMethod]
    public void ExistingFieldsRemainCompatible_UnknownSourcesAreNotAdvertised()
    {
        CollectionAssert.AreEqual(ReportDictionary.Fields.ToArray(), ReportDictionary.ForSubject("stock", All).ToArray());
        CollectionAssert.AreEqual(OrderReportRecipe.Fields.ToArray(), ReportDictionary.ForSubject("orders", All).ToArray());
        CollectionAssert.AreEqual(new[] { "stock", "orders", "stockMovements" }, ReportSourceCatalog.ForPermissions(All).Select(s => s.Id).ToArray());
        Assert.AreEqual(0, ReportDictionary.ForSubject("payments", All).Count);
        Assert.AreEqual(0, ReportDictionary.ForSubject(null!, All).Count);
        Assert.AreEqual(0, ReportSourceCatalog.ForPermissions(new HashSet<string> { "inventory.view" }).Count);
    }

    [TestMethod]
    public void ScopedReportingCanDiscoverOrders_ButCannotOpenGlobalStock()
    {
        var effective = new EfektifYetkiler(false, new()
        {
            ["reports.ai.use"] = new() { A }, ["orders.view"] = new() { A, B },
            ["inventory.view"] = null, ["catalog.products.view"] = null
        });
        var permissions = ReportSourceCatalog.ResolvePermissions(effective);
        CollectionAssert.AreEqual(new[] { "orders" }, ReportSourceCatalog.ForPermissions(permissions).Select(s => s.Id).ToArray());
        CollectionAssert.AreEqual(new[] { A }, ReportSourceCatalog.ResolveChannels("orders", effective)!);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => ReportSourceCatalog.ResolveChannels("stock", effective));
        Assert.IsTrue(ReportDefinitionValidator.Parse(OrderReportRecipeTests.Json, permissions).IsValid);
    }

    [TestMethod]
    public void EmptyAndMissingScopesNeverBecomeUnrestricted()
    {
        var effective = new EfektifYetkiler(false, new()
        { ["reports.ai.use"] = new() { A }, ["orders.view"] = new() { B } });
        Assert.AreEqual(0, ReportSourceCatalog.ResolveChannels("orders", effective)!.Length);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => ReportSourceCatalog.ResolveChannels("orders", EfektifYetkiler.Bos));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => ReportSourceCatalog.ResolveChannels("unknown", new(true, new())));
        Assert.IsNull(ReportSourceCatalog.ResolveChannels("orders", new(true, new())));
    }

    [TestMethod]
    public void ControlledAttributesCannotShadowFieldsOrChangePermissionsAndOperators()
    {
        var field = ReportAttributeCatalog.Field(A, "Cinsiyet", "gender");
        Assert.AreEqual(ReportDictionary.Fields.Count + 1, ReportDictionary.ForPermissions(All, new[] { field }).Count);
        foreach (var invalid in new[]
        {
            new[] { field, field }, new[] { field with { Id = "stock.quantity" } },
            new[] { field with { Permission = "inventory.view" } }, new[] { field with { Kind = "metric" } },
            new[] { field with { Operators = new[] { "sql", "in" } } }
        }) Assert.ThrowsExactly<ReportCatalogException>(() => ReportDictionary.ForPermissions(All, invalid));
        var noCatalog = new HashSet<string> { "reports.ai.use", "inventory.view" };
        Assert.AreEqual(ReportDictionary.Fields.Count, ReportDictionary.ForPermissions(noCatalog, new[] { field }).Count);
        Assert.IsFalse(ReportDictionary.ForSubject("orders", All, new[] { field }).Any(f => f.Id == field.Id));
    }
}
