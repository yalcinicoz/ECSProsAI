# Misharix → ECSPros Storefront: Razor Taşıma İş Planı

> Durum: **ONAYLANDI — uygulama başlamadı** (plan onayı: 2026-07-07)
> Karar: Storefront SPA'dan (store/index.html + app.js) çıkarılıp **Razor/MVC sunucu render**'a geçirilecek.
> Kaynak tasarım: `/opt/misharixWebSites/misharix` — envanter: `docs/misharix-tasarim-projesi-inceleme.md`
> Vitrin & kişiselleştirme spesifikasyonu: `docs/anasayfa-dizayn-yönetimi.txt` (kullanıcı dokümanı — Faz G bunun üzerine kuruludur)
> Hedef: Tasarımdaki **her element, her buton, her işlev** hiç bozulmadan taşınacak; ana projede olmayan işlevler backend'e **gerçek özellik** olarak eklenecek.
> Açık sorular 2026-07-07'de kullanıcıyla tek tek karara bağlandı — bkz. Bölüm 6 (Karar Kaydı).

---

## 0. BU PLANI KULLANMA TALİMATI (Claude için)

1. **Her session başında** bu dosyayı ve `docs/misharix-tasarim-projesi-inceleme.md`'yi oku; "Durum Panosu"ndan (Bölüm 1) kaldığın yeri bul.
2. Bir görevi bitirince satırındaki `[ ]`'i `[x]` yap ve sonuna tarih ekle: `[x] (2026-07-09)`. Kısmen bitti ise `[~]` + kısa not.
3. **Bir fazı bitirmeden sonrakine geçme** (kullanıcı kuralı). Faz sonundaki "Kabul Kriterleri"nin tamamı sağlanmadan faz kapanmaz.
4. Her faz kapanışında bu dosyanın Durum Panosu'nu ve `PROGRESS.md`'yi güncelle.
5. Bölüm 2'deki **Değişmez Kurallar**'a aykırı hiçbir şey yapma; kural ile görev çelişirse durup kullanıcıya sor.
6. Bölüm 8'deki **İşlev Envanteri** kutsal listedir: bir yüzey "bitti" sayılmadan önce o yüzeyin envanterindeki her satır tek tek doğrulanır.

---

## 1. DURUM PANOSU

| Faz | Konu | Durum |
|---|---|---|
| A | Razor host + kabuk altyapısı | ✅ Tamamlandı (2026-07-07) — 8080'de canlı, görüntü doğrulaması yapıldı |
| B | Navigasyon + Ana Sayfa + Liste + Detay (gerçek port) | 🔶 Devam ediyor — B1 nav + B2 arama + B3 duyuru (geçici statik) + B6 ana sayfa (geçici kompozisyon) + B7 ürün listesi + B8 kart derinleştirme + B9 ürün detay + B14 domain geçişi ✅ (2026-07-08); sırada B4-B5 (giriş modalları + mini sepet) / B10 (sunucu tarafı filtre-sıralama) / B13 (görsel QA) |
| C | Sepet + Checkout | ⬜ Başlamadı |
| D | Üye oturumu (Razor tarafı) + SMS/OTP altyapısı | ⬜ Başlamadı |
| E | Hesabım kümesi (12 sayfa + yeni backend özellikleri) | ⬜ Başlamadı |
| F | Kurumsal sayfalar + Footer | ⬜ Başlamadı |
| G | Vitrin & Kişiselleştirme Sistemi (G-M1: bloklar+yayınla · G-M2: kural motoru) | ⬜ Başlamadı |
| H | Özel yetenekler (görsel arama, fatura PDF, kargo takip, mobil alt bar) | ⬜ Başlamadı |
| İ | SPA emekliliği + son QA + kural devri | ⬜ Başlamadı |

---

## 2. DEĞİŞMEZ KURALLAR (taşımanın anayasası)

