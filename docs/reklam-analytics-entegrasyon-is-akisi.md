# Reklam, Analytics ve Sosyal Medya Entegrasyonları — Uygulama İş Akışı (v2)

> Kaynak: `docs/E-Ticaret-Reklam-Entegrasyon-Teknik-Dokumani.md` (§1–§24)
> Sürüm: **v2 — 2026-08-22** (v1'in inceleme sonrası revizyonu; "Revizyon notları" bölümüne bak)
> Hedef: ECSPros üzerinde reklam/analiz/dönüşüm takibi altyapısını mevcut mimariyle uyumlu,
> kalıcı ve **uygulamaya hazır** biçimde kurmak. Her faz bir iş emridir: dosya/sınıf adları,
> veri modeli, kabul kriterleri ve test adımları yazılıdır.

---

## 0a. Uygulama durumu (2026-08-23)

**A–F fazları CANLIDA.** 2026-08-22 restart sonrası kullanıcı kabul testi: katalog/form ✓, çerez bandı + consent
günlüğü ✓, band metni ayarı ✓, test event kuyruğu ✓ (adapter yokken `Atlandı`), feed üretimi ✓ (mishar 5.792 ürün /
37.162 kalem, 7,8 sn; `/feeds/mishar/google-shopping.xml?key=…` 200, yanlış anahtar 404). Bulgular düzeltildi:
zorunlu şema alanı doğrulaması (panel + sunucu, `IntegrationSchemaValidator`), feed worker 10 dk tarama.
**Açık:** gerçek GA4/Meta/Merchant kimlikleri girilince son doğrulama (GA4 DebugView, Meta Test Events + CAPI
dedup, Merchant feed kabulü/uyarıları); Faz G yalnız istenirse. Öneri: `appsettings.Production.json` →
`"Feeds": { "OutputPath": "/opt/ECSProsAI/feeds" }` (şu an publish/App_Data altında).

## 0. Revizyon notları (v1 → v2)

v1 incelemesinde bulunan eksikler ve bu sürümdeki çözümleri:

| # | v1 eksiği | v2 çözümü | Nerede |
|---|---|---|---|
| 1 | Consent 4. sırada; KVKK Çerez Rehberi (TR) de açık rıza ister, yalnız EU meselesi değil | **Consent Mode v2 `default` (deny) + minimum banner Faz C'nin parçası**; zengin yönetim Faz F | §4.1, Faz C-4, Faz F |
| 2 | `CommerceEvent`'te tarayıcı eşleştirme bağlamı yok (fbp/fbc/client_id/hash'li PII) → CAPI eşleşme kalitesi düşük | Sözleşmeye `ClientContext` eklendi; PII yalnız SHA256 hash'li | Faz B-1 |
| 3 | Platform event adı eşleme tablosu yok; `refund` eksik | Tek eşleme tablosu (GA4/Meta/TikTok/Pinterest/UET) + `refund`/`order_cancelled` | §4.3 |
| 4 | Client purchase (teşekkür sayfası) ile server purchase (confirm) farklı anlar; legacy/POS/pazaryeri siparişleri filtrelenmiyor | `event_id = OrderId` kuralı; client purchase `/siparis-tamamlandi`'da, server `OrderConfirmedEvent`'te; **kaynak filtresi**: `LegacyOrderId != null`, `ExternalOrderNumber != null`, POS satışları purchase ÜRETMEZ | §4.2, Faz B-2 |
| 5 | Süreç-içi kuyruk restart'ta event kaybeder | Doğrudan **DB outbox** (`integration.tracking_event_outbox`) + worker — `LegacyOrderOutbox`/`ful_cargo_notify_outbox` deseni | Faz B-4 |
| 6 | Google Ads server-side (Enhanced/offline) OAuth ister → Faz G ilkesiyle çelişki; GA4 `apiSecret` yok | Google Ads dönüşümü Faz C'de gtag + enhanced conversions (client); server-side Google = **GA4 Measurement Protocol** (`apiSecret`, OAuth yok); Google Ads offline conversions Faz G'ye taşındı | Faz A, C, D, G |
| 7 | Feed'de giyim alanları yok; 28K ürün istek anında üretilirse yavaş | `item_group_id/color/size/gender/age_group` eklendi; **zamanlanmış üretim + dosya** | Faz E |
| 8 | `consent` bir IntegrationService değil | `FirmPlatform.Settings."tracking"` altına taşındı (navigation.megaMenuHover deseni) | Faz A-4, Faz F |
| 9 | Panel karşılığı (K16) ve §22 durum kontrolü belirsiz | Pazarlama → **"Takip & Reklam"** sayfası: kanal bazlı durum, son event/hata, test event gönderimi | Faz D-5 |
| 10 | PageSpeed riski (mobil 84 / desktop 98) | Script'ler `async` + consent sonrası yükleme; provider çıktısı IMemoryCache (2 dk) | §4.5, Faz C-6 |
| 11 | Mobil uygulama akışı yok | `POST /api/store/events` — mobil aynı sözleşmeyle server-side'a yazar | Faz B-6 |
| 12 | Telemania demo kanalında tracking varsayılanı belirsiz | **Varsayılan KAPALI**; entegrasyon kaydı yoksa hiçbir script/event yok (zaten tasarım gereği) | §4.6 |

---

## 1. Özet: Ne yapacağız?

Teknik doküman üç katman tanımlıyor; mimari karar bu ayrımı ECSPros'a taşımak:

| Katman | Amaç | Panelde istenen | ECSPros karşılığı |
|---|---|---|---|
| **1. Tracking (client-side)** | Ziyaretçi/davranış ölçümü | GA4 ID, GTM container, Pixel ID, UET ID | `_Layout.cshtml` `<head>`'e kanal-bazlı script enjeksiyonu + `dataLayer` |
| **2. Server-side dönüşüm** | Satışın backend'ten doğrulanıp gönderilmesi | Access Token (gizli) | Outbox + worker + adapter'lar; şifreli `Credentials` |
| **3. Reklam yönetimi** | Panelden kampanya/bütçe yönetimi | OAuth bağlantısı | Uzun vadeli, ayrı akış (opsiyonel) |

Yatay konular: **Ürün Feed** (Merchant Center + Meta katalog), **Consent/Cookie** (KVKK + GDPR +
Google Consent Mode v2), **durum izleme** (panel).

**Öz:** Tracking + server-side dönüşüm + feed + consent kalıcı altyapı; reklam yönetimi (OAuth)
yalnız "panelden kampanya yönetelim" netleşirse ayrı başlar.

---

## 2. Mevcut durum — ECSPros'ta hazır olanlar (2026-08-22'de kod üzerinde doğrulandı)

- **Entegrasyon kataloğu** `IntegrationService` → `definition.integration_services`
  (`Code`, `ServiceType`, `IsAvailable`, `SettingsSchemaJson` = camelCase `List<PlatformSchemaField>`;
  alan tipleri `text|password|number|date|boolean`, `Section = credentials|settings`, `HelpI18n`).
  Seed: `DatabaseSeeder.SeedPlatformServiceCatalogAsync` (`src/ECSPros.Api/Extensions/DatabaseSeeder.cs:212`)
  — mevcut satırlar `smtp`, `gestelekom`, `paytr`, `google_oauth`, `facebook_oauth`, kargo firmaları;
  **backfill deseni** hazır (satır ~363: mevcut şemaya eksik alan ekler, admin düzenlemesini ezmez).
- **Firma/kanal hesabı** `FirmPlatformIntegration` → `core.core_firm_platform_integrations`
  (`FirmPlatformId` null = firma geneli, dolu = kanala özel; `Credentials` Data Protection ile
  şifreli, `Settings` jsonb, `IsActive`, `Status`).
- **Ayar çözümleyici deseni** — `DbSmtpSettingsProvider`, `SocialLoginSettingsProvider`,
  `Store/DbPaymentSettingsProvider`, `DbSmsSettingsProvider` (`src/ECSPros.Api/Services/`):
  aktif kayıt + kanal tercihi + **IMemoryCache 2 dk** + secret'lar asla loglanmaz. Yeni
  `TrackingSettingsProvider` birebir bu kalıpla yazılır.
- **Şema-güdümlü admin formu** — `admin` React `FirmDetailPage`: `SettingsSchemaJson`'dan form
  üretir; `credentials` alanları şifreli, maskeli döner. Yeni servis = otomatik form.
- **Kanal kapsamı** — Misharitalia (üretim) ve Telemania (demo) ayrı `FirmPlatform`.
  SSR'de `ViewData["MsPlatform"]` (`StorePlatformBilgisi`: Id, Code, Theme…)
  `StorePageController.cs:28`'de set edilir; `IStoreContext.GetPlatformAsync`.
