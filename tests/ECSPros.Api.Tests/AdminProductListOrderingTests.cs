namespace ECSPros.Api.Tests;

[TestClass]
public sealed class AdminProductListOrderingTests
{
    [TestMethod]
    public void UrunKartlari_SayfalamaOncesindeEnYeniAcilanUrunuOneAliyor()
    {
        // 2026-09-08 DataGrid F4: newest sıralaması ProductGrid.ApplySort'a taşındı (davranış aynı: CreatedAt desc + Id tie-breaker,
        // handler sıralamayı sayfalamadan önce uygular). Test aynı sözleşmeyi yeni yerinde doğrular.
        var grid = File.ReadAllText(RepoFile(
            "src", "Modules", "Catalog", "ECSPros.Catalog.Application",
            "Queries", "GetProducts", "ProductGrid.cs"));
        var handler = File.ReadAllText(RepoFile(
            "src", "Modules", "Catalog", "ECSPros.Catalog.Application",
            "Queries", "GetProducts", "GetProductsQueryHandler.cs"));
        var page = File.ReadAllText(RepoFile(
            "admin", "src", "pages", "catalog", "ProductsPage.tsx"));
        var controller = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Controllers", "CatalogController.cs"));

        var newestBranch = grid.IndexOf("\"newest\"", StringComparison.Ordinal);
        var createdAtOrder = grid.IndexOf("query.OrderByDescending(x => x.CreatedAt)", StringComparison.Ordinal);
        var idOrder = grid.IndexOf(".ThenByDescending(x => x.Id)", StringComparison.Ordinal);
        var sortCall = handler.IndexOf("ProductGrid.ApplySort(query, request.Grid, request.Sort)", StringComparison.Ordinal);
        var pagination = handler.IndexOf(".Skip((request.Page - 1) * request.PageSize)", StringComparison.Ordinal);

        Assert.IsTrue(newestBranch >= 0, "API newest sıralama seçeneğini desteklemelidir.");
        Assert.IsTrue(createdAtOrder > newestBranch, "Newest seçeneği ürünleri CreatedAt azalan sıralamalıdır.");
        Assert.IsTrue(idOrder > createdAtOrder, "Eşit açılış zamanlarında kararlı Id sıralaması bulunmalıdır.");
        Assert.IsTrue(sortCall >= 0 && pagination > sortCall, "Sıralama sayfalama uygulanmadan önce yapılmalıdır.");
        StringAssert.Contains(page, "sort: 'newest'");
        StringAssert.Contains(controller, "new GetProductsQuery(search, productGroupId, activeOnly, grid.Page, grid.PageSize, sort, grid)");
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "Repository root bulunamadı.");
        return Path.Combine([directory.FullName, .. parts]);
    }
}
