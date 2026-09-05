# Mobil Uygulama API Referansı

Web sitesinin (misharix vitrini) kullandığı JSON servislerinin mobil istemci için dökümüdür.
Vitrin mimarisi hibrittir: Razor sayfaları yalnız HTML kabuk üretir, canlı veri bu belgede
listelenen `/api/store/*` JSON uçlarından gelir. **Mobil uygulama bu uçları olduğu gibi
kullanır — mobil için ayrı bir API katmanı yoktur ve gerekmez.**

- **Base URL**: `https://51.178.208.59/api` (prod, nginx) — geliştirmede `http://localhost:5050/api`
- **Yanıt zarfı**: her uç `{ "success": true, "data": ... }` veya `{ "success": false, "error": "..." }` döner
- **JSON**: camelCase, null alanlar gizlenir, enum'lar string
- **Swagger**: `http://51.178.208.59/swagger-mobile` (bağımsız adres, prod'da da açık; ham şema: `/swagger/mobile/swagger.json`). Partner API'nin ayrı adresi vardır (`/swagger-partner`) — pazaryeri/dropshipping partnerlarına aittir, mobil onu kullanmaz.

---

## 1. Açılış (bootstrap) ve kanal kimliği

Tüm `/api/store/*` uçları **`firmPlatformId`** (kanal kimliği, GUID) ister — web sitesi bunu
Host başlığından çözer, mobil ise açılışta bir kez öğrenip tüm çağrılarda gönderir:

| Method | Uç | Açıklama |
|--------|-----|---------|
| GET | `/api/store/bootstrap` | Varsayılan kanal (`mishar`) — `firmPlatformId`, `code`, `nameI18n` döner |
| GET | `/api/store/bootstrap?code=mishar` | Kanal kodu build config'te sabitlenirse açık çağrı |
| GET | `/api/store/segment` | Ziyaretçinin çözülmüş segmenti (şehir/bölge/cinsiyet/cihaz/üyelik) |

**Zorunlu header (tüm isteklerde):** `X-Client-Platform: ios` veya `android` — segment
çözümleyici cihaz kanalını buradan tanır (hedefli vitrin blokları/kampanyalar buna göre gelir).

Şehir seçimi: web'de `ms_sehir` cookie'si ile taşınır; mobil aynı davranışı `ms_sehir=<plaka>`
cookie'si göndererek sağlar (2 haneli plaka kodu, ör. `34`).

## 1.5 Cihaz doğrulama (attestation) — `/api/store/device` (2026-07-23)

Uygulamaya **sabit token/API key gömülmez**. Sunucu, isteğin gerçek ve değiştirilmemiş
uygulamadan geldiğini platform attestation'ıyla doğrular ve **anlık** token üretir:

**Akış (her açılışta / token dolunca):**
1. `GET /api/store/device/challenge` → tek kullanımlık `challenge` (10 dk geçerli)
2. Uygulama challenge'ı platform doğrulamasına gömer:
   - **Android**: Play Integrity API → integrity token (challenge = nonce alanında)
   - **iOS**: App Attest (FAZ 2 — sunucu tarafı iOS kimliği alınınca tamamlanacak)
3. `POST /api/store/device/attest` `{platform, attestation, challenge}` →
   `{deviceToken, signingSecret, expiresAt}` — device token **15 dk** ömürlü anonim JWT,
   `signingSecret` o oturuma özel sunucu üretimi HMAC anahtarı (güvenli bellekte tutulur,
   diske yazılmaz).

**İmzalı istek (device token taşıyan HER istekte zorunlu):**
```
Authorization: Bearer <deviceToken>
X-Timestamp: <unix saniye>            (±300 sn tolerans)
X-Nonce: <benzersiz değer>            (tek kullanımlık — tekrarında 401 "replay")
X-Signature: hex(HMACSHA256(base64decode(signingSecret),
    "METOD\n/path?query\ntimestamp\nnonce\nsha256hex(body)"))
```
Boş gövdede `sha256hex(body)` = boş dizinin SHA256'sı (`e3b0c442…`). Token dolunca akış
baştan (challenge → attest) tekrarlanır.

**Kapı (`MobileGate:EnforceStoreTokens`)**: **AÇIK (2026-07-23 cutover yapıldı)** —
`/api/store/*` uçlarının tamamı (ürün listeleme dahil) kimliksiz isteklere **401** döner.
Geçerli kimlikler: device token (mobil, imzalı), üye JWT, web token (site) veya admin.
İstisna: `/api/store/device/*` (challenge/attest — token üretim uçları).

**Web istemcisi**: SSR her sayfaya 15 dk ömürlü `type=web` token gömer
(`<meta name="ms-api-token">`); layout'taki global fetch yaması `/api/*` çağrılarına
otomatik ekler — sitedeki hiçbir view değişmedi. Sekme açıkken 10 dk'da bir sessiz
yenilenir; yenileme zinciri 8 ile sınırlı (≈2 saat, sonra sayfa yenileme SSR'dan taze
token alır). Süresi dolan token'da yama bir kez yenileyip isteği tekrarlar.

**Üye girişi**: attestation'dan sonra login/OTP istekleri device token + imza ile yapılır;
dönen üye JWT'si sonraki isteklerde device token'ın YERİNE geçer (üye token'lı isteklerde
imza başlıkları gerekmez).

### Geliştirme sırasında test (Play Integrity yayına hazır olmadan)

Play Integrity, GCP servis hesabı + yayınlanmış paket adı ister; bunlar hazır olmadan mobil
geliştirici gerçek attestation üretemez. Bu yüzden **DevBypass** köprüsü vardır: sunucuya
`MobileAttestation__DevBypassSecret=<güçlü-secret>` env var'ı verildiğinde, `attest` ucuna
attestation olarak bu secret gönderilirse gerçek device token üretilir. **Attestation ADIMI
dışındaki her şey (imza, nonce, replay, kapı, üye akışı) prod'la AYNI** — geliştirici tam
akışı test eder, yalnız "cihaz gerçek mi?" kontrolü atlanır.

- Secret **APK'ya konmaz**, geliştiriciye ayrı kanaldan verilir, Play Integrity canlanınca
  kaldırılır. Ortam seçenekleri: (a) ayrı bir staging instance'ta açık, prod'a dokunmadan;
  (b) prod'da geçici, güçlü secret + kaldırma planıyla (kapıyı zayıflatır — dikkatli).
- Referans istemci: **`tools/mobile/reference-client.mjs`** — challenge → attest → imzalı
  istek → üye girişi akışının çalışan Node örneği (harici bağımlılık yok). İmza üretimi
  (`HMACSHA256` mantığı) uygulamada Kotlin/Swift'e bunun üzerinden çevrilir. Çalıştırma:
  `BASE=<url> BYPASS=<secret> EMAIL=<üye> PASSWORD=<şifre> node tools/mobile/reference-client.mjs`

**SSL pinning (istemci tarafı önerisi)**: sunucu Cloudflare arkasında olduğundan sertifika
pinlemesi Cloudflare'ın kök/aracı sertifikalarına yapılmalı (yaprak sertifika CF tarafından
rotasyona uğrar — yaprağa pinleme uygulamayı kırar). Certificate transparency + pin
güncellemesi için uzaktan config düşünülmeli.