- **Domain event'ler (MediatR, süreç-içi)** — `OrderConfirmedEvent(orderId, warehouseId, confirmedBy, items)`,
  `OrderCancelledEvent`, `OrderShippedEvent`, `OrderDeliveredEvent`, `ReturnReceivedEvent`
  (`src/Modules/Order/ECSPros.Order.Domain/Events/`), `PosSaleCompletedEvent`.
- **Outbox + worker deseni** — `LegacyOrderOutbox` (`integration.legacy_order_outbox`:
  `JobType/Status/AttemptCount/LastError/ProcessedAt`) + `LegacySyncWorker`;
  `ful_cargo_notify_outbox` + `CargoNotifyWorker` (`Services/Fulfillment/`). Aynı desen.
- **Entegrasyon logu** — `IntegrationLog` (`integration.integration_logs`:
  `FirmIntegrationId, ServiceType, OperationType, Status, Request/ResponsePayload, HttpStatusCode,
  ErrorMessage, DurationMs, ReferenceId/Type`) + `GetIntegrationLogsQuery`.
- **Vitrin enjeksiyon noktası** — `src/ECSPros.Api/Views/Shared/_Layout.cshtml` `<head>`
  (satır ~21–107; `</head>` satır 107), `site.js` satır ~143 (`npm run build-js` ŞART).
- **Teşekkür sayfası** — `GET/POST /siparis-tamamlandi` (`SepetController.cs:67`), içerik
  `sessionStorage.msSiparisSonucu`'ndan render edilir → client-side purchase burada atılır.
- **Sipariş kaynağı alanları** — `Order.FirmPlatformId`, `Order.MemberId?`, `Order.LegacyOrderId?`,
  `Order.ExternalOrderNumber?`, `Order.OrderNumberSource`, `CurrencyCode`.
- **Admin menü** — `admin/src/components/layout/Sidebar.tsx:110` **"Pazarlama"** grubu.
- **OAuth altyapısı** — `StoreAuthController` + `SocialLoginService` (Google/Facebook giriş);
  Faz G bu deseni reklam API'lerine uyarlar.
- **Üye pazarlama izinleri** — `UpdateMemberMarketingConsentsCommand` (CRM); Faz F üye-bazlı
  consent kaydı için genişletilir.
- **Kodda tracking izi YOK** (gtag/dataLayer/fbq aranmadı) → sıfırdan, çakışma yok.

**Eksik olanlar (bu iş akışının konusu):** tracking servis tipleri, merkezi commerce-event
sözleşmesi + outbox, client-side enjeksiyon + dataLayer, server-side adapter'lar, ürün feed'i,
consent, panel durum ekranı, mobil event ucu.

---

## 3. Hedef mimari

```
   Vitrin (Razor SSR + site.js)        Mobil uygulama (/api/store/*)
        │ dataLayer + window.ecspros.track        │ POST /api/store/events
        │ (GA4/GTM/Pixel/TikTok/UET/Clarity)      │
        ▼                                         ▼
 ───────────────────────────────────────────────────────────────────
   ECSPros çekirdeği (Sepet / Checkout / Sipariş / Üye / Katalog)
        │ domain event + command handler'lar
        ▼
   ICommerceEventPublisher  ──►  integration.tracking_event_outbox   (DB, kalıcı)
        (normalize: CommerceEvent + ClientContext + DedupId)
                                          │  TrackingDispatchWorker (5 sn dilim)
                                          │  filtre: kanal entegrasyonu aktif + consent + kaynak
                    ┌─────────────────────┼───────────────────────────┐
                    ▼                     ▼                           ▼
           MetaConversionsAdapter  TikTokEventsAdapter    Ga4MeasurementProtocolAdapter
                    └──────────── IntegrationLog (son başarı/hata, süre) ────────────┘

   Ürün Feed: FeedGeneratorWorker (zamanlı) → wwwroot/feeds/{kanal}/google-shopping.xml
   Panel: Firma→Entegrasyonlar (form) + Pazarlama→Takip & Reklam (durum/test)
```

Kurallar: **sipariş/ürün/sepet mantığı reklam platformlarından bağımsız kalır.** Yeni platform =
yeni adapter + yeni katalog satırı; çekirdek değişmez. Dış HTTP çağrısı **asla** isteği bloklamaz.

---

## 4. Çapraz kesen kurallar (tüm fazlar uyar)

### 4.1 Consent (KVKK + GDPR + Google Consent Mode v2)
- Analitik/reklam script'leri **izin olmadan veri göndermez**. Head'de script'lerden ÖNCE
  `gtag('consent','default',{analytics_storage:'denied',ad_storage:'denied',ad_user_data:'denied',
  ad_personalization:'denied',functionality_storage:'granted',security_storage:'granted'})` yazılır.
- İzin çerezi: `ms_consent` (1. taraf, 180 gün, JSON `{v:1,analytics:bool,ads:bool,personalization:bool,ts}`);
  SSR bunu okur → izin varsa script'ler doğrudan, yoksa consent `update` sonrası yüklenir.
- Server-side dispatcher **aynı consent'i** kullanır: event'e `ConsentState` bağlanır
  (tarayıcıdan gelen çerez / üye kaydı); `ads=false` ise Meta/TikTok'a gitmez, `analytics=false`
  ise GA4 MP'ye gitmez. Sipariş event'leri için consent, sipariş anındaki çerezden alınır ve
  outbox satırına yazılır (sonradan değişmez).
- Consent kapsamı dışı olanlar: sipariş e-postası/SMS (sözleşme gereği), güvenlik çerezleri.
- **EU hedefi var (karar 2026-08-22):** banner kapatılamaz; `ads_data_redaction=true` reddedilince;
  consent log (F-5) zorunlu; IP anonimleştirme GA4'te varsayılan.

### 4.2 Dedup ve "satın alma" anı
- `DedupId` (Meta `event_id`, TikTok `event_id`, GA4 `transaction_id`): **purchase için `OrderId`**
  (GUID string), diğer event'ler için `Guid.NewGuid()` tarayıcıda üretilip server'a taşınır.
- Client purchase: `/siparis-tamamlandi` render'ında `msSiparisSonucu` içinden (orderId, orderNumber,
  total, items, currency) → `purchase` atılır (**bir kez**: sessionStorage'da `msPurchaseSent:{orderId}`
  bayrağı). Server purchase: `OrderConfirmedEvent` (kapıda ödeme/havale siparişi confirm olunca;
  kart siparişi ödeme alınıp confirm olunca — `AutoConfirm=false` (politika onayı) olan
  siparişlerde onay linki tıklandığında). Aynı `event_id` sayesinde Meta/TikTok tek sayar.
- **Kaynak filtresi (purchase üretmez):** `Order.LegacyOrderId != null` (eski sistem senkronu),
  `Order.ExternalOrderNumber != null` (pazaryeri), POS satışları (`PosSaleCompletedEvent` dinlenmez),
  MigrationTool ile yaratılan siparişler (LegacyOrderId ile zaten kapsam dışı), `MockPayment`
  (Development dışında zaten yok). Satıcı paneli/partner API ürünlerinin vitrin siparişleri → purchase ÜRETİR
  (vitrin satışıdır) — **karar §7-9 onaylandı (2026-08-22)**.
- `refund`: `OrderCancelledEvent` (confirmed sonrası iptal) ve `ReturnReceivedEvent` →
  GA4 `refund` (tam/kısmi, `transaction_id = OrderId`). Meta/TikTok'ta iade event'i yok — gönderilmez.

### 4.3 Event adı eşleme tablosu (Faz B iç adı → platform adı)

| İç ad (`CommerceEvent.Name`) | GA4 (gtag/MP) | Meta (Pixel/CAPI) | TikTok | Pinterest | Microsoft UET |
|---|---|---|---|---|---|
| `product_viewed` | `view_item` | `ViewContent` | `ViewContent` | `pagevisit` | `view_item` |
| `product_list_viewed` | `view_item_list` | — | — | — | — |
| `search` | `search` | `Search` | `Search` | `search` | `search` |
| `added_to_cart` | `add_to_cart` | `AddToCart` | `AddToCart` | `addtocart` | `add_to_cart` |
| `removed_from_cart` | `remove_from_cart` | — | — | — | — |
| `cart_viewed` | `view_cart` | — | — | — | — |
| `checkout_started` | `begin_checkout` | `InitiateCheckout` | `InitiateCheckout` | — | `begin_checkout` |
| `shipping_info_added` | `add_shipping_info` | — | — | — | — |
| `payment_info_added` | `add_payment_info` | `AddPaymentInfo` | `AddPaymentInfo` | — | — |
| `order_completed` | `purchase` | `Purchase` | `CompletePayment` (+`PlaceAnOrder`) | `checkout` | `purchase` |
| `refund` | `refund` | — | — | — | — |
| `sign_up` | `sign_up` | `CompleteRegistration` | `CompleteRegistration` | `signup` | — |
| `login` | `login` | — | — | — | — |
| `wishlist_added` | `add_to_wishlist` | `AddToWishlist` | `AddToWishlist` | — | — |
| `newsletter_subscribed` | `generate_lead` | `Lead` | `SubmitForm` | `lead` | — |

