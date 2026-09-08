# Misharitalia Mobil — Push Bildirim Entegrasyonu (Backend Uygulama Belgesi)

> **Hedef okuyucu:** Backend ekibi / backend'i geliştiren yapay zekâ. Bu belge
> tek başına yeterli olacak şekilde yazıldı: mevcut altyapı, gönderim
> yöntemi, mesaj sözleşmesi, uygulamanın tanıdığı linkler, senaryolar,
> hedefleme kuralları, veri modeli, test prosedürü ve kabul kriterleri.
> Mobil uygulama tarafı **hazır ve canlıda doğrulanmış**; yapılacak iş
> tamamen backend'de.
>
> Tarih: 2026-09-07. Kaynak: mobil uygulama kodu (`lib/core/push/`,
> `lib/core/action/action_resolver.dart`) ve staging mobil swagger (119 uç).

---

## 0. Özet — ne var, ne istiyoruz

**Var olan (mobil + backend):**
- Uygulama her açılışta ve FCM token yenilenince `POST /api/store/push-devices`
  ile cihazını kaydediyor. Üye giriş yapınca kayıt üyeye bağlanıyor, çıkışta
  bağ kopuyor. Kayıtlar `storefront.push_devices` tablosunda (token, platform,
  deviceId, appVersion, memberId?, status).
- Uygulama, bildirime **tıklanınca** `data.link` alanındaki yolu (web sitesiyle
  aynı yollar: `/urun/...`, `/siparislerim/...`, `/sepet`…) uygulama içi
  ekrana çeviriyor. Uygulama kapalıyken ve arka plandayken çalışıyor.

**İstenen (backend):**
1. FCM HTTP v1 ile **gönderim servisi** (tek cihaz, üyenin tüm cihazları,
   segment).
2. Aşağıdaki **senaryoların tetikleyicileri** (sipariş durumu, favori fiyat
   düşüşü, stok alarmı, sepet hatırlatma, soru/yorum, kupon, kampanya).
3. **Kayıt/gönderim logu**, token yaşam döngüsü, tekilleştirme, sessiz
   saatler, izin (consent) kuralları.
4. Panelden yönetilebilir **şablonlar** (metin + link).

---

## 1. Mevcut altyapı (değiştirilmeyecek gerçekler)

| Öğe | Değer |
|---|---|
| Firebase projesi | `misharitalia-37192` — **CANLI MAĞAZAYLA ORTAK**. Firebase konsolundan "tüm kullanıcılara" deneme kampanyası ASLA gönderilmez; testler yalnız tek token'a, backend'den yapılır. |
| Android paket / iOS bundle | `com.misharix.misharitalia` |
| iOS | APNs anahtarı Firebase projesine tanımlı; `aps-environment` development (TestFlight/App Store'da production). |
| Kayıt ucu | `POST /api/store/push-devices` gövde `{firmPlatformId, platform:"android"\|"ios", token, deviceId, appVersion}` — token'a göre **upsert**; üye JWT ile gelirse `memberId` bağlanır, device token ile gelirse `memberId` boşalır (son istek geçerli). |
| Diğer uçlar | `POST /push-devices/revoke {firmPlatformId, token}` (uygulama çıkışta ÇAĞIRMAZ; yalnız üye bağı kopar), `GET /push-devices/mine` (üyenin cihazları: platform, status, maskeli token). |
| Uygulamanın kayıt zamanları | Açılış (izin verilmişse), token yenilenince (`onTokenRefresh`), **giriş ve çıkıştan hemen sonra**. |
| Tıklama işleme | `data.link` (yoksa `data.url`) → uygulama içi çözücü. Bilinmeyen link → hiçbir şey olmaz, uygulama açılır. |
| **Ön plan** | Uygulama ÖN PLANDAYKEN gelen bildirim henüz ekranda GÖSTERİLMİYOR (mobil yapılacaklar listesinde). Arka plan + kapalı durumda gösterim/tıklama tam çalışıyor. |
| Pazarlama izni | `PUT /api/store/account/marketing-consents {email, sms, phone}` mevcut — **`push` alanı YOK**, eklenmeli (bkz. §6). |
| Deep link altyapısı | Android App Links + iOS Universal Links `www.misharitalia.com` / `misharitalia.com` doğrulanmış; özel şema `misharix://`. `data.link` göreli yol (`/urun/...`) ya da tam URL (`https://www.misharitalia.com/urun/...`) olabilir; ikisi de çözülür. |

---

## 2. Gönderim yöntemi — FCM HTTP v1

- Uç: `POST https://fcm.googleapis.com/v1/projects/misharitalia-37192/messages:send`
- Yetki: Firebase **servis hesabı** JSON'u (Proje ayarları → Servis hesapları
  → "Yeni özel anahtar"), OAuth2 `https://www.googleapis.com/auth/firebase.messaging`
  kapsamı. Anahtar gizli yapılandırmada tutulur (repoya yazılmaz).
