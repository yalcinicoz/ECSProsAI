namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ReviewModerationOrderingTests
{
    [TestMethod]
    public void ModerationOrdersNewestFirstBeforePagination_WithStableTieBreaker()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;
        Assert.IsNotNull(directory);
        var source = File.ReadAllText(Path.Combine(directory.FullName, "src", "Modules", "Storefront",
            "ECSPros.Storefront.Application", "Queries", "GetReviewsForModeration", "GetReviewsForModerationQuery.cs"));
        var filter = source.IndexOf("q.Where(r => r.Status == request.Status)", StringComparison.Ordinal);
        var order = source.IndexOf("q.OrderByDescending(r => r.CreatedAt)", StringComparison.Ordinal);
        var tie = source.IndexOf(".ThenByDescending(r => r.Id)", StringComparison.Ordinal);
        var skip = source.IndexOf(".Skip((request.Page - 1) * request.PageSize)", StringComparison.Ordinal);
        var take = source.IndexOf(".Take(request.PageSize)", StringComparison.Ordinal);
        Assert.IsTrue(filter >= 0 && order > filter);
        Assert.IsTrue(tie > order && skip > tie && take > skip,
            "Moderasyon tüm sonuçları en yeni önce sıralayıp sonra sayfalamalıdır.");
        StringAssert.Contains(source, ".OrderBy(p => p.SortOrder)");
    }
}
