# Misharix Tasarım Güncelleme Senkronu — İş Planı (2026-07-16)

> **Amaç:** `/opt/misharix`'teki güncel tasarım paketini (patch notları 2026-07-06 → 2026-07-16)
> ECSPros storefront'una **yorumsuz/birebir** uygulamak. Tasarım nihai kabul edilir;
> ekleme-çıkarma yapılmaz, "iyileştirme" yasak (bkz. `feedback_use_design_source_verbatim`).
>
> **Kaynaklar:**
> - ESKİ kaynak (merge bazı): `/opt/misharixWebSites/misharix` — Faz A–H taşımasının temeli (2026-07-06 durumu)
> - YENİ kaynak (nihai tasarım): `/opt/misharix` — özet sözleşme: `/opt/misharix/md/patch-notlari-ozet.md`
> - Uygulama yöntemi: 60 ortak view'da üçlü merge (baz=ESKİ, tasarım=YENİ, bizim=ECSPros data-binding'li)

## Kullanıcı Kararları (2026-07-16 — kesin)

1. **Tasarım nihaidir, birebir uygulanır.** Yorum/ekleme/çıkarma yok. Tasarımın kaldırdıkları
   (videolu carousel, Flash Vitrin/Senin İçin ara tasarımları) ECSPros'tan da kaldırılır.
2. **Yeni görünüm tipleri ŞART:** `_CokluBanner`, `_GorselliYorumlarCarousel`, `_KategoriCokSatanlar`
   vitrin sistemine yeni blok tipi olarak eklenir (katalog + admin Vitrin Yönetimi + Razor render — K16 gereği panel karşılığıyla).
3. **`site.min.js` hattı ŞART:** tasarımın esbuild `build-js` adımı ECSPros'a alınır;
   layout Production'da `site.min.js`, diğer ortamlarda `site.js` kullanır (tasarım _Layout kalıbı).
4. **Kargo takip DEMO kalır:** `/uyeliksiz-kargo-takip(-sonuc)` sayfaları statik demo veriyle taşınır.
   ⏰ **HATIRLATMA:** Kargo servisi entegrasyonu gündeme geldiğinde bu iki sayfa gerçek takip
   servisine bağlanacak (sözleşme: `/opt/misharix/md/backend-devir-entegrasyonlari.md`).

**Varsayım (kullanıcı itiraz etmedikçe):** ProjeElementleri **katalog kabuğu** (Index.cshtml,
_Sayfalar, _Modallar, GorunumTipleri demo sayfaları vb. tasarım-aracı sayfaları) eskiden olduğu
gibi taşınMAZ; yalnız gerçek sayfaların kullandığı partial'lar taşınır. "Birebir uygula" kuralı
sitenin gerçek sayfaları içindir.

## Fazlar

### Faz 0 — Hazırlık ✅ (2026-07-16)
- [x] Bekleyen `site.js` düzeltmesi (lazy-load + pointerCapture) + docs görselleri commit'lendi (5a841f7); `publish-staging/` gitignore'a eklendi.
- [x] `tools/misharix-sync/check.sh` KAYNAK → `/opt/misharix` (ESKİ klasör merge bazı olarak iş bitene kadar tutulur).
- [x] İkon denetimi yapıldı — bulgular:
  - Subset'te olmayan FA ikonları: `fa-location-crosshairs` (ECSPros'a özgü `_SehirSecim` — kalıcı ihtiyaç), `fa-cart-shopping` (_HesabimTekrarSatinAl) ve `fa-square-plus` (_SepetSayfasi) — son ikisi Faz 4 merge'ünde tasarımın yeni markup'ıyla muhtemelen kaybolur. **Çözüm:** tasarımın `fontawesome-subset.mjs` aracı ECSPros'a alınır ve ECSPros Views+site.js üzerinden çalıştırılır → subset otomatik tam olur (Faz 1/2).
  - Kökten `ikons/kullanilmayanlar`a taşınan 55 dosyanın 6'sına ECSPros'ta hâlâ referans var — hepsi `_AnaNavigasyonGirisMenu.cshtml`'de; tasarım bu ikonları FA'ya çevirdi, Faz 4 merge'ü referansları zaten kaldırır. **Kural:** Faz 1 asset senkronu EKLEMELİ yapılır (silme yok); kök ikon temizliği Faz 4 sonrası sıfır-referans doğrulamasıyla yapılır.

