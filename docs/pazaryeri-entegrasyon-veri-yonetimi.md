# Pazaryeri Entegrasyon Veri Yönetimi — Tasarım

> Durum: **ONAYLANDI — F1 CANLIDA, F2 UYGULANDI (2026-07-26, API restart bekliyor; admin build canlıda)**
> **F2 uygulama kararı:** "bizim kategori" = **ProductGroup** (`definition.product_groups`, düz
> liste — katalogda hiyerarşik kategori yok; her ürünün tam bir grubu var, özellik şeması da
> grupta). Site kategorileri (`storefront.channel_categories`) dinamik/filtre bazlı olduğundan
> yükleme taksonomisi olarak KULLANILMAZ. Ekran kurgusu kullanıcı onaylı (2026-07-26): ayrı
> sayfa `/marketplaces/eslestirme`, sol liste + sağ editör, değer önerileri onaylı toplu uygulanır (%90+).
> Tarih: 2026-07-26 · Alan: Admin panel / Pazaryerleri modülü (mevcut `/marketplaces` üzerine)
> İlgili mevcut altyapı: `integration.marketplace_products`, `MarketplacesController`,
> 6 pazaryeri IntegrationService kataloğu (trendyol/hepsiburada/n11/amazon/ciceksepeti/pazarama).

## Amaç

Pazaryerlerinin milyonlarca satırlık ve sık değişen referans verisini (kategori, özellik, değer)
yönetmek; kendi katalogla eşlemeleri sağlıklı tutmak; personelin ürünleri hızlı ve öngörülebilir
biçimde yükleyebilmesini sağlamak; asenkron yükleme sonuçlarını güvenilir izlemek.

Kapsam dışı: sipariş çekme/işleme (ayrı iş), kargo entegrasyonu, gerçek adapter'ların
pazaryeri-özel HTTP detayları (adapter fazlarında ele alınır).

---

## Temel Kararlar (kullanıcı ile mutabık)

| # | Karar |
|---|-------|
| K1 | Referans verisi **ayrı veritabanında** (`marketplace_ref`, aynı PostgreSQL instance). Yeniden indirilebilir cache'tir: yedeğe dahil edilmez, bozulursa drop + yeniden indirme meşrudur. |
| K2 | Eşlemeler ve yüklü ürün durumu **ana DB'de** (`integration` şeması) kalır — yeniden üretilemez iş verisidir, ana yedeğe girer, katalogla join ister. |
| K3 | Eşleme kayıtları hedefin **snapshot'ını** (ad, yol) taşır — referans DB yeniden kurulsa bile eşleme ekranı kör kalmaz, "hedef değişti" tespiti yapılabilir. |
| K4 | Kategori istisnası **ürün düzeyindedir** ve genel eşlemeye dokunmaz (Trendyol katalog çakışması: Sneaker/Yürüyüş Ayakkabısı vakası). |
| K5 | Barkodla katalog kategori tespiti **yalnız kategori nedeniyle reddedilen üründe** tanı aracı olarak kullanılır; ön kontrol olarak KULLANILMAZ (satıcı-üretimi barkodlar anlamsız sonuç verir, kota tüketir). |
| K6 | Bizde olmayan/olması anlamsız pazaryeri özellikleri kendi kataloğa **eklenmez**; değerleri ürün×mağaza düzeyinde pazaryeri tarafında saklanır ve personel tamamlama ekranından doldurulur. |
| K7 | Yükleme sonuçları batch takip altyapısıyla izlenir: kısmi cevap normaldir, item bazlı çözülür, backoff'lu polling + zaman aşımı + listing doğrulaması vardır. |
| K8 | Referans senkronunda **hard-delete yok** — kaybolan kayıt `RemovedAt` ile işaretlenir; değişiklikler change log'a düşer ve eşleme sağlığını besler. |

---

## Mimari Genel Bakış

