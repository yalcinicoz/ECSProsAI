namespace ECSPros.Api.Models.Store;

/// <summary>
/// G5: vitrin bloğunun Razor görünüm modeli — PageComposer'ın ResolvedBlockDto'sundan
/// VitrinVmBuilder kurar (config alanları çözülmüş, koleksiyon kartları görsel/üye
/// adıyla zenginleşmiş). _VitrinBloklar partial'ı tip başına tasarımın GorunumTipleri
/// markup'ını basar.
/// </summary>
public sealed record VitrinBlokVm(
    Guid Id,
    string Tip,                      // PageBlockCatalog kodu
    string? Sablon,                  // banner: tekli..reklam; carousel: standart|ozel-fiyat|flash
    string Baslik,
    string? AltBaslik,
    string? Tema,                    // carousel standart arka planı (config.tema)
    string? TumunuGorUrl,            // config.seeAllUrl
    DateTime? FlashBitis,            // config.endsAt (flash geri sayımı)
    bool MobilCarousel,              // config.mobileCarousel — grid'e ms-gorunum-banner-mobil-carousel
    string? Gorunum,                 // categories: kapsul (vars.) | vitrin
    IReadOnlyList<VitrinBlokOgeVm> Ogeler,
    IReadOnlyList<UrunKartVm> Urunler,
    IReadOnlyList<VitrinKoleksiyonKartVm> Koleksiyonlar);

public sealed record VitrinBlokOgeVm(
    Guid Id,
    string Baslik,
    string? AltBaslik,
    string? GorselUrl,
    string? MobilGorselUrl,
    string? VideoUrl,
    string? LinkUrl,
    bool YeniSekme,
    string? ButonMetni,
    string? Rozet,
    IReadOnlyList<UrunKartVm> Urunler,        // tabs: sekme ürünleri
    IReadOnlyList<string> EkGorseller,        // brands/instagram kolajı (config.images)
    string? UrunSayisiMetni,                  // brands (config.productCount)
    string? StoryFramesJson);                 // story: config.frames ham JSON (yoksa null → tek kare)

public sealed record VitrinKoleksiyonKartVm(
    string Ad,
    string? Aciklama,
    string UyeAdi,                   // maskeli (E7 deseni)
    string UyeBasHarfleri,
    int UrunSayisi,
    int Goruntulenme,
    IReadOnlyList<KoleksiyonKapakVm> Kapak,   // ilk 3 ürün
    int KalanUrun,                   // kapak-bos "+N"
    string ShareCode);

public sealed record KoleksiyonKapakVm(string GorselUrl, string UrunAdi, string UrunUrl);
