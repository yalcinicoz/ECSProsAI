# Misharix Tasarım ↔ Uygulama Fark Düzeltme Planı (2026-07-16)

Kaynak: `/opt/misharix` (NİHAİ tasarım) ↔ `/opt/ECSProsAI/src/ECSPros.Api` (canlı uygulama).
Yöntem: 60 ortak view'ın satır diff'i + tasarımda olup uygulamada bulunmayan `data-ms-*`
kancası / UI etiketi taraması + şüpheli dosyaların elle doğrulaması. CSS/JS varlıkları
bayt-eşit (site.js'te yalnız 2 bilinçli düzeltme farkı var) — farklar view + sayfa script
katmanında.

**Yanlış alarm çıkanlar (iş YOK):** TCKN akışı (uygulamada gerçek mantıkla var, C7),
Favorilerim kartları (ortak _UrunKarti hepsini kapsıyor), koleksiyon modal onay/red
butonları (test butonlarıydı, gerçek akış köprüden açılıyor), Search/sepet paneli demo
şablonları (bilinçli: gerçek veri basılıyor), Home/Index sayfa seçici (tasarım projesinin
dev navigasyonu), mobil menü/adres/iade demo etiketleri (demo veri gürültüsü).

---

## İP-1 — Filtre davranışı (kullanıcının örneği) ✅ TAMAM (2026-07-16, E2E 11/11)
- [x] 1.1 Sol filtrede anında uygulama kaldırıldı; seçimler birikir, YALNIZ "Filtrele"
      butonu uygular. "Filtreleri Temizle" `ms:filtreler-temizlendi` olayına bağlandı —
      URL'de filtre varsa tek tıkta temizler.
- [x] 1.2 Kategori bloğuna "Filtrele" butonu eklendi; kategori checkbox tek seçim,
      Filtrele basılınca kategori sayfasına gider (mevcut seçimler query ile taşınır).
- [x] 1.3 E2E 11/11 (staging 5051, playwright): anında uygulama yok, kopya senkronu,
      SSR geri işaretleme, temizle, kategori tek seçim + gezinme, fiyat aralığı.

## İP-2 — Ürün detay eksikleri ✅ TAMAM (2026-07-16; 2.4/2.5/2.6 ERTELENDİ)
- [x] 2.1 Puan dağılım tooltip'i: `GetProductReviewSummaryQuery.RatingCounts` builder'a
      bağlandı (`UrunDetayVm.PuanDagilimi`); bar genişliği en kalabalık puana oranlı
      (tasarım kalıbı). Staging'de geçici yorumlarla doğrulandı (sonra silindi).
- [x] 2.2 "Teslimat Bilgileri" bloğu ilk satırıyla açıldı ("Siparişiniz en kısa sürede
      kargoya verilecektir."); kargo markası + tahmini teslim satırı Faz H kargo
      entegrasyonunda.
- [x] 2.3 Beden Tablosu zaten @if(false) gizliydi — boş başlık basılmıyor; ölçü verisi
      gelince açılacak.
- [~] 2.4 Fiyat gösterim varyantları — ERTELENDİ (veri kaynağı: Faz G fiyat mimarisi).
- [~] 2.5 Kampanya + Süper Fırsat etiketleri — ERTELENDİ (veri kaynağı yok).
- [~] 2.6 Teslimat sosyal-kanıt mesaj rotasyonu — ERTELENDİ (veri kaynağı kararı).
- [x] 2.7 Beden öneri satırı tasarım metniyle eklendi ("Kullanıcıların çoğu kendi
      bedeninizi almanızı öneriyor.").

## İP-3 — Hesabım / Siparişlerim aksiyonları ✅ TAMAM (2026-07-16; 3.3/3.4 ERTELENDİ)
- [x] 3.1 İNCELEMEDE ÇIKTI: kargo takip modalı H2'de zaten gerçek veriyle varmış — tek fark
      buton etiketi. Düzeltme: kargodaki (shipped) siparişte "Kargoyu Takip Et", öncesinde
      "Siparişi Takip Et" (tasarım ayrımı). Modal hero'suna sürükleme alanı eklendi.
- [x] 3.2 "İade Durumu" butonu (returned siparişte) → /Hesabim/Iadelerim.
- [~] 3.3 "Dekontu Gör" — ERTELENDİ (dekont veri modeli yok).
- [~] 3.4 "Yorum Yaz — %10 İndirim" etiketi — ERTELENDİ (kampanya verisi yok).
- [x] 3.5 Sürükleme alanı eklendi: kargo takip modal hero + İade Sayfası Modalı üst şeridi
      (+ yeni adres modalı başlığı). Detay modalında zaten vardı.

## İP-4 — Hesabım küçük eksikler ✅ TAMAM (2026-07-16; 4.1/4.2 yanlış alarm çıktı)
- [x] 4.1 YANLIŞ ALARM: iade kodu modalı `data-ms-iade-kodu-kopya-bilgi` ile impl'de
      birebir varmış (_HesabimIadeDogrulamaModallari); kart üstü Kopyala'nın da kendi
      "Kopyalandı" geri bildirimi var. İş çıkmadı.
- [x] 4.2 YANLIŞ ALARM: ülke seçici BİLİNÇLİ kaldırılmış — OTP üyenin kayıtlı telefonuna
      gider, alan salt okunur (E8 kararı). İş çıkmadı.
- [x] 4.3 Kupon geri sayımı: `HesabimKuponVm.Bitis` (Promotion EndsAt) eklendi; son 7 güne
      giren kuponda tasarımın sayacı basılır (site.js sayar, UTC ISO hedef).
- [x] 4.4 Adreslerim tasarımın ortak adres modalına geçirildi: kart listesi + Düzenle/Yeni
      Adres modal açar; il/ilçe tasarımın ARAMALI özel-select'i (geo API'den, düğüm her
      dolumda yeniden kurulup msOzelSelectleriBaslat ile bağlanır); Fatura Türü backend
      alanı olmadığından @if(false); E3 POST/PUT/DELETE/default akışı aynen korundu.

## İP-5 — Ürün Değerlendirmeleri sayfası (EN BÜYÜK kalem — KURGU ONAYI BEKLİYOR)
Tasarım sayfayı komple yenilemiş (1057 satır; bizde eski sayfa 415). Yapı: sol sütun ürün
özeti; sağda 5 sekme — Değerlendir (yorum formu: puan/konu select, FOTO yükleme),
Onay Bekleyenler (üyenin bekleyenleri), Onaylananlar (ürünün tüm onaylı yorumları:
filtre/arama/sıralama/fotoğraflar + AI özet), Reddedilenler, Silinenler; kriter/başarı/
resim-büyütme modalları.

✅ TAMAM (2026-07-16, kurgu kullanıcı onaylı; anonim akış E2E 14/14):
- [x] 5.a Veri modeli: `storefront.product_review_photos` tablosu + `product_reviews.Topic`
      kolonu (migration `AddReviewPhotosAndTopic`, canlı DB'ye UYGULANDI — additive).
      Upload: POST `/api/store/reviews/images` (E8 iade görseli kalıbının kopyası —
      5 dosya × 5 MB, uzantı içerik tipinden, /media/reviews/yyyyMM). Create komutu
      Topic+PhotoUrls alır; API katmanı yalnız /media/reviews/ URL'si kabul eder.
      Okuma sorguları (ürün yorumları + üye yorumları + moderasyon + özet) foto/konu döner;
      özet ayrıca TopicCounts + PhotoReviewCount verir; liste sorgusuna topics/photosOnly
      filtreleri eklendi.
- [x] 5.b Panel karşılığı (K16): admin Yorum Moderasyonu satırında konu rozeti + foto
      küçük önizlemeleri (yeni sekmede tam boy); admin dist yeniden derlendi.
- [x] 5.c Sayfa UI: tasarımın 5 sekmeli sayfası gerçek veriyle — Değerlendir (form:
      puan/konu select + foto yükleme; yalnız teslim edilmiş + yorumlanmamış üründe),
      Onay Bekleyenler/Reddedilenler/Silinenler (üyenin bu ürüne ait yorumları — /mine),
      Onaylananlar (arama/sıralama/puan+konu+fotoğraflı filtreleri, infinite, fotoğraflı
      değerlendirme şeridi, resim büyütme modalı, Filtreleri Temizle chip'i). Girişsiz
      kullanıcı üye sekmelerinde giriş çağrısı görür (data-ms-giris-modal-ac); varsayılan
      sekme Onaylananlar. @if(false): AI özet, beden filtresi, favori sayısı, "Adınız"
      alanı yok (yayın adı üyelikten maskeli). Eski sayfanın bozuk merge-artığı script'i
      de bu yeniden yazımla temizlendi.
- [x] 5.d Yorumlarım'daki E7 modalı tasarımın yorumYapModal'ıyla değiştirildi (konu
      chip'leri + puan/konu select + foto yükleme; "Yeniden Düzenle" eski yorumu silip
      yenisini gönderir — akış korundu).

## İP-6 — Sepet küçük eksik ✅ KAPANDI (2026-07-16 — iş çıkmadı)
- [x] 6.1 İNCELEMEDE ÇIKTI: koşul metni gerçek kupondan zaten basılıyor (kosul =
      kuponDurum.aciklama, varsayılan "Tüm Ödeme Yöntemleri"). Tasarımın ödeme-tipine-özel
      kupon kısıtı (KART25/KAPIDA15) backend'de alansız — Promotion'a ödeme-tipi alanı
      gelince açılacak (ERTELENDİ).

---

## Sıra + kapanış
- İP-1/2/3/4/6 TAMAM (2026-07-16): build 0 hata; İP-1 E2E 11/11 (staging 5051, playwright);
  İP-2 tooltip staging'de geçici yorumlarla doğrulandı (sonra silindi); Hesabım akışları
  girişli olduğundan kullanıcı testinde. Canlı publish alındı — restart kullanıcıda.
- ERTELENENLER (veri kaynağı gelince): 2.4, 2.5, 2.6 (ürün detay fiyat varyantları/
  etiketler/mesaj rotasyonu), 3.3 (dekont), 3.4 (%10 yorum indirimi), 6.1 (ödeme-tipi kupon).
- İP-5 kurgu onayı bekliyor (5.a–5.d).
- Bu turda site.js DEĞİŞMEDİ → build-js gerekmedi.