1. **HTML elden yeniden yazılmaz.** Partial dosyası misharix'ten **dosya olarak kopyalanır**; içine yalnızca Razor data-binding eklenir (demo metin/görsel → `@Model` alanı). Yapı, class, `data-ms-*` attribute'ları, element sırası **değiştirilmez**. "Sadeleştirme", "iyileştirme", "modernleştirme" yasak.
2. **`tailwind.css` ve `site.js` TAMAMI alınır** — 11.546 satır CSS, 4.388 satır JS. Kırpma/özet yok. (6 Temmuz hatası: site.js 1.150 satıra indirilmişti.)
3. **Tek doğru kaynak** `/opt/misharixWebSites/misharix`'tir. Tasarım değişikliği önce orada yapılır, sonra buraya senkronlanır. Ana projede partial'a tasarım müdahalesi yapılmaz.
4. **Drift kontrolü zorunlu:** Her faz kapanışında `tools/misharix-sync/check.sh` (Faz A'da yazılacak) çalıştırılır; kaynakla hedef arasındaki farklar sadece bilinçli data-binding satırları olmalıdır.
5. **Görsel doğrulama zorunlu:** Her sayfa için misharix ↔ ECSPros yan yana headless Chromium ekran görüntüsü (desktop 1440px + mobil 390px) karşılaştırılır (root'suz Chromium tarifi: `reference_headless_chromium_no_root.md`).
6. **Backend bağlama sözleşmesi:** Bir bloğu veriye bağlamak = partial'daki HTML + `data-ms-*` + partial sonu config script üçlüsünü korumak. Endpoint/veri `site.js`'e yazılmaz; sayfa config'i partial sonunda kalır.
7. Misharix skill kuralları geçerlidir: renk tokenları (`--ms-renk-primary`), `#333` siyah, `rounded-xl`, 13px başlık/12px metin, ikonlar sadece `/ikons` + FontAwesome, modal standardı `ms-ornek-modal` ailesi, semantik `<a>`/`<button>` ayrımı, tek `h1`.
8. **Hassas veri taşınmaz:** misharix `appsettings.json`'daki MySQL bağlantısı ve `GorselAramaController`'daki hardcoded API key kopyalanmaz; gerekli olanlar ECSPros config'ine (gitignore'lu) alınır.
9. Canlıda deneme-yanılma yok (`feedback_avoid_repeated_live_prod_iteration`); şema değişikliğinde migration + `database update` birlikte biter; toplu insert sonrası `ANALYZE`.
10. Build/publish/restart adımlarını kullanıcıyla koordine et (bu box'ta şifresiz sudo yok).

---

## 3. MİMARİ KARARLAR

### 3.1 Host: ECSPros.Api içine MVC (KARAR — 2026-07-07 kullanıcı onaylı)
- `ECSPros.Api`'ye `AddControllersWithViews()` + `Views/Store/…` eklenir; store sayfa controller'ları MediatR handler'larını **süreç içinde** çağırır (HTTP çift sekmesi yok).
- Artıları: tek systemd servisi, tek deploy, mevcut DI/modül kayıtları aynen kullanılır.
- Alternatif (istenirse): ayrı `src/ECSPros.StoreWeb` host'u — ayrı servis/port, modül DI kayıtlarının kopyası gerekir. Karar değişirse sadece Faz A etkilenir.
- Not: misharix net9.0, ana proje net8.0 — partial'lar düz cshtml, sorun beklenmiyor; net9'a özel `MapStaticAssets` kullanılmaz.

### 3.2 Yerleşim
```
src/ECSPros.Api/
├── Controllers/Store/            # Sayfa controller'ları (StoreHomeController, StoreUrunController...)
├── Views/Store/                  # misharix Views birebir kopyası (ProjeElementleri yapısı korunur)
│   ├── Shared/_StoreLayout.cshtml
│   └── ProjeElementleri/...      # kaynaktaki klasör adları AYNEN korunur (diff için şart)
└── wwwroot/store/                # css/site.css, js/site.js, ikons/, images/, video/, fontawesome/
```
- URL şeması misharix ile aynı tutulur: `/`, `/urun-listesi`, `/UrunDetay`, `/sepet`, `/teslimat`, `/odeme`, `/siparis-tamamlandi`, `/Hesabim/...`, `/hakkimizda`... (partial'ların içindeki linkler değişmeden çalışsın diye).
- Çok kanallı yapı (FirmPlatform) sayfa controller'larında host/config üzerinden çözülür; partial HTML'ine kanal mantığı sızmaz.

### 3.3 Veri bağlama modeli
- Her yüzey için ViewModel (`StoreUrunDetayVm` vb.) — partial'lar `@model` alır, demo değerler modelden gelir.
- Boş/eksik veri durumları tasarımdaki boş-durum blokları ile gösterilir (tasarımda çoğu var: "Reddedilen yorumunuz yok" vb.).
- İlk sayfa render sunucudan; sayfalama/filtre/infinite-scroll gibi devam yüklemeleri mevcut `api/store/*` endpoint'lerinden JSON ile (partial sonu config script'leri buraya bağlanır).

### 3.4 API-first kuralı (mobil app garantisi — 2026-07-07 kararı)
- **Mobil app `api/store/*`'ı kullanmaya devam edecek; Razor'a geçiş API'yi kaldırmaz.**
- Her özellik önce **Application handler'ı** olarak yazılır; API controller (mobil) ve Razor sayfa controller'ı (web) **aynı handler'ı** çağırır. Yeni özellikler (favori, yorum, koleksiyon, vitrin...) otomatik olarak mobil-hazır API olarak doğar.
- Web'in sayfa içi devam çağrıları (infinite scroll, sepete ekle, canlı arama, filtre) tarayıcıdan yine `api/store/*`'ı çağırır — API web tarafından da sürekli test edilir.

### 3.5 Tema mimarisi (2026-07-07 kararı: platform başına site sahibi seçer; ilk etap iskelet + token override)
- **Seviye 1 — görünüm farklılaşması:** Misharix tamamen token'lı; FirmPlatform'un tema ayarları (renk/logo/radius) layout'ta `:root` custom property override'ı olarak basılır. Kod değişmeden her site kendi kimliğini alır.
- **Seviye 2 — tam tema:** Tema = Views klasörü + asset seti. Faz A'da `Views/Store/Themes/misharix/` yapısı + `IViewLocationExpander` (istekteki FirmPlatform'un `ThemeCode`'una göre view çözümü) kurulur. Misharix **ilk tema**dır; ikinci tasarım geldiğinde `Themes/<yeni>/` olarak yanına eklenir, mevcut temaya dokunulmaz.
- Ziyaretçiye tema seçtirme kapsam dışı (platform sahibi seçer).

### 3.6 Yeni backend özelliklerinin evi
- Üyeye dönük etkileşim özellikleri **Storefront modülüne** eklenir (schema `storefront`, tablo adında şema tekrarı yok — `feedback_table_naming`):
  favoriler, koleksiyonlar, ürün yorumları, favori aramalar, gezinme geçmişi, stok haberi, ana sayfa vitrin bölümleri, story.
- SMS/OTP genel altyapısı Core/Shared tarafına (sağlayıcı soyutlaması + dev modda konsol/log sağlayıcısı).

---

## 4. FAZLAR VE ADIMLAR

### FAZ A — Razor host + kabuk altyapısı
> Amaç: Tek bir sayfa (ana sayfa iskeleti) Razor'dan, misharix CSS/JS/asset'leriyle piksel-doğru render olsun; drift ve ekran görüntüsü araçları hazır olsun.

- [x] A1. Host kararını kullanıcıya teyit ettir — **KARAR: ECSPros.Api içinde MVC** (2026-07-07).
- [x] A2. `ECSPros.Api`'ye MVC view desteği: `AddControllersWithViews()` (API JSON ayarları korunarak), Razor runtime compilation (dev), `UseStaticFiles`, Store sayfa controller'ları için `Controllers/Store/` alanı. (2026-07-07)
- [x] A3. Asset taşıma (birebir kopya): `ikons/`, `images/`, `video/`, `fontawesome-free-7.2.0-web/` → **`wwwroot/` kök ağacına** (plandaki `wwwroot/store/` yerine — bilinçli sapma: partial'lardaki `/ikons/...` mutlak yolları değişmeden çalışsın diye misharix kök yolları aynen korundu). (2026-07-07)
- [x] A4. `tailwind.css` birebir kopya (`@source "../../Views"` bizde de aynı yola çözülüyor — dosya bayt-bayt aynı); `package.json` (`store-css:build`) kuruldu ve test derlemesi yapıldı. **Yayınlanan `site.css` = misharix'in derlenmiş çıktısının birebir kopyası** (832.293 bayt, md5 aynı — tam kapsam garantisi; view'lar fazlarla eklendikçe yeniden derlenecek). (2026-07-07)
- [x] A5. `site.js` 4.388 satırın tamamı kopyalandı — md5 birebir aynı, hiçbir değişiklik yok. (2026-07-07)
- [x] A6. Layout: misharix `_Layout.cshtml` birebir alındı (ayrı `_StoreLayout` açılmadı — bilinçli sapma: bayt-eşitlik için orijinal ad/yol korundu); nav (10 partial) + footer + görsel arama modal + arama ürün kartı statik kopya olarak render ediliyor. Tek fark: A12 tema hook satırı (allowed-diffs.txt'te kayıtlı). (2026-07-07)
- [x] A7. Drift kontrol aracı `tools/misharix-sync/check.sh` + `allowed-diffs.txt` — çalıştırıldı, TEMİZ ✓ (tek izinli fark: _Layout tema satırı). (2026-07-07)
- [x] A8. Ekran görüntüsü aracı `tools/misharix-sync/screenshot.mjs` (playwright-core + CHROME_PATH; desktop 1440 + mobil 390, konsol hatası raporlu). Chromium kurulumu ilk QA'da yapılacak (`reference_headless_chromium_no_root.md`). (2026-07-07)
- [x] A9. Nginx paralel yayın: compose'a `8080:8080` + `default.conf`'a 8080 server bloğu (→ host:5000); kullanıcı publish + restart + `up -d nginx` çalıştırdı, **http://51.178.208.59:8080 canlı** ve doğrulandı. Not: certs `:ro` volume mount'u artık ÇALIŞIYOR — manuel cert kopyalama gereksiz (ve ro olduğu için mümkün de değil); eski bug geçersiz. SPA `/`'ta aynen duruyor. (2026-07-07)
- [x] A10. Eşleme tablosu: `docs/misharix-partial-vm-eslemesi.md` (A fazı satırları işlendi, sonraki fazların şablonu hazır). (2026-07-07)
- [x] A11. **Tema iskeleti**: `StoreThemeViewLocationExpander` kuruldu — varsayılan tema (misharix) kök `~/Views/` ağacında yaşar (bilinçli sapma: partial'lardaki `~/Views/...` mutlak referansları bayt-aynı kalsın diye `Themes/misharix/` alt klasörü yerine kök; ikinci tema `Views/Themes/{kod}/` altına gelir, bulunamayan view köke düşer). Tema kodu **FirmPlatform.Settings JSONB'sinde `theme` anahtarı** (bilinçli sapma: ayrı ThemeCode kolonu/migration yerine mevcut Settings — canlı tabloya kolon eklenmedi). (2026-07-07)
- [x] A12. **Site bazlı görünüm override**: `IStoreContext` (host→platform çözümü: `Store:Hosts:{host}` → `Store:DefaultFirmPlatformCode` config'i, 5 dk IMemoryCache) + `_MsTemaTokenlari.cshtml` partial'ı — `Settings.themeTokens`'taki `--ms-*` anahtarlarını (CSS-injection süzgeçli) `:root`'a basar; platform/token yoksa hiçbir şey basmaz. Farklı `--ms-renk-primary` doğrulaması platform kodu config'e girilince yapılacak (B1). (2026-07-07)

**Kabul kriterleri:** Boş ana sayfa iskeleti (nav+footer statik) Razor'dan render oluyor; CSS pixel-diff temiz; `check.sh` sıfır beklenmeyen fark raporluyor; SPA etkilenmedi.
**Doğrulama (2026-07-07):** 5051 portunda Production modda duman testi — `/` 200 (nav+footer tam HTML), `/css/site.css` 200 (832.293 bayt, misharix ile md5 aynı), `/js/site.js` 200, ikonlar 200, `api/store/*` 200 (API etkilenmedi), log'da 0 hata, Redis AKTİF. `check.sh` TEMİZ ✓. **Deploy sonrası (kullanıcı):** 8080 canlı; headless Chromium ile desktop (1440) + mobil (390) ekran görüntüleri alındı ve görsel olarak doğrulandı — duyuru barı, arama+kamera, mega menü bandı, mobil hamburger/kategori şeridi/footer akordiyonları tasarımın birebir aynısı. Tek konsol 404'ü favicon'du → kaynaktan kopyalandı (bir sonraki publish'te canlıya gider). Henüz taşınmamış sayfalara giden linkler beklendiği gibi 404 (Faz B–F'te dolacak).

---

### FAZ B — Navigasyon + Ana Sayfa + Ürün Listesi + Ürün Detay (gerçek port)
> Amaç: 6 Temmuz'da "yorumlanarak" yapılan dört yüzeyi, bu kez partial'lar birebir alınarak canlı veriye bağlamak. SPA'daki karşılıkları bundan sonra kullanılmaz.

- [x] B1. **Navigasyon** — TAMAM (2026-07-07):
  - **Veri kararı:** `nav_menus` tablosu TÜM platformlarda BOŞ — navigasyon menüden değil, **kanal kategorilerinden** beslenir (SPA da öyle yapıyordu). Mishar: 4 kök (Kadın 25 / Erkek 4 / Çocuk & Bebek 8 / Ev & Yaşam 6) + 43 çocuk (2 seviye), hiçbir kategoride `DisplayImageUrl` yok → grid öğelerinde `<img>` koşullu render. Kategori linki `/{slug}` (sayfa B7'de gelecek; o zamana dek 404 normal). `GetChannelCategoriesQuery(platformId, activeOnly:true)` düz liste döner; ağaç host'ta kurulur.
  - **Altyapı:** `Models/Store/NavigasyonVm.cs` (NavKategori/NavigasyonVm + UcuncuSeviyeVar), `Controllers/Store/StorePageController.cs` (taban controller — `ViewData["MsNavigasyon"]` + `ViewData["MsPlatform"]`, platform başına 5 dk IMemoryCache), `HomeController : StorePageController`, appsettings `Store:DefaultFirmPlatformCode="mishar"`.
  - **Veriye bağlanan partial'lar (markup sınıfları birebir korunarak):** `_AnaNavigasyonDesktopMenu.cshtml` (mega gruplar + üst şerit linkleri; ilk grup `-varsayilan`; UcuncuSeviyeVar dallanması hazır; kampanya şeridi STATİK — Faz G), `_AnaNavigasyonMobilMenu.cshtml` (ana sekmeler=kökler, 2 seviyede tek yan sekme+tek panel; kampanya bölümü + alt nav statik), `_AnaNavigasyonUst.cshtml` (yalnız mobil kategori kaydırma şeridi; sepet/oturum kısmı B5/D6'ya kaldı). Üçü de gerekçeyle `allowed-diffs.txt`'e eklendi.
  - **Doğrulama:** build 0 hata; 5051 Production duman testi — 4 kök grup + 43 grid linki (desktop ve mobil ayrı ayrı), gerçek `/{slug}` href'leri, `check.sh` TEMİZ ✓; headless Chromium ile mega menü hover (Kadın varsayılan + Erkek grup geçişi) ve mobil menü (sekme geçişi Çocuk & Bebek) ekran görüntüleriyle doğrulandı, 0 konsol hatası. `api/store/*` regresyon yok (menus/header 404'ü canlıyla aynı — nav_menus boş, bilinen durum).
  - **UYARI (hâlâ geçerli):** SPA'nın `store/js/app.js`'teki mega menü markup'ı ÖRNEK ALINMAZ — orijinalde olmayan yapılar uydurmuş; orijinal misharix markup'ı korundu (JS `ms-magaza-mega-sol-kolon`u runtime'da kendisi kurar, partial'a yazılmaz).
- [x] B2. Navigasyon içi **arama paneli** — TAMAM (2026-07-07): canlı öneri `_AnaNavigasyonSearch.cshtml` sonundaki script'le (misharix'in görünürlük script'i değişmedi — bizimki DOMContentLoaded'da bağlanıp ondan sonra çalışır; debounce 300ms, min 2 karakter, eski-cevap koruması). Ürünler `products?search`'ten (10 kart + toplam sayaç + "Tümünü Gör" → `/urunler?search=`, ürün linki `/urun/{code}` — B7/B9'a kadar 404 normal); kategori önerileri nav ağacından client-side ("Kök › Çocuk" etiketiyle — çift "Elbise" karışıklığı önlendi). **Backend:** arama artık kod VEYA Türkçe ad eşleşmesi (`PgJsonFunctions.JsonText` → `jsonb_extract_path_text` DbFunction eşlemesi, CatalogDbContext'te kayıtlı; Dictionary indexer'ı dinamik JSON'da çevrilmediği için) — GetStoreProducts + GetStoreFacets tutarlı; ~0.7-0.85s/sorgu (gerekirse ileride pg_trgm index). Popüler aramalar: gerçek terimli geçici statik chip'ler; popüler ürünler: ilk ürünler (15 dk sessionStorage) — ikisinin kalıcısı E11. Son aramalar: localStorage (6 kayıt, temizle butonu çalışır). **"Kategoride ara":** buton yalnız kategori sayfalarında görünür (misharix kuralı `!anaSayfaMi`), kategori sayfası B7'de geleceği için kapsam daraltma B7'de bağlanacak — envantere not düşüldü. E2E doğrulama: headless Chromium — panel açılışı, "elbise" (8045 ürün + 2 kategori chip), sonuçsuz arama mesajı, 0 konsol hatası. NOT: bazı ürün görselleri CDN'de yok (kısa slug'lı .jpg'ler "RESİM HAZIRLANIYOR" placeholder'ı dönüyor) — B2'den bağımsız, önceden var olan veri sorunu.
- [x] B3. **Duyuru şeridi** — TAMAM (2026-07-08, geçici statik): `_AnaNavigasyonDuyuru.cshtml` zaten her sayfada statik render ediliyordu (B1'den beri `_AnaNavigasyon` kompoziti içinde); demo marka metni ("Misharitalia") mishar'a uyarlandı, allowed-diffs'e gerekçeli eklendi. Sağ linkler (Kargo Takip → Faz H, Yardım & Destek → Faz F) ve uygulama linkleri `#` — ilgili fazlarda bağlanacak. **Kalıcısı Faz G kişiselleştirme sisteminin global "duyuru" bloğudur** (K7 kararı) — G8'de bu geçici çözüm kaldırılır.
- [ ] B4. Giriş/kayıt modalları ve hesap paneli **UI olarak** taşınır (davranış Faz D'de canlanır; şimdilik mevcut store auth login/register API'sine e-posta yoluyla bağlanabilir, SMS sekmesi D'ye kadar pasif etiketli).
- [ ] B5. Mini sepet (hover panel) → `GET /api/store/cart`'a bağla.
- [x] B6. **Ana sayfa** — TAMAM (2026-07-08, geçici kompozisyon): sayfa seçici kaldırıldı; `Home/Index.cshtml` GorunumTipleri bloklarından kuruldu — **Kapsül Kategori Şeridi** (kök kanal kategorileri; görsel = kategori görseli yoksa ilk ürün görseli, görselsiz kapsül basılmaz; mobilde `ms-gorunum-banner-mobil-carousel`) + **Standart Carousel × kök kategori (ilk 3)** (kategori başına ilk 10 ürün, "Tümünü Gör" → `/{slug}`, tema `varsayilan`). Banner bloğu bilinçli atlandı (banner görseli yok — Faz G'de vitrin sisteminden gelecek). Veri: `HomeController` → `GetChannelCategoryProductsQuery` kök başına **paralel** (görev başına ayrı DI scope — scoped DbContext paylaşılamaz), `AnaSayfaVm` platform başına 15 dk IMemoryCache. **Kart tek kaynağa alındı:** B7'nin `UrunKarti` local function'ı `ProjeElementleri/Urun/_UrunKarti.cshtml` paylaşılan partial'ına taşındı (liste SSR + `<template>` + ana sayfa carousel aynı markup; `UrunKartMap` dönüştürücüsü Models/Store'da). Kart davranışları site.js bootstrap'inde yalnız `data-ms-infinite-liste` konteynerlerine bağlandığından sayfa sonuna `msUrunKartDavranislariYenile` çağıran config script'i eklendi (DOMContentLoaded'a ertelenir — site.js body sonunda yükleniyor). `check.sh`'a "İZİNLİ YENİ" durumu eklendi (kaynakta karşılığı olmayan bilinçli dosyalar allowed-diffs ile). E2E (headless Chromium, 5051 Production **publish çıktısından** — bin/Release'te wwwroot yok, statikler 404 olur, test oradan yapılmaz): desktop 19/19 ✓ (kapsüller, 3 carousel, ok kaydırma, hover galeri + tooltip + ?color= detay linki, /kadin infinite scroll 24→72 paylaşılan partial'la, 0 konsol hatası) + mobil 6/6 ✓ (kapsül yatay carousel, duyuru bar mobilde gizli). G8'de bu geçici kompozisyon kaldırılır.
- [x] B7. **Ürün Listesi** — TAMAM (2026-07-07): üç yüzey tek `UrunListesiController` + `UrunListesiVm` ile — kategori `/{slug}` (regex kısıtlı `[a-z0-9-]+`; kısıtsız hali /favicon.ico gibi kök statik dosyaları yutuyordu — örtük UseRouting endpoint'i pipeline başında eşleştirince StaticFileMiddleware devre dışı kalıyor), arama `/urunler?search=` (B2 "Tümünü Gör" hedefi), tümü `/urun-listesi`. **İlk sayfa SSR** (plan 3.3; kart markup'ı `UrunKarti` local function — SSR ve `<template>` aynı markup), devamı misharix infinite-scroll modülü `kartHazirla`/`sonra` hook'larıyla (`ilk:0`, gerçek toplam; iskelet kartlar JSON gelince dolar). Facet'ler controller'da süreç içi MediatR'dan SSR (kategori facets Redis 0.01s; tüm-katalog ilk çağrı ~5-6s sonra 15dk IMemoryCache — sadece /urun-listesi'ni etkiler). **Filtre/sıralama SPA paritesiyle client-side** (seçili valueId'ler kartın colors+attrs id'leriyle OR; fiyat min/max + hazır aralıklar; fiyat artan/azalan sıralama; sayaç günceller) — sunucu tarafı filtre/sıralama B10 ile gelmeli. Sol filtre: Kategori bloğu alt/kök kategorilere gider; mobil panel/chip/detay markup'ları gerçek facet gruplarından üretilir (`anaFiltreAdlari` bağlandı); veri karşılığı olmayan demo blokları (hızlı filtre chip'leri, kampanya bloğu, kart puan/teslimat/video/sponsor) `@if` ile gizli (B8/B10/B11/Faz G). E2E: headless Chromium — Kadın 24 SSR kart → scroll 48 (24'ü JSON'la doldu), renk filtresi 33/15 + sayaç, fiyat sıralaması artan, mobil filtre panelleri, 0 konsol hatası; drift TEMİZ (5 izinli B7 girdisi).
  - NOT: **"Kategoride ara" kapsam daraltması yine ertelendi** — backend'de kategori+arama birleşik sorgu yok (`products?search` kategori almıyor, `channel-categories/{id}/products` arama almıyor); B10'da sorgu genişleyince bağlanmalı.
- [x] B8. **Ürün kartı derinleştirme** — TAMAM (2026-07-08): (1) **Hover görsel galerisi + nokta göstergeleri** — kartın (seçili) rengine ait görsel havuzunun ilk 4'ü `data-ms-urun-galeri-resimler`'e | ayraçlı verilir (misharix site.js modülü mousemove/touch ile gezdirir); renk çözülemiyorsa galeri verilmez (farklı renklerin karışık havuzu "tekrarlı galeri" üretir — detay handler'ı dersi). Kategori tarafı `GetChannelCategoryProductsQuery`'de varyant başına tek görsel yerine (ProductId,ColorValueId)→görsel havuzu; arama tarafı `GetStoreProductsQuery`'de ana görselin VariantId'sinden renk çözülür. (2) **Renk tooltip'i** ("Farklı Renk Seçenekleri") — eksen (renk) kartları kendi görselleri ve `/urun/{code}?color={eksenDeğerId}` linkleriyle (ilk 4 desktop, tam liste mobil alt panel; rozet tıklaması mobilde bottom-sheet açar); **görselsiz renkler listelenmez** (B9 kuralı — detay onları göstermediğinden linklenirse ilk görünür renge düşüp kullanıcıyı yanıltırdı, E2E yakaladı); eksen verisi yoksa filtre_rengi'ne düşer. (3) **Kart → detay linki artık renk taşır** — kategori kartları `?color={eksenDeğeri}`; `UrunDetayController` eksen-dışı değeri de (filtre_rengi bucket) o değeri taşıyan varyantın eksen rengine çözer. (4) JSON kartlarda aynı markup enjekte edilir (tooltip rozetten ÖNCE — site.js rozeti bağlarken tooltip alanı yoksa bir daha bağlamaz; galeri "hazır" bayrağı silinir ki `msUrunKartDavranislariYenile` yeniden bağlasın). DTO'lar additive genişledi: `ProductListingColorDto.ImageUrl`, `StoreProductDto.GalleryUrls`, `ChannelCategoryProductItemDto.GalleryUrls/AxisColors`. Puan/teslimat/kargo/kampanya alanları veri kaynağı olmadığından `@if` ile gizli kaldı (B11/E7/Faz G). E2E (headless Chromium, 5051 Production): desktop 20/20 ✓ (SSR+JSON hover galerisi, nokta göstergeleri, tooltip aç/kapa, tıklanan renk = detayda seçili renk, regresyon, 0 konsol hatası) + mobil 5/5 ✓ (bottom-sheet panel, kapat butonu).
- [x] B9. **Ürün Detay** — TAMAM (2026-07-08): `/urun/{code}?color={valueId}` tamamen SSR — `UrunDetayController` + `UrunDetayVm`; renk değişimi ?color= navigasyonudur (SSR yeniden yükleme), beden seçimi client-side (misharix script'i değişmedi), sepete ekleme partial sonundaki config script'iyle `api/store/cart/items` (SPA ile aynı localStorage anahtarları: ecspros_sid/ecspros_cart; Şimdi Al da şimdilik sepete ekler — gerçek akış Faz C). 5 partial + Index bağlandı (allowed-diffs'te gerekçeli). **Breadcrumb**: yeni `GetProductChannelCategoryChainQuery` — kategoriler filtre tanımlı olduğundan ters eşleme kural değerlendirmesiyle (ProductGroupIds + AttributeFilters, listelemeyle aynı semantik; manuel atama/IsExcluded dahil; en derin aday kazanır). **DTO genişledi (additive)**: `StoreProductDetailDto`'ya DescriptionI18n + Attributes (ürün seviyesi) + ProductGroupNameI18n eklendi (api/store/catalog/products/{code} aynı endpoint — mobil etkilenmez). Beden sıralaması konfeksiyon sırasına göre (S<M<L<XL…, numerikler sayısal); sıfır fiyatlı varyantlar gösterim fiyatına girmez (SPA paritesi); "renk"/"filtre_rengi" eksenleri asla beden sanılmaz; filtre_rengi yoksa "renk" ekseni renk kabul edilir. **Canlı bug düzeltildi (B9 dışı ama kabul kriteri)**: `AddToCartCommand` mevcut sepete ikinci farklı ürünü hiç ekleyemiyordu (tracked cart koleksiyonuna Id'li yeni satır → EF Modified sanıp UPDATE, DbUpdateConcurrencyException) — `db.CartItems.Add`'e çevrildi. Gizlenenler (@if): puan tooltip demo, dönen teslimat mesajları (B8/B11), çoklu fiyat senaryoları (Faz G), model ölçüleri (manken verisi ürünlerde yok), teslimat bilgileri (Faz H), beden tablosu (veri yok), video/etiketler (B11/G). **Benzer ürünler bölümü misharix detay tasarımında yok** — envanterdeki satır Faz G vitrin sistemine devredildi. E2E (headless Chromium): 12/12 ✓ — galeri thumb/slide, beden seçimi+modal, sepete ekle API 200 (bedenli+bedensiz akış), renk SSR değişimi, görsel modalı, paylaş modalı gerçek adla, mobil sabit bar + sticky beden; 0 konsol hatası; drift TEMİZ (6 izinli B9 girdisi).
- [ ] B10. Sıralama seçenekleri: store products sorgusunda mevcut sıralamaları doğrula, eksikse (fiyat artan/azalan, en yeni, çok satan*) ekle (*veri yoksa etiketle birlikte kullanıcıya sor).
- [ ] B11. **"Öne çıkar" bayrağı** (K8 kararı): ChannelProduct'a tarih aralıklı öne çıkarma alanı + admin'den işaretleme + listede sıralama önceliği + karttaki "Sponsorlu" rozeti buna bağlanır.
- [ ] B12. **Stok kontrolü anahtarı** (stok kararı): kod gerçek stoğu okur; FirmPlatform'da "stok kontrolü kapalı" anahtarı varken her şey satılabilir görünür (bugünkü veri durumu için varsayılan: kapalı). Stok verisi dolunca anahtar açılır, kod değişmez.
- [ ] B13. Görsel + davranış QA: dört yüzeyin ekran görüntüsü karşılaştırması + Bölüm 8.1–8.4 envanter satırlarının tek tek işaretlenmesi.
- [x] B14. Nginx'te `/` Razor'a çevrildi (2026-07-07 — kullanıcı kararıyla ERKEN geçiş, dört yüzey tamamlanmadan): `locations.inc` `/` bloğu artık host:5000'e proxy (https://new.ecspros.com Razor'ı sunuyor, Cloudflare Flexible 80 üzerinden); eski SPA yedeği 8080 portuna taşındı (rol değişimi — api+media+statik blokları eklendi; Faz İ'de kaldırılacak); appsettings `Store:Hosts["new.ecspros.com"]="mishar"`. NOT: B7'ye kadar kategori linkleri 404, ana sayfa geçici sayfa seçici — bilinçli kabul.

**Kabul kriterleri:** Dört yüzey pixel-doğru + canlı veriyle çalışıyor; envanter 8.1–8.4 tüm satırlar ✅/🕐(ileri faz) işaretli; drift raporu temiz.

---

### FAZ C — Sepet + Checkout
> Mevcut backend: cart CRUD+merge, checkout POST, adresler, kupon validate/use → çoğu VAR; eksikler taksit, ödeme sağlayıcı, TCKN, stok haberi.

- [ ] C1. `_SepetSayfasi` birebir port → cart API (adet artır/azalt, satır sil onay modalı, tümünü seç, satır tutarları).
- [ ] C2. "Favoriye taşı" butonu: Faz E5 favorilere bağımlı — UI taşınır, E5'e kadar gizli/pasif işaretle (envantere not düş).
- [ ] C3. Kupon modalı: kupon kodu uygula/kaldır → `promotion/coupon/validate` + `use`; üyenin kullanılabilir kupon listesi (E9'daki "kuponlarım" API'siyle ortak) — yoksa önce kod-girişli akış.
- [ ] C4. `_SepetTeslimatSayfasi`: adres listesi/seçimi → account addresses; adres ekleme/düzenleme modalı (telefon ülke-kodlu input). **Adres hiyerarşisi (K6 kararı): Core modülünde `countries` → `provinces` (il + bölge alanı) → `districts` (ilçe) → `neighborhoods` (mahalle + posta kodu) tabloları + resmi veri seed'i (PTT veri seti) + kademeli aramalı-select API'si** (mahalle listesi büyük — tasarımın `data-ms-ozel-select-arama` bileşeni kullanılır). Aynı kaynak kişiselleştirme şehir seçicisini ve profil şehrini de besler.
- [ ] C5. `_SepetOdemeSayfasi`: ödeme yöntemleri (kart formu canlı önizleme, kapıda ödeme, havale) — **K2 kararı: test modu** (sipariş oluşur, tahsilat mock); sağlayıcı seçilince H6'da gerçek entegrasyon.
- [ ] C6. Taksit listesi (`data-ms-taksit-*`): K2 gereği statik/konfigüratif taksit tablosu; sağlayıcı gelince gerçek BIN sorgusu (H6).
- [ ] C7. TCKN doğrulama modalı — **K9 kararı: format + algoritma kontrolü** (11 hane + kontrol basamakları), backend'de saklama; NVİ/KPS resmi doğrulaması ileriye bırakıldı.
- [ ] C8. Sözleşme modalları (mesafeli satış, ön bilgilendirme…): CMS pages'ten içerik; sipariş onayında kabul kaydı (Order note/alan).
- [ ] C9. "Stok gelince haber ver" (`data-ms-stok-haber-ver`): yeni Storefront özelliği — `stock_alerts` tablosu + API + (bildirim gönderimi H/ileri faz).
- [ ] C10. `POST /api/store/checkout` uçtan uca: sepet → sipariş → sipariş tamamlandı sayfası (`_SepetSiparisTamamlandiSayfasi`).
- [ ] C11. QA: Bölüm 8.5 envanteri satır satır + görsel karşılaştırma.

**Kabul kriterleri:** Üye, sepetten sipariş tamamlandı sayfasına kadar gerçek akışı yürütüyor (ödeme test modu kabul); envanter 8.5 işaretli.

---

### FAZ D — Üye oturumu (Razor) + SMS/OTP altyapısı
> Backend auth VAR (register/login/refresh/me). Eksik: Razor tarafında oturum yönetimi, SMS ile giriş/doğrulama, şifre güvenliği.

- [ ] D1. Razor oturum stratejisi: store JWT'sini HttpOnly cookie'de taşı (sayfa render'da kimlik) + JS tarafına fetch'ler için mekanizma; refresh akışı.
- [ ] D2. Giriş modalı canlandırma: e-posta/şifre sekmesi → `store/auth/login`; hata/başarı durumları tasarımdaki bildirim elementleriyle.
- [ ] D3. Kayıt modalı: form → `store/auth/register`; KVKK/üyelik sözleşmesi belge modalı (CMS'ten metin) + onay kaydı.
- [ ] D4. **SMS/OTP altyapısı** (yeni): sağlayıcı soyutlaması (`ISmsSender`), dev'de log sağlayıcısı; OTP üretim/doğrulama (süre+deneme sınırı). Telefonla giriş sekmesi (`data-ms-giris-sms-*`, kod kutuları, geri sayım, yeniden gönder) buna bağlanır. Gerçek sağlayıcı seçimi kullanıcı kararı.
- [ ] D5. CRM member şifreleri SHA256 → BCrypt geçişi (ilk girişte re-hash stratejisi).
- [ ] D6. Hesap paneli (nav'daki): giriş/çıkış durumuna göre panel içerikleri; çıkış → session iptali.
- [ ] D7. QA: Bölüm 8.1'in auth satırları + oturumlu/oturumsuz nav görüntü karşılaştırması.

**Kabul kriterleri:** E-posta ve telefon(OTP-dev) ile giriş/kayıt/çıkış çalışıyor; oturum sayfa render'ına yansıyor.

---

### FAZ E — Hesabım kümesi (12 sayfa)
> Her sayfa = partial birebir port + üye-kapsamlı API + gerekiyorsa yeni backend özelliği. Yan menü (`_HesabimYanMenu`) + mobil menü ilk iş.

- [ ] E1. Hesabım çerçevesi: yan menü + route'lar (`/Hesabim/...` + kebab-case kısa yollar, misharix'teki çift route şeması).
- [ ] E2. **Üyelik Bilgilerim** → profile GET/PUT (telefon ülke-kodlu input, şifre değiştirme, TCKN alanı). **+ Cinsiyet ve şehir alanları** (kişiselleştirme segmenti bunlardan beslenir — G9 bağımlılığı; cinsiyet yalnızca profilde varsa kullanılır, tahmin yok).
- [ ] E3. **Adreslerim** → addresses CRUD (C4'teki modal yeniden kullanılır).
- [ ] E4. **Siparişlerim** → account orders (+detay modal); durum filtre chip'leri; kargo takip modalı (H2'ye köprü — takip verisi yoksa "kargo firması+takip no" gösterimi); fatura PDF modalı (H1'e köprü).
- [ ] E5. **Favorilerim** (YENİ backend): `favorites` tablosu (MemberId+ProductId/VariantId unique) + API (ekle/çıkar/listele) + ürün kartlarındaki kalp butonlarının tümü buna bağlanır (liste, detay, sepet "favoriye taşı" dahil).
- [ ] E6. **Koleksiyonlarım** (YENİ backend): `collections` + `collection_items`; oluştur modalı (ad, açıklama, herkese açık, paylaşılabilir link); kartlardaki koleksiyon (bookmark) butonları bağlanır. **+ Moderasyon/onay durumu** (pending/approved) ve admin onay ekranı — Faz G "Koleksiyonlar bloğu" yalnızca onaylı+herkese açık koleksiyonları gösterebilir (spec şartı).
- [ ] E7. **Yorumlarım + Ürün Değerlendirme modülü** (YENİ backend — en büyük kalem):
  - `product_reviews` (üye, ürün, sipariş kalemi ilişkisi, puan, metin, foto?, durum: pending/approved/rejected + red nedeni)
  - Store API: yorum yaz (satın alma şartı), listele (ürün bazlı, sayfalı), "yorumlarım" (3 sekme: yayında/onay bekleyen/reddedilen)
  - Admin API+UI: moderasyon kuyruğu (onay/red)
  - Ürün kartı/detay puanları artık gerçek ortalamadan.
- [ ] E8. **İadelerim + iade talebi akışı**: mevcut returns API'sini üye-kapsamlı tamamla (store'dan `POST` iade talebi); iade modalları (ana/alt neden seçimi — neden listesi Lookup'a, görsel yükleme, SMS doğrulama → D4, iade kodu üretimi/kopyalama, kargo durum adımları).
- [ ] E9. **İndirim Kuponlarım** (backend eki): üyeye tanımlı/kullanılabilir kupon listesi API'si (Promotion'a member ilişkisi) + sayfa; C3 kupon modalı da bu listeden beslenir.
- [ ] E10. **Tekrar Satın Al**: geçmiş sipariş kalemlerinden türetilen liste + "sepete ekle" toplu aksiyonu.
- [ ] E11. **Favori Aramalarım** (YENİ backend): `saved_searches` (sorgu+filtre JSON) + kaydet/sil/çalıştır; arama panelindeki "popüler aramalar" da bu tablonun agregasyonundan beslenebilir (B2'deki geçici kaynak kaldırılır).
- [ ] E12. **Önceden Gezdiklerim** (YENİ backend): `viewed_products` (üye+ürün+zaman, son N kayıt); detay sayfası render'ında kayıt; misafir için localStorage fallback (partial config script'inde).
- [ ] E13. **Hesabım ana sayfa** (`_HesabimVarsayilan`): özet kartları (son sipariş, kupon sayısı, favori sayısı…) hazır API'lerden; wallet/loyalty widget'ları mevcut API'lere bağlanır.
- [ ] E14. QA: Bölüm 8.6 envanteri satır satır + 12 sayfanın görüntü karşılaştırması.

**Kabul kriterleri:** 12 sayfa canlı veriyle çalışıyor; 5 yeni backend özelliği (favori, koleksiyon, yorum, favori arama, gezinme geçmişi) migration'larıyla + admin moderasyon (yorum) ile tamam.

---

### FAZ F — Kurumsal + Footer
- [ ] F1. `_KurumsalSayfasi` çerçevesi (yan menü + tab + `data-ms-lazy-panel-url` lazy panel yüklemesi) → CMS pages'e bağla; 7 sayfanın içerikleri CMS'e girilir (admin'den düzenlenebilir).
- [ ] F2. SSS akordiyonu: CMS içerik yapısıyla (soru/cevap listesi) uyumlu render.
- [ ] F3. İletişim sayfası: form → basit mesaj kaydı (Storefront `contact_messages` — YENİ, küçük) veya e-posta; kullanıcıya sor.
- [ ] F4. `_Footer` (127 satır) birebir; footer menüleri nav menus `footer` kodundan; abonelik (bülten) formu → YENİ küçük tablo `newsletter_subscriptions` + API.
- [ ] F5. QA: Bölüm 8.7 envanteri + görüntü karşılaştırma.

**Kabul kriterleri:** Kurumsal içerikler admin/CMS'ten yönetiliyor; footer tüm linkleriyle canlı.

---

### FAZ G — Vitrin & Kişiselleştirme Sistemi
> **Spec: `docs/anasayfa-dizayn-yönetimi.txt`** — blok bazlı, segment kurallı, taslak→Yayınla→**versiyonlu JSON snapshot**→rollback mimarisi. Canlı site admin/taslak tablolarına asla join atmaz, sadece aktif snapshot okur.
> **Yerleşimler (2026-07-07 kararı):** anasayfa (tüm sayfa) · global üst alan (duyuru şeridi) · ürün listesi/kategori (üst+alt) · ürün detay (alt) · sepet/teslimat/ödeme sayfaları.
> **Teslimat: iki milestone** (karar). G-M1 sonunda anasayfa canlıya çıkabilir (bloklar kuralsız=herkese); G-M2 kişiselleştirmeyi açar.

**Blok paleti** (kullanıcı listesi + spec birleşimi; kural seviyeleri spec'e göre):
| Blok | Şablon/varyantlar | Kural seviyesi |
|---|---|---|
| Banner | Tekli / İkili / Üçlü / Dörtlü / Beşli / Reklam / Bilgi / İkon / Çoklu | Blok |
| Slider | tek genel şablon | Öğe (slide) |
| Story | — | Öğe (story) |
| Carousel Ürün Listesi | Standart / Özel Fiyatlar / Flash Ürünler | Blok + ürün kaynağı/filtresi |
| Infinity Ürün Listesi (Grid) | — | Blok + ürün kaynağı/filtresi |
| Tabs | — | Blok + öğe (tab) |
| Koleksiyonlar | — | Blok + koleksiyon kaynağı/filtresi |
| Categories / Brands / Instagram | — | Blok (spec'e ek — mimari aynı) |
| Duyuru (global şerit) | — | Öğe (duyuru satırı) |

**G-M1 — Blok sistemi + yayınlama (kurallar hariç, her şey herkese görünür):**
- [ ] G1. Veri modeli (Storefront şeması): blok tablosu (FirmPlatformId, **yerleşim**, tip, şablon/varyant, başlık/alt başlık, sıra, aktif, tarih aralığı, öncelik, config JSONB) + blok öğeleri tablosu (slide/story/tab/banner-öğesi/duyuru: içerik, mobil+desktop görsel, link, sıra, aktif, kural alanı, tarih, öncelik) + `published_snapshots` (Version, JsonData, PublishedAt/By, IsActive, Status, Note) + `publish_logs` (PreviousVersion, Status, ErrorMessage). Taslak/yayın ayrımı baştan kurulur.
- [ ] G2. Tüm blok tipleri + şablonları model olarak tanımlanır (yukarıdaki palet); banner şablonları layout bütünlüğü için blok seviyesinde (spec gerekçesi).
- [ ] G3. **Ürün kaynağı/filtre motoru**: kaynaklar (çok satanlar, yeni gelenler, kampanyalı, kategori bazlı, marka bazlı, manuel liste; *veri geldikçe açılır:* son gezilenler→E12, favorilere eklenenler→E5, önerilen ürünler→ileride) + filtreler (kategori, marka, stok, fiyat aralığı, indirim, etiket) + limit + sıralama. Koleksiyon kaynağı: yalnızca onaylı+herkese açık (E6).
- [ ] G4. Store API: `GET /api/store/pages/{yerlesim}` — aktif snapshot'tan çözülmüş blok dizisi (ürün blokları kaynak konfigürasyonundan doldurulur). **Mobil app da aynı endpoint'i kullanır (3.4).** Infinity için sayfalı devam endpoint'i.
- [ ] G5. Razor render: yerleşim → blok dizisi → blok tipi → ilgili GorunumTipleri/Story partial'ı (**birebir HTML**); görünmeyen/boş blok hiç basılmaz, boşluk bırakmaz. Flash-sale geri sayımı config'ten bitiş zamanı.
- [ ] G6. Admin UI: blok CRUD + öğe CRUD + sıralama + aktif/pasif + **Yayınla** (validasyon → snapshot üretimi) + yayın geçmişi + **rollback** (eski versiyonu aktif et). (Liste satırı tıklanabilir → detay.)
- [ ] G7. Versiyon bazlı cache: `page:{yerlesim}:{version}:...` — Redis kurallarına uygun (hata-güvenli, ICacheService); yeni yayın eski anahtarları otomatik geçersizleştirir. Infinity ürün cache'i: `page-products:{version}:{blockId}:page:{n}`.
- [ ] G8. B6 geçici anasayfa + B3 geçici duyuru kaldırılır; anasayfa/duyuru/diğer yerleşimler tamamen bu sistemden render olur.

**G-M2 — Kural motoru + segment + önizleme + audit:**
- [ ] G9. **Segment tespiti**: üyelik durumu (üye/misafir), **üye grubu (karar: ilk sürümde dahil — CRM MemberGroup)**, cinsiyet (yalnızca profilden; yoksa "bilinmiyor" — E2), cihaz tipi (UA: mobil/tablet/desktop), şehir/bölge. **Konum zinciri (karar):** teslimat adresi → profil şehri → manuel seçim (nav'da şehir çipi, cookie) → **kullanıcı tetiklemeli tarayıcı konum izni** ("Konumumu kullan" butonu; koordinat 81 il merkezine en-yakın-nokta hesabıyla lokal çözülür, dış servis yok) → GeoLite2 lokal IP tahmini → "bilinmiyor". Sayfa açılışında izin pop-up'ı YOK. Bölge il→bölge tablosundan (C4 provinces.bölge).
- [ ] G10. **Kural motoru**: alan içi çoklu seçim OR, alanlar arası AND; boş alan değerlendirmeye girmez; tarih aralığı (boş=hemen/süresiz); öncelik; kural yoksa herkese, eşleşmeyene default içerik YOK (spec davranışı birebir).
- [ ] G11. Segment bazlı cache anahtarı: `{yerlesim}:{version}:{sehir}:{bolge}:{cinsiyet}:{cihaz}:{uyelik}:{grup}`.
- [ ] G12. Admin **önizleme**: segment parametreleri seçilerek taslak veri üzerinden render + "neden görünüyor/neden gizli" açıklamaları (spec örnekleri).
- [ ] G13. **Audit log + yayın logu**: spec'teki ActionType/EntityType listesiyle; admin'de "Değişiklik Geçmişi" ve "Yayın Geçmişi" ekranları.
- [ ] G14. QA: Bölüm 8.8 envanteri + her blok tipi/şablonu görüntü karşılaştırması + kural senaryo matrisi (şehir/bölge/cihaz/cinsiyet/üyelik/grup kombinasyonları) + rollback tatbikatı.

**Kabul kriterleri:** Tüm yerleşimler admin'den yönetiliyor; Yayınla/rollback çalışıyor; canlı site sadece snapshot okuyor; kurallar segmentlere göre doğru içerik gösteriyor; önizleme taslaktan çalışıyor; audit/yayın logları tutuluyor.

---

### FAZ H — Özel yetenekler
- [ ] H1. **Fatura PDF modalı**: misharix `FaturaController` proxy'si ECSPros'a taşınır (host allowlist config'e); Order invoice → entegratör URL'i → modal iframe + indir + yeni sekme + fallback.
- [ ] H2. **Kargo takip**: Fulfillment shipment verisinden takip modalı; kargo firması takip URL şablonları (FirmIntegration sözleşme alanlarıyla uyumlu).
- [ ] H3. **Görsel arama**: `POST /gorsel-arama` benzeri endpoint — dış servis (`search.misharitalia.com`) API key config'ten; sonuçlar ECSPros katalog eşlemesiyle (barkod/modelCode üzerinden `erp_variant_data`); nav'daki kamera butonu + sonuç modalı (`_GorselAramaModal*`) port edilir. Servisin ECSPros kataloğuyla eğitim/kapsam durumu kullanıcıyla netleştirilir.
- [ ] H4. **Mobil Alt Bar** (`_MobilAltBar`): mobilde sabit alt navigasyon; rota-duyarlı aktif durum.
- [ ] H5. **Ürün videoları**: `product_videos` (veya ProductImage'a tip alanı) — videolu ürün rozetinin gerçek veriye bağlanması; video yoksa rozet render edilmez.
- [ ] H6. Ödeme sağlayıcı gerçek entegrasyonu (C5/C6 kararına bağlı — sağlayıcı seçilince taksit/BIN, 3DS akışı).
- [ ] H7. QA: Bölüm 8.9 envanteri.

**Kabul kriterleri:** Fatura/kargo/görsel arama/alt bar canlı; video rozeti veriye bağlı.

---

### FAZ İ — SPA emekliliği + son QA + kural devri
- [ ] İ1. Tüm yüzeyler Razor'dayken nginx `/` tamamen Razor host'a döner; `/opt/ECSProsAI/store` SPA'sı arşivlenir (silinmez, `store-spa-arsiv/`).
- [ ] İ2. Uçtan uca senaryo QA: misafir gezinme → kayıt → arama → filtre → detay → sepet → kupon → sipariş → iade talebi → yorum → favori/koleksiyon.
- [ ] İ3. Kalite Kontrol taraması (misharix agent tarifi): çift `h1`, doğrudan `#f27a1a`, inline style, `href="#"`, eksik `alt`, konsol hataları — headless Chromium ile tüm sayfalar.
- [ ] İ4. Performans: liste/detay/ana sayfa sorgu süreleri; gerekli yerlere cache (Redis kuralları); toplu veri işlemlerinden sonra `ANALYZE`.
- [ ] İ5. **Kural devri**: misharix `project-basics.md` kuralları ana proje CLAUDE.md'ye "Storefront Tasarım Kuralları" bölümü olarak eklenir; drift script'i CI-vari alışkanlık olarak dokümante edilir.
- [ ] İ6. PROGRESS.md + hafıza güncellemesi; bu plan dosyası "TAMAMLANDI" işaretlenir.

---

## 5. YENİ BACKEND ÖZELLİKLERİ ÖZETİ (ana projede bugün OLMAYANLAR)

| # | Özellik | Tablo(lar) | Faz | Admin tarafı |
|---|---|---|---|---|
| 1 | Favoriler | `favorites` | E5 | rapor amaçlı liste (ops.) |
| 2 | Koleksiyonlar | `collections`, `collection_items` | E6 | ops. |
| 3 | Ürün yorumları + moderasyon | `product_reviews` | E7 | **moderasyon UI zorunlu** |
| 4 | Favori aramalar | `saved_searches` | E11 | — |
| 5 | Gezinme geçmişi | `viewed_products` | E12 | — |
| 6 | Stok haber ver | `stock_alerts` | C9 | ops. |
| 7 | SMS/OTP altyapısı | `otp_codes` (veya cache) | D4 | sağlayıcı config |
| 8 | **Vitrin & kişiselleştirme sistemi** (bloklar, kural motoru, snapshot/rollback, önizleme, audit) — spec: anasayfa-dizayn-yönetimi.txt | blok + öğe + `published_snapshots` + `publish_logs` + audit | G | **geniş admin UI zorunlu** |
| 9 | Story (blok sisteminin öğe tipi) | G1 kapsamında | G | G6 kapsamında |
| 10 | Bülten aboneliği | `newsletter_subscriptions` | F4 | liste/CSV |
| 11 | İletişim mesajları | `contact_messages` | F3 | liste |
| 12 | Ürün videoları | `product_videos` | H5 | ürün detayına yükleme UI |
| 13 | Üye kupon listesi | Promotion'a üye ilişkisi | E9 | kupon atama |
| 14 | Fatura PDF proxy / kargo takip / görsel arama | — (servis) | H1–H3 | config |
| 15 | Adres hiyerarşisi (ülke/il+bölge/ilçe/mahalle+posta kodu) | Core: `countries`,`provinces`,`districts`,`neighborhoods` | C4 | referans veri ekranı (ops.) |
| 16 | "Öne çıkar" bayrağı (Sponsorlu rozeti) | ChannelProduct alanı | B11 | işaretleme UI |
| 17 | Stok kontrolü anahtarı | FirmPlatform alanı | B12 | ayar |
| 18 | Tema altyapısı (ThemeCode + görünüm token override) | FirmPlatform alanları | A11–A12 | tema/renk ayar UI |
| 19 | Üye profili: cinsiyet + şehir (segment kaynağı) | Member alanları | E2 | profil görünümü |
| 20 | Koleksiyon moderasyonu | collections durum alanı | E6 | **onay ekranı zorunlu** |
| 21 | GeoIP (GeoLite2 lokal mmdb) + il-merkez koordinat tablosu | dosya + seed | G9 | — |

Mevcut olup **bağlanacaklar**: store auth, cart, checkout, adresler, siparişler, iadeler (listeleme), wallet, loyalty, kupon validate/use, CMS menü/sayfalar, katalog/facet sorguları.

---

## 6. KARAR KAYDI (2026-07-07 — kullanıcıyla tek tek karara bağlandı)

- [x] K1. **Host:** ECSPros.Api içinde MVC. Mobil app `api/store/*`'ı aynen kullanmaya devam eder (bkz. 3.4 API-first kuralı).
- [x] **Tema:** Platform başına site sahibi seçer (ziyaretçi seçemez); ilk etap iskelet + token override (bkz. 3.5, A11–A12).
- [x] K2. **Ödeme:** Şimdilik test modu (sipariş oluşur, tahsilat mock); sağlayıcı seçimi sonraya — seçilince H6'da gerçek entegrasyon. *(Açık: sağlayıcı adı)*
- [x] K3. **SMS:** `ISmsSender` soyutlaması + dev'de log sağlayıcısı; sağlayıcı seçimi sonraya. *(Açık: sağlayıcı adı)*
- [x] K4. **Vitrin modeli:** CMS PageSection DEĞİL — `docs/anasayfa-dizayn-yönetimi.txt` spec'ine göre Storefront'ta blok+kural+snapshot sistemi (Faz G). Bloklar **her sayfada** kullanılabilir (yerleşim modeli); yerleşimler: anasayfa, global duyuru, liste üst/alt, detay alt, sepet/teslimat/ödeme.
- [x] K5. **Görsel arama:** Aynı servis (search.misharitalia.com) + `erp_variant_data` üzerinden katalog eşleme; API key config'e. *(Açık: servis indeksinin güncelliği H3'te doğrulanacak)*
- [x] K6. **Adres verisi:** Ülke→il(+bölge)→ilçe→mahalle(+posta kodu) hiyerarşisi Core modülünde amaca özel tablolarda, resmi veri seed'i ile; kademeli aramalı-select API (C4).
- [x] K7. **Duyuru şeridi:** Kişiselleştirme sisteminin global yerleşimli "duyuru" bloğu (B3 geçici → G8 kalıcı).
- [x] K8. **Sponsorlu rozeti:** Basit "öne çıkar" bayrağı — ChannelProduct'ta tarih aralıklı alan + admin işaretleme + liste sıralama önceliği (B11). Tam reklam modülü kapsam dışı.
- [x] K9. **TCKN:** Format + algoritma kontrolü; NVİ/KPS resmi doğrulaması ileride (C7).
- [x] **Stok:** Gerçek stok okunur + FirmPlatform'da "stok kontrolü" anahtarı (bugün: kapalı → her şey satılabilir); veri dolunca anahtar açılır, kod değişmez (B12).
- [x] **Konum tespiti:** Pasif zincir (adres→profil→manuel seçim/cookie) + kullanıcı tetiklemeli tarayıcı konum izni (81 il en-yakın merkez, lokal hesap) + GeoLite2 IP fallback; sayfa açılışında izin pop-up'ı yok (G9).
- [x] **Faz G dilimleme:** İki milestone — G-M1 (bloklar+yayınla/snapshot/rollback) sonunda anasayfa canlıya çıkabilir; G-M2 kural motoru+segment+önizleme+audit.
- [x] **Üye grubu segmenti:** İlk sürümde kural alanı olarak DAHİL (CRM MemberGroup).

**Kalan açık noktalar:** ödeme sağlayıcısı adı (K2), SMS sağlayıcısı adı (K3), görsel arama indeks güncelliği (K5/H3), Instagram bloğunun içerik kaynağı (elle görsel mi, API mi — G2'de sorulacak).

---

## 7. TAŞINMAYACAKLAR (bilinçli kapsam dışı)

- ProjeElementleri katalog kabuğu, `/agent` sayfası, ikon/tema **kopyalama araçları** (tasarım projesinin iç araçları) — kaynak projede yaşamaya devam eder.
- misharix `appsettings.json` MySQL bağlantısı, hardcoded API key (güvenli config'e alınmadan hiçbir kopya).
- `cdn.tozlu.com` demo görselleri ve demo metinler (model verisiyle değişir).
- misharix'in `GorselAramaController`'daki legacy-MySQL zenginleştirmesi (ECSPros kendi kataloğundan zenginleştirir).

---

## 8. İŞLEV ENVANTERİ (kutsal liste — her satır doğrulanmadan yüzey kapanmaz)

> Kaynak: 82 partial'ın `data-ms-*` attribute taraması + partial içi script incelemesi (2026-07-07).
> Durum sütunu: ⬜ taşınmadı · 🕐 UI taşındı/işlev ileri fazda · ✅ canlı doğrulandı. Backend: VAR / KISMEN / YOK.

### 8.1 Navigasyon
| İşlev | Davranış | Backend | Faz | Durum |
|---|---|---|---|---|
| Duyuru şeridi | kayan metin | G'de kişiselleştirme bloğu (K7) | B3→G8 | ✅ 2026-07-08 (B3 geçici statik; kalıcısı G8) |
| Logo/ana sayfa linki | statik | — | B1 | ⬜ |
| Arama kutusu + panel | aç/kapat/temizle/geri | — (UI) | B2 | ⬜ |
| Canlı arama önerisi | ürün+kategori sonuç grupları, sonuç sayısı, ürün şeridi kaydırma | VAR (search) | B2 | ⬜ |
| Kategoride ara | seçili kategori kapsamında arama | VAR | B2 | ⬜ |
| Popüler aramalar/ürünler | öneri panelinde | YOK→E11 | B2/E11 | ⬜ |
| Giriş menüsü (hover panel) | oturumsuz/oturumlu içerik | VAR (me) | B4/D6 | ⬜ |
| Giriş modal — e-posta sekmesi | login | VAR | D2 | ⬜ |
| Giriş modal — telefon/SMS sekmesi | kod gönder/sayaç/yeniden gönder/onayla (OTP kutuları) | YOK | D4 | ⬜ |
| Kayıt modalı | register + belge (KVKK/üyelik) modal onayı | VAR + CMS | D3 | ⬜ |
| Hesap paneli + çıkış | session iptali | VAR | D6 | ⬜ |
| Mini sepet (hover) | sepet özeti | VAR | B5 | ⬜ |
| Mega menü (desktop) | kategori grupları, menü kaydırma | VAR (menus) | B1 | ⬜ |
| Kampanya şeridi | yatay kaydırma kontrolleri | KISMEN (kampanya görselleri kaynağı → G) | B1/G | ⬜ |
| Mobil menü | ana sekme/yan sekme/panel/kampanya listesi | VAR (menus) | B1 | ⬜ |
| Görsel arama (kamera) | modal + upload + sonuçlar | YOK | H3 | ⬜ |

### 8.2 Ürün Kartı (liste/vitrin/hesabım her yerde aynı kart)
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Kart → detay linki (`data-ms-kart-link`) | VAR | B8 | ✅ (2026-07-08 — kategori kartları `?color={eksenDeğeri}` taşır, detay çözer) |
| Hover görsel galerisi + nokta göstergeleri | VAR (variant images) | B8 | ✅ (2026-07-08 — seçili rengin ilk 4 görseli; SSR + JSON kartlarda) |
| Videolu ürün rozeti (hover'da oynatma) | YOK | H5 | ⬜ |
| Kampanya etiketi + kampanya bandı | KISMEN (Promotion) | B8/G | ⬜ |
| Sponsorlu rozeti → "öne çıkar" bayrağı | YOK→B11 (karar K8) | B11 | ⬜ |
| Favori (kalp) butonu + animasyon | YOK | E5 | ⬜ |
| Koleksiyona ekle (bookmark) | YOK | E6 | ⬜ |
| Renk rozeti + renk tooltip (diğer renk linkleri) | VAR (varyantlar) | B8 | ✅ (2026-07-08 — eksen renkleri kendi görselleriyle; görselsiz renk listelenmez) |
| Dönen teslimat/kargo mesajları | model alanları | B8 | ⬜ → B11'e devredildi (2026-07-08 — veri kaynağı yok, @if ile gizli) |
| Puan + yıldız + yorum sayısı | YOK | E7 | ⬜ |
| Fiyat (ms-urun-fiyat) | VAR (varyant fiyatı) | B8 | ✅ (2026-07-07 B7'de bağlandı — varyant fiyatı, B8'de doğrulandı) |
| Lazy load (`data-ms-lazy-src`) | — | B7 | ✅ (2026-07-07 — SSR kartlar dahil tüm görseller lazy) |

### 8.3 Ürün Listesi
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Sol filtre grupları (aç/kapa, seçim, sayaç) | VAR (facets) | B7 | ✅ (2026-07-07 — SSR facet'lerden; seçim client-side) |
| Seçili filtre chip şeridi + kaldır + temizle | VAR | B7 | ✅ (2026-07-07 — mobil şerit; misharix script'i yönetiyor) |
| Sıralama (özel select, desktop+mobil panel) | VAR/dogrula | B10 | ⬜ |
| Görünüm değiştirme (grid tipi) | — (UI) | B7 | ✅ (2026-07-07 — misharix script'i, değişiklik yok) |
| Mobil filtre paneli (detay panelleri, sayaç, hızlı filtre chip'leri) | VAR | B7 | ✅ (2026-07-07 — paneller gerçek facet gruplarından; Hızlı Teslimat/Ücretsiz Kargo chip'leri Faz G'ye kadar gizli) |
| Infinite scroll + "yükleniyor" + state restore | VAR (paging) | B7 | ✅ (2026-07-07 — state restore tasarımdaki gibi kapalı: sadeceIlkYukle) |
| Dinamik kartlara davranış yenileme (`msUrunKartDavranislariYenile`) | — | B7 | ✅ (2026-07-07 — modülün sonra hook'u + JSON dolumu sonrası) |
| Sonuç sayısı gösterimi | VAR | B7 | ✅ (2026-07-07 — SSR toplam; filtre aktifken yüklü-görünen sayısı) |

### 8.4 Ürün Detay
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Breadcrumb | VAR (kategori zinciri) | B9 | ✅ (2026-07-08 — GetProductChannelCategoryChainQuery filtre kuralı ters eşlemesi) |
| Galeri: thumb'lar, ok, sürükleme, slide takibi | VAR | B9 | ✅ (2026-07-08 — seçili rengin görselleri SSR; tek görselde oklar gizli) |
| Hover zoom lens + zoom paneli | — (UI) | B9 | ✅ (2026-07-08 — misharix script'i aynen) |
| Tam ekran resim modalı (thumb, sürükle, pinch, paylaş) | — (UI) | B9 | ✅ (2026-07-08) |
| Beden seçimi (ana + sticky bar + beden modalı) | VAR (varyant) | B9 | ✅ (2026-07-08 — gerçek eksen değerleri, konfeksiyon sıralı; beden yoksa alanlar gizli) |
| Beden/stok durumu (tükendi vb.) | VAR + B12 stok kontrolü anahtarı (karar) | B9/B12 | 🔶 B9: hepsi satılabilir (stok kontrolü kapalı varsayımı); tükendi işaretleme B12 anahtarına bağlı |
| Sepete ekle (+sticky) → mini sepet açılışı | VAR | B9 | 🔶 (2026-07-08 — API'ye ekleme ✓ bedenli+bedensiz+modal akışları; mini sepet paneli B5'te bağlanınca açılış eklenecek; AddToCart çoklu-ürün bug'ı düzeltildi) |
| Favori / koleksiyona ekle | YOK | E5/E6 | ⬜ |
| Paylaş modalı (FB/X/WhatsApp/Pinterest/link kopyala) | — (UI) | B9 | ✅ (2026-07-08 — gerçek ürün adı/görsel/fiyat; paylaşım metni DOM'dan) |
| Açıklama "daha fazla" + ek detay akordiyonları | VAR (DescriptionI18n/özellikler) | B9 | ✅ (2026-07-08 — DescriptionI18n/ShortDescription; pazaryeri-özel demo maddeleri çıkarıldı) |
| Ürün özellikleri tablosu | VAR (attributes) | B9 | ✅ (2026-07-08 — ürün seviyesi attributes + Kategori Grubu + Stok Durumu; DTO'ya additive eklendi) |
| Değerlendirme özeti + değerlendirmeler linki | YOK | E7 | ⬜ |
| Benzer/önerilen ürün vitrinleri | VAR (sorgu) | B9 | 🕐 misharix detay tasarımında benzer ürün bölümü YOK — Faz G vitrin sistemine devredildi |
| Videolu ürün | YOK | H5 | ⬜ |

### 8.5 Sepet + Checkout
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Satır adet artır/azalt, satır tutarı | VAR | C1 | ⬜ |
| Satır sil + onay modalı | VAR | C1 | ⬜ |
| Tümünü seç / satır checkbox | VAR (UI+cart) | C1 | ⬜ |
| Favoriye taşı | YOK | E5 (C2 köprü) | ⬜ |
| Kupon modalı: listeden seç / kod uygula / kaldır | KISMEN (validate/use VAR, üye listesi YOK) | C3/E9 | ⬜ |
| Sipariş özeti + adım göstergesi (sepet→teslimat→ödeme) | VAR | C1–C5 | ⬜ |
| Adres seçimi + adres ekle/düzenle modalı | VAR | C4 | ⬜ |
| Telefon ülke-kodlu input (arama, ülke seçimi) | — (UI) | C4 | ⬜ |
| İl/ilçe özel select | YOK (K6) | C4 | ⬜ |
| Ödeme yöntemleri (kart/kapıda/havale) + kart canlı önizleme + kart tip algılama | YOK (sağlayıcı K2) | C5 | ⬜ |
| Taksit listesi | YOK | C6 | ⬜ |
| TCKN doğrulama modalı | YOK (K9) | C7 | ⬜ |
| Sözleşme modalları + onay kaydı | KISMEN (CMS VAR) | C8 | ⬜ |
| Ödeme eksikleri uyarısı (onaya geç kontrolü) | — (UI) | C5 | ⬜ |
| Stok gelince haber ver | YOK | C9 | ⬜ |
| Sipariş oluştur → tamamlandı sayfası | VAR | C10 | ⬜ |

### 8.6 Hesabım (12 sayfa)
| Sayfa / işlev | Backend | Faz | Durum |
|---|---|---|---|
| Yan menü + mobil aç/kapat | — | E1 | ⬜ |
| Hesabım özet (varsayılan) + wallet/loyalty widget | VAR | E13 | ⬜ |
| Üyelik bilgilerim (profil/şifre/TCKN) | VAR | E2 | ⬜ |
| Adreslerim CRUD | VAR | E3 | ⬜ |
| Siparişlerim: filtre chip, kart toggle, detay modal | VAR | E4 | ⬜ |
| Kargo takip modalı | KISMEN (shipment) | E4/H2 | ⬜ |
| Fatura PDF modalı (iframe/indir/yeni sekme/fallback) | YOK (proxy) | E4/H1 | ⬜ |
| İade talebi: ürün seç, ana/alt neden (aramalı select), açıklama, görsel yükleme | KISMEN (returns API üye tarafı eksik) | E8 | ⬜ |
| İade SMS doğrulama + iade kodu al/kopyala | YOK (OTP D4) | E8 | ⬜ |
| İadelerim: filtreler, durum akışı, neden paneli | KISMEN | E8 | ⬜ |
| Tekrar satın al | VAR (türetme) | E10 | ⬜ |
| Önceden gezdiklerim | YOK | E12 | ⬜ |
| Yorumlarım (3 sekme: yayında/bekleyen/reddedilen+neden) | YOK | E7 | ⬜ |
| Favorilerim | YOK | E5 | ⬜ |
| Koleksiyonlarım (oluştur modal, herkese açık/paylaşılabilir, ürün seç) | YOK | E6 | ⬜ |
| İndirim kuponlarım | YOK (üye ilişkisi) | E9 | ⬜ |
| Favori aramalarım (kaydet/sil/çalıştır) | YOK | E11 | ⬜ |

### 8.7 Kurumsal + Footer
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Yan menü + tab + lazy panel yükleme (`data-ms-lazy-panel-url`) | VAR (CMS) | F1 | ⬜ |
| 7 içerik sayfası (Hakkımızda…Gizlilik) | VAR (CMS, içerik girilecek) | F1 | ⬜ |
| SSS akordiyonu (tek soru açık) | VAR (CMS) | F2 | ⬜ |
| İletişim formu | YOK (küçük) | F3 | ⬜ |
| Footer kolonları + mobil akordiyon | VAR (menus) | F4 | ⬜ |
| Bülten aboneliği | YOK (küçük) | F4 | ⬜ |
| Banka logoları / sosyal linkler | statik/config | F4 | ⬜ |

### 8.8 Vitrin & Kişiselleştirme Sistemi (Faz G — spec: anasayfa-dizayn-yönetimi.txt)
| Bölüm tipi / yetenek | Backend | Faz | Durum |
|---|---|---|---|
| Banner (Tekli/İkili/Üçlü/Dörtlü/Beşli/Reklam/Bilgi/İkon/Çoklu) — kural: blok | YOK → blok sistemi | G-M1 | ⬜ |
| Slider — kural: slide öğesi | YOK | G-M1 | ⬜ |
| Story (grup, progress, video/görsel, aksiyon linki, modal) — kural: story öğesi | YOK | G-M1 | ⬜ |
| Carousel Ürün Listesi (Standart/Özel Fiyatlar/Flash+geri sayım) — kural: blok + kaynak/filtre | YOK | G-M1 | ⬜ |
| Infinity Ürün Listesi (Grid + infinite scroll) — kural: blok + kaynak/filtre | YOK | G-M1 | ⬜ |
| Tabs — kural: blok + tab öğesi | YOK | G-M1 | ⬜ |
| Koleksiyonlar bloğu (yalnız onaylı+açık) — E6 bağımlı | YOK | G-M1 | ⬜ |
| Categories / Brands / Instagram vitrinleri | YOK (kategori verisi VAR) | G-M1 | ⬜ |
| Duyuru bloğu (global yerleşim) | YOK | G-M1 | ⬜ |
| Yerleşimler: anasayfa/liste üst-alt/detay alt/sepet-teslimat-ödeme/global | YOK | G-M1 | ⬜ |
| Ürün kaynağı/filtre motoru (çok satan, yeni, kampanyalı, manuel...) | YOK | G-M1 | ⬜ |
| Taslak→Yayınla→versiyonlu snapshot→rollback + yayın logu | YOK | G-M1 | ⬜ |
| Admin: blok/öğe CRUD + sıralama + yayın geçmişi | YOK | G-M1 | ⬜ |
| Kural motoru (OR-içi/AND-arası, tarih, öncelik, default yok) | YOK | G-M2 | ⬜ |
| Segment tespiti (şehir/bölge/cinsiyet/cihaz/üyelik/üye grubu + konum zinciri) | YOK | G-M2 | ⬜ |
| Segment+versiyon bazlı cache | YOK | G-M2 | ⬜ |
| Admin önizleme (segment seçerek, "neden görünüyor/gizli") | YOK | G-M2 | ⬜ |
| Audit log + değişiklik geçmişi ekranları | YOK | G-M2 | ⬜ |

### 8.9 Diğer sayfalar + ortak elementler
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Ürün Değerlendirmeleri sayfası: sekmeler, çoklu-seçim filtreler, liste+sayfalama, yorum formu, kriter modalı | YOK | E7/H | ⬜ |
| Mobil Alt Bar | — (UI) | H4 | ⬜ |
| Özel select ailesi (arama/çoklu/checkbox/temizle/uygula) | — (site.js) | A5 | ⬜ |
| Telefon ülke-kodlu input | — (site.js) | A5 | ⬜ |
| OTP kod giriş kutuları | — (site.js) | A5 | ⬜ |
| Modal ailesi (standart/başarı/hata/uyarı, boyutlar) | — | A5 | ⬜ |
| Bildirimler, rozetler, statüler | — | A5 | ⬜ |
| Slider elementi | — | A5 | ⬜ |
| Lazy load + Infinite scroll motorları (opt-in `lazy-infinite-on`) | — | A5 | ⬜ |
| ProjeElementleri scroll-restore, SSS akordiyon, favori animasyonu | — | A5 | ⬜ |

---

*Bu plan `docs/misharix-tasarim-projesi-inceleme.md` ile birlikte okunmalıdır. Kaynak tasarımda değişiklik olursa önce envanter güncellenir, sonra ilgili faz görevi revize edilir.*
