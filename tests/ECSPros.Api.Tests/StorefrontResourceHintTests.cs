namespace ECSPros.Api.Tests;

[TestClass]
public sealed class StorefrontResourceHintTests
{
    [TestMethod]
    public void Layout_GorselCdnBaglantisiniErkenBaslatir()
    {
        var layout = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Views", "Shared", "_Layout.cshtml"));

        StringAssert.Contains(layout, "rel=\"dns-prefetch\" href=\"//cdn.misharitalia.com\"");
        StringAssert.Contains(layout, "rel=\"preconnect\" href=\"https://cdn.misharitalia.com\"");

        var preconnectIndex = layout.IndexOf("rel=\"preconnect\"", StringComparison.Ordinal);
        var stylesheetIndex = layout.IndexOf("rel=\"stylesheet\"", StringComparison.Ordinal);
        Assert.IsTrue(preconnectIndex >= 0 && preconnectIndex < stylesheetIndex,
            "CDN preconnect, render-blocking stylesheetlerden önce bulunmalıdır.");
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
