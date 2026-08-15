using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Catalog.Application.Queries.FilterSimilarProducts;
using ECSPros.Catalog.Application.Queries.GetStoreFacets;
using ECSPros.Catalog.Application.Queries.GetStoreProducts;
using ECSPros.Integration.Application.Queries.ResolveErpProductRefs;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryFacets;
using ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;
using ECSPros.Storefront.Application.Queries.GetProductByChannelSlug;
using ECSPros.Storefront.Application.Queries.GetProductsLeafChannelCategories;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Ürün listesi sayfaları (B7): kategori (/{slug}), arama (/urunler?search=) ve
/// tüm ürünler (/urun-listesi). İlk sayfa + facet'ler sunucudan (MediatR süreç içi,
/// plan 3.4); devam sayfaları partial config script'i üzerinden api/store/* JSON.
/// </summary>
public class UrunListesiController(IMediator mediator, IStoreContext storeContext, StoreUrunDetayBuilder detayBuilder, UrunKategoriHaritasi kategoriHaritasi) : StorePageController
{
    private const int SayfaBoyu = 24;

    // B10: sayfa query parametreleri api/store parametreleriyle AYNI adları kullanır
    // (attrs=virgüllü valueId, priceMin, priceMax, sort, search) — DevamApiUrl'e birebir taşınır.
    public sealed record ListeFiltre(string? Attrs, decimal? PriceMin, decimal? PriceMax, string? Sort)
    {
        public List<Guid>? DegerIdler =>
            string.IsNullOrWhiteSpace(Attrs)
                ? null
                : Attrs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                    .Where(g => g != Guid.Empty)
                    .ToList() is { Count: > 0 } ids ? ids : null;

        public string QueryEki()
        {
            var parcalar = new List<string>();
            if (DegerIdler is { } ids) parcalar.Add("attrs=" + string.Join(",", ids));
            if (PriceMin.HasValue) parcalar.Add("priceMin=" + PriceMin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (PriceMax.HasValue) parcalar.Add("priceMax=" + PriceMax.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(Sort)) parcalar.Add("sort=" + Uri.EscapeDataString(Sort));
            return parcalar.Count > 0 ? "&" + string.Join("&", parcalar) : "";
        }
    }

    // 2026-07-17: ?page=N — paylaşılan/izlenen sayfa numarası; SSR o sayfadan başlar.
    private static int SayfaNo(int? page) => Math.Clamp(page ?? 1, 1, 10000);

    [HttpGet("/urun-listesi")]
    public Task<IActionResult> Index([FromQuery] ListeFiltre filtre, [FromQuery] int? page, CancellationToken ct)
        => GenelListeAsync(null, filtre, SayfaNo(page), ct);

    [HttpGet("/urunler")]
    public Task<IActionResult> Arama([FromQuery] string? search, [FromQuery] string? codes, [FromQuery] ListeFiltre filtre, [FromQuery] int? page, CancellationToken ct)
        => !string.IsNullOrWhiteSpace(codes)
            ? GorselAramaSonucListesiAsync(codes, filtre, ct)
            : GenelListeAsync(string.IsNullOrWhiteSpace(search) ? null : search.Trim(), filtre, SayfaNo(page), ct);

    /// <summary>2026-08-03: görsel arama sonuç sayfası — dropdown'daki "Tümünü Gör"/Enter,
    /// eşleşen ürün KODLARINI ?codes= ile taşır; standart liste sayfası o kodlarla, görsel
    /// aramanın benzerlik sırası korunarak render edilir. 2026-08-15: filtre/facet'ler bu
    /// kod kümesinden üretilir (attrs/priceMin/priceMax/sort — SSR yeniden yükleme).</summary>
    private async Task<IActionResult> GorselAramaSonucListesiAsync(string codes, ListeFiltre filtre, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound();

        var kodListesi = codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();
        if (kodListesi.Count == 0)
            return NotFound();

        var vm = await KodListesiVmAsync(platform, kodListesi, filtre,
            "Görsel Arama Sonuçları", "Görsel aramayla eşleşen ürün bulunamadı.", ct);
        return vm is null ? NotFound() : ListeGoster(vm);
    }

