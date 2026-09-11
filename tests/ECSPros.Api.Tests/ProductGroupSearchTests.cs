using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Queries.GetProductGroups;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Tests;

[TestClass]
public class ProductGroupSearchTests
{
    [TestMethod]
    public void NamesAcrossLanguagesAndTurkishAsciiArePreserved()
    {
        var names = new Dictionary<string, string> { ["tr"] = "IŞIKLI İç Giyim Gömlek", ["en"] = "Shirt" };
        foreach (var term in new[] { "gomlek", "GÖMLEK", "shirt", "  İÇ   GİYİM ", "ic giyim", "ISIKLI", "ışıklı" })
            Assert.IsTrue(ProductGroupSearch.Matches(names, ProductGroupSearch.PathFor(term)), term);
        foreach (var term in new[] { "bulunmayan", "tr", ".*", "\" or true" })
            Assert.IsFalse(ProductGroupSearch.Matches(names, ProductGroupSearch.PathFor(term)), term);
    }

    [TestMethod]
    public void ActiveFilterAndCodeSearchRemainComposed()
    {
        var rows = new[] { new ProductGroup { Code = "grp_83", IsActive = true }, new ProductGroup { Code = "grp_83", IsActive = false } }.AsQueryable();
        Assert.AreEqual(1, ProductGroupGrid.ApplyNamed(rows, new(true, " GRP_83 ")).Count());
    }

    [TestMethod]
    public void SearchAndPaginationTranslateWithoutConnectingToDatabase()
    {
        using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var sql = ProductGroupGrid.ApplyNamed(db.ProductGroups, new(true, "Gömlek"))
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Id).Skip(20).Take(20).ToQueryString();
        StringAssert.Contains(sql, "jsonb_path_exists");
        StringAssert.Contains(sql, "jsonpath");
        StringAssert.Contains(sql, "LIMIT");
        StringAssert.Contains(sql, "OFFSET");
        StringAssert.Contains(sql, "@__namePath");
    }
}