## 2. Üye kimlik doğrulama — `/api/store/auth`

JWT Bearer akışı; access token 60 dk, refresh token 30 gün (rotasyonlu). Web'in kullandığı
cookie mekanizması SSR'a özeldir — mobil yalnız `Authorization: Bearer <token>` gönderir,
token'lar yanıt gövdesinde döner.

| Method | Uç | Auth | Açıklama |
|--------|-----|------|---------|
| POST | `/auth/register` | — | Üye kaydı (sözleşme onayları dahil) |
| POST | `/auth/login` | — | E-posta/şifre girişi → access + refresh token |
| POST | `/auth/otp/send` | — | SMS OTP gönder (şifresiz giriş) |
| POST | `/auth/otp/verify` | — | OTP doğrula → token'lar |
| POST | `/auth/refresh` | — | Access token yenile (refresh token gövdede) |
| POST | `/auth/logout` | Üye | Oturumu sonlandır |
| GET | `/auth/me` | Üye | Mevcut üye bilgisi |

"Üye" = `MemberOnly` policy (token'da `type=member` claim'i). Admin/`api_client` token'ları bu
uçlarda geçmez, üye token'ı da admin uçlarında geçmez.

## 3. Katalog — `/api/store/catalog` (anonim)

| Method | Uç | Açıklama |
|--------|-----|---------|
| GET | `/catalog/channel-categories?firmPlatformId=` | Kanal kategori ağacı (menü/nav bundan kurulur) |
| GET | `/catalog/channel-categories/{id}/products?page=&pageSize=` | Kategori ürün listesi (sayfalı, filtre paramlı) |
| GET | `/catalog/channel-categories/{id}/facets` | Kategori filtre yüzleri (beden/renk/fiyat…) |
| GET | `/catalog/products?search=&page=` | Arama / genel ürün listesi |
| GET | `/catalog/products/facets?search=` | Arama sonucu filtre yüzleri |
| GET | `/catalog/products/{code}?firmPlatformId=` | Ürün detayı (varyantlar, görseller, fiyat) |
| GET | `/catalog/product-groups/{id}/products` | Aynı gruptaki kardeş ürünler (renk seçenekleri) |
| POST | `/gorsel-arama` | Görselle arama (multipart image → ürün kartları JSON) |

