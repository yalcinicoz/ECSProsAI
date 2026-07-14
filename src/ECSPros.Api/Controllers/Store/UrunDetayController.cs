using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.GetProductIdByCode;
using ECSPros.Catalog.Application.Queries.GetStoreProductDetail;
using ECSPros.Storefront.Application.Queries.GetProductChannelCategoryChain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Ürün detay sayfası (B9): /urun/{code}?color={valueId}. Tamamen SSR — seçili renk
/// sunucuda çözülür (renk butonları ?color= navigasyonu yapar), beden seçimi ve sepete
/// ekleme client-side'dır (misharix script'i + partial sonundaki config/cart script'i).
/// Veri süreç içi MediatR'dan gelir; mobil uygulama aynı sorguyu api/store/catalog/
/// products/{code} üzerinden kullanır (plan 3.4).
/// </summary>
public class UrunDetayController(IMediator mediator, IStoreContext storeContext, ECSPros.Shared.Contracts.IStockService stockService, ECSPros.Shared.Contracts.IProductReviewStatsService reviewStats) : StorePageController
{
    [HttpGet("/urun/{code}")]
    public async Task<IActionResult> Index(string code, [FromQuery] string? color, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return Redirect("/");   // platform çözülemedi — 404 yerine ana sayfa

        // Satışa kapalı / erişilemeyen ürün: 404 yerine ürünün kategorisine, yoksa ana sayfaya
        // 301 (kullanıcı kararı — 404'ten kaçınılıyor). Kapalı ürün detay sorgusundan düşer.
        var sonuc = await mediator.Send(new GetStoreProductDetailQuery(code, platform.Id), ct);
        if (sonuc.IsFailure)
            return await KapaliUrunYonlendir(code, platform.Id, ct);

        var urun = sonuc.Value!;
        var varyantlar = urun.Variants.Where(v => v.IsActive).ToList();
        if (varyantlar.Count == 0)
            return await KapaliUrunYonlendir(code, platform.Id, ct);

        // Renk ekseni: filtre_rengi (IsColor) öncelikli; hiç atanmamışsa serbest-metin "renk"
        // ekseni renk kabul edilir (handler görsel gruplamada da aynı geri düşüşü yapar).
        // Görseli olmayan renkler listelenmez (SPA paritesi).
        var renkTipKodu = varyantlar
            .SelectMany(v => v.Attributes)
            .FirstOrDefault(a => a.IsColor)?.AttributeTypeCode
            ?? varyantlar
                .SelectMany(v => v.Attributes)
                .FirstOrDefault(a => a.AttributeTypeCode == "renk")?.AttributeTypeCode;

        var renkDegerleri = renkTipKodu is null
            ? []
            : varyantlar
                .SelectMany(v => v.Attributes.Where(a => a.AttributeTypeCode == renkTipKodu)
                    .Select(a => (a.AttributeValueId, Ad: TrAd(a.AttributeValueNameI18n))))
                .DistinctBy(x => x.AttributeValueId)
                .ToList();

        var renkGorselleri = new Dictionary<Guid, string>();
        foreach (var (valueId, _) in renkDegerleri)
        {
            var gorselluVaryant = varyantlar.FirstOrDefault(v =>
                v.Images.Count > 0 && VaryantRenktenMi(v, renkTipKodu!, valueId));
            if (gorselluVaryant is not null)
                renkGorselleri[valueId] = gorselluVaryant.Images[0].ImageUrl;
        }
        var gorunurRenkler = renkDegerleri.Where(r => renkGorselleri.ContainsKey(r.AttributeValueId)).ToList();

        // ?color= öncelikle renk ekseninin kendi değeri; değilse eksen-dışı bir değer olabilir —
        // liste kartları primary axis ("renk") değeriyle link verir, filtre_rengi bucket'ına
        // burada çözülür: o değeri taşıyan varyantın rengi seçilir.
        Guid? seciliRenk = null;
        if (Guid.TryParse(color, out var istenen))
        {
            if (gorunurRenkler.Any(r => r.AttributeValueId == istenen))
                seciliRenk = istenen;
            else if (renkTipKodu is not null)
                seciliRenk = varyantlar
                    .Where(v => v.Attributes.Any(a => a.AttributeValueId == istenen))
                    .SelectMany(v => v.Attributes.Where(a => a.AttributeTypeCode == renkTipKodu))
                    .Select(a => (Guid?)a.AttributeValueId)
                    .FirstOrDefault(id => gorunurRenkler.Any(r => r.AttributeValueId == id));
        }
        seciliRenk ??= gorunurRenkler.Count > 0 ? gorunurRenkler[0].AttributeValueId : null;

        // Fiyat/beden havuzu: seçili renkteki varyantlar (renk ekseni yoksa tümü)
        var havuz = seciliRenk is { } renk
            ? varyantlar.Where(v => VaryantRenktenMi(v, renkTipKodu!, renk)).ToList()
            : varyantlar;
        if (havuz.Count == 0)
            havuz = varyantlar;

        // Galeri: seçili rengin görsel havuzu (handler aynı renk varyantlarına aynı listeyi verir)
        var gorseller = (havuz.FirstOrDefault(v => v.Images.Count > 0)
                         ?? varyantlar.FirstOrDefault(v => v.Images.Count > 0))
            ?.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).Distinct().ToList() ?? [];

