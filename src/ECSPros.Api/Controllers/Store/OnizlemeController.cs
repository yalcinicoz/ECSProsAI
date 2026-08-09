using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Ürün Kartı F1 (2026-08-09): panel Storefront → Ürün Kartı ekranının iframe önizlemesi.
/// Demo verili kartı gerçek site markup'ı + site.css ile render eder; ayarlar query'deki
/// JSON'dan gelir (kaydedilmemiş değişiklik anında görünür, DB'ye dokunmaz). Sayfa statik
/// ve kişisel veri içermez — auth istemez (iframe Authorization header taşıyamaz).
/// </summary>
[Route("onizleme")]
public sealed class OnizlemeController : Controller
{
    [HttpGet("urun-karti")]
    public IActionResult UrunKarti([FromQuery] string? ayar)
    {
        var kartAyar = StoreKartAyarlari.Varsayilan;
        if (!string.IsNullOrWhiteSpace(ayar))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(ayar);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                    kartAyar = StoreKartAyarlari.FromJson(doc.RootElement.Clone());
            }
            catch (System.Text.Json.JsonException)
            {
                // bozuk query → varsayılan ayarlarla render (önizleme hata sayfası göstermez)
            }
        }
        ViewData["MsKartAyarlari"] = kartAyar;

        // Tüm elementleri tetikleyen demo kart: video + sponsor + 3 renk + galeri +
        // kampanya rozetleri + puan + indirim + kampanyalı fiyat. Görseller no-image
        // placeholder'ı — önizlemenin konusu içerik değil, hangi elementin açık olduğu.
        const string demoGorsel = "/images/no-image.svg";
        var renkler = new[] { "Siyah", "Bej", "Yeşil" }
            .Select(ad => new KartRenkVm(Guid.NewGuid(), ad, demoGorsel))
            .ToList();
        var demo = new UrunKartVm(
            Kod: "DEMO-0001",
            Ad: "Marka Örnek Ürün Adı",
            GorselUrl: demoGorsel,
            Fiyat: 1299.90m,
            RenkHexleri: ["#1f2937", "#d6c7b0"],
            RenkSayisi: renkler.Count,
            DegerIdler: [],
            GaleriUrller: [demoGorsel, demoGorsel, demoGorsel],
            RenkSecenekleri: renkler,
            SeciliRenkId: null,
            Sponsorlu: true,
            Puan: 4.6,
            PuanSayisi: 128,
            VideoUrl: "about:blank",
            EskiFiyat: 1699.90m,
            KampanyaFiyat: 1199.90m,
            KampanyaRozetleri:
            [
                new CampaignBadge("Kargo Bedava", null),
                new CampaignBadge("2. Ürün %50", "#7c3aed")
            ]);

        return View("~/Views/Onizleme/UrunKarti.cshtml", demo);
    }
}
