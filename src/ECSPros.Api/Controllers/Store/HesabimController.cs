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

    [HttpGet("/Hesabim/TekrarSatinAl")]
    [HttpGet("/tekrar-satin-al")]
    public IActionResult TekrarSatinAl() =>
        HesabimSayfasi("Tekrar Satın Al", "~/Views/ProjeElementleri/Hesabim/_HesabimTekrarSatinAl.cshtml");

    [HttpGet("/Hesabim/OncedenGezdiklerim")]
    [HttpGet("/onceden-gezdiklerim")]
    public IActionResult OncedenGezdiklerim() =>
        HesabimSayfasi("Önceden Gezdiklerim", "~/Views/ProjeElementleri/Hesabim/_HesabimOncedenGezdiklerim.cshtml");

    [HttpGet("/Hesabim/Iadelerim")]
    [HttpGet("/iadelerim")]
    public IActionResult Iadelerim() =>
        HesabimSayfasi("İadelerim", "~/Views/ProjeElementleri/Hesabim/_HesabimIadelerim.cshtml");

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

    [HttpGet("/Hesabim/IndirimKuponlarim")]
    [HttpGet("/indirim-kuponlarim")]
    public IActionResult IndirimKuponlarim() =>
        HesabimSayfasi("İndirim Kuponlarım", "~/Views/ProjeElementleri/Hesabim/_HesabimIndirimKuponlarim.cshtml");

    [HttpGet("/Hesabim/FavoriAramalarim")]
    [HttpGet("/favori-aramalarim")]
    public IActionResult FavoriAramalarim() =>
        HesabimSayfasi("Favori Aramalarım", "~/Views/ProjeElementleri/Hesabim/_HesabimFavoriAramalarim.cshtml");

    private IActionResult HesabimSayfasi(string baslik, string partial)
    {
        ViewData["Title"] = baslik;
        ViewData["MsHesabimPartial"] = partial;
        return View("~/Views/Hesabim/Sayfa.cshtml");
    }
}