Google Ads dönüşümleri: `purchase` (zorunlu), `add_to_cart`, `begin_checkout` — gtag
`conversion` olayı, label'lar `google_ads` ayarından.

### 4.4 Kalem (item) sözleşmesi — her platforma aynı kaynak
`item_id = variant SKU/barkod (satılabilir birim = varyant)`, `item_group_id = Product.Code`,
`item_name`, `item_brand`, `item_category` (yaprak kanal kategorisi adı), `item_variant`
(renk/beden), `price` (KDV dahil, kanal efektif fiyatı — `EffectivePriceProvider`), `quantity`,
`discount` (kalem ağırlıklı indirim — `OrderItem.DiscountAmount`), `currency` (`TRY`).
Meta `content_ids` = item_id listesi, `content_type = product`; feed `id` ile **aynı** değer
(dinamik reklam eşleşmesi için şart).

### 4.5 Performans ve güvenlik
- Tüm harici script'ler `async`; head'e eklenen toplam satır içi JS ≤ 3 KB; consent yoksa
  Pixel/TikTok/UET **yüklenmez** (yalnız gtag consent default + GA4 "consent mode" sinyali).
- `TrackingSettingsProvider` çıktısı kanal başına IMemoryCache 2 dk — istek başına DB sorgusu YOK.
- Secret'lar (`accessToken`, `apiSecret`, `conversionApiToken`) yalnız `Credentials`'ta; script'e
  gömülmez; `IntegrationLog.RequestPayload`'a **token maskelenerek** yazılır; hata log'larında
  yalnız HTTP kodu + mesaj.
- PII: e-posta/telefon yalnız **SHA256 hex lowercase** (trim + lowercase; telefon E.164) olarak gider.
- Bot UA'ları (`BotDisiRotalar`/XRobotsTag deseni) için tracking script'i de basılmaz (gereksiz hit).

### 4.6 Kanal izolasyonu
- Her şey `FirmPlatformId` kapsamlıdır. Entegrasyon kaydı yoksa o kanalda **hiçbir** script/event
  yok → **Telemania varsayılan KAPALI** (kayıt açılmadıkça). Misharitalia ayarı Telemania'yı etkilemez.
- Staging (5055) ve izole 5051 testleri gerçek platformlara event göndermesin:
  `Tracking:Enabled=false` config bayrağı (appsettings.Development/staging) outbox yazımını
  engeller; `Tracking:DryRun=true` outbox'a yazar ama HTTP atmaz, log'a yazar.

---

## 5. Aşamalı iş akışı

Fazlar bağımlılık sırasına göredir. A ve B temel; C/D/E/F üstüne biner. G bağımsız/opsiyonel.

---

### Faz A — Servis kataloğu + ayar modeli (definition) — **İE-1**

> **DURUM: UYGULANDI 2026-08-22 (⚠️ canlı restart bekliyor).** Seed 10 servis + ortak `ownership`
> alanı (`DatabaseSeeder.TakipSemasi/TakipServisTipleri`), backfill takip tiplerini kapsar;
> `UpdateTrackingSettingsCommand` + `PUT /api/core/firm-platforms/{id}/tracking-settings`;
> `Services/Store/TrackingSettingsProvider` (`ITrackingSettingsProvider`). İzole 5051 Development
> açılışında paylaşımlı DB'ye seed edildi (10 satır) ve backfill testi geçti (silinen `ownership`
> alanı yeniden eklendi). Admin tip etiketleri eklendi (build alındı). Kabul testi 1-3 kullanıcıda.

**Hedef:** Panelde her platform için aktif/pasif + alan formu; secret → şifreli `Credentials`,
ID → `Settings`.