- Eski "legacy HTTP" API **kullanılmayacak** (kapatıldı).

### 2.1 Mesaj şablonu (bire bir kullanılabilir)

```json
{
  "message": {
    "token": "<push_devices.token>",
    "notification": {
      "title": "Siparişin kargoya verildi 📦",
      "body": "MIS0000061 numaralı siparişin DHL/MNG Kargo ile yola çıktı.",
      "image": "https://cdn.misharitalia.com/img/640/85/....webp"
    },
    "data": {
      "type": "order_shipped",
      "link": "/siparislerim/43362d68-38b7-4e55-bdc4-303419ac1270",
      "orderNumber": "MIS0000061",
      "dedupId": "order_shipped:43362d68-38b7-4e55-bdc4-303419ac1270",
      "sentAt": "2026-09-07T14:05:00Z"
    },
    "android": {
      "priority": "HIGH",
      "ttl": "86400s",
      "collapse_key": "order_shipped",
      "notification": {
        "channel_id": "default",
        "sound": "default",
        "click_action": "FLUTTER_NOTIFICATION_CLICK"
      }
    },
    "apns": {
      "headers": { "apns-priority": "10", "apns-collapse-id": "order_shipped" },
      "payload": {
        "aps": { "sound": "default", "badge": 1, "mutable-content": 1 }
      }
    }
  }
}
```

**Kurallar**
- `notification.title` + `notification.body` **zorunlu** (yalnız `data`
  gönderilirse Android arka planda hiçbir şey göstermez).
- `data` içindeki **tüm değerler string** olmalı (FCM şartı).
- `data.link` zorunlu (bkz. §3). `data.type` zorunlu (loglama/analitik).
- `data.dedupId` zorunlu: aynı olay iki kez gönderilmez (bkz. §6).
- `notification.image`: Android'de kendiliğinden görünür; iOS'ta görsel için
  Notification Service Extension gerekir (mobilde yok) → iOS'ta görsel
  gösterilmez, zararsız. Ürün bildirimlerinde ürün görselini koyun.
- Pazarlama bildirimlerinde `android.priority: "NORMAL"`, `apns-priority: "5"`.
- `ttl`: işlem bildirimleri 1 gün, kampanya 12 saat, stok/fiyat 6 saat.
- Aynı üyenin **tüm aktif cihazlarına** gönderilir (bir üye = N token).
- Toplu gönderimde FCM `sendEach` (batch 500) ya da sıralı; saniyede 500
  mesajı geçmeyin; hata durumunda üstel bekleme (429/5xx).

### 2.2 FCM hata → token durumu

| FCM hatası | Yapılacak |
|---|---|
| `UNREGISTERED` (404) | `push_devices.status = 'revoked'`, bir daha gönderme |
| `INVALID_ARGUMENT` (token biçimi) | `status = 'invalid'` |
| `SENDER_ID_MISMATCH` | Yanlış Firebase projesi — yapılandırma hatası, alarm |
| `UNAVAILABLE` / `INTERNAL` / 429 | Yeniden dene (üstel bekleme, en çok 5) |
| `QUOTA_EXCEEDED` | Gönderimi yavaşlat |