```
┌─ marketplace_ref DB (ayrı, yedek dışı, yeniden indirilebilir) ─────────┐
│  mp_categories · mp_category_attributes · mp_attribute_values          │
│  mp_change_log · mp_sync_runs                                          │
└────────────────────────────────────────────────────────────────────────┘
                 ▲ full snapshot + hash diff (adapter başına indirici)
                 │
┌─ ana DB, integration şeması (iş verisi, ana yedekte) ──────────────────┐
│  EŞLEME:    mp_category_mappings (+rules) · mp_attribute_mappings      │
│             mp_value_mappings · mp_product_category_overrides          │
│  TAMAMLAMA: mp_product_attribute_values (pazaryerine özel değerler)    │
│  DURUM:     marketplace_products (mevcut, genişler) ·                  │
│             mp_product_readiness · mp_batches · mp_batch_items ·       │
│             marketplace_issues                                         │
└────────────────────────────────────────────────────────────────────────┘
```

Referans DB'ye erişim ikinci bir `NpgsqlDataSource` ile (`ConnectionStrings:MarketplaceRef`).
İki DB arasında FK/join yoktur; köprü her zaman `(marketplace, externalId)` çiftidir.

---

## 1. Referans Veri Katmanı (`marketplace_ref` DB)

Tüm pazaryerleri **tek DB'de, tek jenerik şemada**, `Marketplace` kolonu ile ayrışır
(pazaryeri başına ayrı DB açılmaz).

### Tablolar

**`mp_categories`**
- `Marketplace` (trendyol/hepsiburada/…), `ExternalId`, `ParentExternalId`
- `Name`, `Path` (kök→yaprak birleşik ad, arama/öneri için), `IsLeaf`
- `IsActive`, `FirstSeenAt`, `RemovedAt` (soft — K8; `LastSeenAt` bilinçli yok — her senkronda
  tüm satırlara dokunmayı gerektirirdi, kaldırılma tespiti zaten snapshot diff'iyle yapılır)
- `Raw` (jsonb, pazaryerinin ham cevabı), `ContentHash`

**`mp_category_attributes`** — özellikler kategoriye bağlıdır (Trendyol/HB modeli):
- `Marketplace`, `CategoryExternalId`, `AttributeExternalId`, `Code`, `Name`
- `IsRequired`, `AllowCustom` (serbest giriş), `IsMultiValue`, `IsVariantAxis`
- `ValueMode`: **`id` | `code` | `literal`** — gönderimde değerin ID'si mi, kodu mu,
  metni mi beklenir. "Bazısında ID hiç yok" vakası `literal` ile modellenir; üç kimlik
  kolonu da değer tablosunda nullable durur, adapter payload'ı ValueMode'a göre kurar.
- `IsActive/RemovedAt`, `Raw`, `ContentHash`

**`mp_attribute_values`**
- Özellik referansı, `ExternalId?`, `Code?`, `Value` (metin)
- `IsActive/RemovedAt`, `ContentHash`

**`mp_sync_runs`** — her indirme koşusu: marketplace, kapsam (categories/attributes/values),
başlangıç/bitiş, eklenen/değişen/kaldırılan sayıları, durum, hata. Panel "Senkron Geçmişi"
sekmesi buradan da beslenir.

**`mp_change_log`** — diff çıktısı, olay bazlı:
- `Marketplace`, `EntityType` (category/attribute/value), `ExternalId`, `SyncRunId`
- `ChangeType`: `added` | `removed` | `changed`
- `ChangeDetail` (jsonb): ör. `requiredChanged: false→true`, `allowCustomChanged: true→false`
  (serbest giriş listeye bağlandı), `nameChanged`, `valueModeChanged`…
- `ProcessedAt` — eşleme sağlık job'ı işleyince damgalanır.

### Senkron stratejisi

1. **Full snapshot** indirilir (pazaryerleri delta API vermez), sayfalı/limitli, resumable.
2. Satır başına `ContentHash` hesaplanır; **hash değişmeyen satıra UPDATE atılmaz**
   (milyonlarca satırda write amplification'ı önleyen ana kural). Yazım COPY + staging
   tablo + tek MERGE ile.
3. Snapshot'ta gelmeyen kayıt `RemovedAt` alır (K8), diff `mp_change_log`'a yazılır.
4. Toplu yazım sonrası `ANALYZE` (mevcut geliştirme kuralı).
5. Kadans pazaryeri başına ayarlanabilir (varsayılan: kategori ağacı günlük; özellik/değer
   ağacı yalnız **eşlenmiş** kategoriler için günlük, tümü için haftalık — milyonlarca
   değerin çoğu bizim hiç kullanmadığımız kategorilerdedir, kota oraya yakılmaz).

---

## 2. Eşleme Katmanı (ana DB, `integration` şeması)

### 2.1 Kategori eşlemesi — üç kip

Pazaryerlerinin kategori mantığı üç farklı vaka çıkarır; tek tablo `mp_category_mappings`,
`MappingKind` ile:

**a) `direct` — birebir:** bizim kategori → tek pazaryeri kategorisi. En yaygın hal.

**b) `rules` — koşullu:** "bizde Pantolon tek kategori, onlarda Kadın Pantolon / Erkek
Pantolon" vakası. Eşleme kaydı sıralı **kural listesi** taşır; her kural =
özellik-değer koşulları (ör. `cinsiyet = kadın`) + hedef kategori. İlk eşleşen kural
kazanır; hiçbiri tutmazsa opsiyonel varsayılan hedef, o da yoksa ürün "eksik" listesine
düşer. Koşullar kendi katalog attribute değerlerimiz üzerinden değerlendirilir.