    /// <summary>Sabit kod listesiyle liste sayfası (görsel arama sonucu + benzer ürünler):
    /// kartlar kod sırasında (benzerlik sırası) — sıralama seçildiyse sorgu sırası; filtre
    /// grupları + fiyat aralığı yalnız bu kod kümesinden hesaplanır (seçim-duyarlı).
    /// Kategori grubu: ürün→yaprak kategori haritasıyla (bkz. KategoriGrubu) — tüm liste
    /// sayfalarıyla aynı mekanizma (2026-08-15).</summary>
    private async Task<UrunListesiVm?> KodListesiVmAsync(
        StorePlatformBilgisi platform, List<string> kodListesi, ListeFiltre filtre,
        string baslik, string bosMesaj, CancellationToken ct)
    {
        var kartlar = new List<UrunKartVm>();
        StoreFacetsDto? facetDto = null;
        var harita = await kategoriHaritasi.GetAsync(platform.Id, ct);
        var (seciliKategoriler, seciliOzellikler) = harita?.Ayir(filtre.DegerIdler) ?? ([], filtre.DegerIdler);
        if (kodListesi.Count > 0)
        {
            var sayfaBoyu = Math.Max(SayfaBoyu, kodListesi.Count);
            var urunler = await mediator.Send(new GetStoreProductsQuery(
                platform.Id, null, 1, sayfaBoyu,
                seciliOzellikler, filtre.PriceMin, filtre.PriceMax, filtre.Sort,
                ProductCodes: kodListesi, ProductIds: harita?.UrunIdleri(seciliKategoriler)), ct);
            if (urunler.IsFailure)
                return null;

            var kartSirasi = urunler.Value!.Items.AsEnumerable();
            if (string.IsNullOrEmpty(filtre.Sort))
            {
                // Benzerlik sırası korunur (sorgu kod sırasına göre dönmez)
                var sira = kodListesi.Select((k, i) => (k, i))
                    .ToDictionary(x => x.k, x => x.i, StringComparer.OrdinalIgnoreCase);
                kartSirasi = kartSirasi.OrderBy(u => sira.GetValueOrDefault(u.Code, int.MaxValue));
            }
            kartlar = kartSirasi.Select(KartaCevir).ToList();

            var facets = await mediator.Send(new GetStoreFacetsQuery(
                platform.Id, null, platform.StokBitenGoster, platform.StokBitenGosterTarih,
                seciliOzellikler, filtre.PriceMin, filtre.PriceMax,
                ProductCodes: kodListesi,
                ProductCategoryMap: harita?.UrunKategori, SelectedCategoryIds: seciliKategoriler), ct);
            if (facets.IsSuccess) facetDto = facets.Value;
        }

        return new UrunListesiVm(
            Baslik: baslik,
            ToplamUrun: kartlar.Count,
            SayfaBoyu: Math.Max(SayfaBoyu, Math.Max(1, kartlar.Count)),
            IlkSayfa: kartlar,
            DevamApiUrl: $"/api/store/catalog/products?firmPlatformId={platform.Id}&pageSize={SayfaBoyu}",
            FiltreGruplari: FiltreGruplariKur(facetDto, harita, seciliKategoriler),
            FiyatMin: facetDto?.PriceMin ?? 0,
            FiyatMax: facetDto?.PriceMax ?? 0,
            KategoriSecenekleri: [],
            SeciliDegerler: filtre.DegerIdler,
            SeciliFiyatMin: filtre.PriceMin,
            SeciliFiyatMax: filtre.PriceMax,
            SeciliSiralama: filtre.Sort,
            BosDurumMesaji: kartlar.Count > 0 ? null : bosMesaj);
    }