Listelerde varyant görseli + varyant fiyatı döner; stoğu bitenlerin görünürlüğü kanal
ayarından sunucu tarafında uygulanır — istemci ek filtre yapmaz.

## 4. Sepet — `/api/store/cart` (anonim; misafir sepeti `sessionId` ile)

| Method | Uç | Açıklama |
|--------|-----|---------|
| GET | `/cart?cartId=&memberId=&sessionId=&firmPlatformId=` | Sepeti getir |
| POST | `/cart/items` | Sepete ekle (stok kontrolü sunucuda) |
| PUT | `/cart/{cartId}/items/{itemId}` | Adet güncelle |
| DELETE | `/cart/{cartId}/items/{itemId}` | Kalem sil |
| DELETE | `/cart/{cartId}` | Sepeti boşalt |
| POST | `/cart/merge` (Üye) | Girişte misafir sepetini üye sepetiyle birleştir |

Misafir akışı: mobil bir `sessionId` (rastgele GUID) üretip saklar; üye girişinde
`/cart/merge` çağrılır. Üye "sepetten çıkarılanlar": `/api/store/cart/removed` (GET/POST/DELETE, Üye).

## 5. Ödeme (checkout) — `/api/store/checkout`

| Method | Uç | Auth | Açıklama |
|--------|-----|------|---------|
| GET | `/cargo-options?firmPlatformId=&neighborhoodId=` | — | Kargo seçenekleri + ücretleri |
| POST | `/checkout/coupon/validate` | — | Kupon doğrula + indirim hesapla |
| POST | `/checkout` | — | Siparişi tamamla (misafir de verebilir; üye token'ı varsa üyeye bağlanır) |

Sipariş sonrası kupon kullanımı ve sözleşme versiyonları sunucu tarafında işlenir.
TCKN eşiği (`Store:TcknThreshold`) üstü tutarlarda kimlik no zorunluluğu sunucu doğrular.

## 6. Hesabım — `/api/store/account` (tümü Üye)

| Alan | Uçlar |
|------|-------|
| Profil | `GET/PUT /account/profile`, `PUT /account/marketing-consents`, `POST /account/identity` (TCKN), `POST /account/phone-verification/send|verify`, `GET /account/sessions`, `DELETE /account` (hesap silme) |
| Adresler | `GET /account/addresses`, `POST`, `PUT /{id}`, `PUT /{id}/default`, `DELETE /{id}` |
| Siparişler | `GET /account/orders` (sayfalı), `GET /account/orders/{id}`, `GET /account/orders/{id}/invoices` (+ `/pdf`) |
| İadeler | `GET /account/returns`, `GET /account/returns/{id}`, `POST /account/returns`, `POST /account/returns/images` |
| Cüzdan & sadakat | `GET /account/wallet`, `GET /account/loyalty`, `GET /account/coupons` |

## 7. Üye etkileşimleri (tümü Üye, aksi belirtilmedikçe)

| Alan | Uçlar |
|------|-------|
| Favoriler | `GET/POST /api/store/favorites`, `DELETE /favorites/{productCode}` |
| Koleksiyonlar | `GET/POST /api/store/collections`, `PUT/DELETE /{id}`, `POST /{id}/items`, `POST /saved/toggle`; paylaşım linki verisi kataloğa `shareCode` ile gelir |
| Yorumlar | `GET /api/store/reviews/product/{code}` + `/summary` (anonim); `GET /reviews/mine`, `GET /reviews/reviewable`, `POST /reviews`, `POST /reviews/images`, `DELETE /reviews/{id}` |
| Satıcıya sorular | `GET /api/store/questions/product/{productCode}?firmPlatformId=&limit=` (anonim — yayındaki cevaplanmış sorular, ad maskeli, limit ≤50); `GET /questions/mine` (Sorularım: cevaplananlar + bekleyenler), `POST /questions` (aynı üründe cevap bekleyen soru varken yenisi engellenir) |
| Kayıtlı aramalar | `GET/POST /api/store/saved-searches`, `PUT/DELETE /{id}` |
| Stok alarmı | `GET/POST /api/store/stock-alerts` |
| Gezilen ürünler | `GET /api/store/viewed-products`, `DELETE` (temizle) |

## 8. İçerik / vitrin — anonim

| Method | Uç | Açıklama |
|--------|-----|---------|
| GET | `/api/store/pages/{placement}?firmPlatformId=` | Vitrin kompozisyonu (ana sayfa = `home`, duyuru barı = `global-top`) — segment'e göre hedeflenmiş bloklar |
| GET | `/api/store/pages/blocks/{blockId}/products?page=` | Blok ürünlerinde sonsuz kaydırma devamı |
| GET | `/api/store/cms/menus/{code}?firmPlatformId=` | Navigasyon menüsü (`footer` vb.) |
| GET | `/api/store/cms/legal?firmPlatformId=&codes=` | Sözleşme/yasal sayfa içerikleri (SSS, KVKK, iade koşulları… kurumsal sayfa içerikleri buradan) |
| GET | `/api/store/geo/countries|cities|districts|neighborhoods` | Adres formu için coğrafi hiyerarşi |
| POST | `/api/store/contact` | İletişim formu |
| POST | `/api/store/newsletter` | Bülten kaydı |

---

## 9. Commerce event (takip) — `POST /api/store/events` (anonim; 2026-08-22, İE-2)

Mobil uygulamada tarayıcı pixel'i olmadığından davranış event'leri sunucuya bildirilir; sunucu
kanal/üye/IP/UA'yı ekleyip kalıcı kuyruğa yazar, aktif takip entegrasyonlarına (Meta CAPI,
TikTok Events API, GA4 Measurement Protocol — Faz D) kanal + consent kuralıyla dağıtır. Plan:
`docs/reklam-analytics-entegrasyon-is-akisi.md`.

