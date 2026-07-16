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

### Faz 4 — 60 ortak view üçlü merge (SÜRÜYOR — 2026-07-16)
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

### Faz 5 — Yeni sayfa/partial'lar
- [ ] Koleksiyon modalları (`_KoleksiyonModal`, `_KoleksiyonVarolanModal`, `_KoleksiyonYeniOzetModal`) + `onayRedModal` layout'ta TEK SEFER render (tasarım sözleşmesi).
- [ ] `_HazirlikDurumuModal`, `yorumYapModal`, `_UrunRenkModal` (kart başına gizli modal → sayfa seviyesinde tek modal + ürün kimliğine göre JSON renk verisi — backend bağlama dahil).
- [ ] Üyeliksiz kargo takip: 2 route (`/uyeliksiz-kargo-takip`, `/uyeliksiz-kargo-takip-sonuc`) + `Views/Home` sayfaları + `KargoTakip` partial'ları; **demo veri**, `noindex`; duyuru barındaki Kargo Takip linki yeni rotaya.
- [ ] `_UrunListesiKartiOrnegi`, `_MobilMenuKaydirma`, `_TemelBilgilendirme`: gerçek sayfalarda kullanım varsa taşınır (katalog-yalnızı ise varsayım gereği atlanır — taşıma anında tek tek doğrulanır).

### Faz 6 — Yeni vitrin blok tipleri (K16: panel karşılığıyla)
- [ ] `coklu_banner`, `gorselli_yorumlar_carousel`, `kategori_cok_satanlar` blok tipleri: katalog kaydı + kaynak motoru + Razor render (YENİ partial markup'ı birebir) + admin Vitrin Yönetimi formu.
- [ ] Kaldırılan tasarımların (videolu carousel, Flash Vitrin/Senin İçin) vitrin kataloğunda karşılığı varsa kaldırılır; yayınlanmış snapshot'larda kullanım taranır, gerekirse yeniden yayın.

### Faz 7 — Vitrin blok eşlemesi (check.sh görmez — elle)
- [ ] Tasarımın `_Story`/`_Slider`/carousel/banner değişiklikleri (story etiket renkleri+tipografi, nokta zeminleri `bg-transparent`, kontrast düzeltmeleri, Flash Ürünler "Tümünü Gör" kaldırma, çerçevesiz carousel, tek `src` görsel sözleşmesi…) `Views/Shared/Store/_VitrinBloklar.cshtml`'e satır satır uygulanır.
- [ ] Ana sayfa görsel sözleşmesi: `srcset/sizes/data-ms-lazy-srcset/-sizes` YOK; tek `src`/`data-ms-lazy-src` + gerçek `width/height`.

### Faz 8 — Layout/SEO/Program.cs seçici uyarlama
- [ ] `_Layout` merge: SEO ViewData sözleşmesi (Title/MetaDescription/CanonicalPath/Robots/OG/JsonLd) farkları hizalanır; ortak modal partial'ları layout seviyesinde.
- [ ] Program.cs'ten SEÇİCİ alınır (kopyalanmaz): Brotli/Gzip response compression (Production), sürüm sorgulu CSS/JS/görsel/FA için 1 yıl `immutable`, HTML `no-cache`.
- [ ] `robots.txt`/`sitemap.xml` ECSPros karşılığı denetlenir (vitrin/dinamik rotalarla çelişmesin).

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
