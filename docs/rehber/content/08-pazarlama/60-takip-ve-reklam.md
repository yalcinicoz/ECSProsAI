---
title: Takip & Reklam
route: /marketing/tracking
group: Pazarlama
order: 60
summary: Kanal bazlı analitik / piksel / dönüşüm entegrasyonlarının (GA4, GTM, Google Ads, Meta, TikTok vb.) durumunun, olay kuyruğunun ve ürün feed'inin izlendiği; test olayı gönderilip hatalı olayların yeniden denendiği ekran.
---

## Ne işe yarar
Sitedeki ziyaretçi ve sipariş hareketleri (ürün görüntüleme, sepete ekleme, satın alma…) izin verilen pazarlama
platformlarına (Google Analytics 4, Google Tag Manager, Google Ads, Meta Pixel / CAPI, TikTok, Pinterest, Microsoft
Ads/Clarity, Merchant Center, Search Console) iletilir. Bu ekran pazarlama/operasyon ekibinin **kanal bazında**
hangi entegrasyonların açık olduğunu, sunucudan gönderilen olayların (kuyruk) durumunu, hata mesajlarını ve ürün
feed'inin üretimini izlemesi içindir. Kimlikler ve ayarlar burada değil, **Sistem → Firmalar → Entegrasyonlar**
ekranında girilir.

## Ekran yerleşimi
![Takip & Reklam — kanal kartları, feed kartı ve olay kuyruğu](img/marketing-tracking.webp)
1. **Başlık** — "Takip & Reklam" ve Firma → Entegrasyonlar bağlantılı açıklama.
2. **Sağ üst araçlar** — kanal seçici ("Firma — Kanal"), yenile (döner ok) butonu, **Test event gönder**.
3. **Durum rozetleri şeridi** — takip açık/kapalı, çerez bandı, satın alma anı, kuyruk özetleri.
4. **Kanal kartları** — aktif takip entegrasyonu başına bir kart (mod rozetleri, ayar özeti, son başarı/hata, 24 saat sayıları).
5. **Ürün feed'i kartı** — Merchant Center / Meta katalog feed durumu, adresleri ve **Şimdi üret**.
6. **Event kuyruğu (outbox)** — durum sekmeleri + tablo + sayfalama (30 kayıt/sayfa), altta açıklama notu.

Sayfa kendini 15 saniyede bir (feed kartı 10 saniyede bir) yeniler; seçilen kanal tarayıcı oturumunda hatırlanır.

## Durum rozetleri şeridi
| Rozet | Anlamı |
|---|---|
| `Takip AKTİF` / `Takip AKTİF (DRY-RUN — dış platforma gönderilmez)` / `Takip KAPALI` | Sunucu tarafı takibin genel durumu. DRY-RUN'da olaylar kuyruğa yazılır ama platformlara gitmez (test ortamı). KAPALI'da kuyruğa da yazılmaz ve **Test event gönder** pasiftir. |
| `Consent bandı: açık/kapalı / varsayılan …` | Çerez izin bandının durumu ve izin varsayılanı. |
| `Satın alma anı: …` | Satın alma olayının hangi sipariş adımında üretildiği. |
| `Kuyruk: N bekliyor` | Gönderilmeyi bekleyen olay sayısı (sarı ise > 0). |
| `N hatalı` | Denemeleri tükenmiş hatalı olay sayısı (kırmızı ise > 0). |
| `24s: N gönderildi / M atlandı` | Son 24 saatte başarıyla giden ve (izin yok / eşleme yok nedeniyle) atlanan olaylar. |

## Kanal kartları
Her kart bir takip servisini gösterir. Kanalda hiç entegrasyon yoksa "Bu kanalda aktif takip entegrasyonu yok — sitede
hiçbir analytics/pixel script'i basılmaz ve çerez bandı gösterilmez." kutusu ve Firma → Entegrasyonlar bağlantısı görünür.

