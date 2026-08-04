# Mobil API — Test Kullanım Rehberi (2026-08-04)

Bu belge mobil geliştiricinin `/api/store/*` yüzeyini uçtan uca test etmesi içindir.
Uç envanteri için `docs/mobil-api-referansi.md`, staging işletimi için
`tools/mobile/STAGING.md` esastır; burası **nasıl test edilir** sorusunu yanıtlar.

## 1. Ortamlar

| Ortam | Adres | Attestation | Not |
|---|---|---|---|
| **Staging (önerilen)** | `http://51.178.208.59:5055` | **DevBypass** — secret ile gerçek device token alınır | Prod'la AYNI kod + AYNI DB; yalnız attestation adımı atlanır. Android debug build'de cleartext izni gerekir. |
| Prod | `https://new.ecspros.com` (veya `https://51.178.208.59`, self-signed) | Gerçek Play Integrity / App Attest | Play Integrity config'i (GCP + paket adı) girilmeden mobil buradan token ALAMAZ. |
| Swagger | `/swagger-mobile` (prod'da da açık) | — | Yalnız şema/deneme; store uçları kapı gerektirdiğinden Swagger'dan çıplak deneme 401 alır. |

**Kapı (önemli):** `/api/store/*` uçlarının TAMAMI kimliksiz istekte **401** döner
(`MobileGate:EnforceStoreTokens=true`). Açık olan yalnız cihaz doğrulama uçlarıdır:
`GET /api/store/device/challenge` ve `POST /api/store/device/attest`. Yani ilk çağrıda
401 görmek hata değildir — önce device token alınır.

## 2. Staging'i ayağa kaldırma (sunucuda, bir kez — sudo)

Kurulum dosyaları hazır (bkz. oturum scratchpad'i — `ecspros-staging.env` taze secret
içerir, `ecspros-staging.service` systemd birimi):

```bash
sudo cp <scratchpad>/ecspros-staging.env     /etc/ecspros-staging.env
sudo cp <scratchpad>/ecspros-staging.service /etc/systemd/system/ecspros-staging.service
sudo chmod 600 /etc/ecspros-staging.env
sudo systemctl daemon-reload && sudo systemctl enable --now ecspros-staging
sudo ufw allow 5055/tcp        # dış erişim için
curl -s http://localhost:5055/api/store/device/challenge | head -c 120   # duman testi
```

- Staging binary'si `/opt/ECSProsAI/publish-staging` — **2026-08-04'te güncellendi**
  (kampanya F5, sipariş onayı, senkron dahil güncel kod). Kod değiştikçe yeniden yayım:
  `dotnet publish ... -o /opt/ECSProsAI/publish-staging` + prod config kopyası +
  `sudo systemctl restart ecspros-staging` (tam komutlar STAGING.md'de).
- **DevBypass secret'ı** `/etc/ecspros-staging.env` içindedir; geliştiriciye GÜVENLİ
  kanaldan verilir, git'e/uygulama koduna asla girmez. Rotasyon: `openssl rand -hex 24`
  ile değiştir + restart.

## 3. Test akışı — adım adım

### 3.1 Cihaz kimliği (her açılışta)
```
GET  /api/store/device/challenge                     → { challenge }   (10 dk, tek kullanım)
POST /api/store/device/attest
     { "platform": "android", "attestation": "<BYPASS_SECRET>", "challenge": "<challenge>" }
     → { deviceToken, signingSecret, expiresAt }     (token 15 dk)
```
Staging'de `attestation` alanına DevBypass secret'ı yazılır; prod'da buraya gerçek
Play Integrity token'ı gelir. **Sonraki tüm adımlar prod'la birebir aynıdır.**

### 3.2 İmzalı istek (device token taşıyan her çağrı)
```
Authorization: Bearer <deviceToken>
X-Client-Platform: android | ios
X-Timestamp: <unix saniye>          (±300 sn)
X-Nonce: <benzersiz>                (tek kullanımlık)
X-Signature: hex(HMACSHA256(base64decode(signingSecret),
    "METOD\n/api/store/...?query\ntimestamp\nnonce\nsha256hex(body)"))
```
- Boş gövdenin hash'i: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`
- İmzadaki path **query string dahil**, host hariç.
- Çalışan örnek: `tools/mobile/reference-client.mjs` (bağımlılıksız Node) —
  ```bash
  BASE=http://51.178.208.59:5055 BYPASS=<secret> \
    EMAIL=mobil.test@ecspros.com PASSWORD='MobilTest2026!' \
    node tools/mobile/reference-client.mjs
  ```
  Bu istemci challenge→attest→bootstrap→katalog→sepet→üye girişi zincirini koşar;
  Kotlin/Swift implementasyonu için referanstır.

### 3.3 Kanal + açılış
```
GET /api/store/bootstrap            → { firmPlatformId, code:"mishar", ... }
```
Dönen `firmPlatformId` tüm çağrılarda query/body parametresi olarak kullanılır
(mishar: `c900c659-8d0f-4754-9658-aa157ea3072e`).

### 3.4 Üye akışı
- Test hesabı: `mobil.test@ecspros.com / MobilTest2026!` (ortak DB'de mevcut).
- `POST /api/store/auth/login` → üye access+refresh token. Üye uçlarında
  `Authorization: Bearer <üyeToken>` kullanılır; **üye token'lı isteklerde HMAC imzası
  gerekmez** (imza yalnız device-token'lı anonim istekler içindir).
- OTP girişi test edilecekse: SMS gerçek gönderilir (GES Telekom) — kendi telefon
  numaranızla üye oluşturup deneyin; maliyet için toplu koşularda e-posta girişini kullanın.

### 3.5 Uçtan uca duman senaryosu (önerilen sıra)
1. challenge → attest → **bootstrap** (kanal id)
2. `pages/home` (vitrin) + `catalog/channel-categories` (menü)
3. Kategori ürünleri + ürün detayı (`catalog/products/{code}`)
4. Sepet: `POST cart/items` (misafir `sessionId` üretin) → `GET cart` →
   **kampanya alanlarını doğrulayın** (`campaignDiscount`, `campaigns`, kalem
   `campaignLineDiscount` — 2AL1ODE senaryosu)
5. `checkout/coupon/validate` → `POST checkout` (kapıda ödeme) → sipariş no döner
6. **Sipariş onayı (YENİ, 2026-08-04):** kapıda sipariş `pending` doğar; telefona SMS
   onay linki gider (`/o/{token}`). Mobil tarafta "Siparişlerim"de pending sipariş için
   `POST /api/store/account/orders/{id}/confirm` ile onay verilebilir. Onaysız sipariş
   operasyona düşmez — test verirken bunu bilerek kurgulayın.
7. Üye: login → `cart/merge` → `account/orders` (sipariş listesi/detayı, kargo takip)

### 3.6 Beklenen hata davranışları (negatif testler)
| Durum | Beklenen |
|---|---|
| Kimliksiz `GET /api/store/catalog/products` | 401 (kapı) |
| Süresi dolmuş device token | 401 → challenge/attest baştan |
| Aynı `X-Nonce` ikinci kez | 401 "replay" |
| `X-Timestamp` 300 sn'den eski | 401 |
| İmza path'inde query eksik | 401 (imza tutmaz — en sık entegrasyon hatası) |
| Yanlış BYPASS secret | attest 401 (staging log'unda görünür) |
| Bozuk onay linki `/o/xyz` | Nazik hata sayfası (200, "Bağlantı Bulunamadı") |

## 4. Sık takılınan noktalar
- **Saat kayması:** emülatör/cihaz saati gerçek zamandan >5 dk sapıksa tüm imzalı
  istekler 401 alır — cihaz saatini otomatik senkrona alın.
- **Cleartext (Android debug):** staging HTTP olduğundan debug manifest'te
  `android:usesCleartextTraffic="true"` (veya network security config) gerekir.
- **Self-signed HTTPS (prod IP):** `https://51.178.208.59` sertifikası self-signed —
  test build'lerinde pinning/trust ayarı gerekir; alan adı `new.ecspros.com` gerçek
  sertifika kullanır.
- **X-Client-Platform** başlığını her istekte gönderin — segment/vitrin hedeflemesi
  buna göre çalışır; unutulursa istek çalışır ama içerik hedeflemesi eksik olur.
- Staging **ortak (canlı) DB** kullanır: test siparişleri gerçek tablolara yazılır ve
  eski sisteme SENKRON edilebilir — sipariş testlerinde "test" olduğu belli alıcı adı
  kullanın ve operasyona haber verin (ya da test sonrası iptal edin).

## 5. Prod'a geçiş için eksikler (mobil canlıya çıkmadan)
1. **Play Integrity config** (GCP service account + paket adı → `MobileAttestation:PlayIntegrity:*`) — kullanıcıda.
2. iOS **App Attest** sunucu doğrulaması (FAZ 2 — Apple team id gelince).
3. Staging'in kapatılması + DevBypass secret imhası (STAGING.md "Kapatma" bölümü).
