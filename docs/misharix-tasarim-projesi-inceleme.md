# Misharix Tasarım Projesi — Detaylı İnceleme

> Kaynak: `/opt/misharixWebSites/misharix` (ayrı, bağımsız tasarım projesi)
> İnceleme tarihi: 2026-07-07
> Amaç: Bu tasarım sistemini ECSProsAI storefront'una **bozmadan** taşımak için envanter ve kural haritası.

---

## 1. Genel Bakış

Misharix, ECSPros storefront'u için hazırlanmış **tam kapsamlı bir e-ticaret tasarım sistemi ve çalışan prototip**tir. ASP.NET Core MVC (net9.0) + Tailwind CSS v4.3.1 üzerine kuruludur. Backend mantığı yok denecek kadar azdır — asıl değer üç katmandadır:

1. **`Views/ProjeElementleri/`** — 82 adet Razor partial'dan oluşan element kataloğu (her sayfa/bileşen kopyalanmaya hazır blok halinde)
2. **`wwwroot/css/tailwind.css`** — 11.546 satır; tüm elementlerin `ms-` prefix'li component class'ları + tema tokenları
3. **`wwwroot/js/site.js`** — 4.388 satır; global UI davranışları + sayfa modülü registry sistemi

Ayrıca projenin kendi **AI hafızası** vardır: `ai-skills/misharix-project/` altında skill + 5 agent rolü tanımlıdır. Bu dosyalar tasarımın "anayasası"dır — taşıma sırasında uyulması gereken tüm kurallar burada yazılıdır (bkz. Bölüm 6).

---

## 2. Proje Yapısı

```
/opt/misharixWebSites/misharix/
├── Program.cs                    # Minimal MVC host (net9.0, MapStaticAssets)
├── misharix.csproj               # Tek paket: MySqlConnector 2.6.1
├── package.json                  # @tailwindcss/cli 4.3.1, heroicons
├── appsettings.json              # ⚠️ Canlı legacy MySQL bağlantısı içerir (juludedb) — commit etme!
├── Controllers/                  # 11 controller (çoğu sadece View döndürür)
├── Models/                       # Sadece ErrorViewModel
├── Views/
│   ├── Shared/                   # _Layout, _Footer, _AnaNavigasyon (wrapper), görsel arama modalı
│   ├── ProjeElementleri/         # ★ ELEMENT KATALOĞU (82 partial + Index)
│   ├── Home, UrunListesi, UrunDetay, Sepet, Hesabim, Kurumsal,
│   │   UrunDegerlendirmeleri, Agent   # Gerçek sayfalar — hepsi ince wrapper
├── ai-skills/misharix-project/   # SKILL.md + references/ (project-basics, agents)
├── scripts/show-misharix-skils.mjs  # skill md → HTML görüntüleyici üretir
└── wwwroot/
    ├── css/tailwind.css          # KAYNAK (11.546 satır)
    ├── css/site.css              # DERLENMİŞ ÇIKTI (npm run build-css)
    ├── js/site.js                # Global UI davranışları (4.388 satır)
    ├── ikons/                    # 67 proje SVG ikonu (tek onaylı ikon kaynağı)
    ├── images/                   # örnek görseller, logolar, story görselleri
    ├── video/test.mp4            # videolu ürün rozeti demo videosu
    ├── fontawesome-free-7.2.0-web/  # FontAwesome 7.2 (lokal, CDN'siz)
    └── misharix-skils/index.html # skill kurallarının HTML görüntüsü
```

### Kritik mimari kalıp: sayfa = partial wrapper

Gerçek sayfalar ProjeElementleri partial'ını çağıran 3-5 satırlık wrapper'lardır:

```cshtml
@* Views/UrunListesi/Index.cshtml — TAMAMI *@
@{ ViewData["Title"] = "Ürün Listesi"; }
@await Html.PartialAsync("~/Views/ProjeElementleri/UrunListesi/_UrunListesiSayfasi.cshtml")
```

`_Layout.cshtml` da aynı şekilde navigasyon ve footer'ı ProjeElementleri'nden çeker. **Yani katalog = canlı site**; ikisi arasında kopya/sürüm farkı yoktur. Taşımada tek doğru kaynak `Views/ProjeElementleri/` dizinidir.

---

## 3. ProjeElementleri Kataloğu (82 partial)

