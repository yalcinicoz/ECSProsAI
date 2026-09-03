namespace ECSPros.Api.Tests;

[TestClass]
public sealed class StorefrontResponsiveMediaTests
{
    [TestMethod]
    public void Home_IlkSliderMobilGorseliniPreloadSozlesmesineEkler()
    {
        var home = File.ReadAllText(RepoFile("src", "ECSPros.Api", "Views", "Home", "Index.cshtml"));
        var layout = File.ReadAllText(RepoFile("src", "ECSPros.Api", "Views", "Shared", "_Layout.cshtml"));

        StringAssert.Contains(home, "ViewData[\"MsPreloadHeroMobile\"]");
        StringAssert.Contains(layout, "ViewData[\"MsPreloadHeroMobile\"]");
        StringAssert.Contains(layout, "media=\"(max-width: 767px)\"");
        StringAssert.Contains(layout, "media=\"(min-width: 768px)\"");
    }

    [TestMethod]
    public void Vitrin_SliderVeBannerMobilGorselleriniPictureIleSecer()
    {
        var view = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Views", "Shared", "Store", "_VitrinBloklar.cshtml"));
        var css = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "wwwroot", "css", "site.css"));

        StringAssert.Contains(view, "<picture class=\"contents\">");
        StringAssert.Contains(view, "slide.MobilGorselUrl");
        StringAssert.Contains(view, "reklamBanner.MobilGorselUrl");
        StringAssert.Contains(view, "banner.MobilGorselUrl");
        StringAssert.Contains(view, "data-ms-lazy-srcset=\"@(slideMobilSrcset ?? slideMobilUrl)\"");
        StringAssert.Contains(view, "media=\"(max-width: 767px)\"");
        StringAssert.Contains(css, ".contents{display:contents}");
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
