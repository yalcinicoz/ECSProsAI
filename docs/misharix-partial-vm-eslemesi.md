# Misharix Partial → ViewModel Eşleme Tablosu (plan A10)

> Her partial taşınırken bu tabloya satırı işlenir: hangi ViewModel'e bağlandı, veriyi hangi
> MediatR handler'ı sağlıyor, bilinçli fark var mı (allowed-diffs.txt ile senkron).
> Doldurulma sırası fazları izler; "—" = veri bağlaması gerekmiyor (statik/salt UI).

| Partial (Views/…) | ViewModel | Veri kaynağı (handler/endpoint) | Faz | Durum |
|---|---|---|---|---|
| Shared/_Layout.cshtml | — (+_MsTemaTokenlari hook) | IStoreContext (tema tokenları) | A6/A12 | ✅ 2026-07-07 |
| Shared/_MsTemaTokenlari.cshtml *(yeni, bizim)* | StorePlatformBilgisi | IStoreContext → FirmPlatform.Settings | A12 | ✅ 2026-07-07 |
| Home/Index.cshtml | — (geçici sayfa seçici; B6'da vitrin, G8'de kalıcı) | — | A/B6/G8 | 🕐 statik kopya |
| Shared/_Footer.cshtml | FooterVm (menü kolonları) | GetNavigationMenus ("footer") | F4 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyon.cshtml | — (kompozit) | — | B1 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonDuyuru.cshtml | DuyuruVm | G kişiselleştirme bloğu (B3 geçici statik) | B3/G8 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonUst.cshtml | NavigasyonVm (mobil kategori şeridi) + oturum/sepet B5/D6'da | GetChannelCategories (StorePageController → ViewData) / StoreAuth me / StoreCart | B1/B5/D6 | 🔶 B1 kısmı ✅ 2026-07-07 (şerit veriye bağlı; sepet/oturum statik) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonDesktopMenu.cshtml | NavigasyonVm/NavKategori | GetChannelCategories (nav_menus boş → kategori ağacı; StorePageController 5dk cache) | B1 | ✅ 2026-07-07 (kampanya şeridi statik — Faz G) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonMobilMenu.cshtml | NavigasyonVm (aynı) | GetChannelCategories (aynı ViewData) | B1 | ✅ 2026-07-07 (kampanya bölümü + alt nav statik — Faz G) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonSearch.cshtml | — (client-side; platform id + kategori JSON gömülü) | products?search (canlı öneri + popüler ürünler); kategori önerisi nav ağacından | B2 | ✅ 2026-07-07 ("kategoride ara" B7'de de ertelendi — backend'de kategori+arama birleşik sorgu yok, B10'da bağlanmalı) |
| ProjeElementleri/Navigasyon/_AnaNavigasyonGirisMenu.cshtml | OturumVm | StoreAuth me | B4/D6 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonGirisModal.cshtml | — (form → JS → api/store/auth) | StoreAuth login + OTP (D4) | B4/D2/D4 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonKayitModal.cshtml | — (form → JS → api/store/auth) | StoreAuth register + CMS sözleşme | B4/D3 | 🕐 statik kopya |
| Shared/_GorselAramaModal(+Kutu).cshtml | — | H3 görsel arama endpoint'i | H3 | 🕐 statik kopya |
| Shared/_SearchUrunKarti.cshtml | — (kullanılmıyor; kartlar B2 script'inde aynı markup'la üretiliyor) | — | B2 | ✅ 2026-07-07 (dosya bayt-aynı duruyor) |

| UrunListesi/Index.cshtml | UrunListesiVm (ViewData["MsUrunListesi"]) | UrunListesiController (kategori /{slug} + /urunler?search + /urun-listesi) | B7 | ✅ 2026-07-07 (başlık bağlaması) |
| ProjeElementleri/UrunListesi/_UrunListesiSayfasi.cshtml | — (kompozit; ViewData taşır) | — | B7 | ✅ 2026-07-07 (bayt-aynı) |
| ProjeElementleri/UrunListesi/_UrunListesiUrunAlani.cshtml | UrunKartVm listesi (SSR ilk sayfa + template) | GetChannelCategoryProducts / GetStoreProducts (devam sayfaları api/store JSON; config script sonu) | B7/B8 | ✅ 2026-07-07 (kart demo blokları @if gizli — B8'de derinleşir) |
| ProjeElementleri/UrunListesi/_UrunListesiSolFiltre.cshtml | FiltreGrupVm + KategoriSecenekleri | GetChannelCategoryFacets / GetStoreFacets (SSR süreç içi) | B7 | ✅ 2026-07-07 (Kampanya bloğu Faz G'ye kadar gizli) |
| ProjeElementleri/UrunListesi/_UrunListesiSagUstFiltre.cshtml | UrunListesiVm (başlık/sayı) + cinsiyet facet | aynı facets | B7/B10 | ✅ 2026-07-07 (hızlı chip'ler Faz G; eksik sıralamalar B10) |
| ProjeElementleri/UrunListesi/_UrunListesiMobilFiltre.cshtml | FiltreGrupVm (paneller gruplardan üretilir) | aynı facets | B7 | ✅ 2026-07-07 (anaFiltreAdlari bağlandı; script değişmedi) |

**Sonraki fazlarda eklenecek satırlar:** UrunDetay/* (B9), Sepet/* (C),
Hesabim/* (E), Kurumsal/* (F), GorunumTipleri/* + Ortak/_Story (G), Navigasyon/_MobilAltBar (H4),
UrunDegerlendirmeleri/* (E7). Satır formatı yukarıdakiyle aynı tutulmalı.