Uygulama yeni token aldığında tekrar kayıt yollar → `revoked` kayıt aynı
`deviceId` ile yeniden `active` olur (upsert `deviceId` üzerinden de eşleşmeli).

---

## 3. Link kataloğu — uygulamanın çözdüğü yollar

`data.link` aşağıdakilerden biri olmalı. Göreli yol tercih edilir. Tümü web
sitesinin yollarıyla aynıdır (aynı link e-posta/SMS'te de kullanılabilir).

| Amaç | `data.link` | Not |
|---|---|---|
| Ürün detayı | `/urun/{productCode}` **veya** `/urun/{slug}` | Kod `P-00021433` gibi; slug da çalışır. Sipariş kalemi / favori / stok alarmı kayıtlarında `productCode` var. |
| Ürün — belirli renk/beden | `/urun/{productCode}` | Şimdilik varyant taşınmıyor (mobil yapılacaklar: `?variant=`). |
| Ürün listesi / kategori | `/{kategori-slug}` (ör. `/kadin-bluz`, `/yeni-gelenler`) | Kategori `slug`'ı; `/urun-listesi?arama=elbise` de çalışır. |
| Arama | `/urun-listesi?arama=<kelime>` | |
| Ana sayfa | `/` | |
| Sepet | `/sepet` | Sepet hatırlatma |
| Favoriler | `/favorilerim` | |
| Hesabım | `/hesabim` | |
| Siparişlerim (liste) | `/siparislerim` | |
| **Sipariş detayı** | `/siparislerim/{orderId}` | **`orderId` = sipariş GUID'i**, sipariş numarası (MIS…) DEĞİL. |
| İadelerim | `/iadelerim` | |
| Kuponlarım | `/indirim-kuponlarim` | |
| Cüzdanım | `/cuzdanim` | |
| Yorumlarım | `/yorumlarim` | Değerlendirme daveti, yorum yayınlandı |
| Sorularım | `/sorularim` | Soru cevaplandı |
| Adreslerim | `/adreslerim` | |
| Önceden gezdiklerim | `/onceden-gezdiklerim` | |
| Koleksiyonlarım | `/koleksiyonlarim` | |
| Üyelik bilgilerim | `/uyelik-bilgilerim` | |
| Kurumsal sayfa | `/hakkimizda`, `/kargo-ve-teslimat`, `/iade-ve-degisim`, `/sik-sorulan-sorular`… | CMS slug'ı |
| İletişim | `/iletisim` | Uygulama içi form |
| Web'e özel | `/odeme`, `/teslimat` | Tarayıcıda açılır — bildirimde KULLANMAYIN |

Bilinmeyen/yanlış link → uygulama açılır, hata göstermez. Gönderim öncesi
backend linki bu tabloya karşı doğrulasın (şablon panelinde "link tipi" seçimi
önerilir).

---

## 4. Senaryolar

Her senaryo: tetikleyici, hedef, zamanlama, `type`, link, örnek metin,
tekilleştirme anahtarı, sınıf (İ = işlemsel, her zaman gönderilir; P =
pazarlama, izin + sessiz saat + sıklık sınırı uygulanır).

### 4.1 Sipariş yaşam döngüsü (İ)
| type | Tetikleyici | Link | Başlık / Gövde (öneri) | dedupId |
|---|---|---|---|---|
| `order_created` | Sipariş oluştu (kapıda ödeme) veya kart ödemesi onaylandı | `/siparislerim/{orderId}` | "Siparişin alındı ✅" / "{orderNumber} numaralı siparişin hazırlanmaya başlıyor." | `order_created:{orderId}` |
| `order_payment_pending` | Kart siparişi oluştu, 60 dk içinde ödeme tamamlanmadı (bir kez) | `/siparislerim/{orderId}` | "Ödemen tamamlanmadı" / "{orderNumber} siparişin ödeme bekliyor. Tamamlamak için dokun." | `order_payment_pending:{orderId}` |
| `order_confirmed` | status → confirmed | `/siparislerim/{orderId}` | "Siparişin onaylandı" / "{orderNumber} onaylandı, hazırlanıyor." | `order_status:{orderId}:confirmed` |
| `order_shipped` | status → shipped (kargo takip no oluştu) | `/siparislerim/{orderId}` | "Siparişin kargoya verildi 📦" / "{orderNumber}, {cargoName} ile yola çıktı. Takip: {trackingNumber}" | `order_status:{orderId}:shipped` |
| `order_delivered` | status → delivered | `/siparislerim/{orderId}` | "Siparişin teslim edildi 🎉" / "Keyifle kullan! Ürünlerini değerlendirmek ister misin?" | `order_status:{orderId}:delivered` |
| `order_review_invite` | delivered + 2 gün, üye ürünleri henüz değerlendirmediyse | `/yorumlarim` | "Ürünlerin nasıldı?" / "{productName} için görüşün diğer müşterilere yol gösterir." | `order_review_invite:{orderId}` |
| `order_cancelled` | status → cancelled (panelden; kullanıcı kendi iptalinde GÖNDERME) | `/siparislerim/{orderId}` | "Siparişin iptal edildi" / "{orderNumber} iptal edildi. Ödemen varsa iade sürecine alındı." | `order_status:{orderId}:cancelled` |
| `return_status` | İade talebi durum değişti (onaylandı / reddedildi / iade tamamlandı) | `/iadelerim` | "İade talebin güncellendi" / "{orderNumber}: {returnStatusLabel}" | `return_status:{returnId}:{status}` |

Kurallar: durum geri alınırsa (delivered → shipped) bildirim gönderilmez;
aynı `orderId+status` bir kez; kullanıcı misafirse (memberId yok) sipariş
bildirimleri **siparişi veren cihaza** (`deviceId`, sipariş anındaki
`push_devices` kaydı) gönderilir — bunun için checkout'ta cihaz kimliği
siparişe yazılmalı (mobil `X-Device-Id` başlığı gönderebilir; istenirse
eklenir) ya da yalnız üyelere gönderilir.

### 4.2 Favori / fiyat / stok (P — izinli üyeler)
| type | Tetikleyici | Link | Metin | dedupId |
|---|---|---|---|---|
| `favorite_price_drop` | Favorideki ürünün (renk bazlı favori: o rengin) satış fiyatı, favoriye eklendiği andaki fiyata göre **≥ %10** düştü (eşik panelden) | `/urun/{productCode}` | "Favorindeki ürün indirimde 🔥" / "{productName} şimdi {newPrice} (eski {oldPrice})." | `favorite_price_drop:{memberId}:{productCode}:{newPrice}` |
| `favorite_back_in_stock` | Favori ürünün favorilenen rengi tükenmişken tekrar stoğa girdi | `/urun/{productCode}` | "Favorin yeniden stokta" / "{productName} tekrar geldi, tükenmeden yakala." | `favorite_back_in_stock:{memberId}:{productCode}:{date}` |
| `favorite_low_stock` | Favori ürün varyantlarında toplam stok ≤ 3 | `/urun/{productCode}` | "Son parçalar!" / "{productName} tükenmek üzere." | `favorite_low_stock:{memberId}:{productCode}` (7 günde 1) |
| `stock_alert` | **"Gelince haber ver"** kaydı (`stock_alerts`) olan varyant stoğa girdi | `/urun/{productCode}` | "{productName} {variantInfo} stokta! 🔔" / "İstediğin beden geldi, hemen sepete ekle." | `stock_alert:{alertId}` (gönderince alarm kapanır) — **İ sınıfı** (kullanıcı özellikle istedi) |

Fiyat düşüşü kontrolü: fiyat değişimi olayında (ürün güncelleme) ya da günlük
toplu iş; üye başına günde en çok 1 fiyat bildirimi (en büyük düşüş).

### 4.3 Sepet (P)
| type | Tetikleyici | Link | Metin | dedupId |
|---|---|---|---|---|
| `cart_reminder` | Sepette ürün var, 3 saattir sepet güncellenmedi, sipariş verilmedi | `/sepet` | "Sepetinde ürün bekliyor 🛒" / "{productName} ve {n} ürün seni bekliyor." | `cart_reminder:{cartId}:{updatedAt}` (sepet başına 1; 24 saat sonra ikinci ve son) |
| `cart_price_drop` | Sepetteki ürünün fiyatı düştü | `/sepet` | "Sepetindeki ürün ucuzladı" / "{productName} şimdi {newPrice}." | `cart_price_drop:{cartId}:{variantId}:{newPrice}` |

### 4.4 Etkileşim (İ)
| type | Tetikleyici | Link | Metin | dedupId |
|---|---|---|---|---|
| `question_answered` | Satıcı soruyu cevapladı | `/sorularim` | "Sorun cevaplandı 💬" / "{productName} hakkındaki sorunun cevabı hazır." | `question_answered:{questionId}` |
| `review_approved` | Yorum onaylandı/yayınlandı | `/yorumlarim` | "Yorumun yayında" / "{productName} yorumun diğer müşterilere gösteriliyor. Teşekkürler!" | `review_approved:{reviewId}` |
| `review_rejected` | Yorum reddedildi (isteğe bağlı, kibar) | `/yorumlarim` | "Yorumun yayınlanamadı" / "Kurallara uymayan ifadeler nedeniyle yayınlanmadı." | `review_rejected:{reviewId}` |

### 4.5 Kupon / cüzdan (P; kupon tanımı kişiye özelse İ)
| type | Tetikleyici | Link | Metin | dedupId |
|---|---|---|---|---|
| `coupon_assigned` | Üyeye kupon tanımlandı | `/indirim-kuponlarim` | "Sana özel kupon 🎁" / "{couponCode}: {discountText}. {expiresAt} tarihine kadar geçerli." | `coupon_assigned:{memberCouponId}` |
| `coupon_expiring` | Kullanılmamış kuponun bitmesine 24 saat | `/indirim-kuponlarim` | "Kuponun yarın bitiyor" / "{couponCode} kuponunu kaçırma." | `coupon_expiring:{memberCouponId}` |
| `wallet_credit` | Cüzdana bakiye yüklendi (iade vb.) | `/cuzdanim` | "Cüzdanına {amount} yüklendi" / "İade tutarın cüzdanında, hemen kullanabilirsin." | `wallet_credit:{transactionId}` |

### 4.6 Pazarlama / yaşam döngüsü (P)
| type | Tetikleyici | Link | Metin | dedupId |
|---|---|---|---|---|
| `welcome` | Kayıttan 1 saat sonra (ilk sipariş yoksa) | `/` veya kampanya linki | "Hoş geldin 👋" / "İlk siparişine özel %10: HOSGELDIN" | `welcome:{memberId}` |
| `campaign` | Panelden segment/ tüm izinli kullanıcılar | Panelden (link tipi seçilir) | Panelden | `campaign:{campaignId}:{memberId}` |
| `winback` | 30 gündür açılış yok (`push_devices.last_seen_at`) | `/yeni-gelenler` | "Seni özledik" / "Yeni gelenlere göz at." | `winback:{memberId}:{month}` |
| `viewed_reminder` | Son 24 saatte ürün gezdi, sepete eklemedi | `/onceden-gezdiklerim` | "Baktığın ürünler burada" / "{productName} hâlâ stokta." | `viewed_reminder:{memberId}:{date}` (haftada 2) |

---

## 5. Hedefleme

- **Üye** → `push_devices` içinde `memberId = X AND status='active'` olan
  tüm token'lar. Aynı üyenin 2+ cihazına aynı `dedupId` ile gönder (cihaz
  başına ayrı kayıt).
