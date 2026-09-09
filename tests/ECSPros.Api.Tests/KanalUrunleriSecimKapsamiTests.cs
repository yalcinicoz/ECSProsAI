using System.Text.RegularExpressions;

namespace ECSPros.Api.Tests;

/// <summary>
/// Kanal Ürünleri — "Filtreye uyan tümünü seç" ile LİSTE aynı filtre kümesini kullanmalı (2026-09-09).
///
/// Neden kritik: ekranda toplu işlem var (kanala al / çıkar / satışı durdur / pazaryerine gönder).
/// Kullanıcı listeyi başlık filtreleriyle 5 ürüne süzüp "tümünü seç" derse, id ucu o filtreleri
/// bilmiyorsa BİNLERCE ürün seçilir ve toplu işlem YANLIŞ ürünlere uygulanır — geri alması zor bir hata.
/// Bu test iki sorgunun da aynı şemayı uyguladığını ve controller'ın grid'i İKİSİNE DE geçtiğini sabitler.
/// </summary>
[TestClass]
public sealed class KanalUrunleriSecimKapsamiTests
{
    private static string Oku(params string[] parcalar)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !Directory.Exists(Path.Combine(d.FullName, "src"))) d = d.Parent;
        Assert.IsNotNull(d);
        return File.ReadAllText(Path.Combine(new[] { d!.FullName, "src" }.Concat(parcalar).ToArray()));
    }

    [TestMethod]
    public void Liste_ve_tumunu_sec_ayni_filtreleri_uygular()
    {
        var klasor = new[] { "Modules", "Storefront", "ECSPros.Storefront.Application", "Queries", "GetChannelProductsAdmin" };
        var liste = Oku(klasor.Append("GetChannelProductsAdminQuery.cs").ToArray());
        var idler = Oku(klasor.Append("GetChannelProductIdsAdminQuery.cs").ToArray());

        StringAssert.Contains(liste, "ChannelProductGrid.Schema.ApplyFilters",
            "Liste sorgusu başlık filtrelerini uygulamalı.");
        StringAssert.Contains(idler, "ChannelProductGrid.Schema.ApplyFilters",
            "\"Tümünü seç\" sorgusu AYNI başlık filtrelerini uygulamalı — yoksa toplu işlem filtre dışına taşar.");

        // Filtre, id listesi ALINMADAN önce uygulanmalı.
        var filtre = idler.IndexOf("ChannelProductGrid.Schema.ApplyFilters", StringComparison.Ordinal);
        var secim = idler.IndexOf(".Select(p => p.Id)", StringComparison.Ordinal);
        Assert.IsTrue(filtre >= 0 && secim > filtre, "Filtre, id kümesi okunmadan önce uygulanmalı.");
    }

    [TestMethod]
    public void Controller_grid_i_her_iki_uca_da_gecirir()
    {
        var ctrl = Oku("ECSPros.Api", "Controllers", "NavigationController.cs");

        // manage (liste) ve manage/ids (tümünü seç) uçlarının İKİSİ de GridRequestParser kullanmalı.
        var manage = ctrl.IndexOf("GetChannelProductsAdminQuery(", StringComparison.Ordinal);
        var ids = ctrl.IndexOf("GetChannelProductIdsAdminQuery(", StringComparison.Ordinal);
        Assert.IsTrue(manage > 0 && ids > 0, "Her iki uç da bulunmalı.");

        foreach (var (ad, konum) in new[] { ("manage", manage), ("manage/ids", ids) })
        {
            // Çağrının hemen öncesindeki blokta grid çözümü olmalı ve çağrıya geçilmeli.
            var blok = ctrl[Math.Max(0, konum - 700)..Math.Min(ctrl.Length, konum + 400)];
            StringAssert.Contains(blok, "GridRequestParser.Parse", $"{ad} ucu grid isteğini çözmeli.");
            Assert.IsTrue(Regex.IsMatch(blok, @",\s*grid\s*\)"), $"{ad} ucu grid'i sorguya geçirmeli.");
        }
    }
}
