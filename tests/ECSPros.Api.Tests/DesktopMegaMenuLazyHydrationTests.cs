namespace ECSPros.Api.Tests;

[TestClass]
public sealed class DesktopMegaMenuLazyHydrationTests
{
    [TestMethod]
    public void IlkHtml_MegaMenuIceriginiTasamaz_EndpointKategorilerEtkilesimindeYuklenir()
    {
        var source = File.ReadAllText(RepoFile("src", "ECSPros.Api", "wwwroot", "js", "site.js"));
        var desktop = File.ReadAllText(RepoFile("src", "ECSPros.Api", "Views", "ProjeElementleri", "Navigasyon", "_AnaNavigasyonDesktopMenu.cshtml"));
        var fragment = File.ReadAllText(RepoFile("src", "ECSPros.Api", "Views", "ProjeElementleri", "Navigasyon", "_AnaNavigasyonMegaMenu.cshtml"));
        var controller = File.ReadAllText(RepoFile("src", "ECSPros.Api", "Controllers", "Store", "StoreNavigationController.cs"));
        var program = File.ReadAllText(RepoFile("src", "ECSPros.Api", "Program.cs"));

        StringAssert.Contains(source,
            "const megaMenuTetikAlani = menu.dataset.msMegaHover === \"1\"");
        StringAssert.Contains(source,
            ": menu.querySelector(\".ms-magaza-menu-tum\");");
        StringAssert.Contains(source,
            "megaMenuTetikAlani?.addEventListener(\"pointerenter\", magazaMenuBaslat, { once: true });");
        StringAssert.Contains(source,
            "megaMenuTetikAlani?.addEventListener(\"focusin\", magazaMenuBaslat, { once: true });");
        StringAssert.Contains(source,
            "megaMenuTetikAlani?.addEventListener(\"pointerdown\", magazaMenuBaslat, { once: true, capture: true });");
        StringAssert.Contains(source, "const yanit = await fetch(url");
        StringAssert.Contains(source, "hedef.innerHTML = await yanit.text();");
        StringAssert.Contains(source, "if (await magazaMenuBaslat())");
        Assert.IsFalse(source.Contains(
            "menu.addEventListener(\"pointerenter\", magazaMenuBaslat, { once: true });",
            StringComparison.Ordinal));

        StringAssert.Contains(desktop, "data-ms-mega-menu-url=\"/store/navigation/mega-menu\"");
        StringAssert.Contains(desktop, "data-ms-magaza-mega-menu-hedef");
        Assert.IsFalse(desktop.Contains("data-ms-magaza-mega-menu-sablon", StringComparison.Ordinal));
        Assert.IsFalse(desktop.Contains("data-ms-magaza-mega-menu\"", StringComparison.Ordinal));
        Assert.IsFalse(desktop.Contains("ms-magaza-mega-resimli-link", StringComparison.Ordinal));

        StringAssert.Contains(fragment, "data-ms-magaza-mega-menu");
        StringAssert.Contains(fragment, "ms-magaza-mega-resimli-link");
        StringAssert.Contains(controller, "[HttpGet(\"mega-menu\")]");
        StringAssert.Contains(controller, "Duration = 300");
        StringAssert.Contains(controller, "_AnaNavigasyonMegaMenu.cshtml");
        StringAssert.Contains(program, "var onbelleklenebilirHtmlParcasi = requestPath.Equals(");
        StringAssert.Contains(program, "&& !onbelleklenebilirHtmlParcasi)");
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        Assert.IsNotNull(directory, "Repository kökü bulunamadı.");
        return Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
    }
}