**Yapılacaklar**
1. `DatabaseSeeder.SeedPlatformServiceCatalogAsync` içine yeni satırlar (mevcut tuple listesi
   `(code, ad, serviceType, List<PlatformSchemaField>)` deseni; `IsAvailable=true`; `HelpI18n`
   her alanda "nereden bulunur" açıklaması — §21 panel formu):

   | `Code` | `ServiceType` | settings alanları | credentials (password) | boolean |
   |---|---|---|---|---|
   | `ga4` | `analytics` | `measurementId` (G-XXXX) | `measurementProtocolApiSecret` | `sendServerSide` |
   | `gtm` | `tag_manager` | `containerId` (GTM-XXXX) | — | `manageGa4` `manageAds` `managePixels` (GTM içinden yönetiliyor → doğrudan script basılmaz) |
   | `google_ads` | `ads` | `conversionId` (AW-XXXX), `purchaseLabel`, `addToCartLabel`, `beginCheckoutLabel` | — | `enhancedConversions` |
   | `google_merchant` | `merchant` | `merchantId`, `feedCountry` (TR), `feedLanguage` (tr), `currency` (TRY) | — | `includeOutOfStock` |
   | `google_search_console` | `search_console` | `verificationCode` (meta tag içeriği) | — | — |
   | `meta` | `meta` | `pixelId`, `testEventCode` | `accessToken` | `conversionApiEnabled` |
   | `tiktok` | `tiktok` | `pixelId` | `accessToken` | `eventsApiEnabled` |
   | `pinterest` | `pinterest` | `tagId` | `conversionApiToken` | `conversionApiEnabled` |
   | `microsoft_ads` | `microsoft_ads` | `uetTagId` | — | — |
   | `microsoft_clarity` | `clarity` | `projectId` | — | — |

   `ServiceType` enum-yorumu `IntegrationService.cs:9`'a eklenir (`analytics, tag_manager, ads,
   merchant, search_console, meta, tiktok, pinterest, microsoft_ads, clarity`).
2. Backfill: mevcut desen (satır ~363) — yeni alanlar eksikse eklenir, admin'in düzenlediği
   label/help ezilmez.
3. Secret alanlar `Section="credentials"`, `Type="password"` → otomatik şifreli + maskeli.
   Tüm tracking servislerine ortak bilgi alanı `ownership` (settings, `customer|platform`, varsayılan
   `customer`; karar §7-10): hesabın müşteriye mi ECSPros'a mı ait olduğunu işaretler, davranışı
   değiştirmez — panel kartında rozet + raporlamada ayrım. (`PlatformSchemaField` select tipi yoksa
   `text` + HelpI18n; select eklemek ayrı küçük iş.)
4. **Consent ve tracking varsayılanları** IntegrationService DEĞİL; `FirmPlatform.Settings."tracking"`:
   ```json
   "tracking": { "consentBanner": true, "consentDefault": "deny",
                 "categories": ["analytics","ads","personalization"],
                 "purchaseAt": "confirmed" }
   ```
   Güncelleme: `UpdateTrackingSettingsCommand` (`UpdateNavigationSettingsCommand` deseni,
   `src/Modules/Core/ECSPros.Core.Application/Commands/`). Panel: Storefront ayarları altında
   "Takip & Çerez" sekmesi (Faz F'de genişler; Faz A'da yalnız komut + varsayılanlar).
5. `TrackingSettingsProvider` (`src/ECSPros.Api/Services/Store/TrackingSettingsProvider.cs`,
   `DbSmtpSettingsProvider` kalıbı): `GetAsync(Guid firmPlatformId)` → `TrackingSettings`
   (aktif kayıtların code → {settings, credentials} sözlüğü; kanala özel kayıt firma geneline
   tercih; IMemoryCache 2 dk; secret'lar yalnız server-side tüketiciye — ayrı `GetSecretsAsync`).
   `Program.cs`'te scoped kayıt.

**Kabul kriterleri**
- Admin → Firma → Entegrasyonlar'da 10 yeni servis listelenir; her biri kanal seçilerek eklenebilir.
- `meta` kaydında `accessToken` GET'te `•••` döner, güncellemede maskeli bırakılınca korunur.
- Telemania'da kayıt yokken `TrackingSettingsProvider.GetAsync(telemaniaId)` boş döner.

**Test (🧪)**
1. Misharitalia için `ga4` ekle (`measurementId=G-TEST`), aktif yap → provider 2 dk içinde döner.
2. Aynı servisi firma geneli (kanal boş) + Telemania'ya özel ekle → Telemania çözümlemesi
   kanala özel kaydı seçer.
3. (olumsuz) `meta` kaydında `pixelId` boş bırak → `Required` doğrulaması formu reddeder.

**Bağımlılık:** yok. **Migration:** yok (seed + jsonb).

---

### Faz B — Merkezi commerce event katmanı (normalize + outbox + dispatch) — **İE-2**

> **DURUM: UYGULANDI 2026-08-22 (⚠️ canlı restart bekliyor; migration `AddTrackingEventOutbox`
> CANLI DB'ye UYGULANDI — additive).** Sözleşme `Shared.Contracts/Tracking/CommerceEvent.cs`
> (`CommerceEvent/CommerceItem/ClientContext/ConsentState/CommerceEventNames/ICommerceEventPublisher`);
> outbox `integration.tracking_event_outbox` + `tracking_order_context` (Integration.Domain);
> Api `Services/Tracking/`: `OutboxCommerceEventPublisher` (hata-güvenli, Tracking:Enabled kapısı,
> dedup), `TrackingOrderContextRecorder` (checkout'ta çerez/UA/IP + ms_consent → siparişe bağlı),
> `TrackingHttpContextReader` (_fbp/_fbc/_ga/ttclid/gclid + SHA256 PII), `OrderTrackingEventBuilder`
> (order_completed/refund; kaynak filtresi Legacy/External; CRM e-posta hash), `TrackingDispatchWorker`
> (5 sn, 50'lik dilim, consent kategorisi, IntegrationLog, backoff 1/5/30/120/360 dk, 90 gün temizlik,
> DryRun), `ITrackingAdapter`; `EventHandlers/OrderTrackingEventHandlers` (Confirmed→purchase,
> Cancelled→refund tam, ReturnReceived→refund kısmi); `StoreAuthController` sign_up/login,
> `StoreNewsletterController` newsletter_subscribed, `StoreCheckoutController` bağlam + purchaseAt=created;
> `POST /api/store/events` (`StoreEventsController`, mobil referans §9). Config `Tracking:Enabled/DryRun`
> (base true/false; Development+Demo false/true). İzole 5051 DRY-RUN testi ✓: 3 event outbox'a yazıldı,
> dedup ✓, geçersiz ad/istemci order_completed 400 ✓, consent/çerez ayrıştırma ✓, worker `skipped` (adapter yok).
> Bilinen: order_completed üretimi gerçek siparişle (kapıda ödeme + onay) kullanıcı testinde doğrulanacak.

**Hedef:** İç olayları tek sözleşmeye indirge; kalıcı kuyruğa yaz; kanal/consent/kaynak filtresiyle
adapter'lara dağıt. Bu fazda adapter yok (Faz D); **dispatcher + outbox + üreticiler** var.

**Yapılacaklar**
1. **Sözleşme** — `src/Shared/ECSPros.Shared.Kernel/Tracking/CommerceEvent.cs`
   (Shared.Kernel: tüm modüller üretebilsin, Integration tüketsin):
   ```csharp
   public sealed record CommerceEvent(
       string Name,                 // §4.3 iç adlar
       DateTime OccurredAt,
       Guid FirmPlatformId,
       string DedupId,              // purchase: OrderId; diğer: tarayıcı GUID
       string Source,               // web | mobile | server
       Guid? MemberId,
       string Currency,             // TRY
       decimal? Value,              // KDV dahil toplam
       string? TransactionId,       // OrderNumber (okunur), purchase/refund
       IReadOnlyList<CommerceItem> Items,
       ClientContext Client,
       ConsentState Consent,
       IReadOnlyDictionary<string,string> Extra);   // coupon, shipping, tax, search_term, list_id …
   public sealed record CommerceItem(string ItemId, string ItemGroupId, string Name, string? Brand,
       string? Category, string? Variant, decimal Price, int Quantity, decimal Discount);
   public sealed record ClientContext(string? Ip, string? UserAgent, string? Fbp, string? Fbc,
       string? GaClientId, string? TtClickId, string? PageUrl, string? Referrer,
       string? EmailSha256, string? PhoneSha256, string? ExternalIdSha256 /* MemberId hash */);
   public sealed record ConsentState(bool Analytics, bool Ads, bool Personalization);
   ```
   `ICommerceEventPublisher.PublishAsync(CommerceEvent, ct)` (Shared.Kernel) — uygulaması
   Integration'da outbox'a yazar; **hata fırlatmaz** (checkout asla tracking yüzünden bozulmaz,
   `LegacyOrderOutbox` ilkesi).
2. **Üreticiler** (yalnız server-side anlamlı olanlar — davranış event'leri tarayıcıdan gider):
   - `order_completed`: `OrderConfirmedEventHandler`'a komşu yeni `OrderConfirmedTrackingHandler`
     (`src/ECSPros.Api/EventHandlers/`, `OrderConfirmedLegacyQueueHandler` deseni): siparişi okur,
     §4.2 kaynak filtresini uygular, `ClientContext`'i **sipariş anında saklanan** bağlamdan alır
     (aşağıda 3), `Value/Items/Extra(coupon,shipping,tax)` doldurur.
   - `refund`: `OrderCancelledEvent` (yalnız daha önce confirmed olmuş sipariş) + `ReturnReceivedEvent`.
   - `sign_up`/`login`: `RegisterMemberCommand` / `LoginMemberCommand` / `ExternalLoginMember` /
     `VerifyLoginOtp` handler'larından sonra (controller seviyesinde — `StoreAuthController`).
   - `newsletter_subscribed`: `SubscribeNewsletterCommand` sonrası.
   - Sepet/ürün görüntüleme/checkout adımları: **tarayıcı** üretir (Faz C) ve isteğe bağlı
     `POST /api/store/events` ile server'a da yazar (Meta CAPI için `AddToCart`/`InitiateCheckout`
     gönderimi istenirse — varsayılan yalnız `purchase` server-side; karar §7-2).
3. **Sipariş anında client bağlamını sakla** — `CheckoutCommand`'a dokunmadan: checkout endpoint'i
   (`Controllers/Store` checkout + `PaymentController` PayTR akışı) istekten `fbp/fbc/_ga/ttclid/UA/IP`
   + consent çerezini okur ve `integration.tracking_order_context` tablosuna yazar
   (`OrderId PK, FirmPlatformId, ContextJson, ConsentJson, CreatedAt`) — confirm geç geldiğinde
   (onay linki/havale) bağlam kaybolmaz. 90 gün sonra temizlenir (worker).
4. **Outbox** — `integration.tracking_event_outbox` (entity `TrackingEventOutbox : BaseEntity`,
   `ECSPros.Integration.Domain/Entities/`): `FirmPlatformId, EventName, DedupId, PayloadJson
   (CommerceEvent), Status (pending|done|error|skipped), AttemptCount, NextAttemptAt, LastError,
   ProcessedAt, TargetsJson (hangi adapter'lar/sonuç)`. Index `(Status, NextAttemptAt)`. Migration:
   `AddTrackingEventOutbox` (IntegrationDbContext). Şema adı tablo önekinde tekrar edilmez (kural).
5. **Worker** — `TrackingDispatchWorker` (`src/ECSPros.Api/Services/Tracking/`, `CargoNotifyWorker`
   kalıbı): 5 sn'de bir 50 satır dilim; her satır için `TrackingSettingsProvider` ile kanalın aktif
   adapter'larını bul, `ITrackingAdapter.Supports(event, settings)` + consent + `Tracking:Enabled`
   filtresi; `SendAsync` sonuçlarını `IntegrationLog`'a yaz (`ServiceType` = adapter kodu,
   `OperationType = "send_event"`, `ReferenceId = OrderId`, `ReferenceType = "Order"`);
   hata → exponential backoff (1,5,30 dk; 5 deneme) → `error`. Adapter yoksa `skipped`.
   `Tracking:Enabled=false` → worker hiç çalışmaz. Adapter arayüzü:
   ```csharp
   public interface ITrackingAdapter {
       string Code { get; }                       // meta | tiktok | ga4 | pinterest
       bool Supports(CommerceEvent e, TrackingServiceSettings s);
       Task<TrackingSendResult> SendAsync(CommerceEvent e, TrackingServiceSettings s, CancellationToken ct);
   }
   ```
6. **Mobil/tarayıcı ucu** — `POST /api/store/events` (`[Authorize]` store kimliği veya cihaz
   attestation; rate limit 60/dk/oturum): gövde `{name, dedupId, items, value, extra, client:{fbp,fbc,gaClientId,ttclid}}`;
   sunucu `FirmPlatformId`, IP, UA, MemberId, consent çerezini ekler → outbox. Geçersiz ad → 400.
   Mobil için `docs/mobil-api-referansi.md`'ye bölüm eklenir.

**Kabul kriterleri**
- Kapıda ödemeli sipariş confirm edilince outbox'a `order_completed` satırı düşer; legacy/pazaryeri
  siparişinde düşmez.
