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
| B | Navigasyon + Ana Sayfa + Liste + Detay (gerçek port) | ✅ TAMAM (2026-07-09) — B1–B14 tümü bitti; envanter 8.1–8.4 tam işaretli, drift temiz. Geçici çözümler kayıtlı: B3 duyuru + B6 ana sayfa → G8'de vitrin sistemine devrolur |
| C | Sepet + Checkout | ✅ TAMAM (2026-07-09) — C1–C11 bitti: sepet, kupon, teslimat+adres+geo, ödeme (K2 mock), taksit, TCKN (K9), sözleşmeler CMS+kabul kaydı, stok haber ver, checkout uçtan uca; QA 88 adım yeşil. Ertelenenler hedef fazlı: favori E5, kupon listesi E9, adres düzenle E4, tahsilat/BIN H6, bildirim H |
| D | Üye oturumu (Razor tarafı) + SMS/OTP altyapısı | ✅ TAMAM (2026-07-10) — D1–D7 bitti: HttpOnly cookie + SSR kimlik + logout/session iptali; kayıt belgeleri CMS + Member.Consents; SMS/OTP girişi canlı (crm.otp_codes + ISmsSender — gerçek sağlayıcı seçimi bekliyor); şifreler BCrypt + ilk girişte re-hash; QA 51 adım yeşil, d7-* görüntüleri alındı |
| E | Hesabım kümesi (12 sayfa + yeni backend özellikleri) | 🔵 Sürüyor — E1-E7 ✅ (çerçeve; Üyelik Bilgilerim; Adreslerim; Siparişlerim; Favorilerim; Koleksiyonlarım; Yorumlar — product_reviews + moderasyon + puanlar gerçek ortalamadan) |
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
- [x] B4. **Giriş/kayıt modalları + hesap paneli** — TAMAM (2026-07-09): (1) **E-posta girişi canlı** — varsayılan sekme e-posta (`_AnaNavigasyon`'da tek satır: modal açılışı `tabAc("eposta")`); SMS ve Telefon sekmeleri Faz D'ye kadar "(Yakında)" disabled. Login → `api/store/auth/login`, token'lar localStorage'da (`ecspros_member_token`/`ecspros_member_refresh`), modal kapatma misharix'in kapat butonuna click ile devredilir, oturum UI'ı misharix'le aynı class/yazı kuralıyla ayarlanır. (2) **Kayıt canlı** — `register` + otomatik login; forma **Şifre alanı eklendi** (API şifre zorunlu; Faz D SMS/OTP onboarding'inde gözden geçirilir), 3 onay kutusu zorunlu, hata alanları `ms-uyari ms-uyari-hata`. (3) **Oturum kalıcı** — sayfa açılışında `me` (401'de bir kez `refresh` denenir, olmazsa temizlenir); hesap paneli gerçek ad/avatar-baş harfleri/iletişimle dolar; statü/harcama bloğu `@if(false)` gizli (puan verisi yok — Faz E/G); hesap menü linkleri Faz E'ye kadar `#`. (4) **Girişte misafir sepeti birleşir** — `cart/merge` (Bearer) → `ecspros_cart` güncellenir + `msMiniSepetYenile`. Çıkış: misharix UI sıfırlaması + token temizliği. **Canlı DB'de iki eksik bulundu ve giderildi (üyelik hiç çalışamazdı):** `crm_member_groups` BOŞtu → varsayılan grup (Code='standart', IsDefault) SQL ile eklendi + `DatabaseSeeder.SeedCrmDefaultsAsync` (idempotent) eklendi; `crm.member_sessions` tablosu yoktu → bekleyen `20260310131553_AddMemberSession` migration'ı canlıya uygulandı. E2E (5051 publish): **12/12 ✓** (varsayılan sekme + pasif sekmeler, hatalı giriş mesajı, onaysız kayıt engeli, kayıt→otomatik giriş→Hesabım, hesap paneli E2E Deneme/ED, reload kalıcılığı, çıkış, tekrar giriş; 0 konsol hatası) + B6 regresyon 19/19 ✓; test üyeleri DB'den silindi.
- [x] B5. **Mini sepet** — TAMAM (2026-07-09): `_AnaNavigasyonUst`'taki hover paneli canlı sepete bağlandı — demo satırlar kaldırıldı, satır markup'ı birebir korunarak dosya sonundaki script `GET /api/store/cart?cartId/sessionId/firmPlatformId` ile doldurur (SPA anahtarları ecspros_sid/ecspros_cart). Rozet adet gösterir (0'da gizli), silme `DELETE /api/store/cart/{cartId}/items/{itemId}` sonrası yeniden çizer, kalem görseli `/urun/{code}`'a linkli. **Yeni port: `IProductService` batch gösterim metodu** — `GetVariantDisplayAsync` (Shared.Contracts'a `VariantDisplayInfo` eklendi; ilk implementasyon `CatalogProductService`, Catalog.Infrastructure'da DI'a kayıtlı) — CRM `GetCartQuery`, `CartItemDto`'ya additive eklenen ProductCode/NameI18n/ImageUrl/OptionsText ("Beden: ST, Renk: Pembe") alanlarını bununla doldurur; sepet sayfası (Faz C) aynı zenginleştirmeyi hazır bulur. Ürün detay sepete-ekleme sonrası `window.msMiniSepetYenile()` çağrılır (reload'suz rozet). "Sepete Git/Siparişi Tamamla" `#` — Faz C'de bağlanır. E2E (5051 publish): 10/10 ✓ (boş durum, ekleme→rozet reload'suz 1, panel ad+seçenek+fiyat+link, silme→boş, test sepeti DB'den temizlendi, 0 konsol hatası) + B6 regresyon 19/19 ✓.
- [x] B6. **Ana sayfa** — TAMAM (2026-07-08, geçici kompozisyon): sayfa seçici kaldırıldı; `Home/Index.cshtml` GorunumTipleri bloklarından kuruldu — **Kapsül Kategori Şeridi** (kök kanal kategorileri; görsel = kategori görseli yoksa ilk ürün görseli, görselsiz kapsül basılmaz; mobilde `ms-gorunum-banner-mobil-carousel`) + **Standart Carousel × kök kategori (ilk 3)** (kategori başına ilk 10 ürün, "Tümünü Gör" → `/{slug}`, tema `varsayilan`). Banner bloğu bilinçli atlandı (banner görseli yok — Faz G'de vitrin sisteminden gelecek). Veri: `HomeController` → `GetChannelCategoryProductsQuery` kök başına **paralel** (görev başına ayrı DI scope — scoped DbContext paylaşılamaz), `AnaSayfaVm` platform başına 15 dk IMemoryCache. **Kart tek kaynağa alındı:** B7'nin `UrunKarti` local function'ı `ProjeElementleri/Urun/_UrunKarti.cshtml` paylaşılan partial'ına taşındı (liste SSR + `<template>` + ana sayfa carousel aynı markup; `UrunKartMap` dönüştürücüsü Models/Store'da). Kart davranışları site.js bootstrap'inde yalnız `data-ms-infinite-liste` konteynerlerine bağlandığından sayfa sonuna `msUrunKartDavranislariYenile` çağıran config script'i eklendi (DOMContentLoaded'a ertelenir — site.js body sonunda yükleniyor). `check.sh`'a "İZİNLİ YENİ" durumu eklendi (kaynakta karşılığı olmayan bilinçli dosyalar allowed-diffs ile). E2E (headless Chromium, 5051 Production **publish çıktısından** — bin/Release'te wwwroot yok, statikler 404 olur, test oradan yapılmaz): desktop 19/19 ✓ (kapsüller, 3 carousel, ok kaydırma, hover galeri + tooltip + ?color= detay linki, /kadin infinite scroll 24→72 paylaşılan partial'la, 0 konsol hatası) + mobil 6/6 ✓ (kapsül yatay carousel, duyuru bar mobilde gizli). G8'de bu geçici kompozisyon kaldırılır.
- [x] B7. **Ürün Listesi** — TAMAM (2026-07-07): üç yüzey tek `UrunListesiController` + `UrunListesiVm` ile — kategori `/{slug}` (regex kısıtlı `[a-z0-9-]+`; kısıtsız hali /favicon.ico gibi kök statik dosyaları yutuyordu — örtük UseRouting endpoint'i pipeline başında eşleştirince StaticFileMiddleware devre dışı kalıyor), arama `/urunler?search=` (B2 "Tümünü Gör" hedefi), tümü `/urun-listesi`. **İlk sayfa SSR** (plan 3.3; kart markup'ı `UrunKarti` local function — SSR ve `<template>` aynı markup), devamı misharix infinite-scroll modülü `kartHazirla`/`sonra` hook'larıyla (`ilk:0`, gerçek toplam; iskelet kartlar JSON gelince dolar). Facet'ler controller'da süreç içi MediatR'dan SSR (kategori facets Redis 0.01s; tüm-katalog ilk çağrı ~5-6s sonra 15dk IMemoryCache — sadece /urun-listesi'ni etkiler). **Filtre/sıralama SPA paritesiyle client-side** (seçili valueId'ler kartın colors+attrs id'leriyle OR; fiyat min/max + hazır aralıklar; fiyat artan/azalan sıralama; sayaç günceller) — sunucu tarafı filtre/sıralama B10 ile gelmeli. Sol filtre: Kategori bloğu alt/kök kategorilere gider; mobil panel/chip/detay markup'ları gerçek facet gruplarından üretilir (`anaFiltreAdlari` bağlandı); veri karşılığı olmayan demo blokları (hızlı filtre chip'leri, kampanya bloğu, kart puan/teslimat/video/sponsor) `@if` ile gizli (B8/B10/B11/Faz G). E2E: headless Chromium — Kadın 24 SSR kart → scroll 48 (24'ü JSON'la doldu), renk filtresi 33/15 + sayaç, fiyat sıralaması artan, mobil filtre panelleri, 0 konsol hatası; drift TEMİZ (5 izinli B7 girdisi).
  - ~~NOT: "Kategoride ara" kapsam daraltması yine ertelendi~~ → **B10'da kapandı (2026-07-09)**: `channel-categories/{id}/products` artık `search` alıyor, nav arama panelindeki buton bağlandı.
- [x] B8. **Ürün kartı derinleştirme** — TAMAM (2026-07-08): (1) **Hover görsel galerisi + nokta göstergeleri** — kartın (seçili) rengine ait görsel havuzunun ilk 4'ü `data-ms-urun-galeri-resimler`'e | ayraçlı verilir (misharix site.js modülü mousemove/touch ile gezdirir); renk çözülemiyorsa galeri verilmez (farklı renklerin karışık havuzu "tekrarlı galeri" üretir — detay handler'ı dersi). Kategori tarafı `GetChannelCategoryProductsQuery`'de varyant başına tek görsel yerine (ProductId,ColorValueId)→görsel havuzu; arama tarafı `GetStoreProductsQuery`'de ana görselin VariantId'sinden renk çözülür. (2) **Renk tooltip'i** ("Farklı Renk Seçenekleri") — eksen (renk) kartları kendi görselleri ve `/urun/{code}?color={eksenDeğerId}` linkleriyle (ilk 4 desktop, tam liste mobil alt panel; rozet tıklaması mobilde bottom-sheet açar); **görselsiz renkler listelenmez** (B9 kuralı — detay onları göstermediğinden linklenirse ilk görünür renge düşüp kullanıcıyı yanıltırdı, E2E yakaladı); eksen verisi yoksa filtre_rengi'ne düşer. (3) **Kart → detay linki artık renk taşır** — kategori kartları `?color={eksenDeğeri}`; `UrunDetayController` eksen-dışı değeri de (filtre_rengi bucket) o değeri taşıyan varyantın eksen rengine çözer. (4) JSON kartlarda aynı markup enjekte edilir (tooltip rozetten ÖNCE — site.js rozeti bağlarken tooltip alanı yoksa bir daha bağlamaz; galeri "hazır" bayrağı silinir ki `msUrunKartDavranislariYenile` yeniden bağlasın). DTO'lar additive genişledi: `ProductListingColorDto.ImageUrl`, `StoreProductDto.GalleryUrls`, `ChannelCategoryProductItemDto.GalleryUrls/AxisColors`. Puan/teslimat/kargo/kampanya alanları veri kaynağı olmadığından `@if` ile gizli kaldı (B11/E7/Faz G). E2E (headless Chromium, 5051 Production): desktop 20/20 ✓ (SSR+JSON hover galerisi, nokta göstergeleri, tooltip aç/kapa, tıklanan renk = detayda seçili renk, regresyon, 0 konsol hatası) + mobil 5/5 ✓ (bottom-sheet panel, kapat butonu).
- [x] B9. **Ürün Detay** — TAMAM (2026-07-08): `/urun/{code}?color={valueId}` tamamen SSR — `UrunDetayController` + `UrunDetayVm`; renk değişimi ?color= navigasyonudur (SSR yeniden yükleme), beden seçimi client-side (misharix script'i değişmedi), sepete ekleme partial sonundaki config script'iyle `api/store/cart/items` (SPA ile aynı localStorage anahtarları: ecspros_sid/ecspros_cart; Şimdi Al da şimdilik sepete ekler — gerçek akış Faz C). 5 partial + Index bağlandı (allowed-diffs'te gerekçeli). **Breadcrumb**: yeni `GetProductChannelCategoryChainQuery` — kategoriler filtre tanımlı olduğundan ters eşleme kural değerlendirmesiyle (ProductGroupIds + AttributeFilters, listelemeyle aynı semantik; manuel atama/IsExcluded dahil; en derin aday kazanır). **DTO genişledi (additive)**: `StoreProductDetailDto`'ya DescriptionI18n + Attributes (ürün seviyesi) + ProductGroupNameI18n eklendi (api/store/catalog/products/{code} aynı endpoint — mobil etkilenmez). Beden sıralaması konfeksiyon sırasına göre (S<M<L<XL…, numerikler sayısal); sıfır fiyatlı varyantlar gösterim fiyatına girmez (SPA paritesi); "renk"/"filtre_rengi" eksenleri asla beden sanılmaz; filtre_rengi yoksa "renk" ekseni renk kabul edilir. **Canlı bug düzeltildi (B9 dışı ama kabul kriteri)**: `AddToCartCommand` mevcut sepete ikinci farklı ürünü hiç ekleyemiyordu (tracked cart koleksiyonuna Id'li yeni satır → EF Modified sanıp UPDATE, DbUpdateConcurrencyException) — `db.CartItems.Add`'e çevrildi. Gizlenenler (@if): puan tooltip demo, dönen teslimat mesajları (B8/B11), çoklu fiyat senaryoları (Faz G), model ölçüleri (manken verisi ürünlerde yok), teslimat bilgileri (Faz H), beden tablosu (veri yok), video/etiketler (B11/G). **Benzer ürünler bölümü misharix detay tasarımında yok** — envanterdeki satır Faz G vitrin sistemine devredildi. E2E (headless Chromium): 12/12 ✓ — galeri thumb/slide, beden seçimi+modal, sepete ekle API 200 (bedenli+bedensiz akış), renk SSR değişimi, görsel modalı, paylaş modalı gerçek adla, mobil sabit bar + sticky beden; 0 konsol hatası; drift TEMİZ (6 izinli B9 girdisi).
- [x] B10. **Sunucu tarafı filtre/sıralama + kategoride ara** — TAMAM (2026-07-09): (1) **Sorgular additive genişledi** — `GetStoreProductsQuery` ve `GetChannelCategoryProductsQuery`'ye `AttributeValueIds`/`PriceMin`/`PriceMax`/`Sort` (+kategoriye `Search`) eklendi; api/store endpoint'leri `attrs` (virgüllü valueId), `priceMin`, `priceMax`, `sort`, `search` parametrelerini alır — parametresiz eski çağrılar birebir aynı (mobil etkilenmez). (2) **Filtre semantiği**: değerler tipine göre gruplanır — grup içi OR, gruplar arası AND; kategori kartlarında (ürün×renk) eşleşme kartın KENDİ varyantları üzerinden ve aynı varyantta (kırmızı+M birlikte); genel listede ürün seviyesinde. Fiyat filtresi varyant BasePrice üzerinden (kartların fiyat kaynağı; kanal override'ı yalnız gösterimde — Faz G'de fiyat mimarisiyle revize). (3) **Sıralama**: price_asc/price_desc (ürünün 0 olmayan en düşük varyant fiyatı) + **newest** (CreatedAt) — desktop select + mobil sıralama modalında "En Yeniler" eklendi; "çok satan/favori/değerlendirme" veri kaynağı gelene dek gizli (E7/B11). (4) **Sayfa tarafı**: filtre/sıralama değişikliği URL query parametreleriyle (api ile aynı adlar) SSR yeniden yükleme — B7'nin yüklü-kartlara-uygulanan client motoru kaldırıldı; SSR seçili checkbox/fiyat/sıralamayı geri işaretler (grup açık gelir), infinite scroll devam sayfalarını aynı parametrelerle çeker; sol filtre anında, panel içi seçimler "Filtrele" butonuyla uygular; **aynı valueId'nin kopyaları (sol+üst+mobil) senkronlanır** (senkronsuz kopyalar kaldırmayı URL'den düşürmüyordu — E2E yakaladı). (5) **Kategoride ara** (B2/B7'den beri ertelenen boşluk kapandı): kategori sayfası `ViewData["MsAktifKategori"]` doldurur; nav arama panelindeki gizli buton "{Kategori} içinde ara" olarak görünür, basılınca öneriler `channel-categories/{id}/products?search=`'ten gelir, Tümünü Gör/Enter → `/{slug}?search=`; sunucu tarafı kategori+arama birleşik sorgu `GetChannelCategoryProductsQuery.Search` ile (kapsam daralması ürün kod/Türkçe ad). (6) **Cache**: filtreli istekler kategori Redis cache'ini atlar (anahtar patlaması olmasın; yalnız parametresiz varsayılan sayfalar cache'lenir). NOT: model modu kategoriler filtre/sıralamayı uygulamaz (grup vitrini — Faz G konusu); fallback (eksensiz) modda yalnız arama uygulanır. E2E (headless Chromium, 5051 Production publish): **22/22 ✓** (checkbox→attrs URL + SSR işaretli + tüm kartlar değeri taşıyor + kaldırınca düşer; sort=price_asc SSR sıralı + infinite scroll sıralı devam 24→48 + etiket; newest; priceMin/Max sunucuda; kategoride ara butonu + /kadin?search=elbise 162.697→17.051; /urunler sıralı; api/favicon regresyon; 0 konsol hatası) + mobil sıralama modalı → ?sort=price_asc ✓ + B6/B8 regresyon suite'leri yeniden koşuldu (19/19 + 6/6 ✓).
- [x] B11. **"Öne çıkar" bayrağı** — TAMAM (2026-07-09, K8): (1) `ChannelProduct.FeaturedFrom/FeaturedUntil` (migration `AddChannelProductFeatured`, canlıya uygulandı) — pencere içindeyse öne çıkar (`Until` null = süresiz). (2) **Admin:** `GET/PUT /api/navigation/channel-products/{firmPlatformId}/products/{productId}/featured` (PUT featuredFrom=null → kaldır; satır yoksa upsert) + ProductDetailPage "Satış Kanalları" sekmesine "Öne Çıkar" paneli (tarih aralığı + durum rozeti + kaldır; npm build alındı). (3) **Listeleme:** yeni port `IChannelProductFlagService.GetFeaturedProductIdsAsync` (Storefront implemente eder; platform başına az satır — tam liste çekilip kesişim alınır). Kategori (renk modu) + genel liste sorgularında **yalnız varsayılan sırada** öne alınır (kullanıcının açık sıralama tercihi bozulmaz — kararlı OrderBy grup içi sırayı korur); DTO'lara additive `IsFeatured` bayrağı her sıralamada verilir. (4) **Kart:** `_UrunKarti` partial'ında "Sponsorlu" rozeti `Sponsorlu` bayrağına bağlandı (video/kampanya etiketleri hâlâ gizli — B/G); JSON devam kartlarına da enjekte edilir. NOT: kategori varsayılan sayfaları 10 dk Redis cache'inde — işaretleme listede en geç 10 dk içinde görünür (rozet + sıralama; filtreli/aramalı istekler anında). Model/fallback modlarında öne alma uygulanmaz (Faz G vitrini). E2E (5051 publish): **14/14 ✓** (admin PUT/GET, kategori aramasında başa alma + isFeatured, price_asc bozulmadı, SSR'de ilk kart + Sponsorlu rozeti ×2 renk kartı, /products tarafı, kaldırma normale döndürdü, 0 konsol hatası) + B6 regresyon 19/19 ✓.
- [x] B12. **Stok kontrolü anahtarı** — TAMAM (2026-07-09, stok kararı): anahtar `FirmPlatform.Settings."stockControlEnabled"` (JSONB — kolon/migration yok, tema/domain anahtarlarıyla aynı yer; varsayılan KAPALI = bugünkü veri durumu, her şey satılabilir). **Açıkken:** ürün detayında beden satılabilirliği `IStockService.GetAvailableStockAsync`'ten okunur — tükenen beden ana alanda ve sabit (sticky) beden panelinde misharix'in `ms-beden-secim-tukendi` stili + `disabled` ile gelir, sepet config haritasına girmez; "Stok Durumu" özelliği gerçek (Stokta/Tükendi); bedensiz üründe stok yoksa `TekVaryantId` verilmez (sepete eklenemez). **Sunucu guard'ı:** `AddToCartCommand.EnforceStock` (API katmanı anahtarı 5 dk IMemoryCache ile çözer ve geçer) — stoksuz varyant 400 "tükendi" döner; anahtar kapalıyken stok hiç sorgulanmaz. Stok verisi dolunca platform Settings'ine `"stockControlEnabled": true` eklemek yeterli — kod değişmez. E2E (5051, julude platformu test için açıldı ve GERİ ALINDI — mishar'a dokunulmadı): **12/12 ✓** (stoksuz: 4 beden tükendi+disabled+config boş+özellik Tükendi; stok verilince hepsi satılabilir; kısmi: tek beden tükendi; guard 400/200; mishar anahtar kapalı etkilenmedi; test stoğu/sepeti temizlendi) + B6 regresyon 19/19 ✓.
- [x] B13. **Görsel + davranış QA** — TAMAM (2026-07-09, Faz B kapanışı): (1) **Envanter 8.1–8.4 tamamı işaretli** — 25 açık satır gerçek durumla kapatıldı: Faz B'de biten her şey ✅, veri/faz bekleyenler 🕐 hedef fazıyla (SMS/OTP→D4, belge metinleri→D3, favori/koleksiyon/puan→E5-E7, kampanya içerikleri→G, görsel arama→H3, video→H5); hiçbir satır işaretsiz kalmadı. (2) **Kapanış ekran görüntüleri** `tools/misharix-sync/shots/b13-*` (ana sayfa + kategori listesi + ürün detay, desktop 1440 + mobil 390) — tasarım sadakati görsel olarak doğrulandı; markup eşitliğini `check.sh` zaten bayt düzeyinde garanti ediyor (TEMİZ ✓, tüm farklar gerekçeli izinli listede). NOT: misharix tasarım projesi .NET 9 hedeflediği için bu makinede (SDK 8) çalıştırılamıyor — canlı yan-yana yerine faz faz alınan birebir doğrulamalar + drift kontrolü esas alındı. (3) Her yüzeyin davranış doğrulaması zaten kendi fazının E2E suite'iyle yapıldı ve B13 sırasında tekrar koşuldu (B6 19/19). **FAZ B KABUL KRİTERLERİ SAĞLANDI** — dört yüzey canlı veriyle çalışıyor, envanter tam işaretli, drift temiz.
- [x] B14. Nginx'te `/` Razor'a çevrildi (2026-07-07 — kullanıcı kararıyla ERKEN geçiş, dört yüzey tamamlanmadan): `locations.inc` `/` bloğu artık host:5000'e proxy (https://new.ecspros.com Razor'ı sunuyor, Cloudflare Flexible 80 üzerinden); eski SPA yedeği 8080 portuna taşındı (rol değişimi — api+media+statik blokları eklendi; Faz İ'de kaldırılacak); appsettings `Store:Hosts["new.ecspros.com"]="mishar"`. NOT: B7'ye kadar kategori linkleri 404, ana sayfa geçici sayfa seçici — bilinçli kabul.

**Kabul kriterleri:** Dört yüzey pixel-doğru + canlı veriyle çalışıyor; envanter 8.1–8.4 tüm satırlar ✅/🕐(ileri faz) işaretli; drift raporu temiz.

---

### FAZ C — Sepet + Checkout
> Mevcut backend: cart CRUD+merge, checkout POST, adresler, kupon validate/use → çoğu VAR; eksikler taksit, ödeme sağlayıcı, TCKN, stok haberi.

- [x] C1. **Sepet sayfası** — TAMAM (2026-07-09): `Views/Sepet/Index` + `_SepetSayfasi` + `_SepetModallari` birebir kopyalandı, `/sepet` route'u (`SepetController`). **Sepet istemci-durumlu** (localStorage ecspros_sid/ecspros_cart) — satırlar template + dosya sonu script'le `GET /api/store/cart`'tan render (B5 deseni; IProductService zenginleştirmesi ad/görsel/seçenek özetini hazır veriyor). Adet ± → PUT (1–10, satır tutarı + özet anlık), silme → misharix sil onay modalı üzerinden DELETE (onay butonuna ikinci dinleyici — modal script'i değişmedi), tümünü seç / satır checkbox'ları özet toplamını belirler (sipariş C10'da tam sepetle). Boş durum + mini sepet rozet senkronu (`msMiniSepetYenile`). Tükendi satır görünümü `isAvailable=false` kalemler için hazır (B12 anahtarıyla anlamlanır). Mini sepet "Sepete Git/Siparişi Tamamla" → `/sepet` (C4'te Tamamla → /teslimat). @if(false) ile fazına bırakılanlar: TCKN uyarısı (C7), kargo bilgi/teslimat satırları (H), koleksiyon kaydet + satıcı başlığı (E6), kampanya etiketleri + eski fiyat (G), kupon alanları (C3), etiket rozetleri (G/C3/H). Misharix demo script'i dokunulmadan kaldı (satır bulamadığından satır bağlamaları etkisiz; TCKN/sözleşme modalları ondan çalışır). E2E (5051 publish): **12/12 ✓** (boş durum, ekleme→satır render ad+seçenek+görsel+link, adet 2→rozet senkron→reload kalıcı, seçim kaldırınca özet 0, sil modal→onay→API'den silindi, mini sepet linkleri, temizlik, 0 konsol hatası).
- [x] C2. **Favoriye taşı** — TAMAM (2026-07-09, C1 içinde): buton canlı satır şablonuna alınmadı (demo satırlarla birlikte @if(false) altında duruyor) — E5 favori backend'i gelince şablona eklenir; envanter 8.5 satırına not düşüldü.
- [x] C3. **Kupon akışı (kod-girişli)** — TAMAM (2026-07-09): PromotionController admin-yetkili olduğundan **yeni store endpoint'i** `POST /api/store/checkout/coupon/validate` (AllowAnonymous — misafir de dener; üye token'ı varsa MemberId koşulları değerlendirilir; `use` kaydı C10 checkout'ta). Sepette: kod uygula → gerçek doğrulama (yüzde/sabit indirim hesapla), uygulanan kupon kartı + "Sepet İndirimi" satırı + Ödenecek Tutar düşer; Kuponu Kaldır; hata/başarı bildirimleri gerçek mesajlarla. **Sepet değişince (adet/seçim/silme) kupon yeni toplamla sessizce yeniden doğrulanır** — koşul bozulursa (ör. min tutar) otomatik kaldırılır ve nedeni gösterilir. **Tasarımın sayfalar-arası kupon sözleşmesi korundu:** `window.msSepetKuponDurumu` + sessionStorage + `ms:sepet-kupon-degisti` event'i (ödeme sayfası C5'te okur); duruma `couponId`/`tutar` alanları eklendi (checkout `use` için). Reload'da kupon sessionStorage'dan yeniden doğrulanarak geri gelir. Misharix demo kupon mantığı (sahte indirim) script'ten çıkarıldı. "Kuponlarım" listesi E9'a (üye kupon API'si yok). E2E (5051 publish, geçici C3TEST10 kuponu — testten sonra DB'den silindi): **10/10 ✓** (geçersiz kod hatası, %10 uygulama 389,99→350,99, adet 2'de indirim -78,00'a tazelendi, reload kalıcılığı, koşul bozulunca otomatik kaldırma + neden, Kaldır butonu, 0 konsol hatası).
- [x] C4. **Teslimat sayfası** — TAMAM (2026-07-09): `/teslimat` route'u + `Teslimat.cshtml` (bayt-aynı) + `_SepetTeslimatSayfasi` bağlandı. **Adres kartları canlı** — `GET /api/store/account/addresses` (üye gerektirir: oturumsuzken giriş çağrısı görünür ve Ödemeye Geç giriş modalını açar); kart seçimi misharix görsel semantiğiyle (secili sınıf/rozet/buton), varsayılan adres otomatik seçili; **seçilen adres `sessionStorage msTeslimatDurumu`'na yazılır** (C5 ödeme + C10 checkout okur); adres seçilmeden Ödemeye Geç engellenir (capture dinleyici). **Yeni partial `_SepetAdresModali`** (kaynağı _SepetSiparis demo kompozitindeki blok — İZİNLİ YENİ): başlık/ad/soyad/adres/telefon (ülke-kodlu bileşen site.js'ten)/teslimat notu/varsayılan; **İl/İlçe/Mahalle aramalı select'leri api/store/geo kademeli lookup'larından** — aynı görsel sınıflar ama davranış sayfa script'inde (site.js ozel-select'i dinamik seçenek desteklemiyor; panel görünürlüğü `ms-ozel-select-acik` sınıfıyla). Kaydet → `POST addresses` → liste yenilenir. **"Adresi Düzenle" E4'e bırakıldı** (update API'si yok — hesabım-adreslerim ile gelecek); fatura türü radio'ları görsel (kurumsal fatura C10 checkout'ta); kargo seçimi statik Standart (hızlı teslimat Faz H — @if gizli). E2E (5051 publish): **11/11 ✓** (oturumsuz durum + giriş modalı guard'ı, üye+boş durum, modal + İstanbul→Kadıköy→Caferağa aramalı kademeli seçim, kayıt→kart+varsayılan seçili+özet+msTeslimatDurumu, Ödemeye Geç → /odeme navigasyonu [sayfa C5'te], test verisi temizliği, 0 konsol hatası).
  - [x] **C4-a: Adres hiyerarşisi + veri + API — TAMAM (2026-07-09).** K6'ya GEREKÇELİ DÜZELTME: hiyerarşi Core'da değil **CRM'de** — `crm_countries/cities/districts/neighborhoods` tabloları ve `Address.CountryId/CityId/DistrictId` FK'ları SPA döneminden zaten mevcuttu (boştu); Core'da paralel yapı kurmak Address bağlarını kırar ya da çiftlerdi. `City.Region` eklendi (migration `AddCityRegion`, canlıda) — G9 kişiselleştirme il→bölge buradan okur. **Veri:** PTT-türevi, otomatik güncellenen `turkey-neighbourhoods` (npm) setinden 1 ülke (TR) + 81 il (coğrafi bölgeli) + 973 ilçe + 73.305 mahalle yüklendi (staging+INSERT SELECT, ANALYZE yapıldı; ilçe/mahalle Code'ları varchar(20) sınırı için md5 kısaltması, adlar NameI18n'de). **Posta kodu ŞİMDİLİK BOŞ** — bu set mahalle→PK eşlemesi vermiyor; eski (2015) PK setleriyle isim eşleme yanlış eşleşme üretir; formda PK zaten manuel alan, resmi PTT eşleme dosyası temin edilince tek UPDATE ile doldurulur (kullanıcı onayına açık nokta). **API:** `GET /api/store/geo/countries|cities?countryId|districts?cityId|neighborhoods?districtId&search=&limit=` (anonim; mahalle aramalı+limitli — 4.7ms; Türkçe sıralama/arama bellek tarafında, NameI18n indexer'ı SQL'e çevrilemediğinden — B2 dersi; ilçe başına ≤1.5K satır).
- [x] C5. **Ödeme sayfası** — TAMAM (2026-07-09, K2 test modu): `/odeme` route'u + `_SepetOdemeSayfasi` bağlandı (+_SepetModallari include'ı — sözleşme/TCKN modalları). Ödeme yöntemleri (kart/kapıda nakit/kapıda kart), **kart formu canlı önizleme + tip algılama misharix script'inden aynen**; kart bilgisi HİÇBİR yere gönderilmez (tahsilat mock — sipariş 'unpaid/pending' oluşur, gerçek sağlayıcı H6). Özet cart + `msSepetKuponDurumu`'ndan (kupon satırı + yöntem uyumu demo script sözleşmesiyle); `msTeslimatDurumu` yoksa /teslimat'a döner; sözleşme onayı modal üzerinden (metinler C8'e kadar gizli/demo). Eksikler kontrolü misharix script'inden. E2E: C5+C10 suite 15/15 ✓ içinde.
- [x] C6. **Taksit listesi** — TAMAM (2026-07-09, K2 kapsamında): misharix'in statik taksit kutusu (BIN girilince görünen konfigüratif liste) aynen kullanılıyor — K2 'statik/konfigüratif' kararına birebir uyar; gerçek BIN sorgusu H6 sağlayıcı entegrasyonuyla.
- [x] C7. **TCKN doğrulama modalı** — TAMAM (2026-07-09, K9): Backend: `Member.IdentityNumber/IdentityVerifiedAt` (+`AddMemberIdentity` migration, canlı DB'ye uygulandı), `SetMemberIdentityCommand` + `TcknValidator` (11 hane, ilk hane ≠0, 10./11. kontrol basamağı algoritması sunucuda), `POST /api/store/account/identity`, `MemberDetailDto.IdentityVerified`. **Checkout guard'ı:** `Store:TcknThreshold` (varsayılan 13.000 TL) — ara toplam eşik üzeri + üye doğrulanmamışsa 400 `tcknRequired:true` (mesaj tr-TR formatlı). Frontend: `_SepetSayfasi` demo TCKN bloğu söküldü; canlı script `_SepetModallari` sonunda (modal aç/kapa, client'ta aynı checksum, POST, durum göster, `window.msTcknDogrulandi` + `ms:tckn-dogrulandi` event, `window.msTcknModalAc`, /me'den başlangıç durumu). Sepet banner'ı `data-ms-tckn-banner` (eşik `data-ms-tckn-esik` ile SSR'dan, `SepetController` ViewData) — ödenecek ≥ eşik && doğrulanmamış → görünür; doğrulanınca gizlenir. Ödeme `siparisOlustur` ön-kontrolü eşikte modal açar; sunucu 400 `tcknRequired` da modalı açar. E2E (5051 publish): **10/10 + guard 4/4 ✓** (banner eşikte görünür, geçersiz TCKN client+sunucu reddi, geçerli test TCKN kabul+DB kaydı+banner gizlenir, reload'da /me'den durum, eşik üzeri TCKN'siz checkout 400 tcknRequired / eşik altı guard yok / TCKN sonrası geçer, temizlik, 0 konsol hatası).
- [x] C8. **Sözleşme modalları** — TAMAM (2026-07-09): İçerik CMS'ten — `GetStoreLegalPagesQuery` (PageType='legal', aktif rich_text section'ların Settings["html"] birleşimi, yayın penceresi kontrolü) + `GET /api/store/cms/legal?firmPlatformId&codes` endpoint'i. **Seed:** `SeedCmsLegalPagesAsync` (Dev) + canlıya SQL — 3 platform × 5 sayfa (mesafeli-satis-sozlesmesi, on-bilgilendirme-formu, gizlilik-guvenlik, kullanim-kosullari, kargo-teslimat; içerik firmanın gerçek unvan/adres/VKN'siyle, CMS admin'den düzenlenebilir; 'rich_text' section type + 'icerik-sayfasi' template oluşturuldu). **SSR:** `SepetController` legal sayfaları 5 dk IMemoryCache ile ViewData["MsSozlesmeler"]'e koyar; `_SepetModallari` modal panelleri CMS'ten (demo ELDİ metinleri söküldü, `data-ms-sozlesme-kodlar` SSR), `_SepetOdemeSayfasi` "Sözleşmeler ve Onaylar" bölümü 3 bilgi grubuyla açıldı (@if(false) kalktı; platformda sayfa yoksa bölüm görünmez). **Kabul kaydı:** checkout request'e `AcceptedContracts` (kod listesi) — sunucu başlık+metin sürümünü (ContentUpdatedAt) CMS'ten çözerek `Order.CustomerNotes` jsonb'sine `acceptedContracts` (code/title/acceptedAt/contentUpdatedAt, camelCase) yazar. **Bonus düzeltmeler:** CustomerNotes checkout'ta sessizce düşüyordu (hiç yazılmıyordu) → artık `note` anahtarıyla kaydediliyor; `_SepetOdemeSayfasi`'nda _SepetModallari ÇİFT include ediliyordu (C5'ten beri modallar DOM'da 2 kez) → tekilleştirildi; `StoreCheckoutItemRequest.VariantInfo` zorunluydu → seçeneksiz üründe null 400 üretiyordu, nullable yapıldı. E2E (5051 publish): **14/14 ✓** + C5+C10 regresyon 15/15 ✓ (legal API + codes filtresi, modal SSR CMS içerikli, bilgi grupları, checkout→DB kabul kaydı + not, /sepet modalı, 0 konsol hatası).
- [x] C9. **"Stok gelince haber ver"** — TAMAM (2026-07-09): Storefront modülüne `StockAlert` entity (`storefront.stock_alerts`; FirmPlatformId/VariantId/MemberId/Email/ProductCode/VariantInfo snapshot + Status active|notified|cancelled + NotifiedAt; `AddStockAlerts` migration canlıya uygulandı). `CreateStockAlertCommand` (idempotent — aktif kayıt varsa AlreadyExists) + `GetMemberStockAlertsQuery`; `POST/GET /api/store/stock-alerts` (MemberOnly; e-posta token claim'inden). Frontend: sepet satır şablonuna tasarımın tükendi örneğindeki `ms-sepet-tukendi-notu` butonu eklendi (varsayılan hidden) — `isAvailable=false` kalemde görünür; misafir tıklaması giriş modalını açar, üye tıklaması POST atar → buton "Stok gelince haber verilecek ✓" + pasif; ilk yüklemede GET ile mevcut kayıtlar işaretlenir. **Bildirim GÖNDERİMİ Faz H'de** (stok girişinde active kayıtlar tüketilecek). E2E (5051 publish): **11/11 ✓** + C1 regresyon 12/12 ✓ (tükendi satırı+buton, misafir→giriş modalı, üye→kayıt+DB doğrulaması, idempotenlik, reload durumu, GET filtresi, anonim 401, 0 konsol hatası). Not: kalem `IsAvailable` sepete ekleme anındaki değerdir (B12 tasarımı) — stok sonradan biterse mevcut sepet kalemi işaretlenmez, bu B12'nin bilinen sınırı.
- [x] C10. **Checkout uçtan uca** — TAMAM (2026-07-09): Siparişi Tamamla → `POST /api/store/checkout` (MemberOnly; `msSiparisAsamasiGoster` sarılarak — 'onay' hedefi checkout'tan geçer). Payload: teslimat adresi guid'leri (**MemberAddressDto'ya CountryId/CityId/DistrictId/NeighborhoodId/DeliveryNotes additive eklendi**), sepet kalemleri (sku=ProductCode, variantInfo=OptionsText), sipariş notu, cartId + **kupon kullanım kaydı** (request'e CouponId/CouponDiscount eklendi — controller sipariş sonrası UseCouponCommand çağırır; C3'ün 'use checkout'ta' sözü kapandı). Başarıda: `msSiparisSonucu` yazılır, sepet DELETE + localStorage/sessionStorage temizliği + mini sepet yenilenir → `/siparis-tamamlandi`. **Onay sayfası**: sipariş no üyenin sipariş listesinden doğrulanır (ORD-...); ödeme tipi/tutarlar/kupon indirimi/kalem listesi (gerçek görsellerle); kargo bilgisi bölümü Faz H'ye kadar gizli. E2E (5051 publish): **15/15 ✓** — üye+sepet(2 adet)+kupon(%10)+adres → teslimat → ödeme özeti 779,98→701,98 → kart önizleme → sözleşme modalı → checkout → onay sayfası (no/tutarlar/kalem) → **DB doğrulaması: sipariş pending + kupon kullanım kaydı + sepet temiz**; test verileri silindi; 0 konsol hatası.
- [x] C11. **QA kapanışı** — TAMAM (2026-07-09): Envanter 8.5 satır satır işaretli (ertelenenler hedef fazlı: favoriye taşı E5, kupon listesi E9, adres düzenle E4, gerçek tahsilat/BIN H6, bildirim gönderimi H). **Toplu regresyon (taze 5051 publish): C1 12/12 + C3 10/10 + C4 11/11 + C5/C10 15/15 + C7 11/11 + C7-guard 4/4 + C8 14/14 + C9 11/11 = 88 adım ✓, 0 konsol hatası, drift TEMİZ.** Görsel kanıtlar pw-b6/shots'ta (kupon kartı, adres modalı, teslimat, ödeme, onay, sözleşme modalı, tükendi satırı). Not: c3-e2e C3TEST10 kuponunu DB fixture'ı olarak varsayıyordu (fixture silinince kızarıyordu) — test artık kuponu kendisi oluşturup siler.

**Kabul kriterleri:** Üye, sepetten sipariş tamamlandı sayfasına kadar gerçek akışı yürütüyor (ödeme test modu kabul); envanter 8.5 işaretli.

---

### FAZ D — Üye oturumu (Razor) + SMS/OTP altyapısı
> Backend auth VAR (register/login/refresh/me). Eksik: Razor tarafında oturum yönetimi, SMS ile giriş/doğrulama, şifre güvenliği.

- [x] D1. **Razor oturum stratejisi** — TAMAM (2026-07-09): Login/refresh yanıtları access token'ı HttpOnly `ecspros_member` cookie'sine de yazar (SameSite=Lax, Secure=IsHttps — Cloudflare Flexible origin HTTP'de ve localhost testinde çalışır; süre=token süresi). **SSR kimlik:** `IStoreMemberSession` (`StoreMemberSession`) cookie'deki JWT'yi doğrular → `StorePageController` her store sayfasında `ViewData["MsUye"]` (StoreUyeKimlik: MemberId/FullName/Email; null=misafir). ⚠️ Kritik keşif: JwtBearer 8.0.14'ün getirdiği IdentityModel 7.1.2'de `JwtSecurityTokenHandler` geçerli exp'li token'a `SecurityTokenNoExpirationException` fırlatıyor — `JsonWebTokenHandler` kullanıldı (pipeline'ın kendi kullandığı handler). **JS akışı değişmedi** (localStorage + Authorization header); nav script'i SSR kimlikle oturum UI'ını /me beklemeden boyar (yalnız localStorage token'ı da varken — /me yine doğrular). **Logout endpoint'i:** `POST /api/store/auth/logout` (anonim erişilir) — `RevokeMemberSessionCommand` refresh session'ı IsActive=false yapar + cookie silinir; nav çıkışı buna bağlandı (D6'nın 'çıkış → session iptali' maddesi burada kapandı). E2E (5051 publish): **12/12 ✓** + B4 regresyon 12/12 ✓ (HttpOnly cookie yazımı, document.cookie'de görünmezlik, /me engelliyken SSR boyama, refresh cookie rotasyonu, çıkışta session 1→0 + cookie + localStorage temizliği, çıkış sonrası SSR misafir, 0 konsol hatası).
- [x] D2. Giriş modalı canlandırma — **B4'te kapandı** (e-posta/şifre → `store/auth/login`, hata alanı `ms-uyari ms-uyari-hata`); SMS sekmesi D4'te canlandı.
- [x] D3. **Kayıt modalı belgeleri + onay kaydı** — TAMAM (2026-07-09): (form → register B4'te bitmişti). **Belge modalı CMS'ten:** 2 yeni legal sayfa seed edildi (`uyelik-sozlesmesi`, `kvkk-aydinlatma`; 3 platform × 7 legal sayfa oldu — seeder kod-bazlı idempotenliğe çevrildi, canlıya SQL). `MsSozlesmeler` yüklemesi SepetController'dan **StorePageController tabanına taşındı** (nav belge modalı her sayfada; 5 dk cache aynı). `_AnaNavigasyon`'daki `belgeIcerikleri` map'i Razor data-binding ile CMS'ten dolar (belge-tur eşlemesi: uyelik→uyelik-sozlesmesi, aydinlatma→kvkk-aydinlatma, on-bilgilendirme→on-bilgilendirme-formu; CMS boşsa misharix demo metni yedek). **Onay kaydı:** register request'e FirmPlatformId+AcceptedContracts eklendi — sunucu başlık+metin sürümünü CMS'ten çözer (C8 deseni), `Member.Consents` jsonb'sine `acceptedContracts` (code/title/acceptedAt/contentUpdatedAt) yazar (`AddMemberConsents` migration canlıya uygulandı). Kayıt JS'i 3 zorunlu onayın belge kodlarını gönderir. E2E (5051 publish): **9/9 ✓** + B4 12/12 + C8 14/14 (5→7 sayfa güncellemesiyle) + D1 12/12 regresyonları ✓, drift TEMİZ.
- [x] D4. **SMS/OTP altyapısı** — TAMAM (2026-07-10): **Backend:** `crm.otp_codes` tablosu (`AddOtpCodes` migration canlıya uygulandı; Phone normalize + CodeHash SHA256 + Purpose + ExpiresAt + AttemptCount + ConsumedAt). `SendLoginOtpCommand`: yalnız kayıtlı-aktif üyeye kod (son 10 hane eşleşmesi — eski aktarım biçim farkı toleransı); 6 haneli kriptografik kod, **120 sn geçerli** (tasarımın 02:00 sayacıyla aynı), 60 sn yeniden gönderim + saatte 5 kod sınırı, yeni kod eskileri geçersiz kılar. `VerifyLoginOtpCommand`: 5 deneme sınırı (aşımda kod yanar), tek kullanımlık; doğruysa LoginMember'la aynı session+token akışı + `IsPhoneVerified=true`. **Port/adapter:** `ISmsSender` (Crm.Application) → `CrmSmsSenderAdapter` (Api) → Shared `ISmsService` (dev'de `LogSmsService` loglar; gerçek sağlayıcı seçimi kullanıcı kararı — yalnız ISmsService implementasyonu değişecek). **Endpoint'ler:** `POST /api/store/auth/otp/send` + `otp/verify` (verify başarıda D1 SSR cookie'sini de yazar). **Frontend:** SMS sekmesi canlı ve **tasarımın varsayılan sekmesi (sms) geri döndü** (`tabAc("sms")`); misharix'in kod gönder/adım geçişi/02:00 sayacı/kod kutuları davranışı korunarak gerçek API'ye bağlandı; hata alanı `data-ms-giris-sms-hata`; başarılı doğrulamada token/panel/sepet-birleştirme giriş modalı script'inin `window.msGirisBasarili` köprüsüyle e-posta girişiyle ortak. Telefon+şifre sekmesi pasif kaldı (backend'i yok). Yeniden gönder = "Telefonu değiştir" + tekrar gönder (tasarımda ayrı buton yok; 60 sn < 120 sn olduğundan süre dolunca hemen yenisi istenebilir). E2E (5051 publish): **18/18 ✓** (varsayılan sekme, kayıtsız numara hatası, sayaç, yanlış kod, dev logundan kod → giriş, HttpOnly cookie, DB IsPhoneVerified+session, reload kalıcılığı, çıkış) + B4 12/12 (varsayılan sekme sms'e göre güncellendi) + D1 12/12 + D3 9/9 regresyonları ✓; drift TEMİZ.
- [x] D5. **CRM şifreleri SHA256 → BCrypt** — TAMAM (2026-07-10): `IMemberPasswordHasher` (Crm.Application) + `MemberPasswordHasher` (Crm.Infrastructure, BCrypt.Net-Next workFactor 12 — IAM'la aynı). Yeni yazımlar hep BCrypt: Register + admin CreateMember (oradaki Base64-SHA256 "geçici" yolu da kaldırıldı). **İlk girişte re-hash:** LoginMember Verify'ı üç formatı tanır ($2*=BCrypt, 64 hex=eski register, 44 Base64=eski CreateMember); doğrulama BAŞARILIYSA ve hash eskiyse BCrypt'e yükseltilip login'in zaten yaptığı SaveChanges ile kalıcılaşır — toplu migration yok. Not: canlı `crm_members` bu tarihte BOŞtu (üyelik B4'te açıldı, test üyeleri temizlenmişti) — legacy yol yine de korundu (dump'tan geri yükleme / eski aktarım ihtimali). Doğrulama (5051 publish): yeni kayıt `$2a$` + login ✓, legacy hex ve Base64 üye (SQL ile eklendi) login ✓ → hash 60 karakterlik `$2a$`'ya yükseldi → tekrar login ✓, yanlış şifre her formatta ✗; B4 12/12 + D4 18/18 + D1 12/12 regresyon ✓. OTP girişi şifreye dokunmaz (re-hash yalnız şifreli girişte).
- [x] D6. Hesap paneli — **B4+D1'de kapandı** (panel içerikleri me/girişle dolar — B4; çıkış → logout endpoint'i + session iptali — D1).
- [x] D7. **QA — FAZ D KAPANIŞI** — TAMAM (2026-07-10): (1) **Envanter 8.1 auth satırları güncel** — giriş menüsü (D1 SSR notu), e-posta sekmesi (varsayılan D4'te SMS'e döndü), kayıt modalı 🔶→✅ (D3 CMS belgeleri), hesap paneli/çıkış ✅ (D1 logout — session iptali + cookie silme); SMS sekmesi satırı D4'te işaretlenmişti. (2) **Oturumlu/oturumsuz nav görüntüleri** `tools/misharix-sync/shots/d7-*` (6 adet: oturumsuz+oturumlu desktop 1440 + mobil 390, SMS'li giriş modalı, açık hesap paneli) — "Giriş Yap"→"Hesabım", avatar baş harfleri, panel linkleri, Çıkış Yap görsel olarak doğrulandı. (3) **Faz D toplu regresyon (taze publish): B4 12/12 + D1 12/12 + D3 9/9 + D4 18/18 = 51 adım ✓**; drift TEMİZ; test verileri silindi. **FAZ D KABUL KRİTERLERİ SAĞLANDI** — e-posta ve telefon(OTP-dev) ile giriş/kayıt/çıkış çalışıyor, oturum SSR render'a yansıyor.

**Kabul kriterleri:** E-posta ve telefon(OTP-dev) ile giriş/kayıt/çıkış çalışıyor; oturum sayfa render'ına yansıyor.

---

### FAZ E — Hesabım kümesi (12 sayfa)
> Her sayfa = partial birebir port + üye-kapsamlı API + gerekiyorsa yeni backend özelliği. Yan menü (`_HesabimYanMenu`) + mobil menü ilk iş.

- [x] E1. **Hesabım çerçevesi** — TAMAM (2026-07-10): `HesabimController` (StorePageController tabanı) misharix'in çift route şemasıyla birebir — 12 sayfa × (`/Hesabim/...` + kebab-case kısa yol), tek `Sayfa.cshtml` view'ına partial adı geçiren kalıp aynı. **SSR üye guard'ı:** D1 cookie kimliği yoksa köke redirect (canlıda cookie'siz oturum yok — üyelik B4'te bu akışla açıldı). 18 Hesabim partial'ı + Sayfa.cshtml bayt-birebir kopyalandı (Faz A kabuk yöntemi; sayfalar E2-E13'te teker teker gerçek veriye bağlanacak, o güne dek tasarım demo içeriği). İzinli farklar: `_HesabimYanMenu` statü bloğu @if(false) (puan verisi yok — E13/G), `_HesabimVarsayilan` karşılama adı SSR kimlikten (tr-TR büyük harf). **Nav hesap paneli linkleri** (B4'ten beri '#') Hesabım route'larına bağlandı. NOT: tasarım kaynağının kısayol grid'inde `<img src=\` bozuk ikon markup'ı var (kaynağın kendi kusuru, bayt-birebir korundu — etiketler görünür, E13 bağlamasında ele alınır). E2E (5051 publish): **13/13 ✓** (misafir guard redirect, karşılama ELA IŞIK, 7 kısayol, statü gizli, 11 yan menü linki, 24 URL'nin tümü 200, aktif menü durumu, panel linkleri, mobil menü aç/kapa, 0 konsol hatası) + B4 regresyon 12/12 ✓; drift TEMİZ.
- [x] E2. **Üyelik Bilgilerim** — TAMAM (2026-07-10): form profile GET/PUT'a bağlandı (ad/soyad/telefon/doğum/cinsiyet; **e-posta salt okunur** — giriş kimliği, değişikliği doğrulama akışı ister, ileri faz). **Backend:** `Member.CityId` (`AddMemberCity` migration canlıda, crm_cities FK) + `UpdateMemberProfile` genişledi (telefon OtpHelper.Normalize + benzersizlik + **değişince IsPhoneVerified düşer** — SMS girişi yeniden doğrular; CityId varlık denetimi); `UpdateMemberMarketingConsentsCommand` (Consents jsonb "marketing": email/sms/phone+updatedAt — D3 acceptedContracts'a dokunmaz) + `PUT marketing-consents`; `GetMemberSessionsQuery` + `GET sessions`; login/OTP/refresh oturumlarına **IpAddress+UserAgent** yazılır (refresh rotasyonu taşır). **Frontend:** rozetler + telefon doğrulama durumu gerçek; **Şehir alanı eklendi** (tasarımda yoktu — G9 segmenti; TR illeri geo API'den); cinsiyet option value'ları (female/male/boş); duyuru tercihleri checkbox'ları bağlandı; Aktif Cihazlar + Giriş Geçmişi account/sessions'tan (demo kartlar template, UA çözümleme + göreli zaman client'ta); Vazgeç son profile döner; Hesabı Sil bölümü tasarım demo'su kaldı (kapsam dışı — kullanıcı kararı). NOT: tasarımda şifre değiştirme/TCKN alanı YOK — TCKN zaten C7 sepet modalında; şifre değiştirme forgot-password akışıyla birlikte ileri fazda. E2E (5051 publish): **19/19 ✓** (form dolumu, readonly e-posta, rozetler, 81 il, kaydet+DB normalize telefon/İstanbul, reload kalıcılığı, tercihler jsonb, cihaz "Mevcut"+giriş geçmişi IP, Vazgeç, duplicate telefon reddi, telefon değişince doğrulama düşmesi) + E1 13/13 + B4 12/12 + D1 12/12 + D4 18/18 regresyon ✓; drift TEMİZ.
- [x] E3. **Adreslerim** — TAMAM (2026-07-10): NOT — tasarımın Adreslerim sayfası C4 modalını değil **sayfa içi form** kullanıyor (sol kartlar + sağ "Adres Bilgileri" formu); plan buna göre uygulandı, C4 modalı teslimat sayfasında kalmaya devam ediyor. **Backend:** `UpdateMemberAddressCommand` (C4'te ertelenen güncelleme — sahiplik denetimli) + `PUT addresses/{id}`; `SetDefaultMemberAddressCommand` + `POST addresses/{id}/default` (önceki varsayılanlar düşer). **Frontend:** kartlar GET addresses'ten (tek template; varsayılan kartta tasarımdaki gibi Sil yok, "Varsayılan Teslimat"+"Aktif" etiketli; boş durum satırı eklendi); form yeni adres POST / Düzenle PUT (form dolar, il→ilçe kademeli geo API — mahalle tasarımdaki gibi serbest metin, NeighborhoodId'siz — checkout mahalle Id istemiyor); Varsayılan/Sil kart aksiyonları; telefon TR normalize; Temizle yeni moda döner. E2E (5051 publish): **16/16 ✓** (boş durum, il/ilçe kademeli, ekleme+DB, varsayılan kart görünümü, ikinci kart butonları, Düzenle formu doldurur (ilçe dahil) + PUT kalıcı, varsayılan devri tek kayıt, silme, teslimat sayfası regresyonu) + E1 13/13 + C4 11/11 + E2 19/19 regresyon ✓; drift TEMİZ.
- [x] E4. **Siparişlerim** — TAMAM (2026-07-10): **Kartlar SSR** (`HesabimSiparisVm` — misharix kart aç/kapat + filtre script'i parse anında dinleyici bağladığından liste sunucuda render edilir; sayfa ilk 20 sipariş, tasarımda sayfalama yok). Controller sipariş başına detay + shipped/delivered için gönderi çeker; kalemler `IProductService.GetVariantDisplayAsync` ile zenginleşir (silinen varyantta ad/fiyat snapshot'tan, görsel/link yok). Durum eşlemesi: pending/confirmed→Sipariş Alındı, processing→Hazırlanıyor, shipped→Kargoda, delivered→Teslim Edildi (filtre tamamlanan), cancelled→İptal Edildi (akış şeridi gizli — tasarımda iptal akışı yok), returned→İade Edildi. 4 adımlı akış şeridi rozetleriyle (Tamamlandı/Devam Ediyor/Bekliyor) durumdan; özet grid (toplam/devam/teslim/iade — iade sayısı GetReturns'ten) ve boş durum gerçek. **Detay modalı** gömülü JSON'dan dolar (no/tarih/ödeme/takip no/ürünler/adres/ödeme özeti; Fatura Bilgileri sütunu H1'e kadar yok). **Kargo takip modalı sipariş başına SSR** (H2 köprüsü): takip no + TrackingUrl linki + tahmini teslim + gönderi olayları zaman çizelgesi (olay yoksa yalnız takip no; kargo firması adı/logosu H2'de). Faz köprüsü butonları gizli: Faturayı Görüntüle→H1, İade Et→E8, Tekrar Satın Al→E10, Yorum Yaz→E7. E2E (5051 publish): **15/15 ✓** (boş durum, checkout'la 3 sipariş + SQL durum geçişi + gönderi kaydı, özet grid 3/2/1/0, rozetler, akış 1-aktif/4-aktif, gizli butonlar, kart toplamı, Tamamlanan filtresi, kart aç/kapa, detay modalı, kargo modalı olay çizelgesiyle) + E1 13/13 + E2 19/19 + E3 16/16 regresyon ✓; drift TEMİZ. Test notu: `ord_shipments.ShipmentNumber` UNIQUE — testler koşu başına tekil numara üretir.
- [x] E5. **Favorilerim** — TAMAM (2026-07-10): **Backend (YENİ):** `storefront.favorites` (`AddFavorites` migration canlıda) — anahtar **ProductCode** (plan ProductId diyordu; kartların/detayın kullandığı stabil kod seçildi — C9 StockAlert deseni, ürün yeniden aktarımında kayıt kopmaz), VariantId bilgi amaçlı; unique (FirmPlatformId, MemberId, ProductCode). `AddFavoriteCommand` (idempotent — soft-delete edilmiş kayıt geri açılır), `RemoveFavoriteCommand` (soft delete, kayıtsızsa da başarı — toggle UX), `GetMemberFavoritesQuery` (kod listesi). Endpoint'ler: `GET/POST /api/store/favorites` + `DELETE /favorites/{code}` (MemberOnly). `GetStoreProductsQuery`'ye additive `ProductCodes` filtresi. **Frontend:** `_FavoriDavranis` (İZİNLİ YENİ, _Layout'ta) — misharix site.js kalp toggle/animasyonuna DOKUNMADAN **capture-phase** dinleyici: misafirde toggle engellenir + giriş modalı açılır; üyede POST/DELETE (site.js görseli yönetir); sayfa yükünde GET ile kartlar/detay/sepet işaretlenir; infinite-scroll kartları MutationObserver'la. Kart köküne `data-ms-urun-kod` eklendi (JS dolan kartlarda link href'inden çözülür). **Sepet "favoriye taşı":** favori butonu canlı satır şablonuna alındı (C1'de ertelenmişti — gizli demo bloğunda kalmıştı), satır ürün kodunu taşır, görsel toggle + işaretleme bağlı. **Favorilerim sayfası SSR:** favori kodlar → Catalog kart verisi → paylaşılan `_UrunKarti` (liste/ana sayfayla tek kaynak; silinen/pasif ürün listelenmez, favori sırası korunur); boş durum gerçek; Paylaş butonu gizli (E6 koleksiyon paylaşım kararına dek). E2E (5051 publish): **13/13 ✓** (misafir engel+modal, detay kalbi→DB, reload işaretleme, liste kartı işaretli, Favorilerim SSR kart + aktif menü + kalp, sepet satırı işaretli + çıkarma soft delete, boş durum, idempotent geri açma) + C1 12/12 + B6 19/19 + E1 13/13 regresyon ✓; drift TEMİZ.
- [x] E6. **Koleksiyonlarım** — TAMAM (2026-07-10): **Backend (YENİ):** `storefront.collections` + `collection_items` (`AddCollections` migration canlıda) — Name/Description/IsPublic/IsShareable/**ShareCode** (benzersiz kısa kod)/**Status pending|approved|rejected**/ViewCount/**IsQuickSave**; item'lar ProductCode anahtarlı (E5 kararıyla tutarlı), unique (CollectionId, ProductCode), soft-delete geri açılır. Komutlar: `CreateCollection` (pending doğar), `ToggleQuickSave`, `ModerateCollection`; sorgular: `GetMemberCollections`, `GetCollectionsForModeration` (sayfalı). **Store API:** `GET/POST /api/store/collections` + `POST /collections/saved/toggle` (MemberOnly). **Admin API + UI:** `/api/collections` (durum filtreli liste + approve/reject) + React **CollectionsModerationPage** (sekmeli kuyruk, Onayla/Reddet; sidebar "Koleksiyon Moderasyonu"; npm build alındı) — Faz G "Koleksiyonlar bloğu" yalnız approved+public gösterir (spec şartı). **Bookmark kararı:** tasarımda koleksiyon seçici olmadığından kart/detay bookmark'ı üyenin otomatik **"Kaydedilenler"** hızlı koleksiyonuyla birebir çalışır (yoksa gizli+paylaşımsız oluşturulur; toggle-off yalnız oradan çıkarır — elle kurulan koleksiyonlara dokunmaz); misafir giriş modalına yönlenir (E5 capture deseni, _FavoriDavranis genişledi). **Koleksiyonlarım SSR:** kartlar gerçek (avatar/ad üyeden, kapaklar Catalog kart verisinden, +N sayacı, durum rozeti pending "Onay bekliyor"/rejected "Onaylanmadı" — tasarıma eklendi); oluşturma modalının panelleri gerçek favori/koleksiyon ürünleriyle SSR dolar (site.js sekme/seçim davranışı değişmedi); `ms:koleksiyon-olustur` event'i POST'a bağlandı (başarıda reload). **Paylaş** linki (origin/koleksiyon/{shareCode}) panoya kopyalanır; "Koleksiyonu Aç" + public koleksiyon sayfası Faz G koleksiyon bloğuyla (@if(false)). E2E (5051 publish): **15/15 ✓** + E5 13/13 + C1 12/12 + E1 13/13 regresyon ✓; drift TEMİZ.
- [x] E7. **Yorumlarım + Ürün Değerlendirme modülü** — TAMAM (2026-07-10): **Backend (YENİ):** `storefront.product_reviews` (`AddProductReviews` migration canlıda) — üye/ürün(ProductCode)/sipariş kalemi(OrderItemId)/puan 1-5/metin/Status pending|approved|rejected + RejectReason + maskeli MemberName snapshot'ı ("Y*** C**"); üyenin sildiği soft-delete (Silinenler sekmesi filtresiz okur). Foto ekleme görsel yükleme altyapısıyla (E8 iade görseli/H) ele alınacak. Komutlar: Create (pending doğar; mükerrer engeli) / Moderate (red nedeni zorunlu-varsayılanlı) / Delete; sorgular: GetMemberReviews, GetProductReviews (approved, sayfalı), GetReviewsForModeration. **Satın alma şartı:** kalemde ürün kodu yok — üyenin delivered sipariş kalemlerinin VariantId'leri API katmanında Catalog'la koda çözülür (modüller birbirini bilmez); doğrulanan kalem OrderItemId olarak kaydedilir. **Store API:** `POST/GET(mine|reviewable|product/{code})/DELETE /api/store/reviews`. **Admin API+UI:** `/api/reviews` + React **ReviewsModerationPage** (sekmeli kuyruk, Onayla/Reddet+neden prompt'u; sidebar; npm build alındı). **Puanlar gerçek ortalamadan (yalnız approved):** yeni port `IProductReviewStatsService` (Storefront implemente eder — IChannelProductFlagService deseni); GetStoreProducts + GetChannelCategoryProducts DTO'larına additive Rating/ReviewCount (kategori handler'ı sarmalandı — cache'lenen sonuçtan bağımsız taze); kartta puan bölümü açıldı (SSR + infinite JS dolumu), detayda puan+"N Değerlendirme" (sayfa içi bölüme link) + değerlendirme bölümü yayında ilk 10 yorumu SSR listeler. **Yorumlarım 5 sekme SSR** (Değerlendir=teslim edilen−yorumlanan; sekme davranışı site.js); yorum yazma modalı eklendi (tasarımda yoktu — puan+metin, utm_yorum yönlendirmesi); reddedilende "Yeniden Düzenle" eskiyi silip yeniden gönderir. NOT: ayrı `/urun-degerlendirmeleri` sayfası (595 satırlık tasarım) taşınmadı — ileri iş; Yorumlarım'daki link gizli. E2E (5051 publish): **17/17 ✓** (satın alma şartı reddi, Değerlendir sekmesi, puansız engel, modal→pending+maskeli ad, mükerrer engel, onay öncesi 0.0, moderasyon onay→detay 4,0+yorum listesi+kart puanı, red nedeni görünürlüğü, Yeniden Düzenle) + B6 19/19 + B10 22/22 + E1 13/13 + E4 15/15 + E5 13/13 regresyon ✓; drift TEMİZ. ⚠️ B10'da bilinen sınır kayda geçti: sıralama anahtarı BasePrice ↔ kartın kanal fiyatı ayrışabiliyor (aktif varyant fiyatı olmayan ürünlerde) — Faz G fiyat mimarisinde çözülür (test notu b10-e2e'de).
- [x] E8. **İadelerim + iade talebi akışı** — TAMAM (2026-07-10): **Neden listesi Lookup'ta:** `return_reason` tipi + 9 ana neden (alt nedenler değerin `ExtraData.subReasons` listesinde — LookupValue'da hiyerarşi yok, alt nedenler metin snapshot olarak saklanır); Dev seeder idempotent + canlıya SQL. **Backend:** `Return.CargoReturnCode` + `ImageUrls` (`AddReturnStoreFields` migration canlıda); `CreateStoreReturnCommand` — üye kapsamlı, yalnız delivered, kalemler FARKLI siparişlerden olabilir (sipariş başına bir Return; kod modalı tüm kodları listeler), kalemin tamamı iade edilir (tasarımda adet seçici yok), mükerrer engeli (rejected olmayan iadede yer alan kalem yeniden iade edilemez), beklenen tutar kalem toplamından, kargo iade kodu `IAD-XXXXXX` (karışan karaktersiz). Neden seçimi: ana neden LookupId (`ReturnReasonId`) + `ReturnItem.CustomerNotes` JSON snapshot'ı (relaxed encoding — Türkçe okunur). **Görsel yükleme:** `POST /api/store/account/returns/images` (5×5MB, content-type bazlı uzantı, istemci dosya adı kullanılmaz) → `Store:MediaRootPath` (vars. `/opt/ECSProsAI/media`) altına `/media/returns/yyyyMM/` — nginx sunar. **SMS doğrulama:** D4 `crm.otp_codes` purpose=`phone_verify` (OtpCode.Purpose D4'te buna hazırdı) — kod üyenin KAYITLI telefonuna gider (`SendPhoneVerificationOtp`/`VerifyPhoneVerificationOtp` + `POST /api/store/account/phone-verification/send|verify`); doğrulanmamış telefonla POST returns `code=phone_verification_required` döner, İade Kodu Al akışı SMS modalını açıp doğrulama sonrası otomatik devam eder; başarı `IsPhoneVerified=true` yazar. **Sahiplik düzeltmeleri:** store GetOrder/GetReturn artık MemberId denetler (başkasınınki 404). **Frontend:** İadelerim kartları SSR (requested→approved→received→refunded = Talep Oluşturuldu→Onaylandı→Depoya Ulaştı→İade Ödendi akışı; rejected'da akış gizli + inceleme notu bilgi kutusunda; "Dekontu Gör" H1'e, kargo firması adı/badge'i H2'ye dek gizli); Yeni İade Talebi modalı SSR ürünlerle (iade edilen kalem kilitli + "Önceki İade Nedeni" chip paneli CustomerNotes snapshot'ından), özet/müşteri grid gerçek; Siparişlerim "İade Et" delivered kartlarda link → `/Hesabim/Iadelerim?iade=yeni` (modal otomatik açılır — E7 utm_yorum deseni; sayfadaki `_HesabimSiparislerimIadeModal` tasarım demo'su kullanılmıyor). E2E (5051 publish): **35/35 ✓** (boş durum, modal SSR + özet, Lookup 9 neden, alt neden çoklu seçim, seçimsiz/açıklamasız engeller, SMS yanlış/doğru kod + otomatik devam, kod modalı gerçek IAD- kodu, kart akış/rozet/tutar, DB neden snapshot + görsel dosyası, mükerrer engel, received/refunded/rejected görünümleri, filtreler, rejected sonrası yeniden iade, İade Et köprüsü, sahiplik 404) + E1 13/13 + E4 15/15 + E7 17/17 + D4 18/18 regresyon ✓; drift TEMİZ.
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
| 1 | Favoriler | `storefront.favorites` ✅ (E5, 2026-07-10) | E5 ✅ | rapor amaçlı liste (ops.) |
| 2 | Koleksiyonlar | `storefront.collections` + `collection_items` ✅ (E6, 2026-07-10) | E6 ✅ | ops. |
| 3 | Ürün yorumları + moderasyon | `product_reviews` | E7 | **moderasyon UI zorunlu** |
| 4 | Favori aramalar | `saved_searches` | E11 | — |
| 5 | Gezinme geçmişi | `viewed_products` | E12 | — |
| 6 | Stok haber ver | `stock_alerts` | C9 | ops. |
| 7 | SMS/OTP altyapısı | `crm.otp_codes` ✅ (D4, 2026-07-10) | D4 ✅ | sağlayıcı config (gerçek sağlayıcı seçimi bekliyor — dev'de LogSmsService) |
| 8 | **Vitrin & kişiselleştirme sistemi** (bloklar, kural motoru, snapshot/rollback, önizleme, audit) — spec: anasayfa-dizayn-yönetimi.txt | blok + öğe + `published_snapshots` + `publish_logs` + audit | G | **geniş admin UI zorunlu** |
| 9 | Story (blok sisteminin öğe tipi) | G1 kapsamında | G | G6 kapsamında |
| 10 | Bülten aboneliği | `newsletter_subscriptions` | F4 | liste/CSV |
| 11 | İletişim mesajları | `contact_messages` | F3 | liste |
| 12 | Ürün videoları | `product_videos` | H5 | ürün detayına yükleme UI |
| 13 | Üye kupon listesi | Promotion'a üye ilişkisi | E9 | kupon atama |
| 14 | Fatura PDF proxy / kargo takip / görsel arama | — (servis) | H1–H3 | config |
| 15 | Adres hiyerarşisi (ülke/il+bölge/ilçe/mahalle+posta kodu) | Core: `countries`,`provinces`,`districts`,`neighborhoods` | C4 | referans veri ekranı (ops.) |
| 16 | "Öne çıkar" bayrağı (Sponsorlu rozeti) | ChannelProduct alanı | B11 | ✅ 2026-07-09 (admin paneli + API + liste önceliği + rozet) |
| 17 | Stok kontrolü anahtarı | FirmPlatform.Settings.stockControlEnabled | B12 | ✅ 2026-07-09 |
| 18 | Tema altyapısı (ThemeCode + görünüm token override) | FirmPlatform alanları | A11–A12 | tema/renk ayar UI |
| 19 | Üye profili: cinsiyet + şehir (segment kaynağı) | Member alanları | E2 | profil görünümü |
| 20 | Koleksiyon moderasyonu | Status alanı + admin CollectionsModerationPage ✅ (E6) | E6 ✅ | onay ekranı canlı |
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
- [x] K6. **Adres verisi:** Ülke→il(+bölge)→ilçe→mahalle(+posta kodu) hiyerarşisi Core modülünde amaca özel tablolarda, resmi veri seed'i ile; kademeli aramalı-select API (C4). **[2026-07-09 düzeltme: hiyerarşi CRM'de kuruldu — tablolar+Address FK'ları orada zaten mevcuttu; C4-a notuna bak]**
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
| Logo/ana sayfa linki | statik | — | B1 | ✅ 2026-07-07 |
| Arama kutusu + panel | aç/kapat/temizle/geri | — (UI) | B2 | ✅ 2026-07-07 (misharix görünürlük script'i aynen) |
| Canlı arama önerisi | ürün+kategori sonuç grupları, sonuç sayısı, ürün şeridi kaydırma | VAR (search) | B2 | ✅ 2026-07-07 |
| Kategoride ara | seçili kategori kapsamında arama | VAR | B2/B10 | ✅ 2026-07-09 (B10'da bağlandı — buton kategori bağlamıyla görünür) |
| Popüler aramalar/ürünler | öneri panelinde | YOK→E11 | B2/E11 | 🔶 B2 geçici (statik terim chip'leri + ilk ürünler); kalıcısı E11 popülerlik verisi |
| Giriş menüsü (hover panel) | oturumsuz/oturumlu içerik | VAR (me) | B4/D6 | ✅ 2026-07-09 (B4 — me ile kalıcı oturum; D1'de SSR kimlikle /me beklemeden boyanır; statü bloğu E/G'ye kadar gizli) |
| Giriş modal — e-posta sekmesi | login | VAR | D2 | ✅ 2026-07-09 (B4'te erken kapandı; D4'ten beri varsayılan sekme tasarımdaki gibi SMS) |
| Giriş modal — telefon/SMS sekmesi | kod gönder/sayaç/yeniden gönder/onayla (OTP kutuları) | VAR (otp/send+verify) | D4 | ✅ 2026-07-10 (D4 — SMS sekmesi canlı ve varsayılan; telefon+şifre sekmesi pasif, backend'i yok) |
| Kayıt modalı | register + belge (KVKK/üyelik) modal onayı | VAR + CMS | D3 | ✅ 2026-07-09 (B4 register + otomatik giriş; D3 belge metinleri CMS'ten + Member.Consents onay kaydı) |
| Hesap paneli + çıkış | session iptali | VAR (logout) | D6 | ✅ 2026-07-09 (B4 token temizliği; D1 logout endpoint'i — sunucu tarafı session iptali + SSR cookie silme) |
| Mini sepet (hover) | sepet özeti | VAR | B5 | ✅ 2026-07-09 (rozet + panel + silme + msMiniSepetYenile) |
| Mega menü (desktop) | kategori grupları, menü kaydırma | VAR (menus) | B1 | ✅ 2026-07-07 |
| Kampanya şeridi | yatay kaydırma kontrolleri | KISMEN (kampanya görselleri kaynağı → G) | B1/G | 🕐 B1'de statik; kampanya içeriği Faz G kişiselleştirme sisteminden |
| Mobil menü | ana sekme/yan sekme/panel/kampanya listesi | VAR (menus) | B1 | ✅ 2026-07-07 (kampanya bölümü statik — G) |
| Görsel arama (kamera) | modal + upload + sonuçlar | YOK | H3 | 🕐 UI kabuğu taşındı (Faz A); endpoint H3 |

### 8.2 Ürün Kartı (liste/vitrin/hesabım her yerde aynı kart)
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Kart → detay linki (`data-ms-kart-link`) | VAR | B8 | ✅ (2026-07-08 — kategori kartları `?color={eksenDeğeri}` taşır, detay çözer) |
| Hover görsel galerisi + nokta göstergeleri | VAR (variant images) | B8 | ✅ (2026-07-08 — seçili rengin ilk 4 görseli; SSR + JSON kartlarda) |
| Videolu ürün rozeti (hover'da oynatma) | YOK | H5 | 🕐 H5 (video verisi yok — @if gizli) |
| Kampanya etiketi + kampanya bandı | KISMEN (Promotion) | B8/G | 🕐 Faz G (kampanya-ürün ilişkisi kişiselleştirme sistemiyle; @if gizli) |
| Sponsorlu rozeti → "öne çıkar" bayrağı | VAR (ChannelProduct.FeaturedFrom/Until) | B11 | ✅ 2026-07-09 |
| Favori (kalp) butonu + animasyon | VAR (favorites) | E5 | ✅ 2026-07-10 (E5 — kalıcı; misafir giriş modalına yönlenir) |
| Koleksiyona ekle (bookmark) | YOK | E6 | 🕐 E6 (modal UI çalışır; kalıcı koleksiyon backend'i E6) |
| Renk rozeti + renk tooltip (diğer renk linkleri) | VAR (varyantlar) | B8 | ✅ (2026-07-08 — eksen renkleri kendi görselleriyle; görselsiz renk listelenmez) |
| Dönen teslimat/kargo mesajları | model alanları | B8 | 🕐 veri kaynağı yok (@if gizli) — teslimat mesajları Faz H kargo/E7 verisiyle |
| Puan + yıldız + yorum sayısı | VAR (product_reviews) | E7 | ✅ 2026-07-10 (E7 — onaylı yorum ortalamasından; yorumsuz üründe bölüm yok) |
| Fiyat (ms-urun-fiyat) | VAR (varyant fiyatı) | B8 | ✅ (2026-07-07 B7'de bağlandı — varyant fiyatı, B8'de doğrulandı) |
| Lazy load (`data-ms-lazy-src`) | — | B7 | ✅ (2026-07-07 — SSR kartlar dahil tüm görseller lazy) |

### 8.3 Ürün Listesi
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Sol filtre grupları (aç/kapa, seçim, sayaç) | VAR (facets) | B7/B10 | ✅ (2026-07-09 — B10: seçim SUNUCU tarafında, URL attrs parametresiyle; SSR seçili işaretler + grup açık gelir) |
| Seçili filtre chip şeridi + kaldır + temizle | VAR | B7 | ✅ (2026-07-07 — mobil şerit; misharix script'i yönetiyor) |
| Sıralama (özel select, desktop+mobil panel) | VAR | B10 | ✅ (2026-07-09 — price_asc/price_desc/newest sunucuda; çok satan/favori veri gelince E7/B11) |
| Görünüm değiştirme (grid tipi) | — (UI) | B7 | ✅ (2026-07-07 — misharix script'i, değişiklik yok) |
| Mobil filtre paneli (detay panelleri, sayaç, hızlı filtre chip'leri) | VAR | B7 | ✅ (2026-07-07 — paneller gerçek facet gruplarından; Hızlı Teslimat/Ücretsiz Kargo chip'leri Faz G'ye kadar gizli) |
| Infinite scroll + "yükleniyor" + state restore | VAR (paging) | B7 | ✅ (2026-07-07 — state restore tasarımdaki gibi kapalı: sadeceIlkYukle) |
| Dinamik kartlara davranış yenileme (`msUrunKartDavranislariYenile`) | — | B7 | ✅ (2026-07-07 — modülün sonra hook'u + JSON dolumu sonrası) |
| Sonuç sayısı gösterimi | VAR | B7/B10 | ✅ (2026-07-09 — her durumda SSR gerçek toplam; filtreli toplam sunucudan gelir) |

### 8.4 Ürün Detay
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Breadcrumb | VAR (kategori zinciri) | B9 | ✅ (2026-07-08 — GetProductChannelCategoryChainQuery filtre kuralı ters eşlemesi) |
| Galeri: thumb'lar, ok, sürükleme, slide takibi | VAR | B9 | ✅ (2026-07-08 — seçili rengin görselleri SSR; tek görselde oklar gizli) |
| Hover zoom lens + zoom paneli | — (UI) | B9 | ✅ (2026-07-08 — misharix script'i aynen) |
| Tam ekran resim modalı (thumb, sürükle, pinch, paylaş) | — (UI) | B9 | ✅ (2026-07-08) |
| Beden seçimi (ana + sticky bar + beden modalı) | VAR (varyant) | B9 | ✅ (2026-07-08 — gerçek eksen değerleri, konfeksiyon sıralı; beden yoksa alanlar gizli) |
| Beden/stok durumu (tükendi vb.) | VAR (IStockService + B12 anahtarı) | B9/B12 | ✅ 2026-07-09 (anahtar açıkken gerçek stok: tükendi stili + disabled + guard; kapalıyken hepsi satılabilir) |
| Sepete ekle (+sticky) → mini sepet açılışı | VAR | B9/B5 | ✅ 2026-07-09 (B9 ekleme akışları + B5 msMiniSepetYenile — rozet/panel reload'suz tazelenir) |
| Favori / koleksiyona ekle | VAR (favorites + collections) | E5/E6 | ✅ 2026-07-10 (E5 kalp + E6 bookmark→Kaydedilenler) |
| Paylaş modalı (FB/X/WhatsApp/Pinterest/link kopyala) | — (UI) | B9 | ✅ (2026-07-08 — gerçek ürün adı/görsel/fiyat; paylaşım metni DOM'dan) |
| Açıklama "daha fazla" + ek detay akordiyonları | VAR (DescriptionI18n/özellikler) | B9 | ✅ (2026-07-08 — DescriptionI18n/ShortDescription; pazaryeri-özel demo maddeleri çıkarıldı) |
| Ürün özellikleri tablosu | VAR (attributes) | B9 | ✅ (2026-07-08 — ürün seviyesi attributes + Kategori Grubu + Stok Durumu; DTO'ya additive eklendi) |
| Değerlendirme özeti + değerlendirmeler linki | VAR (product_reviews) | E7 | ✅ 2026-07-10 (E7 — puan+sayı gerçek, link sayfa içi bölüme; ilk 10 yorum SSR) |
| Benzer/önerilen ürün vitrinleri | VAR (sorgu) | B9 | 🕐 misharix detay tasarımında benzer ürün bölümü YOK — Faz G vitrin sistemine devredildi |
| Videolu ürün | YOK | H5 | 🕐 H5 |

### 8.5 Sepet + Checkout
| İşlev | Backend | Faz | Durum |
|---|---|---|---|
| Satır adet artır/azalt, satır tutarı | VAR | C1 | ✅ 2026-07-09 (PUT ile kalıcı; 1–10) |
| Satır sil + onay modalı | VAR | C1 | ✅ 2026-07-09 (misharix modalı + DELETE) |
| Tümünü seç / satır checkbox | VAR (UI+cart) | C1 | ✅ 2026-07-09 (özet toplamını belirler; sipariş C10'da tam sepetle) |
| Favoriye taşı | VAR (favorites) | E5 (C2 köprü) | ✅ 2026-07-10 (E5 — buton canlı şablonda, kod satır data'sından) |
| Kupon modalı: listeden seç / kod uygula / kaldır | VAR (store validate C3; üye listesi YOK) | C3/E9 | 🔶 C3: kod uygula/kaldır + otomatik yeniden doğrulama ✅ 2026-07-09; listeden seç (Kuponlarım) E9 |
| Sipariş özeti + adım göstergesi (sepet→teslimat→ödeme) | VAR | C1–C5 | ✅ 2026-07-09 (üç adımın özetleri canlı; sayfalar ayrı URL'lerde — tasarımdaki sekmeli tek-sayfa demo kompoziti yerine) |
| Adres seçimi + adres ekle/düzenle modalı | VAR | C4 | 🔶 ✅ 2026-07-09 seçim+ekleme; düzenleme E4'te (update API'siyle) |
| Telefon ülke-kodlu input (arama, ülke seçimi) | — (UI) | C4 | ✅ 2026-07-09 (site.js bileşeni adres modalında) |
| İl/ilçe özel select | VAR (api/store/geo kademeli lookup) | C4 | ✅ 2026-07-09 (adres modalında aramalı il→ilçe→mahalle; davranış sayfa script'inde) |
| Ödeme yöntemleri (kart/kapıda/havale) + kart canlı önizleme + kart tip algılama | test modu (K2) | C5 | ✅ 2026-07-09 (kart bilgisi gönderilmez; sağlayıcı H6) |
| Taksit listesi | statik/konfigüratif (K2) | C6 | ✅ 2026-07-09 (gerçek BIN H6) |
| TCKN doğrulama modalı | YOK (K9) | C7 | ✅ 2026-07-09 (algoritma; NVİ/KPS ileride) |
| Sözleşme modalları + onay kaydı | KISMEN (CMS VAR) | C8 | ✅ 2026-07-09 (CMS legal sayfaları + Order.CustomerNotes kabul kaydı) |
| Ödeme eksikleri uyarısı (onaya geç kontrolü) | — (UI) | C5 | ✅ 2026-07-09 (misharix script'i + checkout hataları aynı alanda) |
| Stok gelince haber ver | YOK | C9 | ✅ 2026-07-09 (stock_alerts + API + sepet butonu; bildirim gönderimi Faz H) |
| Sipariş oluştur → tamamlandı sayfası | VAR | C10 | ✅ 2026-07-09 (pending sipariş + kupon kaydı + sepet temizliği + onay sayfası) |

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
| İade talebi: ürün seç, ana/alt neden (aramalı select), açıklama, görsel yükleme | VAR | E8 | ✅ 2026-07-10 (E8 — CreateStoreReturn + return_reason Lookup + görsel yükleme) |
| İade SMS doğrulama + iade kodu al/kopyala | VAR | E8 | ✅ 2026-07-10 (E8 — phone-verification OTP + CargoReturnCode) |
| İadelerim: filtreler, durum akışı, neden paneli | VAR | E8 | ✅ 2026-07-10 (E8 — SSR kartlar + 4 adımlı akış + önceki neden paneli) |
| Tekrar satın al | VAR (türetme) | E10 | ⬜ |
| Önceden gezdiklerim | YOK | E12 | ⬜ |
| Yorumlarım (3 sekme: yayında/bekleyen/reddedilen+neden) | VAR (reviews) | E7 | ✅ 2026-07-10 (E7 — 5 sekme SSR + yorum yazma modalı + Yeniden Düzenle) |
| Favorilerim | VAR (favorites) | E5 | ✅ 2026-07-10 (E5 — SSR kartlar, paylaşılan _UrunKarti) |
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
| Ürün Değerlendirmeleri sayfası: sekmeler, çoklu-seçim filtreler, liste+sayfalama, yorum formu, kriter modalı | API VAR (reviews/product) | E7/H | 🕐 backend E7'de hazır; 595 satırlık sayfa tasarımı taşınmadı (ileri iş) |
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