**c) `pool` — aday havuzu:** "bizde Bot, onlarda Bağcıklı Bot / Outdoor Bot / Günlük Bot"
vakası — kategori düzeyinde otomatik eşleme fiilen imkânsız. Eşleme kaydı bir **aday
hedef listesi** tutar; kategori seçimi **ürün düzeyinde** yapılır (2.2'deki override
tablosuna `Source=pool_assignment` olarak yazılır). Sistem isim/özellik benzerliğiyle
aday sıralaması önerir, personel tek tıkla atar; atanmamış ürünler "eksik" listesinde
"kategori ataması bekliyor" nedeniyle görünür. Toplu atama desteklenir (seçili N ürün →
aynı hedef).

Ortak alanlar: hedef snapshot (ad + path — K3), `Status: active | broken | needs_review`
(hedef kaldırıldı / değişti), `FirmPlatformId?` (mağazaya özel eşleme > firma geneli;
platform entegrasyonlarındaki çözümleme kalıbının aynısı).

### 2.2 Ürün kategori istisnası — `mp_product_category_overrides`

Genel eşlemeye **dokunmadan** tek ürünü farklı kategoriye yönlendirir (K4):

- `ProductId`, `Marketplace`, `FirmPlatformId?`, hedef `CategoryExternalId` + snapshot
- `Source`: `manual` (personel kararı) | `rejection` (red hatasından; Trendyol katalog
  çakışması) | `pool_assignment` (2.1c havuz ataması) | `remote` (listing senkronunda
  pazaryerinden okunan fiili kategori)
- `Note` (personele gerekçe)

**Gönderimde kategori önceliği:** ürün istisnası > kural sonucu > direct eşleme.
Havuz ataması ile istisna aynı mekanizma olduğundan tek öncelik zinciri yeter.

### 2.3 Özellik ve değer eşlemesi

**`mp_attribute_mappings`** — (marketplace, mpCategory) bazında; karşı tarafta özellik
kategoriye bağlı olduğundan eşleme de öyledir:
- Kendi `AttributeTypeId` → mp özellik (externalId + snapshot)
- `Strategy`: `map_values` (değer eşlemesi tablosundan) | `pass_literal` (kendi değer
  metnini aynen geçir — AllowCustom açık özelliklerde) | `fixed_value` (hep sabit değer)