- Gövde: `{ "name": "added_to_cart", "firmPlatformId": "...", "source": "mobile", "dedupId": "<uuid>",
  "currency": "TRY", "value": 1299.90, "items": [{ "itemId": "<varyant SKU>", "itemGroupId": "<ürün kodu>",
  "name": "...", "variant": "Renk: Siyah, Beden: M", "price": 1299.90, "quantity": 1, "discount": 0 }],
  "extra": { "list_id": "kadin-elbise" }, "client": { "gaClientId": null, "fbp": null, "fbc": null,
  "ttclid": null, "gclid": null }, "consent": { "analytics": true, "ads": false, "personalization": false } }`
- `name` izinli adlar: `product_viewed, product_list_viewed, search, added_to_cart, removed_from_cart,
  cart_viewed, checkout_started, shipping_info_added, payment_info_added, sign_up, login,
  wishlist_added, newsletter_subscribed` — `order_completed`/`refund` **istemciden kabul edilmez**
  (sunucu sipariş onayında üretir). Geçersiz ad → 400 (`allowed` listesiyle).
- `consent` mobilde ZORUNLU sayılır (uygulama içi izin ekranı); gönderilmezse çerez aranır, yoksa
  tüm kategoriler DENY → event hiçbir platforma gitmez (EU/KVKK kararı).
- `dedupId` istemci üretir (uuid); aynı event'i tekrar gönderirse sunucu yok sayar.
- Yanıt her zaman `{ success: true }` (takip kapalıyken de); rate limit `store-sensitive`.

## Bilinen eksikler / notlar

1. **Kargo takip**: gerçek servis yok — sitedeki `/uyeliksiz-kargo-takip` demo HTML'dir.
   Üye sipariş kargoları `GET /account/orders/{id}` detayında gelir. Üyeliksiz takip mobile
   eklenecekse kargo entegrasyonu işiyle birlikte yapılacak.
2. **API versiyonlama yok** — uçlar web sitesiyle ortaktır; kırıcı değişiklik yapılmaz,
   ekleme yapılır. Mobile özel ihtiyaç doğarsa versiyonlama o zaman değerlendirilecek.
3. `firmPlatformId` her istekte zorunludur (bkz. bölüm 1); göndermeyen çağrılar boş/yanlış
   kanal verisi alır.
4. Görsel arama ucu (`POST /gorsel-arama`) `api/` öneksizdir — tarihsel; mobil aynen kullanır.
5. **Hız sınırları (2026-07-23)**: kimlik uçları (`/auth/*`) IP başına dakikada 60, kupon
   doğrulama ve görsel arama dakikada 30 istekle sınırlıdır; aşımda `429` + `{success:false,
   error:...}` döner — istemci 429'da üstel bekleme (exponential backoff) uygulamalıdır.
   Şifreli girişte hesap başına 5 hatalı deneme sonrası 15 dk kilit vardır; kullanıcıya
   dönen `error` mesajı gösterilmelidir.
