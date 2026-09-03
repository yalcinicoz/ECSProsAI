namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ProductCardImageLoadingTests
{
    [TestMethod]
    public void UrunKarti_MarkaliPlaceholderVeYuklemeIsaretiniBasiyor()
    {
        var razor = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Views", "ProjeElementleri", "Urun", "_UrunKarti.cshtml"));

        StringAssert.Contains(razor, "ms-urun-markali-placeholder");
        StringAssert.Contains(razor, "data-ms-urun-gorsel-yukleme=\"true\"");
        StringAssert.Contains(razor, "loading=\"eager\"");
        StringAssert.Contains(razor, "loading=\"lazy\"");
    }

    [TestMethod]
    public void UrunKartiGorselDavranisi_CompleteNaturalWidthVeDecodeKapilariniKullaniyor()
    {
        var script = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "wwwroot", "js", "site.js"));

        StringAssert.Contains(script, "img.complete");
        StringAssert.Contains(script, "img.naturalWidth > 0");
        StringAssert.Contains(script, "await img.decode()");
        StringAssert.Contains(script, "window.msUrunGorselYuklemeyeHazirla");
        StringAssert.Contains(script, "img.addEventListener(\"error\"");
    }

    [TestMethod]
    public void UrunKartiGorselDavranisi_PlaceholderiYalnizIlkYuklemedeGosteriyor()
    {
        var script = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "wwwroot", "js", "site.js"));

        StringAssert.Contains(script, "img.dataset.msUrunGorselIlkYuklendi !== \"true\"");
        StringAssert.Contains(script, "img.dataset.msUrunGorselIlkYuklendi = \"true\"");
        StringAssert.Contains(script, "window.msUrunGorselYuklemeyeHazirla?.(gorsel, true)");
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
