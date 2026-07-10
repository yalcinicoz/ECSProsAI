using ECSPros.Api.Models.Store;
using ECSPros.Api.Services;
using ECSPros.Order.Application.Queries.GetOrderDetail;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Order.Application.Queries.GetOrderShipments;
using ECSPros.Order.Application.Queries.GetReturns;
using ECSPros.Shared.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECSPros.Api.Controllers.Store;

/// <summary>
/// Hesabım çerçevesi (E1) — misharix'in çift route şeması (/Hesabim/... + kebab-case
/// kısa yol) ve tek "Sayfa" view'ına partial adı geçiren kalıbı birebir. Sayfalar
/// üye-özel: SSR kimlik (D1 cookie) yoksa köke yönlendirilir (canlıda cookie'siz
/// oturum kalmadı — üyelik B4'te bu akışla açıldı). Partial'lar E2-E13'te teker teker
/// gerçek veriye bağlanır; o güne dek tasarımın demo içeriği render olur.
/// </summary>
public class HesabimController(
    IMediator mediator, IProductService productService, IStoreContext storeContext) : StorePageController
{
    private Guid _memberId;

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var uye = await context.HttpContext.RequestServices
            .GetRequiredService<IStoreMemberSession>()
            .MevcutUyeAsync(context.HttpContext);
        if (uye is null)
        {
            context.Result = Redirect("/");
            return;
        }

        _memberId = uye.MemberId;
        await base.OnActionExecutionAsync(context, next);
    }

    [HttpGet("/Hesabim")]
    [HttpGet("/hesabim-varsayilan")]
    public IActionResult Index() =>
        HesabimSayfasi("Hesabım Varsayılan", "~/Views/ProjeElementleri/Hesabim/_HesabimVarsayilan.cshtml");

    [HttpGet("/Hesabim/UyelikBilgilerim")]
    [HttpGet("/uyelik-bilgilerim")]
    public IActionResult UyelikBilgilerim() =>
        HesabimSayfasi("Üyelik Bilgilerim", "~/Views/ProjeElementleri/Hesabim/_HesabimUyelikBilgilerim.cshtml");

    [HttpGet("/Hesabim/Adreslerim")]
    [HttpGet("/adreslerim")]
    public IActionResult Adreslerim() =>
        HesabimSayfasi("Adreslerim", "~/Views/ProjeElementleri/Hesabim/_HesabimAdreslerim.cshtml");

    /// <summary>E4: kartlar SSR — misharix kart/filtre script'i parse anında dinleyici
    /// bağladığından liste sunucuda render edilir; detay modalı gömülü JSON'dan dolar.
    /// Sayfa ilk 20 siparişi gösterir (tasarımda sayfalama yok).</summary>
    [HttpGet("/Hesabim/Siparislerim")]
    [HttpGet("/siparislerim")]
    public async Task<IActionResult> Siparislerim(CancellationToken ct)
    {
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var siparisler = new List<HesabimSiparisVm>();
        var listeSonucu = await mediator.Send(new GetOrdersQuery(null, _memberId, null, 1, 20), ct);

        if (listeSonucu.IsSuccess)
        {
            // Kalem/kargo bilgisi liste DTO'sunda yok — sayfadaki her sipariş için detay çekilir
            // (PK sorguları; sayfa 20 kayıtla sınırlı).
            var detaylar = new List<OrderDetailDto>();
            foreach (var ozet in listeSonucu.Value!.Items)
            {
                var detay = await mediator.Send(new GetOrderDetailQuery(ozet.Id), ct);
                if (detay.IsSuccess) detaylar.Add(detay.Value!);
            }

            // Ürün adı/görsel/seçenek zenginleştirmesi (B5 deseni) — silinen varyantlar
            // sözlükte olmaz (sipariş kalemleri snapshot, VariantId FK'siz).
            var varyantIdler = detaylar.SelectMany(d => d.Items.Select(i => i.VariantId)).Distinct().ToList();
            var gorunumler = await productService.GetVariantDisplayAsync(varyantIdler, ct);

            foreach (var detay in detaylar)
            {
                var (durumMetni, durumSinifi, filtre, adim) = detay.Status switch
                {
                    "pending" or "confirmed" => ("Sipariş Alındı", "alindi", "devam", 1),
                    "processing"             => ("Hazırlanıyor", "hazirlaniyor", "devam", 2),
                    "shipped"                => ("Kargoda", "yolda", "devam", 3),
                    "delivered"              => ("Teslim Edildi", "tamamlandi", "tamamlanan", 4),
                    "cancelled"              => ("İptal Edildi", "iade", "tamamlanan", 1),
                    "returned"               => ("İade Edildi", "iade-onaylandi", "tamamlanan", 4),
                    _                        => (detay.Status, "alindi", "devam", 1)
                };

                HesabimKargoVm? kargo = null;
                if (detay.Status is "shipped" or "delivered")
                {
                    var kargoSonuc = await mediator.Send(new GetOrderShipmentsQuery(detay.Id), ct);
                    var gonderi = kargoSonuc.IsSuccess ? kargoSonuc.Value!.FirstOrDefault() : null;
                    if (gonderi is not null)
                        kargo = new HesabimKargoVm(
                            gonderi.TrackingNumber,
                            gonderi.TrackingUrl,
                            detay.Status == "delivered" ? "Paketiniz teslim edildi" : "Paketiniz yolda",
                            gonderi.EstimatedDeliveryDate?.ToString("d MMMM yyyy", tr),
                            gonderi.Events
                                .OrderByDescending(e => e.EventDate)
                                .Select(e => (e.EventDescription,
                                    $"{e.EventDate.ToString("d MMMM yyyy · HH:mm", tr)}{(e.EventLocation is null ? "" : " · " + e.EventLocation)}",
                                    true))
                                .ToList());
                }

                siparisler.Add(new HesabimSiparisVm(
                    detay.Id,
                    detay.OrderNumber,
                    detay.CreatedAt.ToString("d MMMM yyyy", tr),
                    detay.Status,
                    durumMetni, durumSinifi, filtre, adim,
                    detay.GrandTotal,
                    detay.Subtotal,
                    detay.TotalDiscount,
                    detay.PaymentStatus switch
                    {
                        "paid" => "Ödendi",
                        "refunded" => "İade Edildi",
                        _ => "Ödeme Bekliyor"
                    },
                    detay.Items.Select(i =>
                    {
                        gorunumler.TryGetValue(i.VariantId, out var g);
                        return new HesabimSiparisUrunVm(
                            g?.ProductNameI18n.GetValueOrDefault("tr") ?? i.ProductName,
                            string.IsNullOrWhiteSpace(i.VariantInfo) ? g?.OptionsText : i.VariantInfo,
                            i.Quantity,
                            i.Total,
                            g?.ImageUrl,
                            g is null ? null : "/urun/" + g.ProductCode);
                    }).ToList(),
                    kargo,
                    detay.ShippingRecipientName,
                    detay.ShippingAddressLine));
            }
        }

        var iadeSonucu = await mediator.Send(new GetReturnsQuery(null, _memberId, null, 1, 1), ct);

        ViewData["MsSiparisler"] = siparisler;
        ViewData["MsIadeSayisi"] = iadeSonucu.IsSuccess ? iadeSonucu.Value!.TotalCount : 0;
        return HesabimSayfasi("Siparişlerim", "~/Views/ProjeElementleri/Hesabim/_HesabimSiparislerim.cshtml");
    }

    /// <summary>E10: Tekrar Satın Al — teslim edilmiş sipariş kalemlerinden varyant
    /// başına bir kart (en son alışveriş öne). Fiyat GÜNCEL satış fiyatı (ürün detayıyla
    /// aynı kaynak: PlatformPrice ?? BasePrice); silinen/pasif varyant ve fiyatsız
    /// (eksik veri) kalemler listelenmez. Sepete Ekle C1 sepet API'siyle.</summary>
    [HttpGet("/Hesabim/TekrarSatinAl")]
    [HttpGet("/tekrar-satin-al")]
    public async Task<IActionResult> TekrarSatinAl(CancellationToken ct)
    {
        const int KartSiniri = 24;
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var kartlar = new List<HesabimTekrarUrunVm>();
        var platform = await storeContext.GetPlatformAsync(ct);

        var teslimler = await mediator.Send(new GetOrdersQuery("delivered", _memberId, null, 1, 50), ct);
        if (platform is not null && teslimler.IsSuccess)
        {
            // Varyant başına en son alışveriş (siparişler zaten yeniden eskiye sıralı)
            var kalemler = new List<(Guid VariantId, string? VariantInfo, DateTime Tarih)>();
            var gorulen = new HashSet<Guid>();
            foreach (var ozet in teslimler.Value!.Items.OrderByDescending(o => o.CreatedAt))
            {
                var detay = await mediator.Send(new GetOrderDetailQuery(ozet.Id), ct);
                if (detay.IsFailure) continue;
                foreach (var kalem in detay.Value!.Items)
                    if (gorulen.Add(kalem.VariantId))
                        kalemler.Add((kalem.VariantId, kalem.VariantInfo, detay.Value.CreatedAt));
            }

            var gorunumler = await productService.GetVariantDisplayAsync(
                kalemler.Select(k => k.VariantId).ToList(), ct);

            // Güncel varyant fiyatı + aktiflik: ürün başına tek detay sorgusu (ürün
            // detayının fiyat kaynağıyla birebir aynı olsun diye — B10 BasePrice sınırı)
            var urunKodlari = kalemler
                .Where(k => gorunumler.ContainsKey(k.VariantId))
                .Select(k => gorunumler[k.VariantId].ProductCode)
                .Distinct().Take(KartSiniri).ToList();
            var varyantFiyatlari = new Dictionary<Guid, decimal>();
            foreach (var kod in urunKodlari)
            {
                var urun = await mediator.Send(
                    new ECSPros.Catalog.Application.Queries.GetStoreProductDetail.GetStoreProductDetailQuery(
                        kod, platform.Id), ct);
                if (urun.IsFailure || !urun.Value!.IsActive) continue;
                foreach (var varyant in urun.Value.Variants.Where(v => v.IsActive))
                {
                    var fiyat = varyant.PlatformPrice ?? varyant.BasePrice;
                    if (fiyat > 0) varyantFiyatlari[varyant.Id] = fiyat;
                }
            }

            kartlar = kalemler
                .Where(k => gorunumler.ContainsKey(k.VariantId) && varyantFiyatlari.ContainsKey(k.VariantId))
                .Take(KartSiniri)
                .Select(k =>
                {
                    var g = gorunumler[k.VariantId];
                    return new HesabimTekrarUrunVm(
                        k.VariantId,
                        g.ProductNameI18n.GetValueOrDefault("tr") ?? g.ProductCode,
                        string.IsNullOrWhiteSpace(k.VariantInfo) ? g.OptionsText : k.VariantInfo,
                        k.Tarih.ToString("dd.MM.yyyy", tr),
                        varyantFiyatlari[k.VariantId],
                        g.ImageUrl,
                        "/urun/" + g.ProductCode);
                }).ToList();
        }

        ViewData["MsTekrarUrunler"] = kartlar;
        ViewData["MsTekrarPlatformId"] = platform?.Id;
        return HesabimSayfasi("Tekrar Satın Al", "~/Views/ProjeElementleri/Hesabim/_HesabimTekrarSatinAl.cshtml");
    }

    [HttpGet("/Hesabim/OncedenGezdiklerim")]
    [HttpGet("/onceden-gezdiklerim")]
    public IActionResult OncedenGezdiklerim() =>
        HesabimSayfasi("Önceden Gezdiklerim", "~/Views/ProjeElementleri/Hesabim/_HesabimOncedenGezdiklerim.cshtml");

    /// <summary>E8: İadelerim — kartlar SSR (E4 deseni: ilk 20 iade, detay + sipariş PK
    /// sorguları). Yeni İade Talebi modalı teslim edilmiş siparişlerin henüz iadesi
    /// olmayan kalemlerini listeler; neden listesi Lookup'tan (return_reason).</summary>
    [HttpGet("/Hesabim/Iadelerim")]
    [HttpGet("/iadelerim")]
    public async Task<IActionResult> Iadelerim(CancellationToken ct)
    {
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var iadeler = new List<HesabimIadeVm>();
        var siparisDetayCache = new Dictionary<Guid, OrderDetailDto>();

        async Task<OrderDetailDto?> SiparisDetay(Guid orderId)
        {
            if (siparisDetayCache.TryGetValue(orderId, out var d)) return d;
            var sonuc = await mediator.Send(new GetOrderDetailQuery(orderId), ct);
            if (sonuc.IsFailure) return null;
            siparisDetayCache[orderId] = sonuc.Value!;
            return sonuc.Value;
        }

        // Üyenin iadeleri (ilk 20) — kalem adı/görseli için iade detayı + sipariş detayı çekilir
        var iadeListesi = await mediator.Send(new GetReturnsQuery(null, _memberId, null, 1, 20), ct);
        var iadeDetaylari = new List<Order.Application.Queries.GetReturnDetail.ReturnDetailDto>();
        if (iadeListesi.IsSuccess)
            foreach (var ozet in iadeListesi.Value!.Items)
            {
                var detay = await mediator.Send(new Order.Application.Queries.GetReturnDetail.GetReturnDetailQuery(ozet.Id), ct);
                if (detay.IsSuccess) iadeDetaylari.Add(detay.Value!);
            }

        // Teslim edilmiş siparişler → modalın iade edilebilir kalemleri
        var teslimSiparisler = new List<OrderDetailDto>();
        var teslimListesi = await mediator.Send(new GetOrdersQuery("delivered", _memberId, null, 1, 50), ct);
        if (teslimListesi.IsSuccess)
            foreach (var ozet in teslimListesi.Value!.Items)
            {
                var detay = await SiparisDetay(ozet.Id);
                if (detay is not null) teslimSiparisler.Add(detay);
            }

        // Ürün zenginleştirmesi tek seferde (iade kalemleri + teslim kalemleri)
        var varyantIdler = iadeDetaylari.SelectMany(r => r.Items.Select(i => i.VariantId))
            .Concat(teslimSiparisler.SelectMany(o => o.Items.Select(i => i.VariantId)))
            .Distinct().ToList();
        var gorunumler = await productService.GetVariantDisplayAsync(varyantIdler, ct);

        static List<HesabimIadeNedenSecimVm> NedenSnapshotCoz(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                using var dok = System.Text.Json.JsonDocument.Parse(json);
                var liste = new List<HesabimIadeNedenSecimVm>();
                if (dok.RootElement.TryGetProperty("reasons", out var reasons))
                    foreach (var g in reasons.EnumerateArray())
                        liste.Add(new HesabimIadeNedenSecimVm(
                            g.GetProperty("main").GetString() ?? "",
                            g.TryGetProperty("subs", out var subs)
                                ? subs.EnumerateArray().Select(s => s.GetString() ?? "").Where(s => s.Length > 0).ToList()
                                : new List<string>()));
                if (dok.RootElement.TryGetProperty("other", out var other)
                    && other.ValueKind == System.Text.Json.JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(other.GetString()))
                    liste.Add(new HesabimIadeNedenSecimVm("Diğer", new List<string> { other.GetString()! }));
                return liste;
            }
            catch { return new(); } // eski/serbest metin notu — panel boş kalır
        }

        foreach (var iade in iadeDetaylari)
        {
            var siparis = await SiparisDetay(iade.OrderId);
            var (durumMetni, durumSinifi, filtre, adim) = iade.Status switch
            {
                "requested" => ("İade Talebi Alındı", "iade", "devam", 1),
                "approved"  => ("İade Onaylandı", "iade", "devam", 2),
                "received"  => ("İade İnceleniyor", "iade", "devam", 3),
                "refunded"  => ("İade Tamamlandı", "iade-onaylandi", "tamamlanan", 4),
                "rejected"  => ("İade Reddedildi", "iade", "tamamlanan", 0),
                _           => (iade.Status, "iade", "devam", 1)
            };

            var (bilgiBaslik, bilgiMetin, bilgiUyari) = iade.Status switch
            {
                "requested" => ("İade talebiniz alındı",
                    $"Paketi kargo iade kodunuzla anlaşmalı kargoya bırakabilirsiniz. Talebiniz incelendikten sonra süreç adımları burada güncellenir.", true),
                "approved"  => ("İade talebiniz onaylandı",
                    "Paketi kargo iade kodunuzla anlaşmalı kargoya bırakabilirsiniz. Ürün depomuza ulaştığında kontrol süreci başlar.", true),
                "received"  => ("İade incelemesi devam ediyor",
                    "İade kargonuz depoya ulaştı. Ürün kontrolü tamamlandıktan sonra ödeme iadesi başlatılacak.", true),
                "refunded"  => ("İade ödemeniz tamamlandı",
                    $"İade onaylandı ve {iade.RefundAmount.ToString("N2", tr)} TL ödeme iadesi kartınıza gönderildi.", false),
                "rejected"  => ("İade talebiniz onaylanmadı",
                    string.IsNullOrWhiteSpace(iade.InspectionNotes)
                        ? "Yapılan inceleme sonucunda iade talebiniz uygun bulunmadı. Detay için müşteri hizmetlerine ulaşabilirsiniz."
                        : iade.InspectionNotes!, true),
                _ => ("", "", false)
            };

            var urunler = iade.Items.Select(i =>
            {
                gorunumler.TryGetValue(i.VariantId, out var g);
                var kalem = siparis?.Items.FirstOrDefault(k => k.Id == i.OrderItemId);
                return new HesabimSiparisUrunVm(
                    g?.ProductNameI18n.GetValueOrDefault("tr") ?? kalem?.ProductName ?? "Ürün",
                    string.IsNullOrWhiteSpace(kalem?.VariantInfo) ? g?.OptionsText : kalem!.VariantInfo,
                    i.Quantity,
                    i.TotalRefundAmount,
                    g?.ImageUrl,
                    g is null ? null : "/urun/" + g.ProductCode);
            }).ToList();

            iadeler.Add(new HesabimIadeVm(
                iade.Id, iade.ReturnNumber,
                iade.CreatedAt.ToString("d MMMM yyyy", tr),
                iade.Status, durumMetni, durumSinifi, filtre, adim,
                urunler, iade.CargoReturnCode,
                bilgiBaslik, bilgiMetin, bilgiUyari,
                iade.RefundAmount, iade.Status == "refunded"));
        }

        // Modal: iade edilebilir kalemler — reddedilmemiş bir iadede yer alan kalem
        // "iade edildi" işaretlenir ve önceki neden seçimi panelde gösterilir
        var iadeliKalemler = iadeDetaylari
            .Where(r => r.Status != "rejected")
            .SelectMany(r => r.Items)
            .GroupBy(i => i.OrderItemId)
            .ToDictionary(g => g.Key, g => NedenSnapshotCoz(g.First().CustomerNotes));

        var edilebilirler = new List<HesabimIadeEdilebilirUrunVm>();
        foreach (var siparis in teslimSiparisler.OrderByDescending(o => o.CreatedAt))
            foreach (var kalem in siparis.Items)
            {
                gorunumler.TryGetValue(kalem.VariantId, out var g);
                var iadeli = iadeliKalemler.TryGetValue(kalem.Id, out var oncekiler);
                edilebilirler.Add(new HesabimIadeEdilebilirUrunVm(
                    kalem.Id,
                    siparis.OrderNumber,
                    siparis.CreatedAt.ToString("d MMMM yyyy", tr),
                    siparis.GrandTotal,
                    g?.ProductNameI18n.GetValueOrDefault("tr") ?? kalem.ProductName,
                    string.IsNullOrWhiteSpace(kalem.VariantInfo) ? g?.OptionsText : kalem.VariantInfo,
                    kalem.Quantity,
                    kalem.Total,
                    g?.ImageUrl,
                    iadeli,
                    oncekiler ?? new()));
            }

        // Neden listesi Lookup'tan — alt nedenler ExtraData.subReasons (jsonb → JsonElement)
        var nedenler = new List<HesabimIadeNedeniVm>();
        var nedenSonucu = await mediator.Send(
            new ECSPros.Core.Application.Queries.GetLookupValues.GetLookupValuesQuery("return_reason"), ct);
        if (nedenSonucu.IsSuccess)
            foreach (var deger in nedenSonucu.Value!)
            {
                var altlar = new List<string>();
                if (deger.ExtraData is not null && deger.ExtraData.TryGetValue("subReasons", out var ham))
                {
                    if (ham is System.Text.Json.JsonElement el && el.ValueKind == System.Text.Json.JsonValueKind.Array)
                        altlar = el.EnumerateArray().Select(s => s.GetString() ?? "").Where(s => s.Length > 0).ToList();
                    else if (ham is List<string> liste)
                        altlar = liste;
                }
                nedenler.Add(new HesabimIadeNedeniVm(deger.Id, deger.NameI18n.GetValueOrDefault("tr") ?? "", altlar));
            }

        // Modal üst bilgileri: üye + son teslim edilen siparişin teslimat adresi
        var uye = await mediator.Send(new ECSPros.Crm.Application.Queries.GetMemberDetail.GetMemberDetailQuery(_memberId), ct);
        var sonTeslim = teslimSiparisler.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

        ViewData["MsIadeler"] = iadeler;
        ViewData["MsIadeEdilebilirler"] = edilebilirler;
        ViewData["MsIadeNedenleri"] = nedenler;
        ViewData["MsIadeUye"] = uye.IsSuccess ? uye.Value : null;
        ViewData["MsIadeTeslimatAdi"] = sonTeslim?.ShippingRecipientName;
        ViewData["MsIadeTeslimatAdresi"] = sonTeslim?.ShippingAddressLine;
        return HesabimSayfasi("İadelerim", "~/Views/ProjeElementleri/Hesabim/_HesabimIadelerim.cshtml");
    }

    /// <summary>E7: Yorumlarım SSR — "Değerlendir" sekmesi teslim edilmiş ama henüz
    /// yorumlanmamış ürünler (kalem VariantId'leri Catalog'la koda çözülür); diğer
    /// sekmeler üyenin yorumlarından (silinenler dahil).</summary>
    [HttpGet("/Hesabim/Yorumlarim")]
    [HttpGet("/yorumlarim")]
    public async Task<IActionResult> Yorumlarim(CancellationToken ct)
    {
        var platform = await storeContext.GetPlatformAsync(ct);
        var degerlendirilecekler = new List<HesabimKoleksiyonUrunVm>();
        var yorumlar = new List<(ECSPros.Storefront.Application.Queries.GetMemberReviews.MemberReviewDto Yorum, HesabimKoleksiyonUrunVm? Urun)>();

        if (platform is not null)
        {
            // Teslim edilen ürün kodları (kod → sipariş kalemi)
            var teslimKodlari = new List<string>();
            var siparisler = await mediator.Send(new GetOrdersQuery("delivered", _memberId, null, 1, 50), ct);
            if (siparisler.IsSuccess)
            {
                var varyantIdler = new List<Guid>();
                foreach (var ozet in siparisler.Value!.Items)
                {
                    var detay = await mediator.Send(new GetOrderDetailQuery(ozet.Id), ct);
                    if (detay.IsSuccess) varyantIdler.AddRange(detay.Value!.Items.Select(i => i.VariantId));
                }
                if (varyantIdler.Count > 0)
                {
                    var gorunumler = await productService.GetVariantDisplayAsync(varyantIdler.Distinct().ToList(), ct);
                    teslimKodlari = gorunumler.Values.Select(g => g.ProductCode).Distinct().ToList();
                }
            }

            var yorumSonucu = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetMemberReviews.GetMemberReviewsQuery(
                    platform.Id, _memberId), ct);
            var uyeYorumlari = yorumSonucu.IsSuccess ? yorumSonucu.Value! : new();

            var yorumlananlar = uyeYorumlari.Where(y => !y.IsDeleted).Select(y => y.ProductCode).ToHashSet();
            var degerlendirKodlari = teslimKodlari.Where(k => !yorumlananlar.Contains(k)).ToList();

            // Ürün ad/görsel haritası (tek Catalog sorgusu)
            var tumKodlar = degerlendirKodlari.Concat(uyeYorumlari.Select(y => y.ProductCode)).Distinct().ToList();
            var urunMap = new Dictionary<string, HesabimKoleksiyonUrunVm>();
            if (tumKodlar.Count > 0)
            {
                var urunler = await mediator.Send(
                    new ECSPros.Catalog.Application.Queries.GetStoreProducts.GetStoreProductsQuery(
                        platform.Id, ProductCodes: tumKodlar, PageSize: tumKodlar.Count), ct);
                if (urunler.IsSuccess)
                    urunMap = urunler.Value!.Items.ToDictionary(
                        p => p.Code,
                        p => new HesabimKoleksiyonUrunVm(p.Code, UrunKartMap.TrAd(p.NameI18n), p.MainImageUrl));
            }

            degerlendirilecekler = degerlendirKodlari.Where(urunMap.ContainsKey).Select(k => urunMap[k]).ToList();
            yorumlar = uyeYorumlari.Select(y => (y, urunMap.GetValueOrDefault(y.ProductCode))).ToList();
        }

        ViewData["MsYorumDegerlendir"] = degerlendirilecekler;
        ViewData["MsYorumlar"] = yorumlar;
        return HesabimSayfasi("Yorumlarım", "~/Views/ProjeElementleri/Hesabim/_HesabimYorumlarim.cshtml");
    }

    /// <summary>E5: favori kodlar → Catalog'dan kart verisi (liste/ana sayfayla aynı kart
    /// kaynağı); silinen/pasif ürünün favorisi listelenmez, favori sırası korunur.</summary>
    [HttpGet("/Favorilerim")]
    [HttpGet("/Hesabim/Favorilerim")]
    public async Task<IActionResult> Favorilerim(CancellationToken ct)
    {
        var kartlar = new List<UrunKartVm>();
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is not null)
        {
            var kodSonucu = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetMemberFavorites.GetMemberFavoritesQuery(
                    platform.Id, _memberId), ct);
            var kodlar = kodSonucu.IsSuccess ? kodSonucu.Value! : new List<string>();
            if (kodlar.Count > 0)
            {
                var urunler = await mediator.Send(
                    new ECSPros.Catalog.Application.Queries.GetStoreProducts.GetStoreProductsQuery(
                        platform.Id, ProductCodes: kodlar, PageSize: kodlar.Count), ct);
                if (urunler.IsSuccess)
                {
                    var kartMap = urunler.Value!.Items.ToDictionary(p => p.Code, UrunKartMap.KartaCevir);
                    kartlar = kodlar.Where(kartMap.ContainsKey).Select(k => kartMap[k]).ToList();
                }
            }
        }
        ViewData["MsFavoriKartlar"] = kartlar;
        return HesabimSayfasi("Favorilerim", "~/Views/ProjeElementleri/Hesabim/_HesabimFavorilerim.cshtml");
    }

    /// <summary>E6: koleksiyon kartları SSR — kapaklar Catalog kart verisinden; oluşturma
    /// modalının Favorilerim/Koleksiyonlarım panelleri de gerçek ürünlerle SSR dolar.</summary>
    [HttpGet("/Hesabim/Koleksiyonlarim")]
    [HttpGet("/koleksiyonlarim")]
    public async Task<IActionResult> Koleksiyonlarim(CancellationToken ct)
    {
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var koleksiyonlar = new List<HesabimKoleksiyonVm>();
        var favoriUrunler = new List<HesabimKoleksiyonUrunVm>();
        var platform = await storeContext.GetPlatformAsync(ct);

        if (platform is not null)
        {
            var listeSonucu = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetMemberCollections.GetMemberCollectionsQuery(
                    platform.Id, _memberId), ct);
            var kayitlar = listeSonucu.IsSuccess ? listeSonucu.Value! : new();

            var favoriSonucu = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetMemberFavorites.GetMemberFavoritesQuery(
                    platform.Id, _memberId), ct);
            var favoriKodlar = favoriSonucu.IsSuccess ? favoriSonucu.Value! : new List<string>();

            // Tüm koleksiyon + favori kodları tek Catalog sorgusuyla ürün bilgisine çevrilir
            var tumKodlar = kayitlar.SelectMany(k => k.ItemCodes).Concat(favoriKodlar).Distinct().ToList();
            var urunMap = new Dictionary<string, HesabimKoleksiyonUrunVm>();
            if (tumKodlar.Count > 0)
            {
                var urunler = await mediator.Send(
                    new ECSPros.Catalog.Application.Queries.GetStoreProducts.GetStoreProductsQuery(
                        platform.Id, ProductCodes: tumKodlar, PageSize: tumKodlar.Count), ct);
                if (urunler.IsSuccess)
                    urunMap = urunler.Value!.Items.ToDictionary(
                        p => p.Code,
                        p => new HesabimKoleksiyonUrunVm(p.Code, UrunKartMap.TrAd(p.NameI18n), p.MainImageUrl));
            }

            string GoreliZaman(DateTime? t)
            {
                if (t is null) return "—";
                var fark = DateTime.UtcNow - t.Value;
                if (fark.TotalHours < 24) return "bugün";
                if (fark.TotalHours < 48) return "dün";
                return t.Value.ToString("d MMMM yyyy", tr);
            }

            koleksiyonlar = kayitlar.Select(k =>
            {
                var urunlerVm = k.ItemCodes.Where(urunMap.ContainsKey).Select(kod => urunMap[kod]).ToList();
                return new HesabimKoleksiyonVm(
                    k.Id, k.Name, k.Description, k.IsPublic, k.IsShareable, k.ShareCode,
                    k.Status, k.ViewCount, k.IsQuickSave,
                    GoreliZaman(k.UpdatedAt ?? k.CreatedAt),
                    urunlerVm.Count, urunlerVm.Take(3).ToList());
            }).ToList();

            favoriUrunler = favoriKodlar.Where(urunMap.ContainsKey).Select(kod => urunMap[kod]).ToList();

            // Modalın "Koleksiyonlarım" paneli: koleksiyonlardaki tüm ürünler (kod başına
            // bir kez; meta = ilk geçtiği koleksiyonun adı)
            var panel = new List<(HesabimKoleksiyonUrunVm Urun, string Meta)>();
            var gorulen = new HashSet<string>();
            foreach (var k in kayitlar)
                foreach (var kod in k.ItemCodes)
                    if (urunMap.TryGetValue(kod, out var u) && gorulen.Add(kod))
                        panel.Add((u, k.Name));
            ViewData["MsKoleksiyonPanelUrunleri"] = panel;
        }

        ViewData["MsKoleksiyonlar"] = koleksiyonlar;
        ViewData["MsKoleksiyonFavorileri"] = favoriUrunler;
        return HesabimSayfasi("Koleksiyonlarım", "~/Views/ProjeElementleri/Hesabim/_HesabimKoleksiyonlarim.cshtml");
    }

    /// <summary>E9: İndirim Kuponlarım — üyeye/üye grubuna tanımlı kullanılabilir
    /// kuponlar SSR (genel pazarlama kodları listelenmez); "Sepette Kullan" C3'ün
    /// sessionStorage kupon sözleşmesiyle sepete taşır.</summary>
    [HttpGet("/Hesabim/IndirimKuponlarim")]
    [HttpGet("/indirim-kuponlarim")]
    public async Task<IActionResult> IndirimKuponlarim(CancellationToken ct)
    {
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var kuponlar = new List<HesabimKuponVm>();

        var uye = await mediator.Send(
            new ECSPros.Crm.Application.Queries.GetMemberDetail.GetMemberDetailQuery(_memberId), ct);
        var listeSonucu = await mediator.Send(
            new ECSPros.Promotion.Application.Queries.GetMemberCoupons.GetMemberCouponsQuery(
                _memberId, uye.IsSuccess ? uye.Value!.MemberGroupId : null), ct);

        if (listeSonucu.IsSuccess)
            kuponlar = listeSonucu.Value!.Select(k =>
            {
                var kosullar = new List<string>();
                if (k.MinimumCartTotal is decimal min)
                    kosullar.Add($"{min.ToString("N2", tr)} TL ve üzeri alışverişlerde geçerli.");
                if (k.ValidForFirstOrderOnly)
                    kosullar.Add("Yalnızca ilk siparişte geçerli.");
                kosullar.Add(k.EndsAt is DateTime son
                    ? $"Son kullanım: {son.ToString("dd.MM.yyyy", tr)}"
                    : "Süre sınırı yok.");
                return new HesabimKuponVm(k.Code, k.DiscountText, string.Join(" ", kosullar));
            }).ToList();

        ViewData["MsKuponlar"] = kuponlar;
        return HesabimSayfasi("İndirim Kuponlarım", "~/Views/ProjeElementleri/Hesabim/_HesabimIndirimKuponlarim.cshtml");
    }

    /// <summary>E11: Favori Aramalarım — kayıtlı aramalar SSR; kaydet/düzenle/sil
    /// modal + API ile, "Sonuçları Gör" /urunler?search=... çalıştırır.</summary>
    [HttpGet("/Hesabim/FavoriAramalarim")]
    [HttpGet("/favori-aramalarim")]
    public async Task<IActionResult> FavoriAramalarim(CancellationToken ct)
    {
        var aramalar = new List<ECSPros.Storefront.Application.Queries.GetMemberSavedSearches.SavedSearchDto>();
        var platform = await storeContext.GetPlatformAsync(ct);
        if (platform is not null)
        {
            var sonuc = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetMemberSavedSearches.GetMemberSavedSearchesQuery(
                    platform.Id, _memberId), ct);
            if (sonuc.IsSuccess) aramalar = sonuc.Value!;
        }

        ViewData["MsAramalar"] = aramalar;
        ViewData["MsAramaPlatformId"] = platform?.Id;
        return HesabimSayfasi("Favori Aramalarım", "~/Views/ProjeElementleri/Hesabim/_HesabimFavoriAramalarim.cshtml");
    }

    private IActionResult HesabimSayfasi(string baslik, string partial)
    {
        ViewData["Title"] = baslik;
        ViewData["MsHesabimPartial"] = partial;
        return View("~/Views/Hesabim/Sayfa.cshtml");
    }
}