- **Misafir** (memberId yok) → yalnız cihaz bazlı senaryolar (sepet hatırlatma
  için sepet `sessionId` ↔ cihaz eşlemesi yoksa gönderme).
- İki hesap tek cihaz: son giriş yapan hesap kazanır (upsert kuralı) — eski
  hesabın bildirimleri o cihaza GİTMEZ (doğru davranış).
- Platform farkı yok; `platform` yalnız loglama/analitik.
- Dil: şimdilik yalnız Türkçe.

---

## 6. Kurallar: izin, sessiz saat, sıklık, tekilleştirme

| Kural | İşlemsel (İ) | Pazarlama (P) |
|---|---|---|
| İzin | Gerekmez (kullanıcı OS iznini verdi) | `marketing_consents.push = true` şart → **`PUT /account/marketing-consents` gövdesine `push` alanı ekleyin**; mobil ayarlar ekranı buna bağlanacak. Varsayılan: kayıtta `false` (KVKK), kullanıcı açar. |
| Sessiz saat | Yok | 22:00–09:00 (Europe/Istanbul) gönderme, 09:00'a ertele |
| Sıklık | Yok | Üye başına günde en çok **2** pazarlama bildirimi; aynı `type` haftada en çok 2 |
| Tekilleştirme | `dedupId` benzersiz — `push_notifications.dedup_id UNIQUE` | Aynı |
| İptal koşulu | Gönderim anında olay hâlâ geçerli mi kontrol et (ör. sepet boşaldıysa `cart_reminder` gönderme; ürün favoriden çıktıysa fiyat bildirimi gönderme) | Aynı |
| Rozet | `apns.payload.aps.badge` = üyenin okunmamış sayısı (yoksa 1) | Aynı |

