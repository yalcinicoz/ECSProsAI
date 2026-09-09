namespace ECSPros.Api.Tests;

/// <summary>
/// Moderasyon kuyruğunun garantisi: TÜM sonuçlar en yeni önce sıralanır, SONRA sayfalanır; aynı tarihli
/// kayıtlarda sıra sabit kalsın diye kararlı bir tie-breaker vardır (aksi hâlde sayfa 2'de kayıt tekrar
/// eder ya da atlanır). Foto önizlemeleri SortOrder ile sıralanır.
///
/// 2026-09-09 (DataGrid turu): sıralama/filtreleme satır içi LINQ'ten <c>ReviewModerationGrid.Schema</c>'ya
/// taşındı — <c>ApplySort</c> varsayılan sıralamayı uygulayıp tie-breaker'ı AYNI yönde ekler, yani
/// eski <c>OrderByDescending(CreatedAt).ThenByDescending(Id)</c> davranışının birebir aynısı. Test bu yüzden
/// artık iki dosyayı birlikte denetler: şemada varsayılan sıralama + tie-breaker bildirimi, handler'da ise
/// filtre → sıralama → sayfalama sırası.
/// </summary>
[TestClass]
public sealed class ReviewModerationOrderingTests
{
    private static string Oku(params string[] parcalar)
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !Directory.Exists(Path.Combine(dizin.FullName, "src")))
            dizin = dizin.Parent;
        Assert.IsNotNull(dizin);
        return File.ReadAllText(Path.Combine(new[] { dizin.FullName, "src" }.Concat(parcalar).ToArray()));
    }

    [TestMethod]
    public void ModerationOrdersNewestFirstBeforePagination_WithStableTieBreaker()
    {
        var handler = Oku("Modules", "Storefront", "ECSPros.Storefront.Application", "Queries",
            "GetReviewsForModeration", "GetReviewsForModerationQuery.cs");
        var schema = Oku("Modules", "Storefront", "ECSPros.Storefront.Application", "Queries",
            "GetReviewsForModeration", "ReviewModerationGrid.cs");

        // 1) Şema: varsayılan sıralama EN YENİ ÖNCE + kararlı tie-breaker.
        StringAssert.Contains(schema, ".DefaultSort(r => r.CreatedAt, desc: true)",
            "Moderasyon varsayılan sıralaması en yeni önce olmalı.");
        StringAssert.Contains(schema, ".TieBreaker(r => r.Id)",
            "Aynı tarihli kayıtlarda sayfalama sırası sabit kalsın diye tie-breaker şart.");

        // 2) Handler: filtre → sıralama → sayfalama. Sıralama Skip'ten ÖNCE olmalı;
        //    aksi hâlde yalnız sayfa içi sıralanır ve sayfalar arası kayıt tekrarı/atlaması olur.
        var filtre = handler.IndexOf("ReviewModerationGrid.ApplyAll(", StringComparison.Ordinal);
        var sirala = handler.IndexOf("ReviewModerationGrid.Schema.ApplySort(", StringComparison.Ordinal);
        var skip = handler.IndexOf(".Skip((request.Page - 1) * request.PageSize)", StringComparison.Ordinal);
        var take = handler.IndexOf(".Take(request.PageSize)", StringComparison.Ordinal);
        Assert.IsTrue(filtre >= 0, "Filtreler ReviewModerationGrid.ApplyAll üzerinden uygulanmalı.");
        Assert.IsTrue(sirala > filtre && skip > sirala && take > skip,
            "Moderasyon tüm sonuçları en yeni önce sıralayıp sonra sayfalamalıdır.");

        // 3) Sayım sayfalamadan ÖNCE, filtrelenmiş küme üzerinde alınmalı.
        var say = handler.IndexOf("await q.CountAsync(ct)", StringComparison.Ordinal);
        Assert.IsTrue(say > filtre && say < skip, "Toplam sayı filtrelenmiş kümeden, sayfalamadan önce alınmalı.");

        // 4) Y3 kanal kapsamı hâlâ şemada bildirilmiş olmalı (kapsam dışı kanal sızmasın).
        StringAssert.Contains(schema, ".Kanal(r => r.FirmPlatformId)",
            "Y3: moderasyon listesi kanal kapsamına tabi.");

        // 5) Foto önizlemeleri sıralı gelir.
        StringAssert.Contains(handler, ".OrderBy(p => p.SortOrder)");
    }
}