- `Status` (2.1'deki gibi sağlık durumu)

**`mp_value_mappings`** — kendi attribute değerimiz → mp değer (`ExternalId`/`Code`/
`Value` — hangisinin gönderileceğini özelliğin `ValueMode`'u belirler) + snapshot + status.

**Öneri katmanı:** eşleme ekranı yeni kayıt açarken normalize edilmiş ad benzerliğiyle
(trigram) aday listeler; personel onaylar. Milyonlarca değerde elle arama sürdürülemez.

### 2.4 Pazaryerine özel ürün değerleri — `mp_product_attribute_values` (K6)

Bizde karşılığı olmayan/olması anlamsız zorunlu özellikler için (eski projedeki
"yükleme sırasında tamamlama ekranı"nın veri sahibi):

- `ProductId` (veya varyant düzeyi gerekiyorsa `VariantId`), `Marketplace`,
  `FirmPlatformId?`, mp özellik referansı
- Değer: `ValueExternalId?` / `ValueCode?` / `ValueText?` (özelliğin ValueMode +
  AllowCustom durumuna göre biri dolar)

Kendi kataloğa **hiçbir şey yazılmaz**. Gönderimde özellik değeri kaynak önceliği:
**ürün-özel pazaryeri değeri > değer eşlemesi > sabit değer > serbest geçirme.**

### 2.5 Eşleme sağlık job'ı

`mp_change_log`'daki işlenmemiş olayları tarar:
- hedef `removed` → ilgili eşlemeler `broken` + issue
- `requiredChanged` (zorunlu oldu) ve eşleme/tamamlama verisi yok → etkilenen ürünlerin
  readiness'i düşer + issue
- `allowCustomChanged: true→false` (serbest→liste) → o özelliği `pass_literal` gönderen
  eşlemeler `needs_review` + o güne dek gönderilen literal değerler için değer eşlemesi
  görevi
- `nameChanged` → snapshot güncellenir, eşleme `needs_review` (personel bir bakışta
  "aynı şey mi" doğrular)

Olaylar işlenince `ProcessedAt` damgalanır; job idempotent ve tekrar çalıştırılabilir.

---

## 3. Yükleme Hazırlık Denetimi (readiness) — "sorunsuz / eksik" ayrımı

Personelin iki net listesi olur (kullanıcı gereksinimi): **"Yüklenebilir"** ve
**"Eksik/eşleşmeyen bilgi"**. Bunu anlık hesaplamak pahalı olduğundan materialize edilir:

**`mp_product_readiness`** — ürün × mağaza düzeyinde:
- `Status`: `ready` | `missing_info` | `blocked` (kanalda satışa kapalı vb.)
- `Reasons` (jsonb liste, kodlu): `no_category_mapping`, `pool_assignment_pending`,
  `rule_no_match`, `required_attr_missing:<attr>`, `value_unmapped:<attr>`,
  `broken_mapping`, …
- `ComputedAt`

Yeniden hesap tetikleyicileri (kuyruklu job): eşleme değişti → o kategorinin ürünleri;
ürün/attribute değeri değişti → o ürün; referans senkronu change log işlendi → etkilenen
kategorilerin ürünleri. Panel `/marketplaces/:id` Ürünler sekmesindeki çipler bu tablodan
beslenir ("Yüklenecek" çipi ikiye ayrılır: **Hazır** / **Eksik**).

**Tamamlama ekranı:** Eksik listesinden ürüne girilince nedenler tek formda çözülür:
- kategori ataması bekliyorsa aday önerileriyle atama (2.1c)
- eksik zorunlu özellikler, pazaryerinin şemasından üretilen formla doldurulur
  (liste özelliği dropdown — değerler referans DB'den; serbest özellik text) →
  `mp_product_attribute_values`'a yazılır (K6)
- toplu doldurma: seçili N ürüne aynı değer.

Form üretimi mevcut `SettingsSchemaJson`/PlatformSchemaField kalıbının bilinen deneyimidir.

---

## 4. Gönderim ve Asenkron Batch Takibi (K7)

### 4.1 Tablolar

**`mp_batches`**
- `Marketplace`, `FirmPlatformId`, `FirmIntegrationId`, `ExternalBatchId` (batchRequestId)
- `BatchType`: `product_upsert` | `price_stock` | `deactivate`
- `Status`: `submitted` → `polling` → `completed` | `completed_with_errors` | `timed_out`
- `ItemCount`, `ResolvedCount`, `PollAttempts`, `NextPollAt`, `LastPolledAt`, `SubmittedAt`
- `RawSummary` (jsonb, son sorgu cevabı)

**`mp_batch_items`**
- `BatchId`, `MarketplaceProductId` (→ varyant), `PayloadHash`
- `Status`: `pending` | `success` | `failed` | `unknown`
- `ErrorRaw`, `ErrorCode` (normalize — 4.3), `ResolvedAt`

### 4.2 Polling worker

- `NextPollAt ≤ now` olan batch'leri sorgular; **kısmi cevap normaldir**: yalnız cevap
  dönen item'lar çözülür (`success`/`failed` + `ResolvedAt`), kalanlar `pending` kalır ve
  sonraki turda aynı `ExternalBatchId` ile sorgulanmaya devam edilir.
- `ResolvedCount == ItemCount` → batch kapanır (`completed` / hata varsa
  `completed_with_errors`).
- Backoff: 1 → 2 → 5 → 10 → 30 dk (pazaryeri başına ayarlanabilir üst sınır).
- Zaman aşımı (varsayılan 24 saat, ayarlanabilir): batch `timed_out`, çözülmemiş
  item'lar `unknown` + issue. **`unknown` item asla körlemesine yeniden gönderilmez**
  (duplicate riski) — önce listing doğrulaması: bir sonraki yüklü-ürün senkronunda ürün
  pazaryerinde görünüyorsa `success`'e çevrilir, görünmüyorsa yeniden gönderim önerilir.
- Item `success` → `marketplace_products` güncellenir (`SyncStatus=synced`, `ExternalId`,
  `LastSyncedAt`); `failed` → `SyncStatus=failed` + `LastSyncError` + hata sınıflandırma.
- Batch ilerlemesi panelde izlenir (Senkron Geçmişi sekmesi: batch satırı +
  çözülen/toplam sayaç) — "sorgulama bitti mi" takibi artık personelin değil sistemin işi.

### 4.3 Hata sınıflandırma

Adapter başına bir sınıflandırıcı (`IMarketplaceErrorClassifier`): ham hata metni/kodu →
normalize `ErrorCode`:

| ErrorCode | Otomatik aksiyon |
|-----------|------------------|
| `category_conflict` | Issue + **istisna önerisi akışı**: hata mesajından beklenen kategori parse edilebiliyorsa "tek tıkla istisna oluştur + yeniden gönder"; parse edilemiyorsa **barkod tanı aracı** butonu görünür (K5 — yalnız burada) |
| `missing_attribute` | Readiness `missing_info`ya düşer, tamamlama ekranına derin link |
| `invalid_value` | Değer eşlemesi `needs_review` + issue |
| `duplicate_barcode` | Issue (personel kararı gerekir) |
| `rate_limited` | Item'lar `pending` kalır, batch NextPollAt ötelenir; issue açılmaz |
| `unknown` | Issue, ham hata gösterilir |

Sınıflandırıcı tablo/regex tabanlıdır; yeni hata kalıbı kod değişikliği gerektirmeden
eklenebilsin diye kalıplar DB'de tutulur (`mp_error_patterns`: marketplace, regex,
errorCode, beklenen-kategori yakalama grubu).

### 4.4 Diff-based gönderim

`mp_batch_items.PayloadHash` + `marketplace_products.LastSentPayloadHash` (yeni kolon):
içerik/fiyat/stok payload'ının hash'i değişmediyse ürün gönderime hiç girmez. Kota ve
batch boyutu yönetiminin temelidir.

---

## 5. Mutabakat ve Stok/Fiyat Senkronu

- **Hızlı kanal** (sık, hafif): fiyat/stok güncellemesi diff-based (yalnız değişen
  varyantlar), kendi batch tipiyle (`price_stock`) 4. bölümdeki altyapıdan geçer.
- **Mutabakat job'ı** (seyrek, örn. gecelik): pazaryeri listing'i çekilir,
  `marketplace_products`'taki beklenenle karşılaştırılır:
  - fiyat/stok sapması eşik altındaysa otomatik düzeltme gönderilir (bizim veri kazanır),
    eşik üstündeyse issue (pazaryeri tarafındaki fark bilinçli olabilir — kampanya/komisyon)
  - bizde synced görünüp pazaryerinde olmayan ürün → issue + yeniden gönderim önerisi
  - pazaryerindeki **fiili kategori** okunur → `mp_product_category_overrides`'a
    `Source=remote` yazılır/güncellenir (sonraki güncellemeler reddedilmez)
  - `unknown` batch item'ları burada çözülür (4.2).

---

## 6. Sorun Kuyruğu — `marketplace_issues`

- `Marketplace`, `FirmPlatformId`, `IssueType` (broken_mapping / required_attr_new /
  free_text_became_list / category_conflict / price_drift / stock_drift / upload_failed /
  batch_timed_out …), ilgili kayıt referansı, `SuggestedAction` (metin + varsa derin link),
  `Status: open | resolved | dismissed`, `AutoResolvedAt`
- **Otomatik kapanma esastır:** koşulu doğuran durum ortadan kalkınca (eşleme düzeldi,
  ürün başarıyla yüklendi, sapma giderildi) ilgili job issue'yu kapatır — kuyruk çöplüğe
  dönmez. Aynı koşul için açık issue varken duplicate açılmaz (unique koşul anahtarı).
- Panel: mağaza detayına **Sorunlar** sekmesi; kart sağlık şeridi ve sayıları buradan
  beslenir. Requests modülüne karıştırılmaz (o insan talepleri içindir).

---

## 7. Uygulama Fazları

| Faz | İçerik | Not |
|-----|--------|-----|
| **F1** | ✅ **UYGULANDI (2026-07-26)** — `marketplace_ref` DB (açılışta otomatik oluşturma+şema) + senkron çekirdeği (snapshot/hash-diff/change log/sync runs/heartbeat) + **Trendyol referans indirici** (kategori+özellik uçları kimliksiz erişilebilir, doğrulandı: 3.857 kategori) + `POST/GET /api/marketplaces/reference-sync*` + panel "Referans Verisi" modalı. Kod: `Api/Services/Marketplace/Reference/`, `admin/.../ReferenceSyncModal.tsx` | Değer olayları change log'a satır satır değil kategori-kapsam özeti olarak yazılır (tablo şişmesin) |
| **F2** | ✅ **UYGULANDI (2026-07-26)** — `integration.marketplace_category_mappings` (direct/rules/pool, RulesJson/PoolJson jsonb) + `marketplace_attribute_mappings` (map_values/pass_literal/fixed_value) + `marketplace_value_mappings` (migration `20260726105854`, canlı DB'de); `/api/marketplaces/mapping/*` uçları; öneri katmanı (`TextSimilarity` — TR normalize + önek toleransı + path-segment skoru); sağlık job'ı (`MappingHealthService`, senkron sonrası otomatik + elle); panel `/marketplaces/eslestirme` (3 sekme: Kategori / Özellik & Değer / Gözden Geçir) | Kural koşulu v1: tek özellik=değer çifti (JSON genişlemeye açık); mağazaya özel eşleme alanı hazır, v1 ekranı firma geneli |
| **F3** | ✅ **UYGULANDI (2026-07-26)** — `marketplace_product_readiness` + `marketplace_product_category_overrides` (§2.2) + `marketplace_product_attribute_values` (§2.4) (migration `20260726135444`, canlı DB'de); `MarketplaceReadinessService` (bellek-içi toplu hesap: 28.653 ürün ~1,5 sn; yalnız değişen satıra yazım; kodlu nedenler; **özellikleri indirilmemiş kategori `attrs_not_synced` sayılır — körlemesine "hazır" denmez**); `MarketplaceCompletionService` (tekil+toplu tamamlama, kategori→istisna, değerler→ürün-özel, kayıt sonrası anında yeniden denetim); mağaza detayında Hazır/Eksik çipleri + neden rozetleri + Denetle + Tamamla/Toplu Tamamla modalı | Varyant ekseni özellikler (Beden) denetim DIŞI — F4 gönderim payload'ının işi. Kanala açık olmayan ürün aday sayılmaz (mevcut tanım) |
| **F4** | ✅ **UYGULANDI (2026-07-26)** — `marketplace_batches` + `marketplace_batch_items` + `marketplace_error_patterns` + `marketplace_products`'a LastSentPayloadHash/LastErrorCode/SuggestedCategoryExternalId (migration `20260726142024`, canlı DB'de); `TrendyolSellerClient` (gerçek createProducts + batch-requests, base URL `Trendyol:SellerBaseUrl` config'li — mock testine de imkân verdi), `MarketplaceSendService` (yalnız HAZIR ürünler; K6 değer önceliği zinciriyle payload; ≤100'lük paketler; diff-hash), `MarketplaceBatchWorker` (60 sn tick, kısmi cevap item-bazlı, backoff 1→30 dk, 24 saat→timed_out→unknown, körlemesine yeniden gönderme YOK), `MarketplaceErrorClassifier` (DB kalıpları + yerleşikler; "Beklenen kategori: X" adı referans DB'de kimliğe çözülür); panel: Senkron Geçmişi'nde Gönderim Paketleri bloğu + Hatalı satırda "istisna yaz + yeniden gönder" | Ürün gönderimi batch yolunda; stok/fiyat hızlı kanalı F5'te (update-stocks hâlâ stub). Barkod tanı aracı ertelendi — Trendyol katalog-arama ucu satıcıya kapalı, redd mesajı parse'ı aynı işi görüyor (K5). Sözleşme ayarlarına `brandId` + `cargoCompanyId` zorunlu |
| **F5** | ✅ **UYGULANDI (2026-07-26)** — `marketplace_issues` (+batch item SentPrice/SentStock; migration `20260726190028`, canlı DB'de); **fiyat-stok hızlı kanalı** (`SubmitPriceStockAsync`: yalnız synced, diff-based, price-and-inventory ucu, aynı batch takip altyapısı — update-stocks stub'ı emekli); **mutabakat** (`MarketplaceReconciliationService`: sayfalı listing çekimi, stok+eşik-altı fiyat otomatik düzeltme [`Marketplace:PriceDriftPercent` vars. %10], eşik-üstü fiyat→issue, kayıp ürün→issue+yeniden gönderime açılır, fiili kategori→Source=remote istisna [personel kararına dokunmaz], unknown item'lar listing gerçeğiyle çözülür, koşusu integration_logs'a yazılır); **sorun kuyruğu** (`MarketplaceIssueService`: ConditionKey ile duplicate önleme, sahip-tip bazlı OTOMATİK kapanma, Yoksay→koşul sürerse yeniden açılır); panel: Sorunlar sekmesi + kart sağlık şeridi açık sorun sayısı + Senkron dropdown'da Mutabakat | Zamanlanmış otomatik mutabakat/stok senkronu yok — elle tetikleme (kadans açık konu); stok sapması hep otomatik düzeltilir (bizim veri kazanır), yalnız fiyat eşikli |
| **F6+** | Diğer pazaryerleri: pazaryeri başına referans indirici + hata sınıflandırıcı + payload üretici (çekirdek değişmez) | Sıra kullanıcı önceliğine göre |

## Açık Konular (uygulama sırasında karara bağlanacak)

- Öneri katmanının benzerlik eşiği ve Türkçe normalizasyon detayı (İ/ı, çoğul ekleri).
- `mp_product_attribute_values` ürün mü varyant düzeyi mi başlasın (öneri: ürün düzeyi
  başla, varyant-ekseni özellikler zaten varyant verisinden gelir).
- Mutabakat otomatik-düzeltme eşikleri (fiyat % / stok adet) — mağaza ayarı mı global mi.
- Referans senkron kadanslarının pazaryeri başına varsayılanları.