---

## 7. Veri modeli önerisi

```sql
-- mevcut: storefront.push_devices (token upsert). Eklenmesi önerilen sütunlar:
ALTER TABLE storefront.push_devices
  ADD COLUMN IF NOT EXISTS last_seen_at timestamptz,        -- her register'da güncelle
  ADD COLUMN IF NOT EXISTS status text NOT NULL DEFAULT 'active'; -- active|revoked|invalid

CREATE TABLE storefront.push_notifications (
  id uuid PRIMARY KEY,
  member_id uuid NULL,
  device_id text NOT NULL,          -- push_devices.device_id
  token_hash text NOT NULL,         -- token'ın sha256'sı (token'ı loga yazma)
  type text NOT NULL,               -- §4 type
  class text NOT NULL,              -- 'transactional' | 'marketing'
  dedup_id text NOT NULL UNIQUE,
  title text NOT NULL,
  body text NOT NULL,
  link text NOT NULL,
  data jsonb NOT NULL,              -- gönderilen data
  status text NOT NULL,             -- queued|sent|failed|skipped
  fcm_message_id text NULL,
  error_code text NULL,
  scheduled_at timestamptz NOT NULL,
  sent_at timestamptz NULL,
  opened_at timestamptz NULL,       -- ileride mobil POST /push-devices/opened
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ON storefront.push_notifications (member_id, created_at DESC);
CREATE INDEX ON storefront.push_notifications (status, scheduled_at);

CREATE TABLE storefront.push_templates (   -- panelden yönetilir
  type text PRIMARY KEY,
  class text NOT NULL,
  title_tr text NOT NULL,
  body_tr text NOT NULL,            -- {orderNumber} {productName} {newPrice} ... yer tutucular
  link_template text NOT NULL,      -- /siparislerim/{orderId}
  enabled boolean NOT NULL DEFAULT true,
  ttl_seconds int NOT NULL DEFAULT 86400,
  priority text NOT NULL DEFAULT 'high'
);
```