    /// <summary>Filtre grupları = [Kategori sanal grubu] + özellik facet'leri (2026-08-15).
    /// Kategori grubu ürün→yaprak kategori haritasından, facet'in CategoryCounts sayımıyla;
    /// tek seçenekli grup kuralı: ≥2 seçenek ya da seçili değer içeriyorsa gösterilir.
    /// Menü kökü/çocuk kategorileri artık filtre bloğuna girmez (KategoriSecenekleri boş).</summary>
    private static List<FiltreGrupVm> FiltreGruplariKur(
        StoreFacetsDto? facets, UrunKategoriHaritasi.Harita? harita, IReadOnlyCollection<Guid> seciliKategoriler)
    {
        var gruplar = FacetleriCevir(facets);
        if (harita is null || facets?.CategoryCounts is not { } sayim)
            return gruplar;
        var degerler = sayim
            .Where(kv => kv.Value > 0 && harita.Kategoriler.ContainsKey(kv.Key))
            .Select(kv => new FiltreDegerVm(kv.Key, harita.Kategoriler[kv.Key].Ad, null, kv.Value))
            .OrderByDescending(v => v.UrunSayisi).ThenBy(v => v.Ad, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
            .ToList();
        // Seçili ama (diğer filtrelerle) sayımı 0'a düşen kategori panelde kalsın (kaldırılabilsin)
        foreach (var sk in seciliKategoriler)
            if (degerler.All(v => v.ValueId != sk) && harita.Kategoriler.TryGetValue(sk, out var kb))
                degerler.Add(new FiltreDegerVm(sk, kb.Ad, null, 0));
        if (degerler.Count >= 2 || degerler.Any(v => seciliKategoriler.Contains(v.ValueId)))
            gruplar.Insert(0, new FiltreGrupVm("kategori", "Kategori", false, degerler));
        return gruplar;
    }

    /// <summary>Benzer ürünler (2026-08-14): kart ikonundan gelinir. Kaynak ürünün İLK
    /// görseli CDN'den okunup görsel arama servisine gönderilir; dönen adaylar AYNI ürün
    /// grubu + AYNI cinsiyet kuralıyla süzülür ve görsel arama sonuç sayfası kalıbıyla
    /// benzerlik sırasında listelenir. Görsel arama servisi kendi sunucumuz (search.misharitalia.com) — gorsel-arama ile aynı IP limiti (yük freni).</summary>
    [HttpGet("/benzer/{kod}")]
    [EnableRateLimiting("store-sensitive")]
    public async Task<IActionResult> BenzerUrunler(
        string kod,
        [FromServices] IVisualSearchSettingsProvider gorselAramaAyarlari,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] ILogger<UrunListesiController> logger,
        [FromServices] IMemoryCache cache,
        [FromQuery] ListeFiltre filtre,
        CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound();

        // Arama motoru/sosyal tarayıcılar için sayfa indekslenmez ve linkleri izlenmez.
        // (2026-08-15: Meta crawler bir gecede ~8.700 /benzer isteğiyle kendi görsel arama sunucumuzu
        // yordu → servis 500 döndü, sayfa yalnız kaynak ürünle kaldı.)
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        var userAgent = Request.Headers.UserAgent.ToString();
        var tarayiciBot = BotUserAgentMi(userAgent);

        // Kaynak ürünün kartı — ilk görsel URL'i buradan (kartta gösterilen ana görsel)
        var kaynakSonuc = await mediator.Send(new GetStoreProductsQuery(
            platform.Id, null, 1, 1, ProductCodes: [kod]), ct);
        var kaynakUrun = kaynakSonuc.IsSuccess ? kaynakSonuc.Value!.Items.FirstOrDefault() : null;
        if (kaynakUrun?.MainImageUrl is not { Length: > 0 } gorselUrl)
            return NotFound();

        // Bot: görsel arama servisi çağrılmaz, yalnız kaynak ürün render edilir (sayfa yine 200).
        if (tarayiciBot)
            return ListeGoster(await KodListesiVmAsync(platform, [kod], filtre, "Benzer Ürünler", "Bu ürüne benzer ürün bulunamadı.", ct)
                ?? BenzerVm([], null));

        // Sonuç kod listesi platform+ürün başına 12 saat önbellekte — aynı ürüne art arda
        // gelen tıklamalar (ve tarayıcı yenilemeleri) servisi tekrar ücretlendirmez.
        var cacheAnahtari = $"benzer:{platform.Id}:{kod.ToUpperInvariant()}";
        var kodListesi = new List<string>();
        var servisHatasi = false;
        if (cache.TryGetValue(cacheAnahtari, out List<string>? onbellek) && onbellek is not null)
        {
            kodListesi = onbellek;
        }
        else
        {
        var ayarlar = await gorselAramaAyarlari.GetAsync(platform.Id, ct);
        if (ayarlar is not null)
        {
            try
            {
                var http = httpClientFactory.CreateClient("visual-search");

                // 1) İlk görseli CDN'den oku, 2) görsel arama servisine ilet (gorsel-arama sözleşmesi)
                using var gorselCevap = await http.GetAsync(gorselUrl, ct);
                gorselCevap.EnsureSuccessStatusCode();
                var gorselBaytlar = await gorselCevap.Content.ReadAsByteArrayAsync(ct);

                using var form = new MultipartFormDataContent();
                using var dosyaIcerigi = new ByteArrayContent(gorselBaytlar);
                dosyaIcerigi.Headers.ContentType = gorselCevap.Content.Headers.ContentType
                    ?? new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
                form.Add(dosyaIcerigi, "file", "benzer" + Path.GetExtension(new Uri(gorselUrl).AbsolutePath));

                using var istek = new HttpRequestMessage(HttpMethod.Post, ayarlar.ApiUrl) { Content = form };
                istek.Headers.Add("X-API-Key", ayarlar.ApiKey);
                using var cevap = await http.SendAsync(istek, ct);
                var cevapMetni = await cevap.Content.ReadAsStringAsync(ct);
                if (!cevap.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Görsel arama servisi {(int)cevap.StatusCode} döndü.");

                // 3) results[].urunId (legacy ERP id) → modelCode (erp_variant_data)
                using var belge = System.Text.Json.JsonDocument.Parse(cevapMetni);
                var erpIdler = new List<int>();
                if (belge.RootElement.TryGetProperty("results", out var sonuclar)
                    && sonuclar.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var s in sonuclar.EnumerateArray())
                    {
                        if (s.TryGetProperty("urunId", out var uid) && uid.TryGetInt32(out var id)
                            && id > 0 && !erpIdler.Contains(id))
                            erpIdler.Add(id);
                    }
                }

                if (erpIdler.Count > 0)
                {
                    var refSonuc = await mediator.Send(new ResolveErpProductRefsQuery(erpIdler), ct);
                    if (refSonuc.IsSuccess)
                    {
                        var kodByErpId = refSonuc.Value!
                            .GroupBy(r => r.ErpProductId)
                            .ToDictionary(g => g.Key, g => g.First().ModelCode);
                        kodListesi = erpIdler
                            .Select(id => kodByErpId.GetValueOrDefault(id))
                            .Where(k => !string.IsNullOrWhiteSpace(k))
                            .Select(k => k!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Take(100)
                            .ToList();
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Servis hatası sayfayı düşürmez — "şu anda getirilemiyor" boş durumu gösterilir
                servisHatasi = true;
                logger.LogWarning(ex, "Benzer ürünler görsel araması başarısız (kod: {Kod}).", kod);
            }
        }

        // 4) Aynı ürün grubu + aynı cinsiyet süzgeci (kaynak ürün elenmiş, sıra korunmuş döner)
        if (kodListesi.Count > 0)
        {
            var filtreli = await mediator.Send(new FilterSimilarProductCodesQuery(kod, kodListesi), ct);
            if (filtreli.IsSuccess)
                kodListesi = filtreli.Value!;
        }

        // Yalnız BAŞARILI servis sonucu önbelleğe alınır (hata/boş sonuç tekrar denenir)
        if (!servisHatasi && kodListesi.Count > 0)
            cache.Set(cacheAnahtari, kodListesi, TimeSpan.FromHours(12));
        }

        // Servis hatası: kaynak ürünü tek başına gösterip "benzerler bunlar" izlenimi
        // vermek yerine dürüst boş durum
        if (servisHatasi)
            return ListeGoster(BenzerVm([], "Benzer ürünler şu anda getirilemiyor, lütfen biraz sonra tekrar deneyin."));

        // Kaynak ürün listenin BAŞINDA gösterilir (2026-08-15 kullanıcı kararı — önceki
        // "kaynak hariç" kurgusu revize edildi: ürünün kendisi/diğer renkleri de görünsün)
        kodListesi = kodListesi.Where(k => !k.Equals(kod, StringComparison.OrdinalIgnoreCase)).ToList();
        kodListesi.Insert(0, kod);

        // 5) Görsel arama sonuç sayfası kalıbıyla listele (benzerlik sırası + kod kümesi facet'leri)
        var vm = await KodListesiVmAsync(platform, kodListesi, filtre,
            "Benzer Ürünler", "Bu ürüne benzer ürün bulunamadı.", ct);
        return ListeGoster(vm ?? BenzerVm([], "Bu ürüne benzer ürün bulunamadı."));

        UrunListesiVm BenzerVm(List<UrunKartVm> kartListesi, string? bosMesaj)
        {
            var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
            return new UrunListesiVm(
                Baslik: "Benzer Ürünler",
                ToplamUrun: kartListesi.Count,
                SayfaBoyu: Math.Max(SayfaBoyu, Math.Max(1, kartListesi.Count)),
                IlkSayfa: kartListesi,
                DevamApiUrl: $"/api/store/catalog/products?firmPlatformId={platform.Id}&pageSize={SayfaBoyu}",
                FiltreGruplari: [],
                FiyatMin: 0,
                FiyatMax: 0,
                KategoriSecenekleri: nav.Kokler,
                BosDurumMesaji: bosMesaj);
        }
    }

    // Bilinen tarayıcı/bot UA imzaları — görsel arama servisi (kendi sunucumuz, ağır iş) botlara çalıştırılmaz.
    private static bool BotUserAgentMi(string ua)
    {
        if (string.IsNullOrEmpty(ua)) return true; // UA'sız istemci: gerçek tarayıcı değil
        foreach (var imza in BotImzalari)
            if (ua.Contains(imza, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static readonly string[] BotImzalari =
    [
        "bot", "crawler", "spider", "crawl", "slurp", "externalagent", "facebookexternalhit",
        "meta-external", "python-requests", "curl/", "wget/", "go-http-client", "okhttp",
        "headless", "preview", "fetcher", "scrapy", "ahrefs", "semrush", "mj12", "yandex",
        "bingpreview", "petalbot", "bytespider", "gptbot", "claudebot", "ccbot", "applebot",
    ];

    // Tek segmentli kategori sayfası. Literal route'lar (/urunler, /sepet...) ASP.NET
    // route önceliğiyle her zaman bundan önce eşleşir; slug kategori değilse 404.
    // Regex kısıtı şart: kısıtsız {slug}, /favicon.ico gibi kök statik dosyaları da
    // endpoint olarak eşleştirir ve StaticFileMiddleware devre dışı kalır (örtük
    // UseRouting pipeline'ın başında koşar).
    // İkinci alternatif: eski sistemden birebir taşınan ürün URL'leri nokta/virgül/
    // ünlem/iki nokta içerir ama İSTİSNASIZ "-rakam" ile biter — dosya adları
    // (favicon.ico, *.js) bu kalıba uymadığından statikler korunur.
    // \u002C=virgül (route parser'ın kısıt argümanını virgülde bölmemesi için escape).
    [HttpGet("/{slug:regex(^[[a-z0-9-]]+$|^[[a-z0-9.!:\\u002C-]]+-[[0-9]]+$)}")]
    public async Task<IActionResult> Kategori(
        string slug, [FromQuery] string? search, [FromQuery] ListeFiltre filtre, [FromQuery] int? page, CancellationToken ct)
    {
        var sayfa = SayfaNo(page);
        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
        // TumKokler: bos kategoriler menuden gizlense de dogrudan URL ile gelen
        // ziyaretci 404 degil "henuz urun yuklenmedi" sayfasi gormeli.
        var kategori = KategoriBul(nav.TumKokler, slug);
        if (kategori is null)
        {
            // Nav ağacında yok — menüye bağlı olmayan yayınlı kategori olabilir. Doğrudan
            // slug'dan çöz (2026-07-30 düzeltmesi: yayınlı her kategori URL'iyle açılmalı;
            // önceden yalnız menüdeki kategoriler açılıyordu, diğerleri 404 veriyordu).
            var platformNav = await storeContext.GetPlatformAsync(ct);
            if (platformNav is not null)
            {
                var slugKategori = await mediator.Send(
                    new ECSPros.Storefront.Application.Queries.GetChannelCategoryBySlug
                        .GetChannelCategoryBySlugQuery(platformNav.Id, slug), ct);
                if (slugKategori.IsSuccess && slugKategori.Value is { } sk)
                {
                    var ad = sk.NameI18n.TryGetValue("tr", out var trAd) ? trAd
                        : sk.NameI18n.Values.FirstOrDefault() ?? sk.Slug;
                    kategori = new NavKategori(sk.Id, ad, sk.Slug,
                        sk.DisplayImageUrl, sk.BadgeLabel, [], UrunVar: true);
                }
            }
            if (kategori is null)
                return await UrunSlugDeneVeyaNotFound(slug, ct);
        }

        // B10: nav arama paneli "kategoride ara" kapsam butonunu bu bağlamla gösterir
        ViewData["MsAktifKategori"] = kategori;

        var platform = await storeContext.GetPlatformAsync(ct);
        var arama = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        // 2026-08-15: Kategori filtresi = sayfadaki ürünlerin yaprak kategorileri (attrs içinde id)
        var harita = platform is null ? null : await kategoriHaritasi.GetAsync(platform.Id, ct);
        var (seciliKategoriler, seciliOzellikler) = harita?.Ayir(filtre.DegerIdler) ?? ([], filtre.DegerIdler);
        var urunler = await mediator.Send(new GetChannelCategoryProductsQuery(
            kategori.Id, sayfa, SayfaBoyu,
            arama, seciliOzellikler, filtre.PriceMin, filtre.PriceMax, filtre.Sort,
            platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih,
            RestrictProductIds: harita?.UrunIdleri(seciliKategoriler)), ct);
        if (urunler.IsFailure)
            return NotFound();

        var facets = await mediator.Send(new GetChannelCategoryFacetsQuery(
            kategori.Id, platform?.StokBitenGoster ?? false, platform?.StokBitenGosterTarih,
            // 2026-07-17: seçim-duyarlı facet — aktif filtre/fiyat/arama bağlamı
            seciliOzellikler, filtre.PriceMin, filtre.PriceMax, arama,
            ProductCategoryMap: harita?.UrunKategori, SelectedCategoryIds: seciliKategoriler), ct);

        var devamUrl = $"/api/store/catalog/channel-categories/{kategori.Id}/products?pageSize={SayfaBoyu}"
                       + (arama is null ? "" : "&search=" + Uri.EscapeDataString(arama))
                       + filtre.QueryEki();

        var vm = new UrunListesiVm(
            Baslik: kategori.Ad,
            ToplamUrun: urunler.Value!.TotalCount,
            SayfaBoyu: SayfaBoyu,
            IlkSayfa: urunler.Value.Items.Select(KartaCevir).ToList(),
            DevamApiUrl: devamUrl,
            FiltreGruplari: FiltreGruplariKur(facets.IsSuccess ? facets.Value : null, harita, seciliKategoriler),
            FiyatMin: facets.IsSuccess ? facets.Value!.PriceMin : 0,
            FiyatMax: facets.IsSuccess ? facets.Value!.PriceMax : 0,
            KategoriSecenekleri: [],   // 2026-08-15: menü çocukları değil — Kategori grubu FiltreGruplari'nda
            SeciliDegerler: filtre.DegerIdler,
            SeciliFiyatMin: filtre.PriceMin,
            SeciliFiyatMax: filtre.PriceMax,
            SeciliSiralama: filtre.Sort,
            KategorideArama: arama,
            BaslangicSayfa: sayfa,
            BosDurumMesaji: urunler.Value.TotalCount > 0 ? null
                : arama is not null
                    ? $"Bu kategoride \"{arama}\" aramasıyla eşleşen ürün bulunamadı."
                    : (filtre.DegerIdler is not null || filtre.PriceMin.HasValue || filtre.PriceMax.HasValue)
                        ? "Seçtiğiniz filtrelerle eşleşen ürün bulunamadı."
                        : "Bu kategoriye henüz ürün yüklenmedi.");

        return ListeGoster(vm);
    }

    private async Task<IActionResult> GenelListeAsync(string? arama, ListeFiltre filtre, int sayfa, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound();

        // 2026-08-15: Kategori filtresi = sonuçtaki ürünlerin yaprak kategorileri (attrs içinde id)
        var harita = await kategoriHaritasi.GetAsync(platform.Id, ct);
        var (seciliKategoriler, seciliOzellikler) = harita?.Ayir(filtre.DegerIdler) ?? ([], filtre.DegerIdler);
        var urunler = await mediator.Send(new GetStoreProductsQuery(
            platform.Id, arama, sayfa, SayfaBoyu,
            seciliOzellikler, filtre.PriceMin, filtre.PriceMax, filtre.Sort,
            ProductIds: harita?.UrunIdleri(seciliKategoriler),
            ApplyStockFilter: true, ShowOutOfStock: platform.StokBitenGoster, OutOfStockSince: platform.StokBitenGosterTarih), ct);
        if (urunler.IsFailure)
            return NotFound();

        var facets = await mediator.Send(new GetStoreFacetsQuery(
            platform.Id, arama, platform.StokBitenGoster, platform.StokBitenGosterTarih,
            // 2026-07-17: seçim-duyarlı facet — aktif filtre/fiyat bağlamı
            seciliOzellikler, filtre.PriceMin, filtre.PriceMax,
            ProductCategoryMap: harita?.UrunKategori, SelectedCategoryIds: seciliKategoriler), ct);

        var nav = ViewData["MsNavigasyon"] as NavigasyonVm ?? NavigasyonVm.Bos;
        var devamUrl = $"/api/store/catalog/products?firmPlatformId={platform.Id}&pageSize={SayfaBoyu}"
                       + (arama is null ? "" : "&search=" + Uri.EscapeDataString(arama))
                       + filtre.QueryEki();

        var vm = new UrunListesiVm(
            Baslik: arama is null ? "Tüm Ürünler" : $"\"{arama}\" araması",
            ToplamUrun: urunler.Value!.TotalCount,
            SayfaBoyu: SayfaBoyu,
            IlkSayfa: urunler.Value.Items.Select(KartaCevir).ToList(),
            DevamApiUrl: devamUrl,
            FiltreGruplari: FiltreGruplariKur(facets.IsSuccess ? facets.Value : null, harita, seciliKategoriler),
            FiyatMin: facets.IsSuccess ? facets.Value!.PriceMin : 0,
            FiyatMax: facets.IsSuccess ? facets.Value!.PriceMax : 0,
            KategoriSecenekleri: [],   // 2026-08-15: menü kökleri değil — Kategori grubu FiltreGruplari'nda
            SeciliDegerler: filtre.DegerIdler,
            SeciliFiyatMin: filtre.PriceMin,
            SeciliFiyatMax: filtre.PriceMax,
            SeciliSiralama: filtre.Sort,
            BaslangicSayfa: sayfa,
            BosDurumMesaji: urunler.Value.TotalCount > 0 ? null
                : arama is not null
                    ? $"\"{arama}\" aramasıyla eşleşen ürün bulunamadı."
                    : "Gösterilecek ürün bulunamadı.");

        return ListeGoster(vm);
    }

    private IActionResult ListeGoster(UrunListesiVm vm)
    {
        ViewData["MsUrunListesi"] = vm;
        ViewData["Title"] = vm.Baslik;
        return View("~/Views/UrunListesi/Index.cshtml");
    }

    // Kök /{slug} kategori değilse: gerçek (legacy) ÜRÜN URL'i mi? (URL aktarımı 2026-07-15)
    // Bulunursa ürün detayı slug URL'inde in-place render edilir (301 yok). Slug bir ürüne
    // ait ama ürün satışa KAPALI/erişilemezse: 404 yerine ürünün kategorisine 301 (kullanıcı
    // kararı — /urun/{code} ile tutarlı). Slug hiçbir ürüne ait değilse gerçek 404.
    private async Task<IActionResult> UrunSlugDeneVeyaNotFound(string slug, CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is null)
            return NotFound();

        var sonuc = await mediator.Send(new GetProductByChannelSlugQuery(platform.Id, slug), ct);
        if (sonuc.IsFailure || sonuc.Value is not { } bulunan)
            return NotFound();   // slug bir ürüne ait değil — bilinmeyen URL

        var vm = await detayBuilder.BuildAsync(
            bulunan.ProductCode, bulunan.ColorValueId?.ToString(),
            platform.Id, ViewData["MsUye"] as StoreUyeKimlik, ct);
        if (vm is not null)
        {
            ViewData["MsUrunDetay"] = vm;
            ViewData["Title"] = vm.Ad;
            return View("~/Views/UrunDetay/Index.cshtml");
        }

        // Slug bir ürüne ait ama render edilemedi (satışa kapalı) → kategorisine 301.
        return await KapaliUrunKategoriyeYonlendir(bulunan.ProductCode, platform.Id, ct);
    }

    // /urun/{code}'daki KapaliUrunYonlendir ile aynı: kapalı/erişilemez ürünü kategorisine
    // (yoksa ana sayfaya) 301'ler — 404'ten kaçınma (kullanıcı kararı).
    private async Task<IActionResult> KapaliUrunKategoriyeYonlendir(string code, Guid platformId, CancellationToken ct)
    {
        var idSonuc = await mediator.Send(
            new ECSPros.Catalog.Application.Queries.GetProductIdByCode.GetProductIdByCodeQuery(code), ct);
        if (idSonuc.IsSuccess && idSonuc.Value is { } urunId)
        {
            var zincir = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetProductChannelCategoryChain
                    .GetProductChannelCategoryChainQuery(platformId, urunId), ct);
            if (zincir.IsSuccess && zincir.Value!.Count > 0)
                return RedirectPermanent("/" + zincir.Value[^1].Slug);
        }
        return RedirectPermanent("/");
    }

    private static NavKategori? KategoriBul(IReadOnlyList<NavKategori> dallar, string slug)
    {
        foreach (var dal in dallar)
        {
            if (string.Equals(dal.Slug, slug, StringComparison.OrdinalIgnoreCase))
                return dal;
            if (KategoriBul(dal.Cocuklar, slug) is { } bulunan)
                return bulunan;
        }
        return null;
    }

    private static UrunKartVm KartaCevir(ChannelCategoryProductItemDto p) => UrunKartMap.KartaCevir(p);

    private static UrunKartVm KartaCevir(StoreProductDto p) => UrunKartMap.KartaCevir(p);

    private static List<FiltreGrupVm> FacetleriCevir(StoreFacetsDto? facets)
    {
        if (facets is null)
            return [];

        return facets.Attributes
            .Select(a => new FiltreGrupVm(
                a.TypeCode,
                a.TypeNameI18n.TryGetValue("tr", out var ad) ? ad : a.TypeCode,
                a.IsColorType,
                a.Values.Select(v => new FiltreDegerVm(
                    v.ValueId,
                    v.NameI18n.TryGetValue("tr", out var vAd) ? vAd : "",
                    v.HexCode,
                    v.ProductCount)).ToList()))
            .Where(g => g.Degerler.Count > 0)
            .ToList();
    }
}