- Worker, adapter olmadan satırı `skipped` yapar ve çalışmaya devam eder; `Tracking:Enabled=false`
  iken satır yazılmaz.
- Outbox yazımı başarısız olsa (tablo yok) checkout başarıyla biter (log'a warning).

**Test (🧪)**
1. İzole 5051'de `Tracking:Enabled=true, DryRun=true` → sipariş ver → confirm → outbox `done`,
   `IntegrationLog`'da dry-run kaydı.
2. `POST /api/store/events {name:"added_to_cart",…}` → outbox satırı; `name:"xyz"` → 400.
3. (olumsuz) Worker'ı durdurup 3 sipariş ver → satırlar `pending` birikir; worker açılınca sırayla işler.
4. (olumsuz) LegacyOrderId dolu siparişi confirm et → outbox'a yazılmaz.

**Bağımlılık:** Faz A. **Migration:** `AddTrackingEventOutbox` (+ `tracking_order_context`).

---

### Faz C — Client-side tracking + consent temeli (head enjeksiyonu + dataLayer) — **İE-3**

> **DURUM: UYGULANDI 2026-08-22 (⚠️ canlı restart bekliyor).** `Services/Store/TrackingScriptProvider`
> (`ITrackingScriptProvider` → `TrackingHeadModel`, bot UA/entegrasyon yoksa null; GTM manageX çift sayım
> koruması; `ms_consent` çerezi; Meta CAPI açıksa `serverEvents` = product_viewed/added_to_cart/checkout_started);
> `Views/Shared/Store/_TakipBasligi.cshtml` (`</head>` öncesi: Consent Mode v2 default deny + `ads_data_redaction`,
> `window.ecspros` köprüsü {cfg, consent, track, setConsent, onConsent, urunler}, GTM, gtag GA4+Ads(+enhanced),
> izin-sonrası Pixel/TikTok/UET/Pinterest/Clarity yükleyicileri, Search Console meta);
> `_TakipCerezBandi.cshtml` (Kabul/Reddet/Ayarlar 3 kategori, 180 gün çerez, `window.msCerezBandiAc`);
> `site.js` `msTakip` modülü (merkezi fetch gözlemcisi: sepet ekle/çıkar, favori, bülten, giriş/kayıt, checkout
> POST → payment_info_added; /sepet|/teslimat|/odeme ilk sepet GET'i → cart_viewed/checkout_started/
> shipping_info_added; `#ms-takip-urun` → view_item + varyant kayıt defteri; `#ms-takip-liste` → view_item_list
> + search; `/siparis-tamamlandi` → purchase `event_id=orderId` TEK SEFER (`localStorage msPurchaseSent:{id}`));
> `VariantDisplayInfo.Sku` + `CartItemDto.Sku` (additive) → client/server item_id varyant SKU; ödeme sayfası
> `msSiparisSonucu.kalemler` sku/varyantId/birimFiyat/indirim + `masraf`. Headless Chromium E2E 21/21 ✓
> (consent default/update, fbq izin kapısı, view_item, add_to_cart kayıt defteri + Ads conversion label, search,
> view_item_list, purchase tek sefer, reddet). ⚠️ Bilinen: inline head script ~10.8 KB (hedef ≤3 KB'ın üstünde —
> pixel yükleyici snippet'leri; brotli ile ~3 KB; gerekirse yükleyiciler site.js'e taşınır); Lighthouse ölçümü
> restart sonrası canlıda; üye-bazlı consent kaydı Faz F; GTM dataLayer event adı = GA4 adı (`ms_event` iç ad).

**Hedef:** Kanal-bazlı, yalnız aktif entegrasyonlarda, consent'e bağlı script yükleme;
tek `dataLayer` sözleşmesi; PageSpeed korunur.

**Yapılacaklar**
1. `ITrackingScriptProvider` (`Services/Store/TrackingScriptProvider.cs`): `TrackingSettingsProvider`
   + consent çerezi → `TrackingHeadModel { ConsentDefaultJs, GtmContainerId?, Ga4Id?, AdsId?,
   MetaPixelId?, TikTokPixelId?, UetTagId?, ClarityId?, PinterestTagId?, SearchConsoleMeta?,
   Ga4MpEnabled, GtmManages{Ga4,Ads,Pixels} }`. Bot UA → null.
2. `_Layout.cshtml`: `</head>` (satır 107) öncesine `@await Html.PartialAsync("~/Views/Shared/Store/_TakipBasligi.cshtml", model)`.
   Partial sırası: (a) consent default + `dataLayer=[]` + `window.ecspros.consent` okuma,
   (b) GTM container (varsa), (c) gtag.js (GA4 + Ads; GTM `manageGa4/manageAds` açıksa atlanır),
   (d) Pixel/TikTok/UET/Clarity/Pinterest — **yalnız `ads` (Clarity için `analytics`) izni varsa**
   hemen, yoksa `window.ecspros.onConsent(...)` ile izin sonrası yüklenir, (e) Search Console
   `<meta name="google-site-verification">`. Tümü `async`.
3. **`window.ecspros.track(name, payload)`** köprüsü (`wwwroot/js/site.js`'e modül; `npm run build-js`):
   - `dataLayer.push({event:name, ecommerce:{...GA4 ecommerce şeması: currency, value, transaction_id,
     items:[{item_id,item_name,item_brand,item_category,item_variant,price,quantity,discount,item_list_id}]},
     event_id})` — **GA4 ecommerce şeması kanonik** (GTM şablonları bunu bekler).
   - GTM yoksa aynı çağrı gtag(`event`), `fbq('track', MetaAd, {...}, {eventID})`,
     `ttq.track(TikTokAd, {...}, {event_id})`, `uetq.push(...)`, `pintrk('track', …)` köprüler.
     Eşleme tablosu §4.3 JS tarafında `MS_TRACK_MAP` sabiti.
   - Her çağrıya `event_id` (crypto.randomUUID) — purchase'da `orderId`.
   - `sendServer:true` seçeneği → `POST /api/store/events` (yalnız Faz B-2'de seçilen adlar).
4. **Consent temeli (minimum)** — `_TakipConsentBanner.cshtml` (vitrin tasarımıyla uyumlu, `/opt/misharix`
   çerez bandı varsa BİREBİR): "Kabul et / Reddet / Ayarlar (3 kategori)"; `ms_consent` çerezi +
   `gtag('consent','update',…)` + `window.ecspros.onConsent` tetikler. Kapalıysa
   (`tracking.consentBanner` **kapatılamaz — karar §7-5 EU hedefi var**, default hep `deny`). Üye girişliyse
   tercih `UpdateMemberMarketingConsents` benzeri `UpdateMemberTrackingConsentCommand` ile üyeye de
   yazılır (Faz F'de panel).
5. **Üreticiler (Razor + site.js):**
   - `product_viewed`: `UrunDetayController` view'ı (`RecordProductViewCommand` zaten var; aynı
     yerde dataLayer'a `view_item`).
   - `product_list_viewed`: `UrunListesiController` liste view'ları (`item_list_id` = kategori/arama).
   - `search`: `/urunler?search=` + görsel arama modalı.
   - `added_to_cart`/`removed_from_cart`: site.js sepet API çağrılarının (`/api/store/cart`)
     başarı callback'i (kart hızlı sepet dahil).
   - `cart_viewed` `/sepet`; `checkout_started` `/teslimat` girişi; `shipping_info_added`
     teslimat adımı tamam; `payment_info_added` `/odeme` ödeme yöntemi seçimi.
   - `order_completed`: `Views/Sepet/SiparisTamamlandi.cshtml` — `msSiparisSonucu`'ndan,
     `msPurchaseSent:{orderId}` bayrağı ile tek sefer; Google Ads `conversion` (label) da burada.
   - `sign_up`/`login`: giriş modalı/üyelik sayfası başarı callback'i; `wishlist_added`
     favori API'si; `newsletter_subscribed` bülten formu.
6. **Performans:** provider çıktısı cache (A-5); head'e eklenen satır içi JS ≤ 3 KB; Lighthouse
   mobil ≥ 80 / desktop ≥ 95 korunur (ölçülür, kabul kriteri).
7. Secret'lar asla script'e gömülmez (yalnız ID/container).

**Panel karşılığı (K16):** Firma → Entegrasyonlar formu (Faz A) + Storefront ayarları
"Takip & Çerez" (banner aç/kapa, varsayılan).

**Kabul kriterleri**
- Misharitalia'da `ga4`+`meta` aktifken kaynakta gtag + fbq var; Telemania'da hiçbiri yok.
- Consent reddedilince `fbq` yüklenmez, GA4 `consent default denied` ile çalışır; kabul sonrası
  sayfa yenilenmeden pixel yüklenir.
- Teşekkür sayfası yenilenince `purchase` ikinci kez atılmaz.
- GA4 DebugView + Meta Events Manager Test Events'ta `view_item/add_to_cart/begin_checkout/purchase`
  doğru parametrelerle görünür.

**Test (🧪)**
1. Tag Assistant / GA4 DebugView ile ürün → sepet → teslimat → ödeme → teşekkür zinciri.
2. Meta Pixel Helper: `Purchase` event'inde `eventID = orderId`.
3. (olumsuz) Çerezleri sil, banner'da "Reddet" → Network'te `connect.facebook.net` isteği YOK.
4. (olumsuz) GTM `manageGa4=true` iken kaynakta ikinci gtag config YOK (çift sayım önlenir).
5. Lighthouse mobil/desktop ölçümü (öncesi/sonrası).

**Bağımlılık:** Faz A, Faz B (ad sözleşmesi + `/api/store/events`). **Migration:** yok.

---

### Faz D — Server-side dönüşüm (Meta CAPI, TikTok Events API, GA4 MP) + durum ekranı — **İE-4**

> **DURUM: UYGULANDI 2026-08-22 (⚠️ canlı restart bekliyor; admin build alındı — sayfa restart'a dek 404 API görür).**
> Adapter'lar `Services/Tracking/Adapters/`: `MetaConversionsAdapter` (graph v20.0 /{pixelId}/events, event_id=DedupId,
> user_data em/ph/external_id hash + fbp/fbc + IP/UA, `test_event_code`; test event yalnız testEventCode doluyken),
> `TikTokEventsAdapter` (v1.3 event/track, Access-Token, ttclid), `Ga4MeasurementProtocolAdapter` (mp/collect
> api_secret, yalnız purchase/refund + sendServerSide; test event → /debug/mp/collect); `TrackingAdapterBase`
> (5 sn HttpClient "tracking", §4.3 ad eşlemeleri). `TrackingAdminController` `api/tracking`: GET status
> (kanal kartları: mod client/server/gtm, son başarı/hata, 24s sayıları, outbox özeti, enabled/dryRun), GET outbox
> (sayfalı, durum filtresi), POST outbox/{id}/retry, POST test-event (order_completed, consent GRANT, Extra.test).
> Panel `admin/src/pages/marketing/TrackingPage.tsx` → `/marketing/tracking`, Sidebar Pazarlama → "Takip & Reklam"
> (kanal seçici, 15 sn yenileme, test event, yeniden dene, Firma→Entegrasyonlar linki). Worker: tüm hedefler
> consent'le atlanırsa satır `skipped`. İzole 5051 DRY-RUN ✓: meta CAPI açık + consent ads → `dry_run` hedef +
> IntegrationLog (token/PII yok); consent yok → skipped; eşlemesi olmayan event → skipped; admin uçları 401.
> Pinterest Conversions adapter'ı ikinci dalga. Gerçek platform doğrulaması (Meta Test Events / GA4 Realtime)
> kullanıcıda — gerçek token girildikten sonra.

**Hedef:** `order_completed` (ve seçilenler) backend'ten doğrulanmış gönderilsin; engelleyicilerden
etkilenmesin; panelden durum izlensin.

**Yapılacaklar**
1. Adapter'lar (`src/Modules/Integration/ECSPros.Integration.Infrastructure/Tracking/`,
   `HttpClientFactory` adlı istemciler, 5 sn timeout):
   - `MetaConversionsAdapter` → `POST https://graph.facebook.com/v20.0/{pixelId}/events`
     (`access_token` Credentials'tan; `event_id=DedupId`, `action_source=website|app`,
     `user_data{em,ph,external_id (hash), client_ip_address, client_user_agent, fbp, fbc}`,
     `custom_data{currency,value,content_ids,content_type,contents,order_id,num_items}`;
     `test_event_code` ayarı varsa eklenir).
   - `TikTokEventsAdapter` → `POST https://business-api.tiktok.com/open_api/v1.3/event/track/`
     (`Access-Token` header; `event_id`; `user{email,phone (hash),ip,user_agent,ttclid}`;
     `properties{currency,value,contents[]}`).
   - `Ga4MeasurementProtocolAdapter` → `POST https://www.google-analytics.com/mp/collect?measurement_id&api_secret`
     (`client_id` = `ClientContext.GaClientId`, yoksa `MemberId`/rastgele; `events[{name:purchase,
     params{transaction_id,value,currency,items[]}}]`; consent analytics=false → atlanır).
     Yalnız `ga4.sendServerSide=true` iken.
   - `PinterestConversionsAdapter` (ikinci dalga).
   - **Google Ads server-side YOK** (OAuth gerektirir → Faz G).
2. Dedup: client ve server aynı `DedupId` (§4.2). Meta "Deduplication" raporunda eşleşme oranı
   kabul kriteri.
3. Secret'lar yalnız adapter içinde çözülür; `IntegrationLog.RequestPayload` token'sız.
4. Başarısızlık → backoff (B-5); 5 denemede `error`; panelde görünür.
5. **Panel — Pazarlama → "Takip & Reklam"** (`admin/src/pages/marketing/TrackingPage.tsx`,
   Sidebar "Pazarlama" grubuna; permission `integration.view`):
   - Kanal seçici; her platform için kart: aktif/pasif, mod (client/server/GTM), son başarılı
     event zamanı, son hata, 24 saat başarı/hata sayısı (`GetIntegrationLogsQuery` + yeni
     `GetTrackingStatusQuery` outbox özetinden), "Test event gönder" butonu
     (`POST /api/tracking/test-event` → outbox'a `order_completed` test satırı, Meta
     `test_event_code` ile).
   - Outbox tablosu: bekleyen/hatalı satırlar, "yeniden dene" (Status→pending).
   - Satır tıklanınca detay (log payload'u, maskeli).

**Kabul kriterleri**
- Meta Events Manager'da server event `Purchase` görünür, Pixel ile **deduplicated**.
- GA4 Realtime'da MP purchase görünür (`sendServerSide=true` iken).
- Panelde Misharitalia `meta` kartı "son başarılı: <tarih>" gösterir; token bozulunca "son hata: 401".

**Test (🧪)**
1. İzole 5051'de `DryRun=false` + Meta `testEventCode` → test event butonu → Events Manager
   Test Events'te görünür.
2. Gerçek sipariş (kapıda ödeme) → confirm → 5 sn içinde outbox `done`, Meta'da Purchase.
3. (olumsuz) `accessToken`'ı bozuk kaydet → 3 deneme sonra `error`, panelde kırmızı; düzeltip
   "yeniden dene" → `done`.
4. (olumsuz) Consent `ads=false` olan siparişte Meta satırı `skipped`.

**Bağımlılık:** Faz A, B. (C'den bağımsız çalışır ama dedup için C ile birlikte canlıya alınır.)

---

### Faz E — Ürün feed (Merchant Center + Meta katalog) — **İE-5**

> **DURUM: UYGULANDI 2026-08-22 (⚠️ canlı restart bekliyor; migration `AddChannelCategoryGoogleCategory` (Storefront)
> CANLI DB'de; admin build alındı).** `Services/Tracking/Feed/`: `FeedProductReader` (RAW SQL: satışa açık ürün ∩
> kanal ürünü, varyant, kanal fiyatı/compare/slug, renk/beden, marka/cinsiyet, sellable stok, aktif görseller
> [varyant > aynı renk kardeş > ürün], yaprak kanal kategorisi + breadcrumb yolu + GoogleCategoryId miras),
> `FeedGenerator` (RSS/g: XML + Meta CSV, tmp→atomik rename; id=varyant SKU, item_group_id=ürün kodu, title
> "ad - renk - beden", price/sale_price KDV dahil, availability, gtin (8/12/13/14 hane barkod), mpn, color/size/
> gender/age_group (cinsiyet değerinden), google_product_category, product_type, g:shipping ayardan [karar §7-7],
> link kanal slug'ı (renk düzeyi, kardeş fallback) ya da /urun/{kod}?color=), `FeedGeneratorWorker` (Feeds:Enabled,
> IntervalHours=6, FirstRunDelaySeconds=120, panel tetiği Channel, feedKey otomatik üretilip Settings'e yazılır,
> status.json), `FeedController` `GET /feeds/{kanal}/google-shopping.xml|meta-catalog.csv?key=` (anahtar yanlış/yok →
> 404; X-Robots-Tag noindex; dosyadan servis — DB sorgusu yok); `api/tracking/feed-status` + `POST feed/generate`;
> panel Takip & Reklam feed kartı (URL kopyala, son üretim, sayılar, hata, Şimdi üret) + Kanal Kategorileri formunda
> "Google ürün kategorisi" alanı. Seeder `google_merchant` şemasına shippingPrice/shippingService/feedKey eklendi.
> İzole 5051 ✓: mishar 5.792 ürün / 37.162 stoklu kalem 11 sn, XML 52 MB well-formed, CSV 37.163 satır; endpoint
> 200/404'ler ✓. ⚠️ Bilinen: stoksuz varyantlar varsayılan hariç (includeOutOfStock), görselsiz ürün atlanır;
> `google_product_category` yalnız panelden girilince yazılır; Merchant Center'da feed doğrulaması (uyarılar) kullanıcıda;
> Dev/Demo'da Feeds KAPALI. Çıktı `{ContentRoot}/App_Data/feeds/{kanal}/` (Feeds:OutputPath).

**Hedef:** Google Shopping (ve Meta) için kanal-bazlı, zamanlı üretilen XML feed.

**Yapılacaklar**
1. `FeedGeneratorWorker` (`Services/Tracking/FeedGeneratorWorker.cs`): aktif `google_merchant`
   entegrasyonu olan her kanal için 6 saatte bir (`Feeds:IntervalHours`) üretir →
   `wwwroot/feeds/{platformCode}/google-shopping.xml` (+ `.tmp` yazıp atomik rename) ve
   `meta-catalog.csv`. İlk çalışma açılıştan 2 dk sonra. Panelden "Şimdi üret" tetiği.
2. Endpoint: `GET /feeds/{platformCode}/google-shopping.xml?key={feedKey}` — `feedKey` kanal
   entegrasyon `Settings`'inde üretilir (tahmin edilemez); dosya yoksa/kanal pasifse 404;
   `BotDisiRotalar`'a EKLENMEZ (Google bot'u okumalı) ama `noindex` başlığı basılır.
3. Kaynak: Katalog (`GetChannelCategoryProductsQuery`/`IStorefrontDbContext` üzerinden satışa açık
   ürün+varyantlar; `InStockProductProvider`, `EffectivePriceProvider`, `UrunGorselSrcset` ana
   görsel 1200px). **Varyant = item**: `g:id` = varyant SKU/barkod (§4.4 ile aynı),
   `g:item_group_id` = ürün kodu, `g:color`, `g:size`, `g:gender` (grup cinsiyeti),
   `g:age_group` (adult/kids), `g:availability` (in_stock/out_of_stock; `includeOutOfStock` false
   ise stoksuz varyant çıkarılır), `g:price`/`g:sale_price` (KDV dahil TRY), `g:brand`, `g:gtin`
   (barkod EAN ise), `g:mpn`, `g:condition=new`, `g:google_product_category` (kanal kategorisi →
   Google taksonomi eşlemesi: `storefront.channel_categories`'e `GoogleCategoryId` kolonu — panel
   kategori formuna alan), `g:product_type` (breadcrumb), `g:link` (canonical ürün URL +
   `utm_source=google&utm_medium=shopping`), `g:image_link`, `g:additional_image_link` (≤10),
   `g:shipping` (**karar §7-7: feed'e yazılır** — kanalın `core_cargo_rules` kaydından ülke/bedel/ücretsiz eşik;
   sepet tutarına bağlı bedel için Google'ın `g:shipping` yapısı sabit fiyat ister → feed'e **temel bedel**
   yazılır, ücretsiz kargo eşiği Merchant Center "shipping service" ayarında tanımlanır; panel kartında uyarı).
4. Meta katalog CSV aynı veri hattından (`id,title,description,availability,condition,price,link,
   image_link,brand,item_group_id,color,size,gender,google_product_category`).
5. Panel: Takip & Reklam sayfasında "Feed" kartı — son üretim zamanı, ürün/varyant sayısı, hata,
   feed URL kopyala, "Şimdi üret".

**Kabul kriterleri**
- Merchant Center feed doğrulaması hatasız (uyarılar kabul); 28K ürün üretimi < 2 dk, istek
  anında DB sorgusu yok (statik dosya).
- Telemania için feed yok (entegrasyon yoksa 404).

**Test (🧪)**
1. Misharitalia'ya `google_merchant` ekle → 2 dk sonra dosya oluşur; XML şema doğrulaması.
2. (olumsuz) `feedKey` yanlış → 404.
3. Stoksuz varyant `includeOutOfStock=false` iken dosyada yok.

**Bağımlılık:** Faz A. **Migration:** `AddChannelCategoryGoogleCategory` (StorefrontDbContext).

---

### Faz F — Consent yönetimi (tam) — **İE-6**

> **DURUM: UYGULANDI 2026-08-22 (⚠️ canlı restart bekliyor; migration `AddTrackingConsentLog` CANLI DB'de; admin build
> alındı).** Consent ispat günlüğü `integration.tracking_consent_log` (ConsentId = ms_consent v2 `id`, MemberId?,
> 3 kategori, Source banner|settings|member_sync|mobile, IP hash, UA; 12 ay — worker temizler);
> `POST /api/store/consent` (anonim, rate limit; üye token'ı varsa MemberId) + `GET /api/store/consent/me` (üye);
> **üye senkronu**: çerez yokken SSR üyenin son kaydını uygular (`TrackingScriptProvider` → cfg.consentSource=member,
> tarayıcı çerezi sessizce yazar); head köprüsü `setConsent(c,{source,silent})` her tercihte günlüğe POST eder;
> band metinleri kanal ayarından (`tracking.bannerTitle/bannerText/policyUrl/policyLabel` — `UpdateTrackingSettingsCommand`
> genişledi); footer "Çerez Tercihleri" (`[data-ms-cerez-tercih]` → `window.msCerezBandiAc`); panel Vitrin →
> **"Takip & Çerez"** (`/storefront/tracking-consent`: metinler, purchaseAt, son 30 gün izin dağılımı
> `GET /api/tracking/consent-stats`, KVKK/GDPR ek madde şablonu — banner kapatılamaz, default deny sabit).
> Headless E2E ✓ (varsayılan metin, Ayarlar→kaydet POST source=settings + consentId, çerez v2 id, yenilemede
> tekrar POST yok); DB'de günlük satırı IP hash'li; consent-stats ve consent/me 401. Üye senkronu canlıda
> (giriş) doğrulanacak. Mobil: uygulama izin ekranı `POST /api/store/consent {source:"mobile"}` + events'te `consent`.

**Hedef:** Kategori-bazlı izin yönetimi, üye kaydı, yasal metinler ve panel.

**Yapılacaklar**
1. Banner "Ayarlar" paneli: Analytics / Advertising / Personalization ayrı anahtar; "Functional"
   hep açık; tercih `ms_consent` + üye ise `crm.members` yeni jsonb `TrackingConsent`
   (migration `AddMemberTrackingConsent`) — giriş yapınca üye tercihi çerezi günceller.
2. Consent Mode v2 tam sinyal seti (C-4'te temel var): `update` çağrısı kategoriye göre;
   `ads_data_redaction` reddedilince true.
3. Dispatcher (B) + script provider (C) `ConsentState`'i kategori-bazlı uygular (zaten arayüz hazır).
4. Panel: Storefront → "Takip & Çerez": banner aç/kapa, varsayılan (deny/grant), kategori
   metinleri (I18n), çerez politikası sayfası seçimi (CMS legal sayfa deseni), KVKK aydınlatma
   metnine "reklam/analitik platformlarına aktarılan veri" maddesi (hazır metin şablonu).
5. Consent günlüğü: `storefront.consent_logs` (anonim: çerez id hash, kanal, tercih, zaman, UA)
   — GDPR "ispat" için 12 ay.

**Kabul kriterleri**
- Üye A telefonda reddetti, masaüstünde giriş yaptı → masaüstü de reddetmiş sayılır.
- Panelden banner kapatılınca vitrinde banner yok, consent `granted` (yalnız TR-dışı hedef yoksa).

**Test (🧪)**: kategori bazlı seçimlerle Network doğrulaması (yalnız izinli script'ler);
(olumsuz) consent log'una yazım başarısız olsa sayfa çalışır.

**Bağımlılık:** A, C, (D). **Not (karar §7-5, EU hedefi var):** F, C/D ile **aynı dalgada** canlıya çıkar —
EU trafiğine reklam çerezi consent log + kategori yönetimi olmadan açılmaz.

---

### Faz G — OAuth ile reklam yönetimi (opsiyonel / uzun vadeli) — **İE-7**

**Hedef:** Panelden kampanya/bütçe/ROAS **yönetimi** (tracking değil) + Google Ads offline/enhanced
conversions (API).

**Yapılacaklar**
1. §18/§19: kullanıcı adı/şifre istenmez; OAuth (Google Ads API, Meta Marketing API, TikTok
   Marketing API). `SocialLoginService` deseni → `AdsOAuthService`; refresh token şifreli
   `Credentials`; scope'lar platform başına.
2. Google Ads Conversions upload (offline conversions, `gclid` — `ClientContext`'e `Gclid` eklenir
   ve `tracking_order_context`'te saklanır; bu yüzden B-3 bağlamı bugünden `gclid` de toplar).
3. Panel: bağlı hesaplar, kampanya listesi/performans (salt-okunur önce), sonra yazma işlemleri.

**Bağımlılık:** iş kararı + platform uygulama onayları (Meta App Review, Google Ads API Basic Access).

---

## 6. Öncelik sırası ve iş emirleri

| Sıra | İş emri | Faz | Tahmini kapsam | Çıktı |
|---|---|---|---|---|
| 1 | **İE-1** | A | seed + provider + komut | 10 servis formu, TrackingSettingsProvider |
| 2 | **İE-2** | B | sözleşme + outbox + worker + üreticiler + `/api/store/events` | purchase/refund/sign_up outbox'ta |
| 3 | **İE-3** | C | head partial + site.js köprü + consent temel + 12 üretici | GA4/GTM/Ads/Pixel canlı, KVKK banner |
| 4 | **İE-4** | D | Meta CAPI + TikTok + GA4 MP + panel durum sayfası | server-side purchase, dedup, panel |
| 5 | **İE-5** | E | feed worker + endpoint + Google kategori eşlemesi + panel kartı | Merchant Center feed URL |
| 6 | **İE-6** | F | kategori yönetimi + üye kaydı + panel + consent log | tam consent — **İE-3/İE-4 ile aynı dalga (EU)** |
| 7 | **İE-7** | G | OAuth reklam yönetimi | yalnız istenirse |

**İlk canlı dalga = İE-1 + İE-2 + İE-3 + İE-6** (GA4 + GTM + Google Ads + Meta Pixel + **tam consent**, EU
hedefi kararı), ardından İE-4 (Meta CAPI) ve İE-5 (feed). TikTok/Pinterest/UET/Clarity adapter'ları katalogda
hazır olur, kanal kaydı açılınca çalışır (ikinci dalga).

**Çalışma alanı (K19):** İE-1/İE-2 ortak çekirdek (Core/Integration/Order), İE-3/İE-5/İE-6 **Web
sitesi**, panel parçaları **Admin panel**, `/api/store/events` **Mobil API** referansına yazılır.
Her iş emri tek alanda açılır; çekirdek dokunuşları o iş emrinin "zorunlu ortak çekirdek" notudur.

**Deploy sırası:** migration (B, E, F) → publish → `sudo systemctl restart ecspros` (kullanıcı) →
`journalctl -u ecspros -n 50 | grep -i "tracking"` (worker açılış satırı: `Tracking dispatch: AKTİF/KAPALI`).

---

## 7. Kararlar — varsayılanlar ve onay gerekenler

Tüm kararlar 2026-08-22'de kullanıcı ile netleşti (5/7/9/10 KARAR satırları); diğerleri varsayılan.

| # | Konu | Varsayılan | Not |
|---|---|---|---|
| 1 | İlk dalga platformları | GA4 + GTM + Google Ads + Meta (Pixel+CAPI) + Merchant | TikTok/Pinterest/UET/Clarity katalogda hazır, ikinci dalga |
| 2 | Server-side gönderilen event'ler | yalnız `order_completed` (+`refund` GA4) | AddToCart/InitiateCheckout CAPI istenirse `/api/store/events` ile açılır |
| 3 | "Satın alma" anı | client: teşekkür sayfası; server: `confirmed` | `tracking.purchaseAt` ayarıyla `created` seçilebilir |
| 4 | GTM vs doğrudan | GTM kaydı varsa ve `manageX=true` ise GTM, yoksa doğrudan | çift sayım koruması C-2 |
| 5 | Consent banner | **KARAR (2026-08-22): EU hedefi VAR** → banner AÇIK, varsayılan `deny`, kapatılamaz; Faz F (tam consent + consent log) C/D ile birlikte canlıya çıkar | GDPR + KVKK; `tracking.consentBanner` panelden kapatılamaz (yalnız metin/kategori düzenlenir) |
| 6 | Telemania | tracking KAPALI (kayıt yok) | demo için istenirse test ID'leriyle açılır |
| 7 | Feed `g:shipping` | **KARAR (2026-08-22): feed'e YAZILIR** — kanalın kargo bölge/bedel kuralından (`core_cargo_rules`: ülke, ücretsiz kargo eşiği, bedel) `g:shipping{country,service,price}` + `g:shipping_weight` yoksa atlanır | kural çok değişkense Merchant Center ayarı feed'i ezer |
| 8 | Meta katalog feed | üretilir (aynı hat, CSV) | maliyet yok |
| 9 | Satıcı paneli/partner API ürünlerinin vitrin siparişleri | **KARAR (2026-08-22): purchase ÜRETİR** | müşteri vitrinden geldi; ürün sahibinin satıcı olması tracking'i değiştirmez. Üretmeyenler: legacy senkron, pazaryeri (`ExternalOrderNumber`), POS |
| 10 | Hesap sahipliği | **KARAR (2026-08-22): iki model de sunulur** — (a) *müşterinin kendi hesabı*: firma kendi GA4/Meta/Ads/Merchant hesabını açar, panelde ID/token girer; (b) *platform (ECSPros) hesabı*: firma geneli (`FirmPlatformId=null`, ECSPros sahibi firma) kayıt altında alt mülk/alt pixel/alt Merchant hesabı ECSPros tarafından açılır ve kanal kaydına yazılır | Teknik fark yok: her iki modelde de kanal kaydı ID/token taşır. (b) için ECSPros'un Meta Business Manager / GA4 hesap / Merchant Center multi-client hesabı (MCA) açması gerekir — operasyon öncesi işi. Entegrasyon kaydına bilgi alanı `ownership = customer|platform` (settings, select) eklenir; panel kartında rozet |
| 11 | Reklam yönetimi (OAuth) | ŞİMDİ DEĞİL | Faz G |
| 12 | KVKK aydınlatma metni | "reklam/analitik platformlarına aktarılan veri" maddesi eklenir (hazır şablon) | hukuk onayı kullanıcıda |

---

## 8. Riskler ve notlar

- **Secret güvenliği:** token'lar yalnız şifreli `Credentials`; log'a asla; admin'de maskeli; key ring
  `~/.ecspros/dp-keys` yedeğe dahil (mevcut kural).
- **Çift sayım:** Pixel + CAPI ortak `event_id`; GTM yönetiyorsa doğrudan script basılmaz.
- **Event kaybı:** outbox DB'de — restart/deploy güvenli; worker kapalıyken birikir, açılınca işler.
- **Eşleşme kalitesi:** `ClientContext` boşsa Meta match quality düşer — C-3 köprüsü `fbp/fbc/_ga`
  çerezlerini `/api/store/events` ve checkout isteğine taşır (B-3).
- **PageSpeed:** head büyümesi ölçülür (C-6 kabul kriteri); gerekirse Pixel'ler `requestIdleCallback`.
- **Kanal izolasyonu:** `FirmPlatformId` kapsamı; staging/5051'de `Tracking:Enabled=false` ŞART
  (gerçek hesaba test event gitmesin).
- **Katalog backfill:** yeni alanlar eklenirken admin düzenlemeleri ezilmez (mevcut desen).
- **Yasal:** consent log (F-5) ve aydınlatma metni olmadan EU trafiği için reklam çerezi açılmamalı.
- **Dış bağımlılık:** Meta CAPI/TikTok API sürümleri yılda 2-3 kez değişir — adapter'da sürüm
  sabiti tek yerde; `IntegrationLog` hata artışı panelde görünür.

---

## 9. Sonuç

Teknik dokümandaki "merkezi event + adapter" yaklaşımı, ECSPros'un **IntegrationService /
FirmPlatformIntegration**, **kanal kapsamı**, **şifreli Credentials**, **şema-güdümlü admin**,
**outbox + worker** ve **domain event** altyapısına oturur. v2 ile eklenenler: consent en başta,
tarayıcı eşleştirme bağlamı, tek event adı eşleme tablosu, `event_id = OrderId` dedup kuralı,
kaynak filtreleri, kalıcı outbox, GA4 MP (OAuth'suz server-side Google), giyim feed alanları,
panel durum sayfası, mobil event ucu, Telemania varsayılan kapalı.

**Bir sonraki adım:** kararlar kapandı → **İE-1 (Faz A)** açılır; her iş emri bu
dokümandaki kabul kriterleri + 🧪 test adımlarıyla kapanır (K18 raporu).