Mimari: olay → kuyruk (`push_notifications.status='queued'`) → gönderici
işçi (batch, retry) → sonuç. Sipariş/iade/yorum/soru olaylarında mevcut
domain event'lerine abone olun; fiyat/stok/sepet/kupon için zamanlanmış iş
(15 dk). Şablonlar `push_templates`'tan; şablon kapalıysa gönderme.

---

## 8. Yeni/ek uçlar (mobil bağlanacak)

| Uç | Amaç |
|---|---|
| `PUT /account/marketing-consents` → gövdeye `push: boolean` | Pazarlama push izni |
| `GET /account/marketing-consents` | Mobil ayarlar ekranı mevcut değerleri göstersin (şu an yalnız PUT var) |
| `POST /push-devices/opened {dedupId}` (üye ya da cihaz token'ı) | Açılma takibi (`opened_at`) — mobil `data.dedupId`'yi geri yollar |
| `GET /account/notifications?page=` (isteğe bağlı) | Uygulama içi "Bildirimlerim" listesi (ana sayfadaki zil ikonu için) — `push_notifications` üye kayıtları |
| Checkout gövdesine `deviceId` (isteğe bağlı) | Misafir siparişlerine sipariş bildirimi |

---

## 9. Test prosedürü (staging)

1. Test üyesi `mobil.test@ecspros.com` ile uygulamaya giriş yap (gerçek
   cihaz — emülatörde FCM çalışmıyor). `GET /push-devices/mine` ile token'ın
   kayıtlı ve `active` olduğunu gör.
2. Backend'den **yalnız o token'a** §2.1 şablonuyla `order_shipped` gönder
   (`link: /siparislerim/{gerçek orderId}`).
3. Kontrol:
   - Uygulama **kapalı** → bildirim gelir, tıklayınca sipariş detayı açılır.
   - Uygulama **arka planda** → aynı.
   - Uygulama **ön planda** → bildirim şimdilik görünmez (mobil yapılacak); log'da mesaj alınır.
4. Aynı `dedupId` ile ikinci gönderim → `skipped` (UNIQUE).
5. Geçersiz token ile gönderim → `UNREGISTERED` → kayıt `revoked`.
6. Fiyat düşüşü: test üyesinin favorisine ürün ekle, panelden fiyatı %15
   düşür → 15 dk içinde `favorite_price_drop` gelmeli; favoriden çıkarıp
   tekrar düşür → gelmemeli.
7. Stok alarmı: tükenmiş varyanta "Gelince haber ver" → panelden stok gir →
   `stock_alert` gelir, `stock_alerts` kaydı kapanır.
8. Sessiz saat: pazarlama bildirimi 23:00'te kuyruğa girer, 09:00'da gider.

**ASLA:** Firebase konsolu → Messaging → "Yeni kampanya" ile tüm
kullanıcılara gönderim. Proje canlı mağazayla ortak; gerçek müşterilere gider.

---

## 10. Kabul kriterleri

- [ ] §4.1 sipariş bildirimlerinin tamamı (created, payment_pending,
      confirmed, shipped, delivered, review_invite, cancelled, return_status)
      gerçek durum değişiminde tek cihaza gidiyor; `dedupId` tekilleştiriyor.
- [ ] `stock_alert` ve `favorite_price_drop` zamanlanmış işle çalışıyor,
      iptal koşulları uygulanıyor.
- [ ] `cart_reminder` 3s/24s kuralıyla, sepet boşaldıysa gönderilmiyor.
- [ ] Pazarlama sınıfı: izin (`push` consent) + sessiz saat + günlük sınır.
- [ ] FCM hata kodlarına göre token durumu güncelleniyor.
- [ ] Her gönderim `push_notifications`'a yazılıyor (token ham hâliyle DEĞİL,
      hash).
- [ ] Şablonlar panelden düzenlenebiliyor; kapalı şablon gönderilmiyor.
- [ ] `data.link` §3 tablosuna karşı doğrulanıyor; geçersizse gönderim
      reddediliyor.
- [ ] `marketing-consents` gövdesinde `push` alanı; `GET` ucu.
- [ ] Yük testi: 10.000 token'a kampanya 5 dk içinde, hata oranı < %1.

---

## 11. Mobil tarafında planlı işler (bilgi amaçlı, backend'i bloklamaz)
- Ön planda bildirim gösterimi (Android yerel bildirim kanalı `default`, iOS
  ön plan sunumu).
- `data.link`'te `?variant=` desteği (stok alarmı doğru bedenle açılsın).
- `POST /push-devices/opened` ile açılma bildirimi.
- Ayarlar ekranı: pazarlama push izni anahtarı (`marketing-consents.push`).
- "Bildirimlerim" listesi (ana sayfa zil ikonu) — `GET /account/notifications`
  açılırsa.