| Öğe | Anlamı |
|---|---|
| Başlık | Servis adı: Google Analytics 4, Google Tag Manager, Google Ads, Merchant Center, Search Console, Meta Pixel / CAPI, TikTok, Pinterest, Microsoft Ads (UET), Microsoft Clarity. |
| Mod rozetleri | `Tarayıcı` (site sayfasından gönderim), `Sunucu` (sipariş olayları sunucudan), `GTM` (Tag Manager üzerinden). Bir servis birden çok moda sahip olabilir. |
| `kanala özel` / `firma geneli` | Entegrasyon kaydının kapsamı. |
| `ECSPros hesabı` / `müşteri hesabı` | Kimliğin kime ait olduğu. |
| Sağ üst durum rozeti | Sunucu modunda `çalışıyor` (yeşil) / `hata var` (kırmızı, son 24 saatte hata varsa); yalnız tarayıcı modunda `tarayıcı` (mavi). |
| Ayar özeti | Kimlik/ayarların ilk 6 alanı (ör. measurementId, pixelId); gizli bilgiler gösterilmez. |
| Son başarılı / Son hata / 24 saat | Yalnız sunucu modunda: son başarılı gönderim, son hata zamanı, `N ✓ / M ✗`. |
| Kırmızı metin | Son hata mesajının başı (üzerine gelince tamamı). |

## Ürün feed'i kartı
| Öğe | Anlamı |
|---|---|
| `Merchant entegrasyonu aktif` / `yok` | Kanalda Google Merchant Center kaydı var mı. Yoksa kartta kaydın nasıl açılacağı (Firma → Entegrasyonlar → "Google Merchant Center": merchantId, ülke TR, dil tr, para TRY, kargo bedeli; kategori eşlemesi Kanal Kategorileri → "Google ürün kategorisi") anlatılır. |
| `her N saatte` / `Feeds:Enabled=false` | Otomatik üretim aralığı; ikincisi üretimin sistemce kapalı olduğunu söyler. |
| `üretiliyor…` / `hata` | O anda üretim sürüyor / son üretim hatalı. |
| Son üretim | Zaman ve süre (sn). |
| Ürün / kalem (stokta) | Feed'e yazılan ürün ve varyant sayısı (stokta olan). |
| XML / CSV | Dosya boyutları (KB). |
| Google Shopping XML / Meta katalog CSV | Feed adresleri + **Kopyala**; ilk üretim yapılmadıysa "anahtar ilk üretimde oluşur". |
| Şimdi üret | Üretimi hemen kuyruğa alır ("Üretim kuyruğa alındı — durum 10 sn'de bir yenilenir."). Entegrasyon yoksa, üretim kapalıysa ya da üretim sürüyorsa pasif. |

## Event kuyruğu (outbox)
| Sütun | Anlamı |
|---|---|
| ZAMAN | Olayın oluştuğu zaman. |
| EVENT | Olay adı (`order_completed`, `added_to_cart`, `refund`…). |
| KAYNAK | Olayı üreten taraf (site/sunucu/test). |
| DURUM | `Bekliyor` (sarı) · `Gönderildi` (yeşil) · `Hata` (kırmızı) · `Atlandı` (gri). |
| HEDEFLER | Olayın gönderildiği platformlar ve sonuçları (kısaltılmış; üzerine gelince tamamı). |
| DENEME | Deneme sayısı; bekleyen olayda "→ sonraki deneme zamanı". |
| HATA | Son hata mesajının başı. |
| (son sütun) | **Yeniden dene** (döner ok) — yalnız `Hata` ve `Atlandı` satırlarında. |

| Sekme | Ne yapar |
|---|---|
| `Tümü` / `Bekleyen` / `Hatalı` / `Gönderilen` / `Atlanan` | Kuyruğu duruma göre süzer. Başlık yanında toplam kayıt ve son olay zamanı görünür. |

## Butonlar ve aksiyonlar
| Buton/Aksiyon | Nerede | Ne olur | Ön koşul / yetki |
|---|---|---|---|
| Kanal seçici | Sağ üst | Tüm sayfa seçilen kanala göre yenilenir. | — |
| Yenile (döner ok) | Sağ üst | Durum verisini hemen tazeler. | — |
| Test event gönder | Sağ üst | Kuyruğa bir test satın alma olayı yazılır; "Test event kuyruğa yazıldı (…). 5-10 sn içinde sonucu aşağıda görürsünüz." mesajı çıkar, kuyruk `Tümü` sekmesine döner. Takip kapalıysa "Test event yazılamadı (takip kapalı olabilir)." | Takip AKTİF olmalı. |
| Şimdi üret | Feed kartı | Feed üretimini başlatır. | Merchant entegrasyonu + üretim açık, üretim sürmüyor. |
| Kopyala | Feed kartı | Feed adresini panoya kopyalar. | Adres üretilmiş olmalı. |
| Yeniden dene | Kuyruk satırı | Olay `Bekliyor`'a alınır ve tekrar gönderilir. | Satır `Hata` ya da `Atlandı`. |
| Firma → Entegrasyonlar bağlantıları | Başlık / boş kutu / feed kartı | Ayarların girildiği Firmalar ekranına gider. | — |

