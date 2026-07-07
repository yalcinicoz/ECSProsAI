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
| ProjeElementleri/Navigasyon/_AnaNavigasyonSearch.cshtml | AramaVm | products?search + facets | B2 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonGirisMenu.cshtml | OturumVm | StoreAuth me | B4/D6 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonGirisModal.cshtml | — (form → JS → api/store/auth) | StoreAuth login + OTP (D4) | B4/D2/D4 | 🕐 statik kopya |
| ProjeElementleri/Navigasyon/_AnaNavigasyonKayitModal.cshtml | — (form → JS → api/store/auth) | StoreAuth register + CMS sözleşme | B4/D3 | 🕐 statik kopya |
| Shared/_GorselAramaModal(+Kutu).cshtml | — | H3 görsel arama endpoint'i | H3 | 🕐 statik kopya |
| Shared/_SearchUrunKarti.cshtml | AramaUrunVm | products?search | B2 | 🕐 statik kopya |

**Sonraki fazlarda eklenecek satırlar:** UrunListesi/* (B7–B8), UrunDetay/* (B9), Sepet/* (C),
Hesabim/* (E), Kurumsal/* (F), GorunumTipleri/* + Ortak/_Story (G), Navigasyon/_MobilAltBar (H4),
UrunDegerlendirmeleri/* (E7). Satır formatı yukarıdakiyle aynı tutulmalı.
