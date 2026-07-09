# Misharix Partial → ViewModel Eşleme Tablosu (plan A10)

> Her partial taşınırken bu tabloya satırı işlenir: hangi ViewModel'e bağlandı, veriyi hangi
> MediatR handler'ı sağlıyor, bilinçli fark var mı (allowed-diffs.txt ile senkron).
> Doldurulma sırası fazları izler; "—" = veri bağlaması gerekmiyor (statik/salt UI).

| Partial (Views/…) | ViewModel | Veri kaynağı (handler/endpoint) | Faz | Durum |
|---|---|---|---|---|
| Shared/_Layout.cshtml | — (+_MsTemaTokenlari hook) | IStoreContext (tema tokenları) | A6/A12 | ✅ 2026-07-07 |
| Shared/_MsTemaTokenlari.cshtml *(yeni, bizim)* | StorePlatformBilgisi | IStoreContext → FirmPlatform.Settings | A12 | ✅ 2026-07-07 |
| Home/Index.cshtml | AnaSayfaVm (ViewData["MsAnaSayfa"]; kapsül kategoriler + vitrin carousel'leri) | HomeController → GetChannelCategoryProducts kök başına paralel, 15 dk IMemoryCache | B6/G8 | ✅ 2026-07-08 (geçici kompozisyon — G8'de vitrin sistemine devredilir; sayfa sonunda msUrunKartDavranislariYenile config script'i) |
| Shared/_Footer.cshtml | FooterVm (menü kolonları) | GetNavigationMenus ("footer") | F4 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyon.cshtml | — (kompozit) | — | B1 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonDuyuru.cshtml | — (B3 geçici statik; kalıcısı G'de DuyuruVm) | G kişiselleştirme bloğu | B3/G8 | ✅ 2026-07-08 (demo marka metni mishar'a uyarlandı; linkler Faz F/H'ye kadar `#`) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonUst.cshtml | NavigasyonVm (mobil kategori şeridi) + mini sepet script'i | GetChannelCategories (ViewData) + GET/DELETE api/store/cart (B5; IProductService zenginleştirmesi) | B1/B5/D6 | 🔶 B1+B5 ✅ 2026-07-09 (mini sepet canlı: rozet+panel+silme+msMiniSepetYenile; oturum/giriş menüsü B4/D6'da) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonDesktopMenu.cshtml | NavigasyonVm/NavKategori | GetChannelCategories (nav_menus boş → kategori ağacı; StorePageController 5dk cache) | B1 | ✅ 2026-07-07 (kampanya şeridi statik — Faz G) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonMobilMenu.cshtml | NavigasyonVm (aynı) | GetChannelCategories (aynı ViewData) | B1 | ✅ 2026-07-07 (kampanya bölümü + alt nav statik — Faz G) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonSearch.cshtml | — (client-side; platform id + kategori JSON gömülü) | products?search (canlı öneri + popüler ürünler); kategori önerisi nav ağacından | B2/B10 | ✅ 2026-07-09 (B10: "kategoride ara" bağlandı — kategori sayfası bağlamıyla buton görünür, öneriler kategori kapsamından, Tümünü Gör/Enter → /{slug}?search=) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonGirisMenu.cshtml | — (login script'i doldurur) | StoreAuth me (B4) | B4/D6 | ✅ 2026-07-09 (kimlik/avatar gerçek; statü bloğu @if(false) — Faz E/G; menü linkleri Faz E) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonGirisModal.cshtml | — (dosya sonu oturum script'i) | StoreAuth login/me/refresh + cart/merge (B4) | B4/D2/D4 | ✅ 2026-07-09 (e-posta canlı varsayılan; SMS/Telefon '(Yakında)' disabled — OTP Faz D) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonKayitModal.cshtml | — (giriş modalındaki script bağlar) | StoreAuth register (B4; sözleşme metinleri CMS'e D3'te bağlanır) | B4/D3 | ✅ 2026-07-09 (Şifre alanı eklendi — API zorunlu; onay kutuları zorunlu) |
| Shared/_GorselAramaModal(+Kutu).cshtml | — | H3 görsel arama endpoint'i | H3 | 🕐 statik kopya |
| Shared/_SearchUrunKarti.cshtml | — (kullanılmıyor; kartlar B2 script'inde aynı markup'la üretiliyor) | — | B2 | ✅ 2026-07-07 (dosya bayt-aynı duruyor) |

| UrunListesi/Index.cshtml | UrunListesiVm (ViewData["MsUrunListesi"]) | UrunListesiController (kategori /{slug} + /urunler?search + /urun-listesi) | B7 | ✅ 2026-07-07 (başlık bağlaması) |
| ProjeElementleri/UrunListesi/_UrunListesiSayfasi.cshtml | — (kompozit; ViewData taşır) | — | B7 | ✅ 2026-07-07 (bayt-aynı) |
| ProjeElementleri/UrunListesi/_UrunListesiUrunAlani.cshtml | UrunKartVm listesi (SSR ilk sayfa + template; KartRenkVm renk tooltip'i, GaleriUrller hover galerisi) | GetChannelCategoryProducts / GetStoreProducts (devam sayfaları api/store JSON; config script sonu) | B7/B8/B10 | ✅ 2026-07-09 (B8: hover galeri + tooltip + ?color=; B6: kart paylaşılan _UrunKarti partial'ında; B10: client filtre motoru kaldırıldı — filtre/sıralama URL parametreleriyle sunucudan, kopya checkbox'lar senkron) |
| ProjeElementleri/Urun/_UrunKarti.cshtml *(yeni, bizim — kaynakta demo _UrunKartiOrnegi)* | UrunKartVm (Model; null → infinite-scroll iskelet kartı) | çağıran yüzeyin sorgusu (liste SSR + template + ana sayfa carousel) | B6 | ✅ 2026-07-08 (tek kaynak; markup misharix kart component'iyle birebir) |
| ProjeElementleri/UrunListesi/_UrunListesiSolFiltre.cshtml | FiltreGrupVm + KategoriSecenekleri | GetChannelCategoryFacets / GetStoreFacets (SSR süreç içi) | B7/B10 | ✅ 2026-07-09 (B10: checkbox/fiyat SSR seçili durumla gelir; Kampanya bloğu Faz G'ye kadar gizli) |
| ProjeElementleri/UrunListesi/_UrunListesiSagUstFiltre.cshtml | UrunListesiVm (başlık/sayı) + cinsiyet facet | aynı facets | B7/B10 | ✅ 2026-07-09 (B10: sıralama seçenekleri sunucu durumundan aktif işaretli + En Yeniler eklendi; hızlı chip'ler Faz G) |
| ProjeElementleri/UrunListesi/_UrunListesiMobilFiltre.cshtml | FiltreGrupVm (paneller gruplardan üretilir) | aynı facets | B7/B10 | ✅ 2026-07-09 (B10: checkbox/fiyat SSR seçili; sıralama modalına En Yeniler + aktif işaret; script değişmedi) |

| UrunDetay/Index.cshtml | UrunDetayVm (ViewData["MsUrunDetay"]) | UrunDetayController (/urun/{code}?color=) | B9 | ✅ 2026-07-08 (başlık bağlaması) |
| ProjeElementleri/UrunDetay/_UrunDetaySayfasi.cshtml | UrunDetayVm (sabit beden + mobil fiyat + paylaş modal içeriği) | süreç içi MediatR; dosya sonunda sepet/renk config script'i → api/store/cart/items | B9 | ✅ 2026-07-08 (misharix davranış script'leri değişmedi) |
| ProjeElementleri/UrunDetay/_UrunDetayBreadcrumb.cshtml | UrunDetayVm.Breadcrumb | GetProductChannelCategoryChainQuery (filtre kuralı ters eşlemesi) | B9 | ✅ 2026-07-08 |
| ProjeElementleri/UrunDetay/_UrunDetayResimAlani.cshtml | UrunDetayVm.Gorseller (seçili renk) | GetStoreProductDetail (renk havuzu görselleri) | B9 | ✅ 2026-07-08 (video+etiketler B11/G'ye kadar gizli) |
| ProjeElementleri/UrunDetay/_UrunDetayBilgi.cshtml | UrunDetayVm (ad/kod/fiyat/renkler/bedenler/özellikler) | GetStoreProductDetail + DTO'ya eklenen Attributes/ProductGroupNameI18n | B9 | ✅ 2026-07-08 (model ölçüleri manken verisi gelince, teslimat Faz H, beden tablosu veri yok) |
| ProjeElementleri/UrunDetay/_UrunDetayAltBilgi.cshtml | UrunDetayVm (görsel/açıklama/özellikler) | aynı sorgu (DescriptionI18n) | B9 | ✅ 2026-07-08 |

| Sepet/Index.cshtml | — (kabuk; script /teslimat yönlendirmesi kaynaktaki gibi) | SepetController (/sepet) | C1 | ✅ 2026-07-09 (bayt-aynı) |
| ProjeElementleri/Sepet/_SepetSayfasi.cshtml | — (istemci-durumlu; satırlar template'ten) | GET/PUT/DELETE api/store/cart + POST checkout/coupon/validate (dosya sonu script) | C1/C3/C7 | ✅ 2026-07-09 (C3: kupon canlı — msSepetKuponDurumu sözleşmesi korunarak; TCKN koşulu C7, kargo H, kampanya/koleksiyon G/E6 — @if gizli) |
| ProjeElementleri/Sepet/_SepetModallari.cshtml | — (sil onay/kupon/sözleşme/TCKN modalları) | sözleşme içerikleri C8'de CMS'ten | C1/C8 | ✅ 2026-07-09 (bayt-aynı; sil onayına API dinleyicisi sayfa script'inden eklenir) |

| Sepet/Teslimat.cshtml | — (kabuk; /odeme yönlendirmesi kaynaktaki gibi) | SepetController (/teslimat) | C4 | ✅ 2026-07-09 (bayt-aynı) |
| ProjeElementleri/Sepet/_SepetTeslimatSayfasi.cshtml | — (adres kartları template'ten; msTeslimatDurumu yayını) | GET/POST api/store/account/addresses + api/store/geo (dosya sonu script) | C4 | ✅ 2026-07-09 (düzenleme E4; kargo statik — H) |
| ProjeElementleri/Sepet/_SepetAdresModali.cshtml *(yeni, bizim — kaynağı _SepetSiparis demo bloğu)* | — (data-ms-adres-* bağları) | api/store/geo kademeli lookup + POST addresses | C4 | ✅ 2026-07-09 (il/ilçe/mahalle aramalı; telefon bileşeni site.js) |

**Sonraki fazlarda eklenecek satırlar:** Sepet/Odeme+SiparisTamamlandi (C5-C10),
Hesabim/* (E), Kurumsal/* (F), GorunumTipleri/* + Ortak/_Story (G), Navigasyon/_MobilAltBar (H4),
UrunDegerlendirmeleri/* (E7). Satır formatı yukarıdakiyle aynı tutulmalı.