### Faz 1 — Asset'ler ✅ (2026-07-16)
- [x] `testimage/` (52), `images/` (210), `ikons/` (67, kullanilmayanlar dahil), `video/` (1), `fontawesome-free-7.2.0-web/` (7, `ikonall.min.css` dahil) rsync ile EKLEMELİ senkronlandı — tasarımla bayt-aynı; kök `ikons/` fazlalıkları (55 taşınan + ECSPros'a özgüler) Faz 4 sonrası temizliğe kadar duruyor. favicon aynı çıktı. `lib/` (validation), `robots.txt`/`sitemap.xml` bilinçli atlandı (Faz 4/8 konusu); `misharix-skils/` tasarım aracı, taşınmaz.
- [x] Subset aracı (`fontawesome-subset.mjs`) Faz 2'ye alındı — package.json/esbuild işleriyle birlikte.

### Faz 2 — site.js + site.min.js ✅ (2026-07-16)
- [x] YENİ `site.js` (7846 satır, LF-temiz — CRLF tuzağı bitti) bütün alındı; ECSPros deltalarından **yalnız ikisi** yeniden uygulandı:
  - Lazy-load: `msLazyHazir` bayrağı kaynak kontrolünden SONRA (infinite-scroll iskelet düzeltmesi — tasarımda hata aynen vardı).
  - Reklam vitrini: pointerCapture yalnız gerçek sürüklemede (tasarım carousel'i yön-duyarlı çözümle düzeltmiş ama reklam vitrinini düzeltmemişti).
  - Carousel pointerCapture deltası UYGULANMADI — tasarımın yön-duyarlı sürüklemesi (satır ~1716) bizim düzeltmeyi kapsıyor.
- [x] `package.json`: `build-js`/`watch-js` (esbuild ^0.28.1) + `fontawesome-subset` script'leri; `scripts/fontawesome-subset.mjs` kopyalandı (çalıştırma Faz 4/5/7 sonrası — Views nihai olunca). `site.min.js` üretildi (137 KB, syntax OK); publish `wwwroot` ile otomatik taşır, **site.js her değiştiğinde `npm run build-js` şart** (Faz 9'da deploy talimatına eklenecek).
- [x→Faz 4] `_Layout.cshtml` Production `site.min.js` geçişi layout merge'üyle birlikte gelecek (tasarım _Layout'u zaten environment-etiketli).

### Faz 3 — CSS ✅ (2026-07-16)
- [x] `tailwind.css` birebir kopya; `site.css` tasarımın derlenmiş çıktısıyla değiştirildi (971.581 bayt, md5 eşit — A4 kalıbı sürüyor: ECSPros yeniden derlemez, tasarım çıktısını kullanır). Değişen `css/fontawesome-all.css` de senkronlandı (hiçbir view referans etmiyor, bayt-eşitlik için).
- [x] Kapsam denetimi GEÇTİ: 7 ECSPros'a özgü view'daki tüm class'lar tarandı; site.css'te olmayan 8 aday (`ms-sehir-modal*`, `ms-uyari*`, `lazy-infinite-on`, `ms-gorunum-daha-fazla-alani`) ESKİ css'te de yoktu — JS marker'ı/view-içi stil, regresyon değil.

