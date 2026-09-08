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
Host başlığından çözer, mobil ise açılışta bir kez öğrenip tüm çağrılarda gönderir.

**TEK KURAL (A6, 2026-09-07):** kanal kimliği her istekte **`X-Firm-Platform: <guid>` başlığıyla**
gönderilir; `?firmPlatformId=` (GET/DELETE) ve gövdedeki `firmPlatformId` (POST/PUT) artık
**gerekmez** — eksik/boş bırakılan alanı sunucu başlıktan doldurur. Eski kullanım (query/gövde)
geriye uyumlu çalışır; ikisi birden verilirse istemcinin açık verdiği değer kazanır.

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
| GET | `/catalog/products?search=&page=` | Arama / genel ürün listesi. **2026-09-07:** küfür/hakaret içeren `search` için sorgu ÇALIŞMAZ → `400 {"success":false,"error":"Bu arama yapılamıyor."}` (aynı kural `products/facets` ve `channel-categories/{id}/products?search=` için); istemci mesajı gösterir, yeniden denemez |
| GET | `/catalog/products/facets?search=` | Arama sonucu filtre yüzleri |
| GET | `/catalog/popular-searches?firmPlatformId=&limit=` | Popüler aramalar (arama kutusu açılışı). **2026-09-07 ölçütleri:** son 30 gün, en az 3 arama, **en az 2 farklı ziyaretçi**, **en az bir aramada sonuç çıkmış**; küfür içerenler hiç sayılmaz/listelenmez; veri azken tohum listeyle tamamlanır (5 dk önbellek) |
| GET | `/catalog/products/{code}?firmPlatformId=` | Ürün detayı (varyantlar, görseller, fiyat). **A5:** `{code}` ürün kodu (`P-xxxxx`) **ya da site ürün slug'ı** (`kadin-...-1475253`) olabilir; slug'la açılırsa `selectedColorValueId` slug'ın rengidir |
| GET | `/catalog/products/by-slug?slug=` | **A5:** slug → `{productCode, colorValueId}` (hafif çözümleyici; 404 = yok) |
| GET | `/catalog/channel-categories/by-slug/{slug}` | **A5:** kategori slug'ı → yayındaki kanal kategorisi (`id, nameI18n, slug, displayImageUrl, badgeLabel`) — istemci slug indeksi kurmaz |
| GET | `/catalog/product-groups/{id}/products` | Aynı gruptaki kardeş ürünler (renk seçenekleri) |
| POST | `/gorsel-arama` | Görselle arama (multipart image → ürün kartları JSON) |