        // Beden ekseni: havuzdaki renk-dışı ilk eksen. "renk" ve "filtre_rengi" her durumda
        // renk eksenidir — beden adayı olamaz (filtre_rengi seçiliyken serbest-metin "renk"
        // ekseni beden sanılmasın).
        var bedenTip = havuz
            .SelectMany(v => v.Attributes)
            .FirstOrDefault(a => !a.IsColor
                                 && a.AttributeTypeCode != renkTipKodu
                                 && a.AttributeTypeCode is not ("renk" or "filtre_rengi"));

        // 2026-07-14: stok HER ZAMAN dikkate alınır — satılabilirlik gerçek online stoktan
        // (satışa-açık kısımlar). Beden bazında Tükendi/sepet gating buradan beslenir.
        var stoklar = new Dictionary<Guid, int>();
        foreach (var v in havuz)
            stoklar[v.Id] = await stockService.GetAvailableStockAsync(v.Id, null, ct);
        bool Satilabilir(Guid variantId) => stoklar.GetValueOrDefault(variantId) > 0;

        var bedenler = new List<BedenSecenekVm>();
        if (bedenTip is not null)
        {
            bedenler = havuz
                .Select(v => (Varyant: v, Beden: v.Attributes
                    .FirstOrDefault(a => a.AttributeTypeCode == bedenTip.AttributeTypeCode)))
                .Where(x => x.Beden is not null)
                .DistinctBy(x => x.Beden!.AttributeValueId)
                .Select(x => new BedenSecenekVm(
                    TrAd(x.Beden!.AttributeValueNameI18n),
                    x.Varyant.Id,
                    x.Varyant.PlatformPrice ?? x.Varyant.BasePrice,
                    Satilabilir(x.Varyant.Id)))
                .OrderBy(x => BedenSirasi(x.Ad))
                .ThenBy(x => x.Ad, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
                .ToList();
        }

        // Sıfır fiyatlı varyantlar (eksik veri) gösterim fiyatına girmez (SPA paritesi);
        // tüm havuz sıfırsa mecburen ilki kullanılır.
        var fiyatliVaryant = havuz
            .Where(v => (v.PlatformPrice ?? v.BasePrice) > 0)
            .OrderBy(v => v.PlatformPrice ?? v.BasePrice)
            .FirstOrDefault() ?? havuz[0];
        var fiyat = fiyatliVaryant.PlatformPrice ?? fiyatliVaryant.BasePrice;
        var eskiFiyat = fiyatliVaryant.CompareAtPrice is { } eski && eski > fiyat ? eski : (decimal?)null;

        var zincir = await mediator.Send(
            new GetProductChannelCategoryChainQuery(platform.Id, urun.Id), ct);
        var breadcrumb = zincir.IsSuccess
            ? zincir.Value!.Select(k => new BreadcrumbAdimVm(TrAd(k.NameI18n), "/" + k.Slug)).ToList()
            : [];

        var ozellikler = new List<OzellikVm>();
        if (urun.ProductGroupNameI18n is { } grupAd)
            ozellikler.Add(new OzellikVm("Kategori Grubu", TrAd(grupAd)));
        ozellikler.AddRange((urun.Attributes ?? [])
            .GroupBy(a => a.TypeCode)
            .Select(g => new OzellikVm(
                TrAd(g.First().TypeNameI18n),
                string.Join(", ", g.Select(a => TrAd(a.ValueNameI18n)).Distinct())))
            .Where(o => o.Ad.Length > 0 && o.Deger.Length > 0));
        // B12: anahtar açıkken gerçek durum; kapalıyken her ürün satılabilir kabul edilir.
        var urunSatilabilir = bedenler.Count > 0
            ? bedenler.Any(x => x.Satilabilir)
            : Satilabilir(havuz[0].Id);
        ozellikler.Add(new OzellikVm("Stok Durumu", urunSatilabilir ? "Stokta" : "Tükendi"));

        // E7: puan + yayında ilk 10 yorum (SSR — kart/detay puanları gerçek ortalamadan)
        var puanlar = await reviewStats.GetStatsAsync(platform.Id, new[] { urun.Code }, ct);
        var puanIstatistik = puanlar.TryGetValue(urun.Code, out var pi) ? pi : null;
        IReadOnlyList<YorumVm>? yorumlarVm = null;
        if (puanIstatistik is not null)
        {
            var yorumSonucu = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetProductReviews.GetProductReviewsQuery(
                    platform.Id, urun.Code, 1, 10), ct);
            if (yorumSonucu.IsSuccess)
                yorumlarVm = yorumSonucu.Value!.Items
                    .Select(y => new YorumVm(y.Rating, y.Text, y.MemberName,
                        y.CreatedAt.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))))
                    .ToList();
        }

        var vm = new UrunDetayVm(
            Kod: urun.Code,
            Ad: TrAd(urun.NameI18n),
            Fiyat: fiyat,
            EskiFiyat: eskiFiyat,
            SeciliRenkAd: seciliRenk is { } sr
                ? gorunurRenkler.First(r => r.AttributeValueId == sr).Ad
                : null,
            Gorseller: gorseller,
            Renkler: gorunurRenkler
                .Select(r => new RenkSecenekVm(
                    r.AttributeValueId, r.Ad, renkGorselleri[r.AttributeValueId],
                    r.AttributeValueId == seciliRenk))
                .ToList(),
            BedenEtiketi: bedenTip is null ? "Beden" : TrAd(bedenTip.AttributeTypeNameI18n),
            Bedenler: bedenler,
            TekVaryantId: bedenler.Count == 0 && urunSatilabilir ? havuz[0].Id : null,
            TekVaryantFiyat: bedenler.Count == 0 ? havuz[0].PlatformPrice ?? havuz[0].BasePrice : null,
            StoksuzTekVaryantId: bedenler.Count == 0 && !urunSatilabilir ? havuz[0].Id : null,
            Ozellikler: ozellikler,
            Aciklama: urun.DescriptionI18n is { } uzun ? TrAd(uzun) : null,
            KisaAciklama: urun.ShortDescriptionI18n is { } kisa ? TrAd(kisa) : null,
            Breadcrumb: breadcrumb,
            FirmPlatformId: platform.Id,
            ParaBirimi: "TRY",
            Puan: puanIstatistik?.Average ?? 0,
            PuanSayisi: puanIstatistik?.Count ?? 0,
            Yorumlar: yorumlarVm,
            Videolar: urun.Videos?.Select(v => new UrunVideoVm(v.VideoUrl, v.ThumbnailUrl)).ToList());

        // E12: üyenin gezme kaydı (Önceden Gezdiklerim) — render'ı aksatmaz;
        // misafir gezmeleri detay script'indeki localStorage fallback'ine düşer.
        if (ViewData["MsUye"] is ECSPros.Api.Services.StoreUyeKimlik uye)
        {
            try
            {
                await mediator.Send(new ECSPros.Storefront.Application.Commands.RecordProductView
                    .RecordProductViewCommand(platform.Id, uye.MemberId, urun.Code), ct);
            }
            catch { /* gezme kaydı sayfayı düşürmez */ }
        }

        ViewData["MsUrunDetay"] = vm;
        ViewData["Title"] = vm.Ad;
        return View("~/Views/UrunDetay/Index.cshtml");
    }

    // Konfeksiyon beden sıralaması; numerik bedenler (36, 38...) sayısal sıralanır,
    // bilinmeyenler listenin sonunda ada göre sıralanır.
    private static readonly string[] BedenSira =
        ["4XS", "3XS", "2XS", "XXS", "XS", "S", "S-M", "M", "M-L", "L", "L-XL",
         "XL", "XXL", "2XL", "3XL", "4XL", "5XL", "6XL", "7XL", "8XL"];

    private static int BedenSirasi(string ad)
    {
        var index = Array.FindIndex(BedenSira, b => b.Equals(ad.Trim(), StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            return index;
        if (decimal.TryParse(ad, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var sayi))
            return 100 + (int)sayi;
        return 10_000;
    }

    // Satışa kapalı/erişilemeyen ürün için 301: ürünün (satış durumundan bağımsız) kanal
    // kategorisine, tespit edilemezse ana sayfaya. 404'ten kaçınma (kullanıcı kararı).
    private async Task<IActionResult> KapaliUrunYonlendir(string code, Guid platformId, CancellationToken ct)
    {
        var idSonuc = await mediator.Send(new GetProductIdByCodeQuery(code), ct);
        if (idSonuc.IsSuccess && idSonuc.Value is { } urunId)
        {
            var zincir = await mediator.Send(new GetProductChannelCategoryChainQuery(platformId, urunId), ct);
            if (zincir.IsSuccess && zincir.Value!.Count > 0)
                return RedirectPermanent("/" + zincir.Value[^1].Slug);
        }
        return RedirectPermanent("/");
    }

    private static bool VaryantRenktenMi(StoreVariantDto varyant, string renkTipKodu, Guid valueId) =>
        varyant.Attributes.Any(a => a.AttributeTypeCode == renkTipKodu && a.AttributeValueId == valueId);

    private static string TrAd(Dictionary<string, string> i18n) =>
        i18n.TryGetValue("tr", out var ad) ? ad : i18n.Values.FirstOrDefault() ?? "";
}