`/ProjeElementleri` adresi sekmeli bir tasarım kütüphanesi sunar (mobil/desktop önizleme iframe'i dahil, `?ms_cerceve=1` mobil çerçeve modu). `ProjeElementleriController` üç partial haritası tutar:

### 3.1 Panel sekmeleri (kütüphane sekmeleri)
| Sekme kodu | Partial |
|---|---|
| sayfalar | Ortak/_Sayfalar |
| gorunum-tipleri | Ortak/_GorunumTipleri (varsayılan sekme) |
| mobil-alt-bar | Navigasyon/_MobilAltBar |
| urun-kartlari | Urun/_UrunKartlari |
| arayuz-elementleri | Ortak/_ArayuzElementleri (butonlar/filtreler/rozetler/ikonlar/bildirimler/modallar buraya alias'lanır) |
| formlar | Ortak/_Formlar |
| statuler | Ortak/_Statuler |
| infinite-scroll | Ortak/_InfiniteScroll |
| lazy-load | Ortak/_LazyLoad |

### 3.2 Klasör envanteri

**Navigasyon/ (10):** `_AnaNavigasyon` (kompozit: Duyuru + Üst + DesktopMenu + MobilMenu partial'larını birleştirir, 1.595 satır), `_AnaNavigasyonSearch`, `_AnaNavigasyonGirisMenu`, `_AnaNavigasyonGirisModal`, `_AnaNavigasyonKayitModal`, `_MobilAltBar` (mobilde sabit alt bar; desktop'ta gizli)

**Urun/ (2):** `_UrunKartlari` (tüm kart varyantları), `_UrunKartiOrnegi`

**UrunListesi/ (5):** `_UrunListesiSayfasi` (kompozit), `_UrunListesiSolFiltre`, `_UrunListesiSagUstFiltre`, `_UrunListesiMobilFiltre` (1.004 satır), `_UrunListesiUrunAlani` (infinite-scroll'lu ürün grid'i + `<template>` kart şablonu)

**UrunDetay/ (5):** `_UrunDetaySayfasi` (1.886 satır, en büyük partial), `_UrunDetayBreadcrumb`, `_UrunDetayResimAlani`, `_UrunDetayBilgi`, `_UrunDetayAltBilgi`

**Sepet/ (6):** `_SepetSayfasi`, `_SepetTeslimatSayfasi`, `_SepetOdemeSayfasi`, `_SepetSiparisTamamlandiSayfasi`, `_SepetSiparis`, `_SepetModallari`

**Hesabim/ (19):** `_HesabimYanMenu`, `_HesabimVarsayilan`, `_HesabimUyelikBilgilerim`, `_HesabimAdreslerim`, `_HesabimSiparislerim` (1.227 satır), `_HesabimSiparislerimDetayModal`, `_HesabimSiparislerimIadeModal`, `_HesabimIadelerim` (1.112 satır), `_HesabimIadeSayfasiModali`, `_HesabimIadeDogrulamaModallari`, `_HesabimTekrarSatinAl`, `_HesabimOncedenGezdiklerim`, `_HesabimYorumlarim`, `_HesabimFavorilerim`, `_HesabimFavoriAramalarim`, `_HesabimKoleksiyonlarim`, `_HesabimIndirimKuponlarim`, `_HesabimFaturaPdfModal`

**Kurumsal/ (8):** `_KurumsalSayfasi` (yan menülü çerçeve) + Hakkımızda, İletişim, Kargo-Teslimat, İade-Değişim, SSS, Kullanım Koşulları, Gizlilik-Güvenlik

**UrunDegerlendirmeleri/ (1):** `_UrunDegerlendirmeleriSayfasi`

**GorunumTipleri/ (8):** `_Banner`, `_Brands`, `_Carousel`, `_Categories`, `_Collection`, `_Grid`, `_Instagram`, `_Tabs` — ana sayfa vitrin yerleşim tipleri

**Ortak/ (18):** `_ArayuzElementleri`, `_Butonlar`, `_Filtreler`, `_Formlar` (fiyat tipleri burada: "Formlar > Fiyatlar"), `_Rozetler`, `_Statuler`, `_Bildirimler`, `_Modallar`, `_Ikons`, `_SelectOrnekleri`, `_Slider`, `_Story`, `_Navigasyon`, `_Sayfalar`, `_GorunumTipleri`, `_InfiniteScroll`, `_LazyLoad`

### 3.3 Partial içi kullanım notları

Kurallar çoğunlukla skill dosyasında merkezi tutulmuş; ayrıca bazı partial'larda görünür "Kullanım kuralı" kutuları var:

- **_InfiniteScroll:** Infinite scroll açılacak ana alana `lazy-infinite-on` class'ı eklenir. Aynı kapsayıcıda `data-ms-infinite-liste`, `<template data-ms-infinite-template>`, `data-ms-infinite-yukleniyor` bulunmalı. `lazy-infinite-on` yoksa `data-ms-infinite-scroll` ve `data-ms-lazy-src` **tek başına çalışmaz** (opt-in tasarım).
- **_LazyLoad:** Görselin gerçek adresi `data-ms-lazy-src`'de bekler; görsel mutlaka `lazy-infinite-on` kapsayıcısı içinde olmalı. Aynı kapsayıcıda infinite scroll varsa ikisi birlikte çalışır.
- **_MobilAltBar:** Mobilde sabit alt navigasyon; desktop'ta gizlenir, katalogda telefon çerçevesinde önizlenir.

### 3.4 Inline script taşıyan partial'lar (18 adet)

Skill kuralı gereği **sayfaya/veriye özel scriptler ilgili partial'ın en altında** tutulur (site.js'e taşınmaz). Script içeren partial'lar:

`_AnaNavigasyon`, `_UrunDetaySayfasi`, `_UrunListesiUrunAlani`, `_UrunListesiSagUstFiltre`, `_UrunListesiMobilFiltre`, `_SepetSayfasi`, `_SepetTeslimatSayfasi`, `_SepetOdemeSayfasi`, `_SepetSiparis`, `_SepetModallari`, `_HesabimSiparislerim`, `_HesabimIadelerim`, `_HesabimKoleksiyonlarim`, `_UrunDegerlendirmeleriSayfasi`, `_Carousel`, `_Story`, `_HesabimYanMenu`, `Index.cshtml`

Her scriptin başında amaç açıklayan `@* Bu script: ... *@` yorumu vardır.

---

## 4. CSS Mimarisi (`wwwroot/css/tailwind.css`)

- Tailwind **v4** sözdizimi: `@import "tailwindcss"; @source "../../Views";` — content taraması Views klasöründen yapılır.
- Derleme: `npm run build-css` → `wwwroot/css/site.css` (minify). **Kural: AI/geliştirici build çalıştırmaz, çıktıyı kullanıcı üretir.**
- Bölüm düzeni korunmalıdır (yeni class ilgili bölümün altına). Ana bölümler ve satır konumları:

| Satır | Bölüm |
|---|---|
| 5 | Tema renk tokenları (`@theme`: `--color-ms-siyah`, `--color-ms-primary`, ...) |
| 19 | Global tema/kontrast/radius (`:root` custom property'leri) |
| 108 | İkon renk yardımcıları (`ms-ikon-siyah/beyaz/orijinal`) |
| 150 | Sayfa iskeleti + ProjeElementleri kabuğu |
| 381 | Agent sayfası |
| 451 | Lazy load durumları, skeleton |
| 592 | Form, select, telefon, kod girişi |
| 819 | Buton, chip, kategori butonları |
| 1184 | Ürün kartları, görsel, favori, rozet, fiyat, vitrin |
| 3630 | Ana navigasyon, arama, giriş/sepet menüleri |
| 5357 | Ürün listesi + mobil filtre |
| 6008 | Kurumsal sayfalar |
| 9733 | Global modal + durum varyantları |
| 9923 | Filtre/sıralama/fiyat aralığı kutuları |
| 10166 | Ana sayfa görünüm tipleri |
| 11208 | Infinite scroll |
| 11229 | Footer |
| 11413 | Ürün değerlendirmeleri |

### Tema tokenları (tek değişim noktası)

```css
--ms-renk-primary: #f27a1a;        /* ana aksiyon (Trendyol turuncusu) */
--ms-renk-primary-hover: #df6811;
--ms-renk-metin: #333;             /* "siyah" standardı = #333 */
--ms-renk-success/warning/danger/info, --ms-renk-muted, --ms-renk-border...
--ms-radius-card / -input / -btn: 0.75rem (rounded-xl standardı)
--ms-radius-badge: 9999px
```

- Yeni class'larda **doğrudan hex yazmak yasak**; `var(--ms-renk-primary)` kullanılır.
- Sayfa scope'una token override yazılabilir; hızlı radius denemesi için `ms-rounded-lg` scope class'ı var.
- Global davranışlar: ince scrollbar, `cursor-pointer` (buton/link/label), focus outline'ları kapatılmış, `body.ms-modal-acik` scroll kilidi.

### Class isimlendirme politikası
- Tüm component class'ları `ms-` prefix'li ve **Türkçe** (`ms-urun-karti`, `ms-sepet-sayfa`).
- Yeni ortak temel isimler: `ms-btn`, `ms-card`, `ms-kapsayici`, `ms-chip`; eskiler (`ms-buton`, `ms-panel`, `ms-chip-buton`) geriye uyumluluk alias'ı olarak duruyor — **ani kaldırılmamalı**.
- Sayfaya özel class **yalnızca layout/yerleşim** için (`ms-urun-detay-ust` gibi); modal/buton/kart/fiyat gibi tekrar kullanılabilir parçalar için sayfa kopyası class açmak yasak.

---

## 5. JS Mimarisi (`wwwroot/js/site.js`)

Katı bir **global ↔ sayfa** sınırı vardır:

- **site.js'te olanlar** (yorum başlıklı gruplar): ProjeElementleri scroll konumu koruma, **sayfa modülü registry**, ürün video rozetleri, **opt-in infinite scroll**, **global lazy load**, SSS akordiyonu, ProjeElementleri sekme/filtre/select/modal/form davranışları, slider + ürün detay galerisi, favori buton animasyonu, dinamik eklenen kartlar için davranış yenileme (`msUrunKartDavranislariYenile`), story modal.
- **site.js'te olamayanlar:** veri üretimi, API/fetch, sayfaya özel filtreleme/sayfalama/validasyon, demo veri, endpoint — bunlar ilgili `.cshtml` sonunda kalır.

### Sayfa modülü kalıbı (backend entegrasyon sözleşmesi)

```html
<section data-ms-page-module="infinite-scroll" data-ms-infinite-scroll
         data-ms-infinite-config="urun-listesi" class="lazy-infinite-on" ...>
  <div data-ms-infinite-liste></div>
  <template data-ms-infinite-template> ...ürün kartı HTML'i... </template>
  <div data-ms-infinite-yukleniyor>Yeni ürünler yükleniyor...</div>
</section>
<script>
  window.msInfiniteConfigs["urun-listesi"] = {
    ilk: 20, adet: 20, toplam: 100, esik: 0.8, sadeceIlkYukle: true,
    stateKey: "ms-infinite-scroll:urun-listesi-demo",
    sonra: (liste) => window.msUrunKartDavranislariYenile?.(liste)
  };
</script>
```

- Başlatıcılar `window.msRegisterPageModule("modul-adi", fn)` ile kaydedilir; HTML tarafı `data-ms-page-module="modul-adi"` ile eşleşir.
- **Backend'e taşınacak birim** = partial'daki HTML + `data-ms-*` attribute'ları + partial sonundaki config script'i. Bu üçlü birlikte kopyalanır.
- Dinamik (AJAX ile) eklenen ürün kartlarında galeri/video/favori/renk-tooltip davranışlarını yeniden bağlamak için `window.msUrunKartDavranislariYenile(kapsayici)` çağrılır — **liste sayfası API'ye bağlanırken bu kritik**.

---

## 6. AI Skill ve Agent Sistemi (`ai-skills/misharix-project/`)

`SKILL.md` → `references/project-basics.md` (47 maddelik tarihli karar kaydı) + `references/project-agents.md` + `references/agents/*.md` (5 rol). `/agent` sayfası bu md'leri web'de gösterir; `npm run misharix-skils` script'i `project-basics.md`'yi `wwwroot/misharix-skils/index.html`'e senkronlar.

### 6.1 Tasarım anayasası — en önemli kurallar (project-basics.md özeti)

**Kaynak/tekillik:**
- Tüm sayfalar `ProjeElementleri`'ndeki elementleri kullanmak **zorunda**; önce mevcut element aranır, varken yeni component/class ailesi açılmaz.
- Katalogda karşılığı olmayan element ihtiyacında **önce kullanıcıya sorulur**; onaysız yeni element/standart eklenmez.
- Element tasarımı Razor içinde uzun Tailwind utility listeleriyle tekrar edilmez; `ms-` component class'ı kullanılır.

**Görsel standartlar:**
- Siyah = `#333` (`ms-siyah`); ana aksiyon = `#f27a1a` (token üzerinden). Yeni renk ihtiyacında önce mevcutlar denenir, yoksa kullanıcıya sorulur.
- Başlık fontu `13px`, normal metin maks `12px`. Radius standardı `rounded-xl`.
- İkon: sadece `wwwroot/ikons` (67 SVG) + `/ikons/<dosya>` yolu; yoksa kullanıcıdan istenir — yeni ikon çizilmez/indirilmez. Renk: `ms-ikon-siyah/beyaz/orijinal`.
- Örnek görsel: `images/ornek-resim.jpg` ve `ornek-resim-2.jpg`. Emoji ve rastgele SVG seti yasak.

**Semantik/SEO:**
- Gerçek yönlendirme → `<a href>`; sadece UI davranışı → `<button type="button">`; `<a href="#">` buton taklidi yasak.
- Tek anlamlı `h1` (ürün adı), sıralı `h2/h3`, görünür breadcrumb, ürün adı/marka/fiyat/stok/renk/beden **gerçek HTML metni** olarak, görsellerde açıklayıcı `alt`.
- Mobil içerik sırası: görsel → ürün adı → fiyat → beden → aksiyonlar.
- Tıklanabilir elementlerde `cursor-pointer`; tıklama sonrası active/focus ring gösterilmez.

**Modal standardı:**
- Tüm modallar katalogdaki `Modallar` türlerinden türetilir: `ms-ornek-modal`, `ms-ornek-modal-kutu`, `-baslik`, `-aciklama`, `-aksiyonlar` + durum ikon/sınıfları. Sayfaya özel modal tasarımı yasak.

**Fiyat standardı:**
- Önce "Formlar > Fiyatlar"daki ortak tipler (`ms-fiyat` / `ms-urun-fiyat`) kullanılır; sayfaya özel fiyat class'ı yazılmaz.

**Build/süreç:**
- Kod değişikliği sonrası **build yapılmaz**, `npm run build-css` çalıştırılmaz, `site.css` yenilenmez — kullanıcı yapar.
- Temel karar değişikliğinde "Bunu skill'e ekleyeyim mi?" diye sorulur.
- Referans siteden ekran görüntüsü birebir kopyalanmaz; Misharix diline uyarlanır.

### 6.2 Agent rolleri (5)

| Agent | Sorumluluk |
|---|---|
| **Proje Mimari** | Koordinasyon, agent seçimi, skill hafızası, iş kapsamı; agent belirtilmezse devreye girer |
| **UI Component** | ProjeElementleri merkezli UI üretimi, `ms-` class arama/genişletme, responsive |
| **E-Ticaret ve SEO** | Liste/detay/sepet/ödeme akış tutarlılığı, h1/breadcrumb/alt denetimi, fiyat dili |
| **Backend Devir** | HTML + `data-ms-*` + config script sınırını koruyarak devir blokları hazırlama |
| **Kalite Kontrol** | `rg` ile statik tarama (çift h1, doğrudan hex, inline style, `href="#"`), responsive QA, teslim öncesi eksik listesi |

---

## 7. Controller'lar ve Route'lar

| Controller | İşlev |
|---|---|
| `HomeController` | Ana sayfa (sayfa seçici demo) |
| `UrunListesiController`, `UrunDetayController`, `UrunDegerlendirmeleriController` | Tek Index — partial wrapper |
| `SepetController` | `/sepet`, `/teslimat`, `/odeme`, `/siparis-tamamlandi` |
| `HesabimController` | 12 hesabım alt sayfası; her biri çift route (`/Hesabim/X` + kebab-case kısa yol) |
| `KurumsalController` | 7 kurumsal sayfa (`/hakkimizda`, `/iletisim`, ...) |
| `ProjeElementleriController` | Katalog Index + `Panel/SayfaPanel/HesabimPanel/KurumsalPanel` partial servis endpoint'leri |
| `AgentController` | `/agent` — agent md dosyalarını okuyup HTML gösterir |
| `GorselAramaController` | `POST /gorsel-arama` — ⚠️ dış servis `https://search.misharitalia.com/v1/search` (**API key kodda hardcoded**) + sonuçları legacy MySQL'den zenginleştirir |
| `FaturaController` | `GET /fatura/pdf` — `portal.doganedonusum.com` fatura sayfasından PDF çekip proxy'ler (host allowlist'li) |

**Fonksiyonel (backend'li) tek iki nokta** GorselArama ve Fatura'dır; gerisi saf tasarım.

---

## 8. Varlıklar (Assets)

- **`wwwroot/ikons/`** — 67 SVG (favori, sepet, kargo, iade, kupon, hesap, ödeme vb.). Tek onaylı ikon kaynağı. Yanında FontAwesome 7.2 lokal kopyası da yaygın kullanılıyor (`fa-solid` + `ms-fa-ikon`).
- **`wwwroot/images/`** — logo (`site_logo.png`), örnek ürün görselleri, 6 story görseli, sosyal medya + banka logoları, app-store rozetleri.
- **`wwwroot/video/test.mp4`** — "Videolu Ürün" rozeti demo videosu.
- **Demo ürün görselleri dış CDN'den**: `cdn.tozlu.com` ve `www.tozlu.com/banner/...` URL'leri partial'larda gömülü. Taşımada bunlar ECSPros'un kendi görsel URL'leriyle değişecek (zaten backend bağlama noktası).

---

## 9. Taşıma Açısından Kritik Gözlemler (ECSProsAI'ye aktarım için)

1. **Taşınacak birim tarifi hazır:** Her ekran için *partial HTML + `data-ms-*` attribute'ları + partial sonu config script* üçlüsü kopyalanır; ortak davranış site.js'ten gelir. Skill bunu açıkça "backend devir" kalıbı olarak tanımlamış.
2. **Teknoloji farkları:** Tasarım projesi **net9.0 MVC + Razor partial**; ECSProsAI storefront'u farklı bir stack kullanıyorsa Razor sözdizimi (partial include'lar, `asp-append-version`) hedef şablon motoruna çevrilmeli. HTML/CSS/JS katmanı framework'ten bağımsız.
3. **CSS tek dosya, tek build:** `tailwind.css` kaynak; `site.css` çıktı. Tailwind v4 CLI (`@tailwindcss/cli`) gerekir; `@source "../../Views"` yolu hedef projede güncellenmelidir (aksi halde kullanılan utility'ler üretilmez ve tasarım sessizce bozulur).
4. **site.js bölünmemeli:** Registry, lazy load, infinite scroll, kart davranışları birbirine `window.ms*` API'leriyle bağlı. Parça parça almak yerine bütün almak; sayfa entegrasyonlarını config script'lerle yapmak güvenli yol.
5. **Opt-in mekanizmaları:** `lazy-infinite-on` class'ı olmadan lazy/infinite çalışmaz — taşıma sonrası "çalışmıyor" görünen davranışların ilk kontrol noktası bu.
6. **Dinamik içerikte davranış yenileme:** API'den kart basıldıktan sonra `msUrunKartDavranislariYenile(kapsayici)` çağrılmazsa galeri/favori/video/tooltip ölü kalır.
7. **FontAwesome bağımlılığı:** İkonların bir kısmı `/ikons` SVG, bir kısmı FA 7.2. FA lokal klasörü de taşınmalı (CDN yok, CSP dostu).
8. **Duyarlı hassas veriler:** `appsettings.json` içinde canlı legacy MySQL bağlantı dizesi (juludedb, kullanıcı+şifre) ve `GorselAramaController.cs` içinde hardcoded API key var. Bunlar ana projeye **kopyalanmamalı / commit edilmemeli**; taşınırsa config'e alınmalı.
9. **ECSProsAI'de mevcut port durumu:** 2026-07-06'da bu tasarımın Faz 0 (Tailwind build) + Faz 1 (Nav/Home/Liste/Detay) portu storefront'a yapılmıştı. Bu doküman geri kalan yüzeylerin (Sepet/Checkout, Hesabım, Kurumsal, Değerlendirmeler, görünüm tipleri, mobil alt bar, görsel arama) envanterini de kapsar.
10. **Kural devri:** `project-basics.md`'deki kurallar tasarımın bütünlüğünü koruyan sözleşmedir; taşıma sonrası ana projede de (CLAUDE.md veya storefront skill'i olarak) yaşatılmalıdır — aksi halde ilk geliştirme turunda tasarım dili erir.