Listelerde varyant görseli + varyant fiyatı döner; stoğu bitenlerin görünürlüğü kanal
ayarından sunucu tarafında uygulanır — istemci ek filtre yapmaz. **A8 (2026-09-07):** arama/genel
liste de kategori ucuyla aynı renk-stok kuralını uygular (kanal "stoğu biteni göster" kapalıysa
stoksuz renk `colors[]`'da yer almaz; kartın ana görseli stoklu renge kayar).

**Ürün detayı alanları (A10, 2026-09-07 eklemeleri):** `minPrice` (satış fiyatı), `compareAtPrice`
(çizili; yalnız satış fiyatından büyükse), `campaignPrice` + `campaignName` (ürün-bazlı kampanya;
null = yok/sepette uygulanır), `descriptionI18n`, `shortDescriptionI18n`, `productGroupId`,
`slug` (kanonik), `variantSlugs` (varyant → slug), `selectedColorValueId`. Varyantlarda
`platformPrice`/`compareAtPrice` zaten vardı. **A3:** `variants[].attributes[]` içinde renk ekseni
(`renk`) artık `isColor:true` gelir (filtre_rengi yalnız renk ekseni olmayan üründe renk sayılır);
liste kartı `colors[]` de filtre_rengi'siz üründe renk ekseninden dolar.

**Liste kartı şeması (B5):** iki liste ucu artık ortak takma adları da taşır — arama kartında
`productId`/`basePrice`, kategori kartında `id`/`minPrice` de bulunur (`id`=`productId`,
`minPrice`=`basePrice`=kanal satış fiyatı). Fark: kategori kartı **ürün×renk**, arama kartı **ürün**.

## 4. Sepet — `/api/store/cart` (anonim; misafir sepeti `sessionId` ile)

| Method | Uç | Açıklama |
|--------|-----|---------|
| GET | `/cart?cartId=&memberId=&sessionId=&firmPlatformId=` | Sepeti getir. **A7:** sepet yoksa `data` **boş sepet nesnesidir** (`id` = `00000000-…`, `items: []`, `subtotal: 0`) — `data`'sız yanıt yok. **A2:** kalemlerde `colorValueId` + `sizeValueId` (renk/beden değer kimlikleri) gelir |
| POST | `/cart/items` | Sepete ekle (stok kontrolü sunucuda) |
| PUT | `/cart/{cartId}/items/{itemId}` | Adet güncelle |
| DELETE | `/cart/{cartId}/items/{itemId}` | Kalem sil |
| DELETE | `/cart/{cartId}` | Sepeti boşalt |
| POST | `/cart/merge` (Üye) | Girişte misafir sepetini üye sepetiyle birleştir |

Misafir akışı: mobil bir `sessionId` (rastgele GUID) üretip saklar; üye girişinde
`/cart/merge` çağrılır. Üye "sepetten çıkarılanlar": `/api/store/cart/removed` (GET/POST/DELETE, Üye).

**A4 (2026-09-07):** checkout başarılıysa sepet **sunucuda** temizlenir — kapıda ödemede sipariş
anında, kartta ödeme onayında (PayTR callback / mock). Başarısız kart denemesinde sepet korunur.
İstemcinin kalem kalem DELETE atması gerekmez.

## 5. Ödeme (checkout) — `/api/store/checkout`

| Method | Uç | Auth | Açıklama |
|--------|-----|------|---------|
| GET | `/cargo-options?firmPlatformId=&neighborhoodId=` | — | Kargo seçenekleri + ücretleri |
| GET | `/payment-options?firmPlatformId=` | — | **A9 (2026-09-07):** `{methods:["kart","kapida-nakit","kapida-kart"], codFee:50, codLimit:3000}` — panel ayarı; checkout aynı kaynağı doğrular (codLimit 0 = sınır yok) |
| POST | `/checkout/coupon/validate` | — | Kupon doğrula + indirim hesapla |
| POST | `/checkout` | — | Siparişi tamamla (misafir de verebilir; üye token'ı varsa üyeye bağlanır) |
| POST | `/payment/paytr/init` | — | Kart ödemesi başlat → **B6:** `{success:true, data:{html}}` (kök `html` eski web istemcisi için de durur); 3D HTML'i WebView'a basılır |
| POST | `/payment/paytr/taksit` | — | BIN'e göre taksit seçenekleri `{taksitler:[{adet,birim,toplam}]}` |

Sipariş sonrası kupon kullanımı ve sözleşme versiyonları sunucu tarafında işlenir.
TCKN eşiği (`Store:TcknThreshold`) üstü tutarlarda kimlik no zorunluluğu sunucu doğrular.

## 6. Hesabım — `/api/store/account` (tümü Üye)

| Alan | Uçlar |
|------|-------|
| Profil | `GET/PUT /account/profile`, `PUT /account/marketing-consents`, `POST /account/identity` (TCKN), `POST /account/phone-verification/send|verify`, `GET /account/sessions`, `DELETE /account` (hesap silme) |
| Adresler | `GET /account/addresses`, `POST`, `PUT /{id}`, `PUT /{id}/default`, `DELETE /{id}` |
| Siparişler | `GET /account/orders` (sayfalı), `GET /account/orders/{id}`, `GET /account/orders/{id}/invoices` (+ `/pdf`). **A1/A2 (2026-09-07):** detay `items[]` her kalemde `productCode`, `imageUrl` (varyant görseli), `colorValueId`, `sizeValueId` taşır — ad/metin eşleme gerekmez |
| İadeler | `GET /account/returns`, `GET /account/returns/{id}`, `POST /account/returns`, `POST /account/returns/images` |
| Cüzdan & sadakat | `GET /account/wallet`, `GET /account/loyalty` (**C3:** hesap yoksa 400 yerine sıfır bakiyeli hesap döner), `GET /account/coupons` |
| Adres kimlikleri | **B4:** web hesap formu da mahalleyi seçiciden yazar (`neighborhoodId` dolu); eski kayıtlar adından tek eşleşmeyle geri dolduruldu — ad→kimlik tahmini gerekmez |

## 7. Üye etkileşimleri (tümü Üye, aksi belirtilmedikçe)

| Alan | Uçlar |
|------|-------|
| Favoriler | `GET/POST /api/store/favorites`, `DELETE /favorites/{productCode}`. **B1:** GET satırı `{productCode, colorValueId, productName, imageUrl (renge göre), minPrice, compareAtPrice, campaignPrice, isAvailable}`; `?light=true` eski hafif liste |
| Koleksiyonlar | `GET/POST /api/store/collections`, `PUT/DELETE /{id}`, `POST /{id}/items`, `POST /saved/toggle`; paylaşım linki verisi kataloğa `shareCode` ile gelir. **B1:** GET satırında `items[{productCode, productName, imageUrl, minPrice, compareAtPrice, campaignPrice, isActive}]` (itemCodes korunur; `?light=true` eski şekil) |
| Yorumlar | `GET /api/store/reviews/product/{code}` + `/summary` (anonim); `GET /reviews/mine` (**B1:** + `productName`, `imageUrl`), `GET /reviews/reviewable` (**A11:** `[{productCode, productName, imageUrl, variantInfo, orderId, orderNumber, orderedAt, orderItemId, variantId, colorValueId, sizeValueId}]`), `POST /reviews`, `POST /reviews/images`, `DELETE /reviews/{id}` |
| Satıcıya sorular | `GET /api/store/questions/product/{productCode}?firmPlatformId=&limit=` (anonim — yayındaki cevaplanmış sorular, ad maskeli, limit ≤50); `GET /questions/mine` (Sorularım: cevaplananlar + bekleyenler; **B1:** + `productName`, `imageUrl`), `POST /questions` (aynı üründe cevap bekleyen soru varken yenisi engellenir) |
| Kayıtlı aramalar | `GET/POST /api/store/saved-searches`, `PUT/DELETE /{id}` |
| Stok alarmı | `GET/POST /api/store/stock-alerts`; **B3:** `DELETE /stock-alerts/{alertId}` (POST yanıtındaki `alertId`) veya `DELETE /stock-alerts?variantId=` → kayıt iptal (404 = yok) |
| Gezilen ürünler | `GET /api/store/viewed-products` (**B1:** + `productName, imageUrl, minPrice, compareAtPrice, campaignPrice, isAvailable`; `?light=true` eski), **B2:** `POST /viewed-products {firmPlatformId, productCode}` (Üye — uygulama ürün detayını açınca), `DELETE` (temizle). Misafir gezmeleri cihazda tutulur (sunucu kaydı üye-bağlıdır) |

## 8. İçerik / vitrin — anonim

| Method | Uç | Açıklama |
|--------|-----|---------|
| GET | `/api/store/pages/{placement}?firmPlatformId=` | Vitrin kompozisyonu — segment'e göre hedeflenmiş bloklar. **B8:** geçerli yerleşimler `homepage` (ana sayfa), `global-top` (duyuru), `list-top`, `list-bottom`, `detail-bottom`, `cart`, `checkout-delivery`, `checkout-payment`, **`search`** (arama açılışı — panelden blok eklenince dolar; blok yoksa boş kompozisyon) |
| GET | `/api/store/pages/blocks/{blockId}/products?page=` | Blok ürünlerinde sonsuz kaydırma devamı |
| GET | `/api/store/cms/menus/{code}?firmPlatformId=` | Navigasyon menüsü (`footer` vb.) |
| GET | `/api/store/cms/legal?firmPlatformId=&codes=&pageType=` | Sözleşme/yasal ve kurumsal sayfa içerikleri. **B7:** `codes` sayfa kodu, **site slug'ı** (`kargo-ve-teslimat`, `gizlilik-ve-guvenlik`) veya takma ad (`sss`) olabilir; `pageType` = `legal` (varsayılan) \| `corporate` \| `all`; yanıtta `slug` (site URL'iyle birebir), `pageType`, `aliases[]` |
| GET | `/api/store/cms/pages/by-slug/{slug}?firmPlatformId=` | **B7:** tek sayfa — slug/kod/takma ad (legal veya kurumsal); 404 = yok |
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

## 10. Push bildirim cihaz kaydı — `/api/store/push-devices` (2026-09-05)

Bildirim gönderimi için cihazın FCM/APNs token'ı üye bilgisiyle birlikte sunucuda tutulur
(`storefront.push_devices`). Kayıt **anonim de yapılabilir** (bildirim izni girişten önce
istenebilir); girişliyken (üye JWT'siyle) gönderilen kayıt üyeye bağlanır. Uçlar idempotenttir.

| Method | Uç | Erişim | Açıklama |
|--------|-----|--------|---------|
| POST | `/api/store/push-devices` | Anonim/Üye | Kayıt + güncelleme (aynı token'a upsert). Gövde: `{ "firmPlatformId": "...", "platform": "android\|ios", "token": "<FCM/APNs token>", "deviceId": "<kurulum kimliği — önerilir>", "appVersion": "1.4.2" }` |
| POST | `/api/store/push-devices/revoke` | Anonim | Kaydı iptal eder. Gövde: `{ "firmPlatformId": "...", "token": "..." }` — token yoksa da başarı döner |
| GET | `/api/store/push-devices/mine?firmPlatformId=` | Üye | Üyenin kayıtlı cihazları (token maskeli, son 8 karakter) |

**İstemci sözleşmesi — token şu anlarda POST edilir:**
1. Token ilk alındığında ve FCM/APNs token'ı her DEĞİŞTİĞİNDE (`deviceId` sabit gönderilir —
   sunucu aynı `deviceId`'nin eski token kayıtlarını `revoked`'a çeker).
2. **Girişten hemen sonra** (üye JWT'siyle) → kayıt üyeye bağlanır.
3. **Çıkıştan hemen sonra** (JWT'siz) → üye bağlantısı kopar; cihaz anonim kayda döner.
   Üyeye özel bildirim istenmiyorsa çıkışta `revoke` da çağrılabilir.

4. **Her uygulama açılışında** (2026-09-08 önerisi) → aynı token'a upsert, yalnız `LastSeenAt` tazelenir; ek maliyeti yok.
   Sunucu üyeye bildirim gönderirken **platform başına yalnız en son görülen cihazı** hedefler
   (`deviceId` sabit tutulamadığında aynı telefonun eski satırlarına tekrar gitmesin diye); bildirime tıklama
   (`opened`) da cihazı "görüldü" sayar. `deviceId` yine de kurulumlar arasında SABİT tutulmalı (iOS Keychain /
   Android güvenli depo UUID) — eski token'ın anında `revoked` olması ancak böyle sağlanır.

Gönderilen üye durumu kaydın SON halidir: girişli istek bağlar, girişsiz istek koparır.
Platform yalnız `android`/`ios`; token 10-512 karakter. Rate limit: `store-auth` havuzu.

## 11. Üyeliksiz sipariş takibi — `/api/store/orders/track` (C2, 2026-09-07; anonim)

Siparişte e-posta kolonu yoktur; misafir kimliği **alıcı telefonudur**. Telefon rakamlarla, son 10
hane üzerinden karşılaştırılır (`0`/`+90` farkı önemsiz). Bulunamayan ve telefonu uyuşmayan için
AYNI hata (`404 "Bu bilgilerle eşleşen sipariş bulunamadı."`); `store-sensitive` oran sınırı (30/dk-IP).

| Method | Uç | Açıklama |
|--------|-----|---------|
| GET | `/api/store/orders/track?orderNumber=&phone=&firmPlatformId=` | Sipariş durumu + kalemler (ad, adet, görsel, kod) + kargolar (`carrierName, trackingNumber, trackingUrl, status, estimatedDeliveryDate, deliveredAt, events[]`) |
| POST | `/api/store/orders/track` `{firmPlatformId, orderNumber, phone}` | Aynı işlev, telefon URL'de görünmesin diye |

Adres/fatura verisi dönmez; alıcı adı maskelidir. Kargo hareketleri entegrasyon bağlandıkça dolar.

## 12. Mobil istek listesi durumu — `docs/BACKEND_ISTEKLERI.md` (2026-09-07)

| Madde | Durum | Not |
|-------|-------|-----|
| A1 sipariş kalemi görsel/kod | ✅ | `items[].productCode/imageUrl/colorValueId/sizeValueId` |
| A2 renk kimlikleri + ad normalizasyonu | ✅ | Sepet+sipariş kalemlerinde `colorValueId/sizeValueId`. `renk` havuzu (5.690 serbest değer) `RenkAdiNormalizer` ile tek yazıma çekildi: bitişik adlar bilinen sözcüklere ayrıldı ("Siyahbeyaz"→"Siyah Beyaz"), `a.`/`k.`→Açık/Koyu, büyük harf→baş harf, sık yazım hataları (pempe→Pembe); 4.165 satır değişti, 382 farklı ad (tek kelimelik gerçek adlar/bilinmeyen bileşikler) olduğu gibi kaldı. Seed adımı idempotent — yeni ERP değerleri de her açılışta normalize edilir. Sipariş kalemindeki `variantInfo` eski metni saklar (snapshot); kimlikleri kullanın |
| A3 renk `isColor` | ✅ | detay + liste `colors[]` |
| A4 checkout sonrası sepet | ✅ | sunucu temizler (kapıda anında, kartta ödeme onayında) |
| A5 slug | ✅ | `products/{slug}`, `products/by-slug`, `channel-categories/by-slug/{slug}`, detayda `slug`/`variantSlugs`; arama kartında slug yok (kategori kartında var) |
| A6 firmPlatformId | ✅ | `X-Firm-Platform` başlığı tek kural, eski yol uyumlu |
| A7 boş sepet | ✅ | boş sepet nesnesi |
| A8 arama stoksuz renk | ✅ | kategori ucuyla aynı kural |
| A9 ödeme yöntemleri | ✅ | `GET /payment-options` |
| A10 detay fiyat/açıklama/grup | ✅ | `minPrice, compareAtPrice, campaignPrice, campaignName, descriptionI18n, productGroupId, slug` |
| A11 reviewable | ✅ | zengin nesne listesi |
| B1 N+1 listeler | ✅ | favorites/viewed/reviews/questions/collections zengin; `light=true` eski |
| B2 gezinme kaydı | ✅ üye / ⏳ misafir | `POST /viewed-products` (Üye). Misafir (device token) sunucu kaydı için cihaz kimliği kolonu gerekir — ayrı iş |
| B3 stok alarmı iptali | ✅ | `DELETE /stock-alerts/{alertId}` ve `?variantId=` |
| B4 adres kimlikleri | ✅ | web formu seçici; 56 eski adres geri dolduruldu (28 "TEST" satırı eşleşmedi) |
| B5 liste şeması | ✅ (takma ad) | iki kart da `id/productId`, `minPrice/basePrice` taşır; tam tek DTO web önbelleğini kırardı |
| B6 paytr/init zarfı | ✅ | `data.html` |
| B7 CMS slug | ✅ | site slug + `aliases[]`, `pages/by-slug`; seed slug'ları site URL'iyle eşitledi |
| B8 yerleşim adı | ✅ | doküman `homepage`; `search` yerleşimi açıldı |
| C2 misafir takip | ✅ | bölüm 11 (telefonla — e-posta siparişte yok) |
| C3 sadakat | ✅ | sıfır hesap 200 |
| C4 Play Integrity | ⏳ | GCP + Play Console yapılandırması (kod dışı, kullanıcı/mobil ekip) |

## Bilinen eksikler / notlar

1. **Kargo takip**: sitedeki `/uyeliksiz-kargo-takip` hâlâ demo HTML'dir; API tarafında
   üyeliksiz takip **bölüm 11** ile açıldı (telefon doğrulamalı). Kargo hareketleri entegrasyon
   bağlandıkça dolar. Üye sipariş kargoları `GET /account/orders/{id}` detayında gelir.
2. **API versiyonlama yok** — uçlar web sitesiyle ortaktır; kırıcı değişiklik yapılmaz,
   ekleme yapılır. Mobile özel ihtiyaç doğarsa versiyonlama o zaman değerlendirilecek.
3. Kanal kimliği: `X-Firm-Platform` başlığı (bkz. bölüm 1, A6); başlık da query/gövde de
   yoksa çağrı boş/yanlış kanal verisi alır.
4. Görsel arama ucu (`POST /gorsel-arama`) `api/` öneksizdir — tarihsel; mobil aynen kullanır.
5. **Hız sınırları (2026-07-23)**: kimlik uçları (`/auth/*`) IP başına dakikada 60, kupon
   doğrulama ve görsel arama dakikada 30 istekle sınırlıdır; aşımda `429` + `{success:false,
   error:...}` döner — istemci 429'da üstel bekleme (exponential backoff) uygulamalıdır.
   Şifreli girişte hesap başına 5 hatalı deneme sonrası 15 dk kilit vardır; kullanıcıya
   dönen `error` mesajı gösterilmelidir.

## 13. Push bildirimleri — backend uygulaması (`docs/PUSH_BILDIRIM_ENTEGRASYONU.md`, 2026-09-07)

**Gönderim:** FCM HTTP v1 (servis hesabı OAuth2; legacy API yok). Servis hesabı JSON'u panelden girilir:
Sidebar **Firmalar** › ilgili firma satırı › **Entegrasyonlar** bölümü › **Entegrasyon Ekle** › servis listesinden
**Firebase Cloud Messaging (Mobil Push)** seçilir; "Servis Hesabı JSON" alanına dosyanın tam içeriği yapıştırılır
(`serviceAccountJson` şifreli saklanır, `projectId` boşsa JSON'daki `project_id` kullanılır). Ayrı bir "Ayarlar › Entegrasyonlar"
sayfası YOKTUR; sidebar'daki "Entegrasyonlar" linki log sayfasıdır, "Ayarlar" linki kullanıcı listesine açılır. Ekleme için ek
yetki gerekmez; kayıtlı JSON'u sonradan "Göster" ile açmak `integration.credentials.reveal` yetkisi ister.
Tanımlı değilse kuyruk birikir, hata üretmez. Worker: 15 sn'de bir gönderim, 15 dk'da bir zamanlanmış tarama (`Push:Enabled`,
`Push:ScanMinutes`, `Push:PriceDropPercent`=10, `Push:LowStockThreshold`=3, `Push:CartReminderHours`=3, `Push:MarketingDailyLimit`=2).

**Mesaj:** §2.1 şablonu birebir — `notification.title/body(/image)`, `data.type/link/dedupId/sentAt` (+ senaryo alanları:
`orderNumber`, `orderId`, `productCode`, `productName`…), android `priority/ttl/collapse_key/channel_id=default`, apns
`apns-priority/collapse-id`, `badge` = üyenin açılmamış bildirim sayısı. İşlemsel HIGH/1 gün, pazarlama NORMAL/12 sa (fiyat/stok 6 sa).

**Kurallar (§6):** pazarlama sınıfı push **opt-out**'tur (2026-09-08): OS bildirim izni cihaz kaydında alınmıştır, ayrıca açma istenmez; yalnız `marketing-consents.push = false` yapan üyeye gitmez; sessiz saat 22:00-09:00 (İstanbul) → 09:00'a
ertelenir; üye başına günde 2, aynı tip haftada 2; `(dedupId, cihaz)` benzersiz; gönderim anında iptal koşulu (sepet boşaldı →
`cart_*` atlanır, favoriden çıktı → `favorite_*`, alarm iptal → `stock_alert`). FCM `UNREGISTERED` → cihaz `revoked`,
`INVALID_ARGUMENT` → `invalid`, 429/5xx → üstel bekleme (5 deneme). Link §3 kataloğuna göre doğrulanır (/odeme, /teslimat reddedilir;
`/siparislerim/{id}` GUID olmalı).

**Senaryolar:** olay tabanlı — `order_created` (sepet→sipariş), `order_confirmed`, `order_shipped` (kargo adı + takip no),
`order_delivered`, `order_cancelled` (üyenin kendi iptalinde YOK), `return_status` (onay/ret/teslim alındı), `question_answered`
(ilk cevap), `review_approved` / `review_rejected`. Zamanlanmış (15 dk) — `stock_alert` (gönderince alarm `notified`),
`favorite_price_drop` (favoriye ekleme anındaki fiyata göre ≥ %10; eski favorilerde ilk tarama fiyatı baz), `favorite_low_stock`
(≤3, haftada 1), `cart_reminder` (3 sa; 24 sa'te ikinci ve son), `coupon_assigned`, `coupon_expiring`, `wallet_credit`,
`welcome`, `winback`, `viewed_reminder`, `order_payment_pending` (kart, 60 dk), `order_review_invite` (teslim + 2 gün),
**`favorite_back_in_stock`** (2026-09-08: favorilenen RENGİN varyantlarında stok 0'dan pozitife döndü — `Favorite.WasOutOfStock`
tarama başında işaretlenir, dönüşte bildirilip sıfırlanır; aynı ürün için "gelince haber ver" bildirimi son 6 saatte gittiyse
tekrarlanmaz; gün başına 1), **`cart_price_drop`** (2026-09-08: sepetteki ürünün kart fiyatı — kanal/base min + etkin kampanya —
`CartItem.EffectivePriceAtAdd` tabanına göre ≥ %10 düştü; taban ilk taramada dolar, `Push:CartPriceDropDays`=14 gün içindeki
sepetler, üye başına en büyük düşüş, kalem sepetten çıkarılırsa gönderim anında iptal).
Yok: misafir sipariş bildirimi (checkout'ta cihaz kimliği yok — istenirse `X-Device-Id`).

**Uçlar (mobil):**
| Uç | Not |
|---|---|
| `PUT /api/store/account/marketing-consents` | gövdeye `push: boolean` eklendi (null → mevcut korunur; **varsayılan true** — ayarlar ekranındaki anahtar KAPATMA içindir) |
| `GET /api/store/account/marketing-consents` | `{email, sms, phone, push}` |
| `GET /api/store/account/notifications?page=&pageSize=` | **Bildirimlerim** listesi — bkz. §14 (yeniden tanımlandı 2026-09-08) |
| `POST /api/store/push-devices/opened {dedupId, token?}` | tıklama → `openedAt` **+ `readAt`** (şablon `dismissOnOpen` ise listeden düşer); üye JWT ile üyenin, değilse token'ın satırı |

**Panel:** Pazarlama › Bildirimler › **Push Şablonları** (başlık/gövde/link/TTL/öncelik/açık-kapalı, link doğrulamalı) ve
**Push Gönderimleri** (log, 24 saat özeti, tek cihaza/üyeye deneme gönderimi — Firebase konsolundan toplu kampanya ASLA). Deneme formundaki **Cihaz Id** = `push_devices.Id`; panelde Üyeler › üye detayı › *Mobil Bildirim Cihazları* bölümünde her cihazın Id'si, **Kopyala** ve **Deneme gönder** (formu `?tab=push-log&deviceId=` ile önceden doldurur) düğmeleri vardır; **Üyeye deneme gönder** üyenin platform başına en son görülen aktif cihazına gider (2026-09-08; `Push:LatestDevicePerPlatform=false` ile tüm aktif cihazlar).
Log: `storefront.push_notifications` (token yalnız SHA-256 hash), şablonlar `storefront.push_templates`.

## 14. Uygulama içi "Bildirimlerim" — `/api/store/account/notifications` (docs/BILDIRIMLERIM_BACKEND_ISTEGI.md, 2026-09-08 UYGULANDI)

Listenin ve durumların tek sahibi backend'dir; mobil yerelde saklamaz. Hepsi **üye JWT** ister — cihaz/web token'ı ya da kimliksiz
istek **401** `{success:false, error, code:"member_required"}`. `firmPlatformId` isteğe bağlıdır (query ya da `X-Firm-Platform`);
verilmezse üyenin tüm platformlardaki satırları döner. Zarf `{success, data, error, code}`; tarihler ISO-8601 UTC.

| Uç | Davranış |
|---|---|
| `GET …?page=1&pageSize=20` | görünür satırlar (`inbox=true`, silinmemiş, süresi dolmamış), `createdAt DESC`; `pageSize` ≤ 50; **her yanıtta `unreadCount`** (tüm liste) + `totalCount/page/pageSize/totalPages/hasNextPage`. Satır: `id, dedupId, type, class, title, body, link, imageUrl?, icon, createdAt, readAt, dismissOnOpen` |
| `GET …/unread-count` | `{unreadCount}` (rozet için hafif uç) |
| `POST …/{id}/read` | `readAt` (idempotent); şablon `dismissOnOpen` ise listeden düşer → `{unreadCount}`; yok/başkasının **404** |
| `POST …/read-all` | görünen okunmamışların tümü → `{unreadCount:0}` |
| `DELETE …/{id}` | kullanıcı silmesi (geri alma yok; zaten silinmiş → 200) → `{unreadCount}`; yok/başkasının **404** |
| `DELETE …` | görünen tüm satırlar silinir → `{unreadCount:0}` |

**Kurallar:** üyeye hedeflenen her bildirim (`memberId` dolu) FCM'e gidemese bile (cihaz yok / push izni kapalı / sıklık sınırı → `status=skipped`,
`errorCode` no_device | no_consent | daily_limit | weekly_limit) cihazsız satırla (`DeviceId` null) listede görünür. Aynı olay üyenin
birden çok cihazına gittiyse listede TEK satır görünür; okuma/silme aynı `dedupId`'li tüm satırlara uygulanır. Misafir cihaza giden
kampanya satırları (`memberId` yok) listede yoktur. `icon` §5 anahtarlarından (order, cargo, payment, return, favorite, stock, cart,
question, review, coupon, campaign, account, info); `imageUrl` ürünle ilgili türlerde (favori, stok, sepet, soru, yorum, gezilen) doludur.
Şablon bazlı panel ayarları (Pazarlama › Bildirimler › Push Şablonları › düzenle): **Listede göster** (`inbox`), **İkon**, **Listede kalma
(gün)** (`expiresDays`; varsayılan işlemsel 90 / pazarlama 30), **Açılınca listeden düşsün** (`dismissOnOpen`). Kolonlar
`storefront.push_notifications`: `Inbox, Icon, DismissOnOpen, ExpiresAt, ReadAt, DismissedAt` (belgedeki `deleted_at` = `DismissedAt`;
BaseEntity soft delete admin logunu gizleyeceği için ayrı kolon). Doğrulama: `NotificationInboxDbTests` (§7 prosedürü, gerçek DB, geri alınır).

## 15. Vitrin fiyat + kampanya rozeti sözleşmesi (B9, 2026-09-08 UYGULANDI)

Mobil ekibin `docs/BILDIRIMLERIM...` benzeri isteği (B9) uygulandı: fiyat TEK biçimde gelir, rozetler her yüzeyde aynıdır.

**Fiyat alanları** (liste, kategori listesi, ürün detayı, favoriler, gezilenler, koleksiyonlar):

| Alan | Anlam |
|---|---|
| `price` | **Satış fiyatı** — ürün-bazlı kampanya varsa kampanyalı fiyat, yoksa kanal satış fiyatı. İstemci başka hesap yapmaz. |
| `compareAtPrice` | **Çizili referans** — indirim yoksa `null`. Kanal indiriminde kanal çizili fiyatı; kampanyada kampanya öncesi satış fiyatı; ikisi birlikteyse en yükseği. Salt gösterimdir, hiçbir hesaba girmez (2026-09-08 kullanıcı kararı). |
| `minPrice` / `basePrice` | Kampanya öncesi satış fiyatı — **eski alan**, geçiş için korunuyor. |
| `campaignPrice` | Kampanyalı fiyat — **eski alan**, `price` ile aynı bilgi. Mobil okumayacak; sahadaki eski sürümler bitince kaldırılacak. |

İndirim üç kaynaktan gelir, ikisi fiyatı değiştirir: **kanal fiyat indirimi** (satış fiyatı zaten düşük, çizili fiyat kanaldan),
**ürün-bazlı kampanya** (percent/amount — satış fiyatının üstüne uygulanır), **sepet-bağımlı kampanya** (2 al 1 öde, kargo bedava —
fiyata dokunmaz, yalnız rozet). İkisi üst üste binebilir. Kural tek yerdedir: `Shared.Contracts.KartFiyatGorunumu.Hesapla`.

**Rozetler:** `campaignName` (kazanan kampanyanın rozet adı) ve `campaignBadges: [{name, color?}]` (ürünü kapsayan TÜM kampanyalar,
bantta dönüşümlü; `color` null → istemci marka rengini kullanır) artık **ürün detayında ve üye listelerinde de** var. Rozet gelmesi
indirim anlamına gelmez: sepet-bağımlı kampanyada `price` değişmez.

Örnek (canlı doğrulama, 2026-09-08):

| Ürün | price | compareAtPrice | campaignBadges |
|---|---|---|---|
| P-00021945 (%15 kampanya) | 679,99 | 799,99 | %15 İndirim, Kargo Bedava |
| P-00020386 (kanal indirimi) | 299,99 | 399,99 | Süper Fırsat, Kargo Bedava |

**Etkilenen uçlar:** `GET /api/store/catalog/products`, `GET /api/store/catalog/products/{code}`, kanal kategori listesi,
`GET /api/store/favorites`, `GET /api/store/viewed-products`, `GET /api/store/collections`. Web (Razor) görünümü değişmedi:
kart çizili satırı yalnız kanal indiriminde çıkar, kampanyanın kendi satırı korunur.

