namespace ECSPros.Api.Tests;

[TestClass]
public sealed class StorefrontSmallImageLoadingTests
{
    [TestMethod]
    public void AnaSayfaKucukGorselleri_MarkaliPlaceholderVeOrtakYuklemeKapisiKullaniyor()
    {
        var razor = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Views", "Shared", "Store", "_VitrinBloklar.cshtml"));

        StringAssert.Contains(razor, "ms-story-gorsel ms-kucuk-gorsel-yukleme");
        StringAssert.Contains(razor, "ms-gorunum-kategori-kapsul-gorsel ms-kucuk-gorsel-yukleme");
        StringAssert.Contains(razor, "ms-gorunum-banner-karti ms-kucuk-gorsel-yukleme");
        Assert.AreEqual(3, Count(razor, "data-ms-urun-gorsel-yukleme=\"true\""));
        Assert.AreEqual(3, Count(razor, "ms-kucuk-gorsel-placeholder"));
        StringAssert.Contains(razor, "msKucukGorselPlaceholderSrc");
        StringAssert.Contains(razor, "msCokluBannerPlaceholderSrc");
        StringAssert.Contains(razor, "width='110' height='165'");
        StringAssert.Contains(razor, "loading=\"eager\"");
        StringAssert.Contains(razor, "loading=\"lazy\"");
    }

    [TestMethod]
    public void AnaSayfaKucukGorselleri_DecodeSonrasiUcYuzMilisedeGorunur()
    {
        var css = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "wwwroot", "css", "tailwind.css"));
        var compiledCss = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "wwwroot", "css", "site.css"));
        var script = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "wwwroot", "js", "site.js"));

        StringAssert.Contains(css, ".ms-kucuk-gorsel-placeholder {");
        StringAssert.Contains(css, "transition-opacity duration-300");
        StringAssert.Contains(css, ".ms-kucuk-gorsel-yukleme img.ms-lazy-gorsel-yuklendi ~ .ms-kucuk-gorsel-placeholder");
        StringAssert.Contains(css, ".ms-story-gorsel .ms-kucuk-gorsel-placeholder");
        StringAssert.Contains(css, ".ms-gorunum-kategori-kapsul-gorsel .ms-kucuk-gorsel-placeholder");
        StringAssert.Contains(compiledCss, ".ms-kucuk-gorsel-placeholder");
        StringAssert.Contains(compiledCss, ".ms-kucuk-gorsel-yukleme img.ms-lazy-gorsel-yuklendi~.ms-kucuk-gorsel-placeholder");
        StringAssert.Contains(compiledCss, "transition-duration:.3s");
        StringAssert.Contains(script, "img.complete");
        StringAssert.Contains(script, "img.naturalWidth > 0");
        StringAssert.Contains(script, "await img.decode()");
        StringAssert.Contains(script, "img.addEventListener(\"error\"");
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
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