### Faz 4 — 60 ortak view üçlü merge ✅ (2026-07-16 — build 0 hata, check.sh DRIFT TEMİZ)
- [x] TAMAMLANDI: 60 dosyanın hepsi + giriş/kayıt modalları çözüldü. Giriş mimarisi:
  UI akışı (modal/sekme/adım/sayaç/kod kutuları/otomatik onay) YENİ site.js'te; _AnaNavigasyon
  inline script'i 126 satıra indi — yalnız gerçek OTP: Kodu Onayla document-capture'da kesilir,
  demo "Giriş Başarılı" yalnız gerçek doğrulama başarısında gösterilir; oturum omurgası
  GirisModal script'inde. **Faz 4'te doğan bilinen sınırlar/ertelemeler:**
  - UrunDegerlendirmeleri sayfası: tasarımın yeni filtre-select/durum-sekmesi UI'ına geçiş
    yapılmadı (çalışan gerçek sayfa korundu) — ayrı bağlama işi.
  - Hesabım Adreslerim: tasarımın ortak-adres-modalı akışına geçilmedi (E3 gerçek form korundu).
  - Yorum yaz: bizim E7 modalı iş başında; tasarımın yorumYapModal'ına geçiş Faz 5'te değerlendirilecek.
  - Koleksiyon: bizim E6 API'li sayfa modalı + layout'taki ortak koleksiyon şablonları yan yana —
    Faz 5'te uzlaştırılacak (çifte modal riski denetlenecek).
  - Statü/harcama, hızlı filtre chip'leri, kampanya filtresi, Süper Fırsat etiketi: veri
    kaynağı yok — tasarımın güncel markup'ı @if(false)/yorum içinde hazır bekliyor.
  - Hesap menü rotaları /Hesabim/* kaldı (lowercase hizalama Faz 8).

#### (arşiv) Faz 4 ara notları
- [x] Toplu geçiş yapıldı: 12 trivial kopya + 44 dosyada merge-file (43 çakıştı). ÇÖZÜLENLER (commit'li):
  4 host Index, _AnaNavigasyon (OTP bloğu korunarak 7 satıra indi), Duyuru, GirisMenu, Footer,
  Breadcrumb, HesabimYanMenu, _Layout, Search (canlı arama korundu — demo şablon bilinçli yok),
  DesktopMenu + MobilMenu (yeniden kuruldu: template'li lazy + FA + bizim döngüler), Ust (sepet
  paneli yeni sözleşme data-ms-sepet-urun-listesi/-urun-sayisi; script uyarlandı; mobil kategori
  şeridi tasarım gereği SİLİNDİ), Sepet grubu 5 dosya. _ViewImports/_ViewStart bizde doğru, dokunulmadı.
- [ ] KALAN 23 çakışmalı dosya: Hesabim 14, UrunDetay 4 (Breadcrumb hariç), UrunListesi 4,
  UrunDegerlendirmeleri 1 — kalan liste: `grep -rl '<<<<<<< ECSPros' src/ECSPros.Api/Views/`.
- **Çözüm kuralları (devam eden oturumlar için):** yapı/markup YENİ tasarımdan; @Model/@foreach/
  data hook'ları bizden; tasarımın demo doldurma şablonları (site.js'in beslediği) BİLİNÇLİ
  konulmaz → site.js demo dolumu no-op kalır, gerçek veriyi bizim sayfa script'leri basar
  (Search + sepet paneli kalıbı); ECSPros rotaları korunur (lowercase hizalama Faz 8);
  theirs tarafı boşsa ours aynen kalır (bizim canlı script hunk'ları).
- [ ] **En hassas ikisi EN SONA ve elle:** `_AnaNavigasyonGirisModal` + `_AnaNavigasyonKayitModal`
      (tasarımın SMS kod odak/otomatik onay/yeniden gönder/`onayRedModal` davranışları ile
      GERÇEK GES Telekom OTP akışının birleşimi — regresyon riski en yüksek yer).
- [ ] Sepet paneli merge'ünde tasarımın demo sepet-silme mantığının gerçek sepet backend'ini ezmediği doğrulanır (`data-ms-sepet-rozet/-urun-sayisi/-toplam` gerçek veriye bağlanır).
- [ ] `allowed-diffs.txt` güncellenir; `check.sh` (YENİ kaynağa karşı) TEMİZ.

### Faz 5 — Yeni sayfa/partial'lar ✅ (2026-07-16 — build 0 hata)
- [x] Koleksiyon modalları + `onayRedModal` layout'ta TEK SEFER (Faz 4'te geldi). ÇİFTE MODAL ENGELİ:
  Koleksiyonlarım sayfası kendi gerçek-verili E6 modalını taşıdığından controller
  `MsOrtakKoleksiyonModallariGizle` bayrağıyla layout şablonunu kapatır; `ms:koleksiyon-olustur`
  köprüsü yeni site.js'te de yaşıyor (2486/2801) — API bağı çalışır.
- [x] `_HazirlikDurumuModal` kopyalandı (include Siparişlerim'de hazır); tetik butonu @if(false) —
  modal timeline'ı statik demo, gerçek fulfillment adım verisi gelince açılır.
  `yorumYapModal`a GEÇİLMEDİ (bizim E7 gerçek modal iş başında; foto upload backend'i yok — bilinen sınır).
- [x] `_UrunRenkModal` kopyalandı + Home/Index'e eklendi — yalnız `data-ms-urun-renk-ortak`
  tetikleyicileri açar (bizim kartlarda yok → nötr); renk JSON'u vitrin kart eşlemesiyle Faz 7'de.
- [x] Üyeliksiz kargo takip: 2 route (Store/HomeController) + 2 Home view + 2 KargoTakip partial'ı
  birebir; DEMO veri + noindex; duyuru linki Faz 4'te bağlanmıştı. ⏰ kargo entegrasyonunda gerçek servise bağlanacak.
- [x] `_UrunListesiKartiOrnegi`/`_TemelBilgilendirme` gerçek sayfalarda kullanılmıyor → taşınmadı;
  kart markup fark denetimi (bizim _UrunKarti ↔ tasarım kart şablonu) Faz 7'de.

### Faz 6 — Yeni vitrin blok tipleri ✅ (2026-07-16 — API+admin build 0 hata)
- [x] 3 tip PageBlockCatalog'a eklendi: `coklu-banner` (öğe: görsel+ad+link), `gorselli-yorumlar`
  (öğe eşlemesi: GorselUrl=yorum foto, MobilGorselUrl=ürün küçük görseli, AltBaslik=yorum,
  Baslik=ürün adı, Rozet=marka), `kategori-cok-satanlar` (RequiresProductSource — mevcut G3
  kaynak motoru generic işler; ilk 4 ürün + config.seeAllUrl 'Kategoriye Git').
- [x] Razor render: _VitrinBloklar.cshtml'e tasarım markup'ıyla 3 case. Admin form katalog
  API'sinden otomatik beslenir (tip dropdown/şablon/öğe generic); kategori-cok-satanlar için
  ORNEK_CONFIG eklendi; admin dist yeniden derlendi.
- [x] Kaldırılan tasarımlar (videolu carousel, Flash Vitrin/Senin İçin) katalogda hiç yoktu —
  temizlik gerekmedi (H5 'Videolu Ürün' kart rozeti ayrı özellik, tasarımda da var, kaldı).

### Faz 7 — Vitrin blok eşlemesi ✅ (2026-07-16 — build 0 hata)

- [x] Mevcut case'lere tasarım deltaları: slider (tek img, ilk slide eager/fetchpriority=high,
  intrinsic 1200x525, ms-mobil-yatay/dikey config.gorunum'dan), story (no-lazy liste/görsel,
  64x64, modal sürükleme alanı + intrinsic avatar/görsel), flash carousel (Tümünü Gör kaldırıldı,
  başlık liste linki), kapsul/banner/marka/reklam görselleri data-ms-lazy-src + intrinsic.
  Nokta zeminleri/kontrast/etiket renkleri CSS'te (Faz 3'te geldi).
- [x] Tasarımın nihai ana sayfasındaki 3 inline blok vitrin tipi olarak eklendi (toplam 6 yeni tip):
  `bilgi-banner`, `ikon-banner`, `cercevesiz-carousel` (öğe-tabanlı; FA ikon class'ı öğe Rozet
  alanından). Admin katalogdan otomatik.
- [x] Kart denetimi: _UrunKarti tasarımın nihai kart şablonuyla eşitlendi — data-ms-urun-id,
  intrinsic/lazy görseller, renk rozeti span→button (aria'lı), tooltip role=dialog + FA
  chevron/xmark + intrinsic; yıldızlar aria-hidden. Infinite-scroll enjeksiyonları SSR ile
  birebir eşitlendi. Kartlar tasarımın VARSAYILAN tooltip varyantında; ortak renk modalı
  (data-ms-urun-renk-ortak) altyapısı hazır ama kartlarda kullanılmıyor (tasarımda da yalnız
  işaretli varyantta) — renk JSON üretimi o varyanta geçilirse yapılır.
- [x] FA subset ECSPros Views+site.js üzerinden yeniden üretildi (64 ikon —
  fa-location-crosshairs dahil); kök `ikons/` temizliği: taşınan 55 dosyanın kök kopyaları
  sıfır-referans doğrulamasıyla silindi, klasör tasarımla birebir.

### Faz 8 — Layout/SEO/Program.cs seçici uyarlama ✅ (2026-07-16)

- [x] SEO ViewData sözleşmesi Faz 4 layout merge'üyle gelmişti (Title/MetaDescription/Canonical/
  Robots/OG/JsonLd); ürün/liste sayfalarında gerçek-veri JsonLd ayrı iş (sahte demo JSON-LD basılmıyor).
- [x] Program.cs: Brotli/Gzip response compression (Production, SmallestSize, EnableForHttps) +
  sürüm sorgulu (?v=) / images-performance / fontawesome dosyalarına 1 yıl immutable cache +
  HTML'e no-cache (tarayıcı bayat vitrin HTML'i tutmaz).
- [x] robots.txt tasarımdan uyarlandı (sabit alan adlı Sitemap satırı çıkarıldı); statik demo
  sitemap.xml EKLENMEDİ — gerçek katalog için dinamik sitemap go-live işi.
- [x] Lowercase rotalar: HesabimController'da lowercase alias'lar zaten mevcut + ASP.NET rota
  eşleşmesi harf-duyarsız — ayrıca iş çıkmadı (canonical'lar layout sözleşmesinden).

### Faz 9 — QA + Deploy
- [ ] `check.sh` TEMİZ; yan yana ekran görüntüleri (1440 + 390) misharix ↔ ECSPros.
- [ ] Regresyon: GERÇEK OTP ile giriş/kayıt (GES Telekom restart sonrası), sepet, şehir çipi (g9b/g9c), vitrin sayfaları, kurumsal, ürün listesi/detay/yorumlar, Hesabım.
- [ ] Vitrin versiyonlu cache: deploy sonrası cache temizliği/yeniden yayın gerekliliği doğrulanır.
- [ ] Publish `publish-staging` üzerinden (çalışan servisin publish'ine rm YOK); `sudo systemctl restart ecspros` kullanıcıda. (Faz P + GES birikimi 2026-07-16 restart'ıyla canlıya çıktı; OTP canlıda kullanıcı tarafından doğrulandı — giriş modalı merge regresyonu için referans çalışır durum mevcut.)

## Bilinen Riskler
- Giriş/kayıt modalı: tasarım demo SMS davranışları ↔ gerçek OTP backend birleşimi (en riskli merge).
- Sepet: tasarımın demo silme/rozet hesabı gerçek sepeti ezmemeli.
- İkon subset'i: ECSPros'a özgü markup'ın ikonları subset dışında kalabilir (Faz 0 denetimi).
- Vitrin cache'i bayat HTML sunabilir (Faz 9'da ele alınır).
- CRLF: mevcut ECSPros `site.js` karışık — Faz 2 wholesale değişimle çözülür; ara düzenleme yapılacaksa python bayt-patch kuralı geçerli.
