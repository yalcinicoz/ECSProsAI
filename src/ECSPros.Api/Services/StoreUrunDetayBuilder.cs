using ECSPros.Api.Models.Store;
using ECSPros.Catalog.Application.Queries.GetStoreProductDetail;
using ECSPros.Storefront.Application.Queries.GetChannelVariantSlugs;
using ECSPros.Storefront.Application.Queries.GetProductChannelCategoryChain;
using MediatR;

namespace ECSPros.Api.Services;

/// <summary>
/// Ürün detay ViewModel'ini kuran paylaşılan servis. Hem /urun/{code} (UrunDetayController)
/// hem gerçek slug URL'i (UrunListesiController /{slug}) aynı render'ı kullansın diye
/// controller'dan çıkarıldı (2026-07-15 URL aktarımı). Bulunamayan/kapalı ürün → null;
/// çağıran taraf 301 yönlendirme/404 kararını verir. Slug URL'de kalır (in-place render).
/// </summary>
public class StoreUrunDetayBuilder(
    IMediator mediator,
    ECSPros.Shared.Contracts.IStockService stockService,
    ECSPros.Shared.Contracts.IProductReviewStatsService reviewStats)
{
    public async Task<UrunDetayVm?> BuildAsync(
        string code, string? color, Guid platformId, StoreUyeKimlik? uye, CancellationToken ct)
    {
        var sonuc = await mediator.Send(new GetStoreProductDetailQuery(code, platformId), ct);
        if (sonuc.IsFailure)
            return null;

        var urun = sonuc.Value!;
        var varyantlar = urun.Variants.Where(v => v.IsActive).ToList();
        if (varyantlar.Count == 0)
            return null;

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

        // URL aktarımı 2b: renk butonları o rengin kendi gerçek slug'ına link versin (adres
        // çubuğu tıklanan rengin URL'ine dönsün, ?color YOK). Varyant→slug haritası (bu platform).
        var slugMap = renkTipKodu is not null && varyantlar.Count > 0
            ? (await mediator.Send(new GetChannelVariantSlugsQuery(
                    platformId, varyantlar.Select(v => v.Id).ToList()), ct)).Value ?? new()
            : new Dictionary<Guid, string>();
        string? RenkSlug(Guid colorValueId) => varyantlar
            .Where(v => VaryantRenktenMi(v, renkTipKodu!, colorValueId))
            .Select(v => slugMap.GetValueOrDefault(v.Id))
            .FirstOrDefault(s => s != null);

        // 2026-07-30: tüm varyant stoğu bir kez hesaplanır — varsayılan renk seçiminde STOKLU
        // rengi öncelemek + beden listesi için (aşağıda). Arama/kategori kartı /urun/{code}'a
        // RENKSİZ link verince eskiden ilk görünür renk (ör. stoksuz Siyah) açılıyordu; oysa
        // yalnız Koyu Gri stoktaydı. Artık renk belirtilmemişse ilk STOKLU görünür renk seçilir.
        var tumStoklar = new Dictionary<Guid, int>();
        foreach (var v in varyantlar)
            tumStoklar[v.Id] = await stockService.GetAvailableStockAsync(v.Id, null, ct);
        bool RenkStoklu(Guid colorValueId) => renkTipKodu is not null && varyantlar
            .Any(v => VaryantRenktenMi(v, renkTipKodu, colorValueId) && tumStoklar.GetValueOrDefault(v.Id) > 0);

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
        // Renk belirtilmemişse: ilk STOKLU görünür renk; hiç stoklu renk yoksa ilk görünür renk.
        seciliRenk ??= gorunurRenkler
            .Select(r => (Guid?)r.AttributeValueId)
            .FirstOrDefault(id => RenkStoklu(id!.Value))
            ?? (gorunurRenkler.Count > 0 ? gorunurRenkler[0].AttributeValueId : null);

        var havuz = seciliRenk is { } renk
            ? varyantlar.Where(v => VaryantRenktenMi(v, renkTipKodu!, renk)).ToList()
            : varyantlar;
        if (havuz.Count == 0)
            havuz = varyantlar;

        var gorseller = (havuz.FirstOrDefault(v => v.Images.Count > 0)
                         ?? varyantlar.FirstOrDefault(v => v.Images.Count > 0))
            ?.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).Distinct().ToList() ?? [];

        var bedenTip = havuz
            .SelectMany(v => v.Attributes)
            .FirstOrDefault(a => !a.IsColor
                                 && a.AttributeTypeCode != renkTipKodu
                                 && a.AttributeTypeCode is not ("renk" or "filtre_rengi"));

        // Stok yukarıda tüm varyantlar için hesaplandı (tumStoklar) — havuz için tekrar sorma.
        bool Satilabilir(Guid variantId) => tumStoklar.GetValueOrDefault(variantId) > 0;

        // Fiyat sıfır düzeltmesi (2026-07-16): kanal kaydı olmayan / kanal fiyatı 0 olan
        // varyantlarda PlatformPrice ?? BasePrice 0 dönebiliyordu ve sepete 0 TL gidiyordu.
        // Varyant fiyatı çözümü: kanal fiyatı → varyant fiyatı → havuzun en düşük pozitif
        // fiyatı → ürün taban fiyatı.
        var enDusukPozitif = havuz
            .SelectMany(v => new[] { v.PlatformPrice ?? 0m, v.BasePrice })
            .Where(f => f > 0)
            .DefaultIfEmpty(0m)
            .Min();
        decimal VaryantFiyati(decimal? platform, decimal taban) =>
            platform is > 0 ? platform.Value : (taban > 0 ? taban : enDusukPozitif);

        var bedenler = new List<BedenSecenekVm>();
        if (bedenTip is not null)
        {
            bedenler = havuz
                .Select(v => (Varyant: v, Beden: v.Attributes
                    .FirstOrDefault(a => a.AttributeTypeCode == bedenTip.AttributeTypeCode)))
                .Where(x => x.Beden is not null)
                .DistinctBy(x => x.Beden!.AttributeValueId)
                .Select(x => (Vm: new BedenSecenekVm(
                    TrAd(x.Beden!.AttributeValueNameI18n),
                    x.Varyant.Id,
                    VaryantFiyati(x.Varyant.PlatformPrice, x.Varyant.BasePrice),
                    Satilabilir(x.Varyant.Id)), x.Beden!.ValueSortOrder))
                // 2026-07-22: beden sırası artık DEĞER HAVUZUNDAKİ SortOrder'dan (tüm 403 beden
                // gerçek-hayat sırasıyla numaralandı); 0 kalan eski kayıt için sezgisel yedek.
                .OrderBy(x => x.ValueSortOrder > 0 ? 0 : 1)
                .ThenBy(x => x.ValueSortOrder)
                .ThenBy(x => BedenSirasi(x.Vm.Ad))
                .ThenBy(x => x.Vm.Ad, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
                .Select(x => x.Vm)
                .ToList();
        }

        var fiyatliVaryant = havuz
            .Where(v => (v.PlatformPrice ?? v.BasePrice) > 0)
            .OrderBy(v => v.PlatformPrice ?? v.BasePrice)
            .FirstOrDefault() ?? havuz[0];
        var fiyat = fiyatliVaryant.PlatformPrice ?? fiyatliVaryant.BasePrice;
        if (fiyat <= 0) fiyat = enDusukPozitif;
        var eskiFiyat = fiyatliVaryant.CompareAtPrice is { } eski && eski > fiyat ? eski : (decimal?)null;

        var zincir = await mediator.Send(
            new GetProductChannelCategoryChainQuery(platformId, urun.Id), ct);
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
        var urunSatilabilir = bedenler.Count > 0
            ? bedenler.Any(x => x.Satilabilir)
            : Satilabilir(havuz[0].Id);
        ozellikler.Add(new OzellikVm("Stok Durumu", urunSatilabilir ? "Stokta" : "Tükendi"));

        var puanlar = await reviewStats.GetStatsAsync(platformId, new[] { urun.Code }, ct);
        var puanIstatistik = puanlar.TryGetValue(urun.Code, out var pi) ? pi : null;
        IReadOnlyList<YorumVm>? yorumlarVm = null;
        IReadOnlyDictionary<int, int>? puanDagilimi = null;
        if (puanIstatistik is not null)
        {
            // İP-2.1: yıldız tooltip'inin 5→1 dağılımı (değerlendirmeler sayfasıyla aynı sorgu)
            var ozetSonucu = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetProductReviewSummary.GetProductReviewSummaryQuery(
                    platformId, urun.Code), ct);
            if (ozetSonucu.IsSuccess)
                puanDagilimi = ozetSonucu.Value!.RatingCounts;

            var yorumSonucu = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetProductReviews.GetProductReviewsQuery(
                    platformId, urun.Code, 1, 10), ct);
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
                    r.AttributeValueId == seciliRenk,
                    RenkSlug(r.AttributeValueId)))   // 2b: rengin kendi slug'ı
                .ToList(),
            BedenEtiketi: bedenTip is null ? "Beden" : TrAd(bedenTip.AttributeTypeNameI18n),
            Bedenler: bedenler,
            TekVaryantId: bedenler.Count == 0 && urunSatilabilir ? havuz[0].Id : null,
            TekVaryantFiyat: bedenler.Count == 0 ? VaryantFiyati(havuz[0].PlatformPrice, havuz[0].BasePrice) : null,
            StoksuzTekVaryantId: bedenler.Count == 0 && !urunSatilabilir ? havuz[0].Id : null,
            Ozellikler: ozellikler,
            Aciklama: urun.DescriptionI18n is { } uzun ? TrAd(uzun) : null,
            KisaAciklama: urun.ShortDescriptionI18n is { } kisa ? TrAd(kisa) : null,
            Breadcrumb: breadcrumb,
            FirmPlatformId: platformId,
            ParaBirimi: "TRY",
            Puan: puanIstatistik?.Average ?? 0,
            PuanSayisi: puanIstatistik?.Count ?? 0,
            Yorumlar: yorumlarVm,
            Videolar: urun.Videos?.Select(v => new UrunVideoVm(v.VideoUrl, v.ThumbnailUrl)).ToList(),
            PuanDagilimi: puanDagilimi);

        // E12: üyenin gezme kaydı — render'ı aksatmaz.
        if (uye is not null)
        {
            try
            {
                await mediator.Send(new ECSPros.Storefront.Application.Commands.RecordProductView
                    .RecordProductViewCommand(platformId, uye.MemberId, urun.Code), ct);
            }
            catch { /* gezme kaydı sayfayı düşürmez */ }
        }

        return vm;
    }

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

    private static bool VaryantRenktenMi(StoreVariantDto varyant, string renkTipKodu, Guid valueId) =>
        varyant.Attributes.Any(a => a.AttributeTypeCode == renkTipKodu && a.AttributeValueId == valueId);

    private static string TrAd(Dictionary<string, string> i18n) =>
        i18n.TryGetValue("tr", out var ad) ? ad : i18n.Values.FirstOrDefault() ?? "";
}
