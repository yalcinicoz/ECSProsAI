namespace ECSPros.Api.Tests;

[TestClass]
public sealed class ProductFilterDomPerformanceTests
{
    [TestMethod]
    public void MobilFiltre_FacetSecenekleriniIlkHtmlIcindeTekrarUretmez()
    {
        var mobile = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Views", "ProjeElementleri", "UrunListesi", "_UrunListesiMobilFiltre.cshtml"));
        var desktop = File.ReadAllText(RepoFile(
            "src", "ECSPros.Api", "Views", "ProjeElementleri", "UrunListesi", "_UrunListesiSolFiltre.cshtml"));

        Assert.IsFalse(mobile.Contains("@foreach (var deger", StringComparison.Ordinal));
        StringAssert.Contains(mobile, "data-ms-mobil-lazy-secenekler");
        StringAssert.Contains(mobile, "data-ms-mobil-chip-panel=\"@grup.TipKodu\"");
        StringAssert.Contains(mobile, "detaySecenekleriniYukle");
        StringAssert.Contains(mobile, "detaySecenekleriniYukle(hedefPanel, panelAdi)");
        StringAssert.Contains(mobile, "sayfa.querySelectorAll(\"[data-ms-filtre-deger]\")");
        StringAssert.Contains(desktop, "data-ms-filtre-grup=\"@grup.TipKodu\"");
        StringAssert.Contains(desktop, "data-ms-filtre-secenek-listesi");
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