## Durumlar ve iş kuralları
- **Her şey kanal bazlıdır:** bir kanalda entegrasyon kaydı yoksa o kanalda hiçbir takip kodu basılmaz, çerez bandı
  çıkmaz ve olay üretilmez. Bir kanalın ayarı diğerini etkilemez.
- **İzin (consent) kapısı:** ziyaretçi çerez bandında reklam/analitik iznini vermediyse olay ilgili platforma gitmez;
  tüm hedefler izin nedeniyle atlanırsa satır `Atlandı` olur. Sipariş olaylarında izin sipariş anındaki değerdir.
- **Tek sayım:** aynı satın alma hem tarayıcıdan hem sunucudan gidebilir; aynı kimlikle gönderildiği için platform tek
  sayar. Eski sistemden aktarılan, pazaryerinden gelen ve mağaza (POS) siparişleri satın alma olayı **üretmez**.
- **Yeniden deneme:** gönderim başarısız olursa artan aralıklarla tekrar denenir; deneme hakkı bitince `Hata` olur ve
  kartta/kuyrukta kırmızı görünür. Hatayı (ör. geçersiz erişim anahtarı) Firma → Entegrasyonlar'da düzeltip **Yeniden
  dene** ile tekrar gönderin.
- **Tarayıcı tarafı olaylar bu listede görünmez** (GA4 DebugView / Pixel Helper ile izlenir); kuyruk yalnız sunucudan
  giden olayları gösterir.
- **Test olayı:** Meta/TikTok'ta yalnız kanal kaydına test olay kodu (`testEventCode`) girilmişse Events Manager → Test
  Events'te görünür; canlıda bu alan **boş** bırakılır. GA4 test olayı doğrulama ucuna gider, mülke yazılmaz.
- **Feed:** aktif Merchant kaydı olan kanal için periyodik üretilir (varsayılan 6 saat); adres tahmin edilemez bir
  anahtar içerir, yanlış anahtar 404 döner. Stoksuz varyantlar (ayar açık değilse) ve görselsiz ürünler feed'e yazılmaz;
  "Google ürün kategorisi" yalnız Kanal Kategorileri'nde girilmişse yazılır. Ücretsiz kargo eşiği Merchant Center'da
  tanımlanır, feed'e temel kargo bedeli yazılır.

## Adım adım
**Meta CAPI kurulumunu doğrulama**
1. Firma → Entegrasyonlar'da kanala Meta kaydı açın (pixelId, erişim anahtarı, geçici `testEventCode`).
2. Bu ekranda kanalı seçin; Meta kartında `Sunucu` modu ve `çalışıyor` rozetini görün.
3. **Test event gönder** → 5-10 sn sonra kuyrukta `Gönderildi`; Meta Events Manager → Test Events'te olay görünür.
4. Doğrulama bitince `testEventCode` alanını boşaltın.

**Hatalı olayları temizleme**
1. `Hatalı` sekmesine geçin, HATA sütunundaki mesajı okuyun (ör. 401 → anahtar geçersiz).
2. Ayarı düzeltin, satırlarda **Yeniden dene**'ye basın; `Gönderildi` olmalı.

**Google Shopping feed adresini Merchant Center'a vermek**
1. Feed kartında **Şimdi üret**, bitince "Google Shopping XML" satırındaki **Kopyala**.
2. Merchant Center'da zamanlanmış getirme (fetch) olarak bu adresi tanımlayın.

## İpuçları ve sık karşılaşılan durumlar
> **Dikkat:** `Takip AKTİF (DRY-RUN…)` rozeti görüyorsanız olaylar dış platformlara **gitmiyordur**; canlı ortamda bu
> rozet görünmemelidir — geliştirme ekibine bildirin.

> **İpucu:** Kart `tarayıcı` rozeti taşıyorsa o servis için sunucudan gönderim yoktur; "Son başarılı/Son hata" alanları
> da görünmez. Bu normaldir (ör. GTM, Clarity).

> **Not:** `Atlandı` çoğunlukla izin verilmemiş ziyaretçi ya da o platformda karşılığı olmayan olay demektir; hata
> değildir. Yine de **Yeniden dene** ile tekrar denenebilir.

## İlgili sayfalar
- [Firmalar ve Entegrasyonlar](/rehber/sistem/firmalar/)
- [Bülten Aboneleri](/rehber/pazarlama/bulten-aboneleri/)
- [Kanal Kategorileri](/rehber/vitrin/kanal-kategorileri/)
