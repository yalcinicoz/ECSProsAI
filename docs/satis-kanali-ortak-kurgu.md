# Satış Kanalı Ortak Kurgusu — Kanal Ürünleri, Dropshipping (iki yön), Pazaryeri, Tedarik Kaynakları

> Sürüm: **v1.2 — 2026-08-23** (v1.1 + F0 uygulama durumu)
> **Uygulama durumu (2026-08-23):** **F0 CANLIDA; F1 Kapsam UYGULANDI ⚠️ restart bekliyor.** F1 uygulama kararları:
> (1) kapsam tanımı `FirmPlatform` yerine **`storefront.channel_scopes`** tablosunda (kanal verisi Storefront'ta);
> (2) **hibrit materyalizasyon** — `all` örtük (satır yoksa kanalda, eski davranış birebir), `filter|mixed` materyalize
> `channel_products.InScope`; (3) kapsam yönetimi **ayrı sayfada** `/storefront/channel-scope` (kullanıcı kararı:
> kapsam kararı sorumluların işi, günlük operasyon ekranına konmaz — K9'a ek); (4) kural şemasına
> **ExcludedProductGroupIds** eklendi ("tümü, şu gruplar hariç"); (5) pazaryeri aday SQL'i storefront ile aynı
> görünürlük anlamına geçti (K10 kapandı). İzole 5051 kabul testleri ✓ (kanal başına sayılar birebir).
> **F2 Listeleme durumu UYGULANDI (2026-08-23) ⚠️ restart bekliyor:** `Api/Services/ChannelListingStatusService`
> (cross-schema raw-SQL + 2 dk IMemoryCache anlık görüntü; **denormalize kolon YOK** — hibrit kapsam satırsız `all`
> kanallarında denormalizasyon anlamsız, §2.2'den bilinçli sapma; readiness motoruna `blocked` EKLENMEDİ — blocked
> durumu bu hesaplayıcıda katman-2+stok+satış kapalıdan türetilir). Uçlar: `listing-summary` (özet çipler),
> `listing-status` (ürün bazlı, ≤500). Panel: Kanal Ürünleri'nde Listeleme sütunu + özet satırı. Çapraz doğrulama:
> mishar `published`=5.792 = Merchant feed ürün sayısı. K15 olay tabanlı readiness tetiği hâlâ açık (F8'e).
> **F3 Ekran UYGULANDI (2026-08-23) ⚠️ restart bekliyor:** tıklanabilir özet çipleri + Sebep filtresi
> (`manage`/`manage/ids` `listing`+`reason` paramları, id-kısıtı ile), satır tıklaması → sağ çekmece
> (`ChannelProductDrawer`: kanal kararı aksiyonları, sebep başına Düzelt hedefi §4.3, pazaryeri varyant/ham hata
> tablosu, Gönder/Yeniden Dene = `sync-products`, ürün-bazlı Hazırlığı Hesapla = `readiness/recompute` body
> productIds), toplu çubukta yetenek bazlı "Pazaryerine Gönder". "Listeden düşür" (deactivate batch) hâlâ YOK —
> backend'i F6+ öncesi ayrı iş. CompletionModal çekmeceye gömülmedi; eşleme sebepleri Eşleme sayfasına link verir.
> **F5 Satıcı kaynağı UYGULANDI (2026-08-23) ⚠️ restart bekliyor:** `Product.SourceType` (own|seller|supply,
> migration `AddProductSourceType` canlı DB'de) — onayda damga (Approve yeni+revizyon) + idempotent seed backfill
> (onaylı gönderimden doğan ürünler). Yetenek zorlaması (K6) dört okuma yolunda: kapsam çözücü (kural kesişimi),
> storefront deny-set, listeleme hesaplayıcısı taban SQL, pazaryeri aday SQL (capabilities jsonb COALESCE).
> Kural şemasına `SourceTypes`; FilterBuilder "Ürün Kaynağı". Sebepler: `seller_update_pending` (BİLGİ — durumu
> değiştirmez), + hesaplayıcı artık evren dışını da açıklar: `no_image`, `out_of_scope`. Satıcı kesiti:
> `POST /api/supplier/products/listing-status` (sahiplik + yalnız satıcıya açık kanallar) + satici "Ürünlerim"
> Listelenme sütunu. Plan sapması: `seller_stock_zero/price_stale/shipping_profile_missing` AYRI kod olmadı —
> satıcı fiyat/stok paylaşımlı katalog+envanterden aktığı için `price_zero`/`out_of_stock` zaten kapsıyor.
> F0 Yetenek modeli **UYGULANDI (2026-08-23)** — `ChannelCapabilities` (Shared.Contracts/Channels) + `IChannelCapabilityResolver` (Core.Infrastructure, 2 dk IMemoryCache), `core_platform_types.capabilities` / `core_firm_platforms.capability_overrides` (jsonb, migration `AddChannelCapabilities` canlı DB'de uygulandı), seed: `dropship_partner` tipi + koda göre backfill (idempotent), `IsMarketplace` artık `pushListing`'den türetilir (kolon korunur), admin: Platform Tipleri yetenek editörü (3 şablon butonu), kanal formu ezme editörü (4 alan) + kart/tablo rozetleri, rehber sayfaları güncellendi. İzole 5051 testi ✓ (ezme yazma/sanitize/negatif/temizleme, tip güncelleme, türetme). **Sırada F1 Kapsam.**
> Alan: 🛠 **Admin panel** (pano #2) + zorunlu çekirdek dokunuşları (Core/Storefront/Integration/Catalog) +
> 🔌 Dış API (pano #3) ile kesişen noktalar işaretlidir.
> İlgili dokümanlar: `docs/pazaryeri-entegrasyon-veri-yonetimi.md` (pazaryeri eşleme/yükleme, F1-F5 canlı),
> `docs/api-hesaplari-tasarimi.md` (Partner API tip/scope, ürün gönderimi Kapı 1/2, F2b-2d dropship sipariş),
> `docs/satici-paneli-tasarimi.md` (üçüncü taraf satıcı paneli), `docs/urun-url-kanal-mimarisi.md`
> (kanal verisi Storefront'ta; IsForSale/IsPublished ayrımı), `docs/menu-kategori.md` (ChannelProduct gerekçesi).
> Hedef: her faz bir iş emri — veri modeli, ekran, kabul kriteri, test adımı yazılı. **Bir faz bitmeden diğerine geçilmez.**

---

## 0. Amaç ve kapsam

Bugün `/storefront/channel-products` tek boyutlu bir istisna listesidir (kanaldan çıkar / satışı durdur). Oysa bir
satış kanalında ürünün yolculuğu üç sorudan oluşur: *(1) bu ürün bu kanalın kapsamında mı, (2) bu kanalda satışa
açık mı, (3) fiilen yayında/satışta mı — değilse neden ve kim düzeltir?* Bu doküman:

- **tek kanal modeli** kurar (site / dropship bayi / pazaryeri ayrı dünyalar DEĞİL; tip yalnız varsayılan + yetenek belirler),
- kanal ürünleri ekranını bu üç katmana göre yeniden tasarlar,
- **dört iş yönünü** aynı kurguya oturtur:
  - **Y1** bizim ürünümüz → pazaryerinde (Trendyol, Amazon…) — *push*, eşleme gerekir (büyük ölçüde var),
  - **Y2** bizim ürünümüz → dropship bayinin kendi mecrasında — bayi **bizim Partner API'mizi** kullanır (pull + sipariş POST),
  - **Y3** üçüncü taraf satıcının ürünü → bizim sitemizde (biz pazaryeriyiz) — satıcı **bizim Partner API'mizi / satıcı panelini** kullanır (var: ürün gönderimi Kapı 1/2),
  - **Y4** ürün sahibinin ürünü → bizim kanallarımızda, **biz dropship satıcıyız** — *onların* API'si ya da Excel/CSV ile beslenmemiz gerekir (YENİ: tedarik kaynağı bağlayıcıları; bugün entegre olunacak somut API yok, altyapı hazır olmalı).

Kapsam dışı (ayrı dokümanlar): kargo entegrasyonu, e-fatura, PayTR, satıcı hakediş motoru, kampanya.

### Terimler
| Terim | Anlamı |
|---|---|
| **Kanal** (`FirmPlatform`) | Firmanın ürün sattığı her mecra: site, mobil, POS, pazaryeri mağazası, dropship bayi |
| **Kanal tipi** (`PlatformType`) | Tipin varsayılanları + yetenek seti; kanal ekranda tipe değil yeteneğe göre davranır |
| **Kapsam** | Kanalın kayıtlı filtresinden (+ manuel ekle/hariç) geçen ürün kümesi — "söz konusu ürünler" |
| **Kanal kararı** | Kapsamdaki ürün için personel kararı: Kanalda / Durduruldu (pencere) / Çıkarıldı |
| **Listeleme durumu** | Sistemin hesapladığı gerçek durum: yayında/yüklü, hazır, eksik bilgi, hatalı, bekliyor, pasif + sebepler |
| **Ürün sahibi / kaynak** | Ürünün kime ait olduğu ve nereden beslendiği: biz (manuel), üçüncü taraf satıcı (Y3), dış ürün sahibi (Y4) |
| **Tedarik kaynağı** (`SupplySource`) | Y4 için bağlayıcı tanımı: dış API adaptörü ya da dosya (Excel/CSV) profili |

---

## 1. Kararlar

Kapalı = kullanıcıyla mutabık (2026-08-23). Açık = uygulama öncesi kapatılacak.

| # | Karar | Durum |
|---|---|---|
| K1 | Kanal tipleri UI'da ayrı dünyalara bölünmez; **tek kanal modeli + tipe göre varsayılan + yetenek bayrakları**. `PlatformType.IsMarketplace` tek bool'u yetenek setine dönüşür. | **KAPALI** |
| K2 | Kanal kapsamı **kanala özel kayıtlı filtre** ile belirlenir (`FilterDef`/`FillType` kalıbı, `FilterBuilder` yeniden kullanılır) + manuel ekle/hariç tut. Mevcut kanallar için varsayılan **"tüm ürünler"** (bugünkü davranış korunur). | **KAPALI** |
| K3 | Üç katman ayrı tutulur: kapsam → kanal kararı → listeleme durumu. Kapsam dışı ürün 2-3. katmanda görünmez. | **KAPALI** |
| K4 | Dropship bayi (Y2) varsayılan kapsamı: **site kapsamı ∩ bu kanalda fiyatı var ∩ stok ≥ N (tip varsayılanı) ∩ kaynak = biz**. Farklılaşma el ile küratörlükle değil sayılabilir kriterlerle (§5). | **KAPALI** |
| K5 | Her dropship bayi **ayrı kanal**; çoğalmayı "kanalı şablondan kopyala" + tip varsayılanları önler. Partner API hesabı bir kanala bağlanır. | **KAPALI** |
| K6 | Üçüncü taraf satıcı (Y3) **kanal değil, ürün kaynağıdır**; kapsam filtresine "ürün sahibi" kriteri eklenir. Satıcı ürünleri sitede varsayılan açık, dropship/pazaryerinde varsayılan kapalı. | **KAPALI** |
| K7 | Pazaryeri eşlemesi: **platform-geneli varsayılan eşleme** (tip bazında, bir kez) + kanal bazında istisna. | **KAPALI** |
| K8 | Y4 (biz dropship satıcıyız) için **tedarik kaynağı bağlayıcı altyapısı** kurulur; dış API ve dosya aynı soyutlamadan geçer; gelen kayıtlar mevcut **ürün gönderimi (ProductSubmission) Kapı 1/2** hattına düşer, doğrudan kataloğa yazılmaz. | **KAPALI** (ilk adaptör: dosya; somut dış API geldiğinde adaptör eklenir) |
| K9 | Kanal ürünleri ekranı "yapılacaklar" görünümüdür; eşleme sayfası "tanım" görünümü kalır; `/marketplaces` mağaza detayı mağaza/batch yönetimi için kalır, ürün bazlı çalışma kanal ürünleri ekranına taşınır. | **KAPALI** (önceki tur önerisi, itiraz gelmedi) |
| K10 | Storefront (opt-out) ile pazaryeri aday sorgusu (opt-in) arasındaki anlam çatlağı **kapsam materyalizasyonu** ile kapanır: kapsamdaki her ürünün `channel_products` satırı olur; "satır yoksa kanalda" kuralı kalkar. | **KAPALI** (teknik sonuç) |
| K11 | Dropship bayiye **fiyatı olmayan ürün gösterilmez** ("fiyatsız, talep et" yok) | **KAPALI** (2026-08-23) |
| K12 | Satıcı ürünlerinin (Y3) pazaryerine gönderilmesi: **kanal bayrağıyla açıkça izin verilmedikçe kapalı**; sözleşme/kargo/fatura zinciri açıldığında ayrı karar | **KAPALI** (2026-08-23) |
| K13 | Y4'te sipariş iletimi: kaynak yeteneği `canPushOrder` varsa API push; yoksa **günlük sipariş dosyası** (Excel/CSV, e-posta/SFTP); her durumda iç kayıt | **KAPALI** (2026-08-23) |
| K14 | Y4'te fiyatlama: ürün sahibinin **alış/liste fiyatı + bizim kural** (marj %, yuvarlama) → kanal fiyat tipine yazılır; mevcut fiyat tipi/kanal fiyatı motoru | **KAPALI** (2026-08-23) |
| K15 | Hazırlık (readiness) yeniden hesaplama: **grup/eşleme/ürün değişince olay tabanlı + gece tam tarama** (manuel tetik kalır) | **KAPALI** (2026-08-23) |
| K16 | Eşlemenin platform-geneli varsayılanı **`definition` kuralına tabi** (yalnız geliştirici firma doldurur); firma/kanal istisnası firma tarafında | **KAPALI** (2026-08-23) |
| K17 | **Kanal stok formülü:** `netStock` = satış kanallarına açık depo/kısımlardaki toplam stok (`WarehouseSection.IsSellableOnline`; mağaza depoları da bu bayrakla dış satışa açılabilir) − toplam rezerv; kanala verilen adet **`stockQuantity = max(0, netStock − minStock + 1)`** (minStock=3, net=3 → 1; net=2 → 0). Kapsam/satılabilirlik şartı `stockQuantity ≥ 1`. Tüm kanal tipleri (site dahil; site varsayılanı minStock=1 → formül bugünkü netStock'a eşittir). | **KAPALI** (2026-08-23) |
| K18 | **Bayi (dropship) fiyat politikası ayrı çalışmadır** — bu dokümanda yalnız "kanal fiyatı var/yok" bilgisi kullanılır; iskonto/marj/liste kuralları ayrı plan dokümanında (`docs/bayi-fiyat-politikasi.md`, henüz yazılmadı) | **KAPALI** (kapsam dışı) |

---

## 2. Ortak kanal modeli — yetenek bayrakları

### 2.1 Yetenek seti (`PlatformType.Capabilities`, jsonb; kanal bazında seçili alanlar ezilebilir)

| Yetenek | Anlamı | Site | Mobil/POS | Dropship bayi (Y2) | Pazaryeri (Y1) |
|---|---|---|---|---|---|
| `pushListing` | Ürün dışarı gönderilir (batch/adapter) | hayır | hayır | hayır (bayi çeker) | **evet** |
| `externalTaxonomy` | Dış kategori/özellik eşlemesi gerekir | hayır | hayır | hayır | **evet** |
| `readinessLevel` | Hazırlık denetimi seviyesi | `light` | `light` | `light+price` | `full` |
| `priceSource` | Fiyat kaynağı | kanal fiyat tipi | kanal fiyat tipi | bayi fiyat listesi (kanal fiyatı) | kanal fiyatı + geri okuma |
| `saleStopWindow` | Satış durdurma penceresi | evet | evet | evet | evet + `deactivate` batch |
| `thirdPartySellerProducts` | Y3 satıcı ürünleri kapsama girebilir | **varsayılan açık** | açık | **kapalı** | **kapalı** |
| `externalSupplyProducts` | Y4 dış kaynak ürünleri kapsama girebilir | açık | açık | kapalı (zincirleme dropship yok) | kanal bazında |
| `orderDirection` | Sipariş yönü | `internal` | `internal` | `partner_push` (Partner API POST) | `pull` (adapter) |
| `defaultMinStock` | Kanal stok formülünün eşiği (K17: `stockQuantity = max(0, netStock − minStock + 1)`) | 1 | 1 | **3** | 1 |
| `autoPublish` | Kapsama giren ürün otomatik "Kanalda" | evet | evet | evet | evet (gönderim yine elle/toplu) |
| `pullsFromPartnerApi` | Karşı taraf bizim Partner API'mizi kullanır | — | — | **evet** (`catalog.read`, `stock.read`, `order.write`) | — |

Ekran kuralı: **buton/sütun/sekme tipe değil yeteneğe bağlanır** ("Pazaryerine Gönder" yalnız `pushListing`; "Eşleme"
yalnız `externalTaxonomy`; "Kaynak" sütunu yalnız `thirdPartySellerProducts || externalSupplyProducts`).

### 2.2 Veri modeli değişiklikleri (özet; detay fazlarda)

| Nerede | Değişiklik |
|---|---|
| `core.platform_types` | `Capabilities` jsonb (+ seed); `IsMarketplace` okunmaya devam eder, türetilir (`pushListing`) — kaldırma sonraki faz |
| `core.firm_platforms` | `ProductFilterDef` jsonb, `ProductFillType` (`all`\|`filter`\|`mixed`), `CapabilityOverrides` jsonb, `MinStock` int?, `ScopeSyncedAt` |
| `storefront.channel_products` | Kapsam materyalizasyonu: `InScope` bool (filtreden geçti), `ScopeSource` (`filter`\|`manual`\|`legacy`), `IsExcluded` (manuel hariç); `IsActive` → anlam "kanal kararı" (mevcut), `ListingStatus` (hesaplanan, denormalize: `published`\|`ready`\|`missing_info`\|`blocked`\|`pending`\|`failed`\|`deactivated`), `ListingReasonsJson`, `ListingComputedAt`, `SourceType` (`own`\|`seller`\|`supply`) denormalize |
| `integration.mp_product_readiness` | `Status`'a **`blocked`** eklenir (kanal kararı kapalı/durduruldu; satış kapalı; stok 0 — doc'ta planlı, kodda yok) |
| `integration.mp_batches` | `BatchType` `deactivate` (listeden düşürme) |
| Eşleme tabloları | `FirmIntegrationId` null = platform-geneli varsayılan; çözümleme: kanal istisnası → firma → platform varsayılanı (K7) |
| `catalog.products` | `SourceType` (`own`\|`seller`\|`supply`), `SupplySourceId?` (Y4), mevcut `SupplierId` korunur (cari) |
| **YENİ** `integration.supply_sources` | Y4 tedarik kaynağı tanımı (§6) |
| **YENİ** `integration.supply_source_records` | Y4 ham kayıt staging (dış ürün id → gönderim/ürün eşlemesi, hash, son görülme) |
| **YENİ** `integration.supply_source_runs` | Çalıştırma günlüğü (dosya/çekim; alınan/eşlenen/atlanan/hatalı sayıları) |

---

## 3. Üç katman akışı (tüm kanal tipleri için aynı)

```
Katalog ürünü
   │  (K2) kanal kapsam filtresi + manuel ekle/hariç   ─► kapsam dışı: görünmez
   ▼
KAPSAM  (channel_products.InScope=true)
   │  (katman 2) personel: Kanalda / Durduruldu(pencere) / Çıkarıldı
   ▼
KANAL KARARI  (IsActive, SaleStoppedFrom/Until)
   │  (katman 3) sistem hesaplar: readiness (+ kaynak kontrolleri) + push sonucu
   ▼
LİSTELEME DURUMU  (ListingStatus + sebepler)  ─► personel "Düzelt" ─► yeniden hesap
```

### 3.1 Kapsam materyalizasyonu (`SyncChannelScope`)
- `ProductFillType=all` → tüm satılabilir ürünler (görseli olan; `IsSaleOpen` kapsam şartı değil, katman 3 sebebidir).
- `filter`/`mixed` → `ProductFilterHelper.BuildFilterQuery(FilterDef)` (kategori/kampanya ile aynı kural şeması) + manuel
  eklenenler − manuel hariç tutulanlar. Kural şemasına eklenecekler: `SourceTypes` (own/seller/supply), `SupplySourceIds`,
  `HasChannelPrice` (bu kanalda fiyatı var), `MinStock` (kanal varsayılanı; K17 formülüyle `stockQuantity ≥ 1` şartı — `netStock` yalnız `IsSellableOnline` kısımlardan, rezerv düşülmüş).
- Tetik: filtre kaydedilince, ürün oluşturulunca/gruba girince (olay), gece tam tarama. Sonuç: kapsama giren ürün için
  satır (InScope=true, IsActive=autoPublish), çıkan için InScope=false (karar geçmişi silinmez).
- Storefront okuma yolu: deny-set yerine **kapsam+karar seti** (`InScope && IsActive && !stopped`); `IChannelProductFlagService`
  yeni metod `GetChannelVisibleProductIdsAsync` — mevcut çağrı noktaları (GetStoreProducts/Facets/Detail/Checkout) ona geçer.
  Geçiş güvenliği: eski kanallar `all` + legacy satırlar → sonuç bugünkü kümeye eşit (kabul testi bunu ölçer).
- Pazaryeri aday sorgusu (`MarketplaceAdminService.ChannelActiveWhere`) aynı kümeye geçer → K10 çatlağı kapanır.

### 3.2 Listeleme durumu hesaplama (`ComputeListingStatus`)
Tek hesaplayıcı; kanal yeteneğine göre kural seti:

| Sebep kodu | Hangi kanallar | Kaynak |
|---|---|---|
| `channel_excluded` / `sale_stopped` → `blocked` | hepsi | katman 2 |
| `sale_closed` (Product.IsSaleOpen=false), `no_image`, `price_zero`, `out_of_stock` | hepsi (light) | Catalog/Inventory |
| `no_channel_price` | `readinessLevel ≥ light+price` | ChannelVariant/fiyat tipi |
| `no_category_mapping`, `required_attr_missing`, `value_unmapped`, `broken_mapping`, `pool_assignment_pending` | `externalTaxonomy` | mevcut readiness motoru |
| `push_pending`, `push_failed:<code>`, `deactivated`, `unlisted_remote`, `price_drift`, `stock_drift` | `pushListing` | MarketplaceProduct/Batch/Issue |
| `seller_approval_pending`, `seller_stock_zero`, `seller_price_stale`, `seller_shipping_profile_missing` | `SourceType=seller` | Y3 satıcı verisi |
| `supply_record_stale` (kaynak X gündür güncellenmedi), `supply_price_missing`, `supply_mapping_missing`, `supply_source_paused` | `SourceType=supply` | Y4 kaynak verisi |

Sonuç: `published` (web/dropship: kanalda+sebep yok; pazaryeri: synced) · `ready` (pazaryeri: gönderilebilir) ·
`missing_info` · `blocked` · `pending` · `failed` · `deactivated`. Her sebep → **Düzelt hedefi** (sayfa/modal) tablosu
§4.3'te.

---

## 4. Kanal Ürünleri ekranı — `/storefront/channel-products` (yeniden tasarım)

### 4.1 Yerleşim
1. **Kanal seçici** (mevcut; oturumda hatırlanır) — yanında kanal tipi etiketi + yetenek rozetleri (Push · Eşleme · Satıcı ürünleri).
2. **Sekmeler:** **Ürünler** (varsayılan) · **Kapsam** · (yalnız `pushListing`) **Gönderimler**.
3. **Ürünler sekmesi**
   - Özet çipleri (tıklanınca filtreler): Kapsamda N · Kanalda · Yayında/Yüklü · **Eksik bilgi** · **Hatalı** · Bekliyor · Engelli · Durduruldu · Çıkarıldı.
   - Filtre çubuğu: arama (kod/ad), kanal durumu, **listeleme durumu**, **sebep kodu**, ürün grubu, tedarikçi/satıcı, kaynak tipi (yetenek varsa).
   - Tablo: ☐ · görsel · ürün (ad+kod) · grup · **kaynak** (yetenek varsa) · kanal fiyatı · stok · kanal durumu · **listeleme durumu rozeti** · **sebep (kısa)** · son işlem · işlem.
   - Satır tıklama → **sağ çekmece** (liste satırı tıklanabilir kuralı): sebep listesi (her biri "Düzelt" bağlantılı), pazaryeri ham hata + normalize kod, gönderim geçmişi, satıcı/kaynak bilgisi; butonlar: Kanala al/Çıkar · Satışı durdur/başlat · (push) Gönder / Yeniden dene / Listeden düşür · Hazırlığı yeniden hesapla · Ürün detayına git.
   - Toplu çubuk (mevcut 4 + yetenek bazlı): Kanala Al · Kanaldan Çıkar · Satışı Durdur · Satışı Başlat · Pazaryerine Gönder · Yeniden Dene · Listeden Düşür · Hazırlığı Yeniden Hesapla. "Filtreye uyan tümünü seç" sunucu tarafında id listesi yerine **filtre imzasıyla** çalışır (28K Guid tarayıcıya inmez).
4. **Kapsam sekmesi:** doldurma tipi (Tümü / Filtre / Karma) · `FilterBuilder` (yeni kriterler dahil) · eşleşen sayı önizleme · **Kapsamı Güncelle** (Sync) · manuel eklenen / hariç tutulan listeleri (ürün ara-ekle) · son sync zamanı/sonucu · (dropship) "Şablondan kopyala".
5. **Gönderimler sekmesi** (`pushListing`): mevcut `/marketplaces` mağaza detayı "senkron" görünümünün ürün odaklı özeti (son batch'ler, kalem sonuçları) — ayrıntı mağaza sayfasına link.

### 4.2 Yetkiler
Kanal kararı (mevcut yetki) · Kapsam düzenleme (`channel.scope.manage` yeni) · Gönder/Listeden düşür (`marketplace.push`, mevcut kalıp) · Hazırlık yeniden hesap (herkes).

### 4.3 Sebep → Düzelt hedefi
| Sebep | Hedef |
|---|---|
| `no_image`, `price_zero`, `sale_closed` | Ürün detayı ilgili sekme (Görseller / Genel / Tehlikeli alan) |
| `no_channel_price` | Ürün detayı → Satış Kanalları sekmesi (kanal seçili) |
| `out_of_stock` | Stok sayfası (ürün filtreli) |
| `no_category_mapping`, `value_unmapped`, `broken_mapping` | Eşleme sayfası (grup/özellik önseçili) |
| `required_attr_missing` | mevcut `CompletionModal` (çekmece içinden) |
| `push_failed:*` | çekmecede ham hata + "Yeniden dene"; `category_conflict` → eşleme istisnası |
| `seller_*` | Satıcı ürün detayı (admin) + satıcıya bildirim |
| `supply_*` | Tedarik kaynağı detayı (§6) / son çalıştırma günlüğü |

---

## 5. Y2 — Dropship bayi kanalı (bizim ürünümüz, bayinin mecrası)

- Tip: `dropship_partner` (`Capabilities` §2.1). Her bayi ayrı kanal (K5); kanal oluşturulurken Partner API hesabı
  (`ApiClientType=dropship`/mevcut tip kataloğundan) bağlanır; token'daki kanal kimliğiyle `catalog.read`/`stock.read`
  sorguları **o kanalın kapsam+karar kümesi ve fiyatıyla** yanıt verir (Dış API alanı ile ortak iş — pano #3).
- Varsayılan kapsam (K4): Tümü ∩ `HasChannelPrice` ∩ `MinStock ≥ 3` ∩ `SourceTypes=[own]`; bayi bazında ek daraltma
  (grup/marka/tedarikçi) `FilterBuilder` ile. Meşru kriter listesi (öncelik): kanal fiyatı var · stok eşiği · kaynak/tedarik
  sahibi · marka/tedarikçi dağıtım kısıtı · içerik yeterliliği (katman 3 sebebi, kapsam değil) · grup daraltması · (ileride) lojistik.
- "Ne kadar farklılaşmalı?" ilkesi: **az** — farklılaşma yalnızca yukarıdaki sayılabilir kriterlerle; elle ürün ürün seçim
  istisnadır (manuel ekle/hariç yine var ama günlük yöntem değil).
- Sipariş: bayi `POST /api/partner/v1/orders` (F2b-2d, henüz yok) → bizim sipariş + kargo; bayi kanalı sipariş listesinde
  kanal olarak görünür (mevcut kanal bazlı sipariş modeli yeterli).
- Bayiye dışa verilen stok (Partner `stock.read`): K17 formülü `stockQuantity = max(0, netStock − minStock + 1)`; 0 dönen ürün kapsamda "stok yok" sebebiyle listelenmez (oversell freni). Aynı formül pazaryeri stok gönderiminde ve site stok kontrolünde kullanılır (tek hesaplayıcı `IChannelStockCalculator`, Inventory modülü).
- Bayi fiyat listesi/iskonto kuralları K18 gereği ayrı çalışma; bu fazda yalnız "kanal fiyatı var" (mevcut ChannelVariant/fiyat tipi) kullanılır.

## 6. Y3 — Biz pazaryeriyiz (üçüncü taraf satıcı ürünleri bizim kanallarımızda)

- Satıcı = kaynak (K6). Ürün `SourceType=seller`, `SupplierId` = satıcının cari hesabı (mevcut). Giriş yolu: satıcı paneli /
  Partner API ürün gönderimi → Kapı 1 (otomatik) → Kapı 2 (insan onayı) → canlı ürün (mevcut).
- Kapsam: filtre kriteri `SourceTypes` (biz / tüm satıcılar / seçili satıcılar); tip varsayılanları §2.1.
- Katman 3 satıcı sebepleri (§3.2) ve satıcı paneline **kendi ürünlerinin listeleme durumu + sebep** kesiti (destek yükünü
  satıcıya gösterir; Satıcı paneli alanı — pano #4, ayrı iş emri).
- Fiyat/stok satıcıdan gelir; kanal fiyat listesi uygulanmaz (kaynak bayrağıyla hesaplayıcı farklı veriden okur, ayrı ekran yok).
- K12 açık: satıcı ürününün pazaryerine gönderimi.

## 7. Y4 — Biz dropship satıcıyız (ürün sahibinin ürünü bizim kanallarımızda) — **tedarik kaynağı altyapısı**

Amaç: yarın X firmasının ürün API'si ya da haftalık Excel'i geldiğinde **yeni ekran ve yeni hat açmadan** kaynağı tanımlayıp
ürünleri mevcut Kapı 1/2 hattına düşürmek, stok/fiyatı düzenli tazelemek, siparişi sahibine iletmek.

### 7.1 Kavram
```
SupplySource (kaynak tanımı: sahip cari + bağlayıcı tipi + profil + zamanlama)
   │  ISupplyConnector.FetchCatalog / FetchStockPrice / (PushOrder) / (FetchOrderStatus)
   ▼
supply_source_records (ham kayıt staging: dış id, hash, ham payload, son görülme)
   │  gelen-eşleme (dış kategori/özellik → bizim grup/özellik/değer)  ← pazaryeri eşlemesinin TERSİ, aynı tablolar, yön alanı
   ▼
ProductSubmission (mevcut Kapı 1/2; SupplierId = kaynak sahibi cari; ApiClientId yerine SupplySourceId)
   │  onay
   ▼
Product (SourceType=supply, SupplySourceId) → kanal kapsamı → katman 2/3 (supply_* sebepleri)
   │
Sipariş (ürün kalemi SourceType=supply) → PushOrder / günlük sipariş dosyası / yalnız kayıt (K13)
```

### 7.2 `ISupplyConnector` (Integration.Application/Adapters, `IMarketplaceAdapter` ile aynı çözümleme kalıbı)
| Üye | Zorunlu | Açıklama |
|---|---|---|
| `ConnectorCode` | evet | `file_xlsx`, `file_csv`, `http_json_generic`, `<firma>_api` |
| `FetchCatalogAsync(profile, since)` | evet | ürün/varyant kayıtları (dış id, ad, kategori yolu, özellikler, görsel URL'leri, barkod, alış/liste fiyatı, stok) |
| `FetchStockPriceAsync(profile, ids)` | evet | artımlı stok/fiyat |
| `PushOrderAsync(order)` | hayır | yetenek yoksa K13 alternatifleri |
| `FetchOrderStatusAsync(ids)` | hayır | kargo/durum geri okuma |
| `Capabilities` | evet | `{ catalog, stockPrice, pushOrder, orderStatus, images:url|binary }` |

**İlk bağlayıcılar (somut API olmadığı için):** (1) `file_xlsx/csv` — **sütun eşleme profili** (şablonumuz ya da sahibin
dosyası için "sütun → alan" haritası, panelde kaydedilir), yükleme: panelden elle / SFTP-klasör izleme / e-posta eki (sonra);
(2) `http_json_generic` — URL + kimlik + **JSON yol eşlemesi** (JSONPath benzeri) ile "basit REST kataloğu" — yarın gelen
çoğu basit API'yi kodsuz bağlar; gerçek karmaşık API'ler için `<firma>_api` sınıfı yazılır (kalıp hazır).

### 7.3 `supply_sources` tanımı (panel: Ayarlar → **Tedarik Kaynakları**; liste + detay)
Sahip cari (supplier account) · bağlayıcı · profil jsonb (bağlantı/sütun/JSON eşlemesi) · kimlik bilgisi (Data Protection ile
şifreli, `core_firm_platform_integrations` kalıbı) · zamanlama (manuel / saatlik / günlük) · fiyat kuralı (K14: alış + marj %,
yuvarlama; kanal fiyat tipine yazılır) · stok kuralı (emniyet payı, "stale" eşiği) · görsel politikası (URL indir/dış link) ·
varsayılan ürün grubu / gelen-eşleme profili · durum (aktif/duraklatıldı) · son çalıştırma özeti.

### 7.4 Gelen eşleme (dış taksonomi → bizim)
Pazaryeri eşleme tabloları (`mp_category/attribute/value_mapping`) **yön** alanı (`out`|`in`) ve `SupplySourceId` ile genişler;
aynı `MappingPage` kalıbı "kaynak" bağlamında açılır. Eşlenemeyen kategori/değer → gönderim Kapı 1'de `pending` kalır,
sebep `supply_mapping_missing`; grup bazında bir kez eşlenince sonraki kayıtlar otomatik akar.

### 7.5 Çalıştırma ve günlük
`supply_source_runs`: başlangıç/bitiş, alınan/yeni gönderim/güncellenen/atlanan (hash aynı)/hatalı, hata özeti; panelde
kaynak detayı "Çalıştırmalar" sekmesi + `Şimdi çalıştır`. `supply_record_stale` kuralı: son görülmeden N gün → katman 3 sebebi,
N×2 gün → kanal kararı otomatik "durduruldu" (pencere) — personel görür.

### 7.6 Sipariş iletimi (K13)
Sipariş onayında kaynak ürünü kalemleri gruplanır → `PushOrderAsync` varsa push (outbox kalıbı: `LegacyOrderOutbox` gibi
tekrar denemeli), yoksa "sipariş dosyası" (günlük Excel/CSV, kaynak sahibine e-posta/SFTP) ya da yalnız iç kayıt + manuel.
Kargo takibi geri okuması `FetchOrderStatusAsync` varsa otomatik, yoksa panelden elle.

### 7.7 Aynı Partner API iki yöne nasıl hizmet eder
- **Biz veren taraf** (Y2 bayi çeker, Y3 satıcı gönderir): mevcut `/api/partner/v1/*` tip/scope modeli; ek iş yalnız
  "token → kanal kimliği → kapsam/fiyat" (Y2).
- **Biz alan taraf** (Y4): karşı tarafın API'si/dosyası → `ISupplyConnector`. Partner API'miz burada devreye girmez; ancak
  karşı taraf isterse **bizim Partner API'mizi kullanarak ürün gönderebilir** (Y3 yolu) — o zaman kaynak = "Partner API"
  (bugünkü `ProductSubmission.ApiClientId`) ve bağlayıcıya gerek kalmaz. Yani Y3 ve Y4 aynı staging'e (ProductSubmission)
  iki farklı kapıdan girer; sonrası ortaktır.

---

## 8. Pazaryeri eşlemesi sahipliği (K7/K16)
- Eşleme kayıtlarında `FirmIntegrationId` null → **platform-geneli varsayılan** (tip bazında). Çözümleme sırası: kanal istisnası →
  firma eşlemesi → platform varsayılanı. `definition` kuralı: varsayılanı yalnız geliştirici firma yazar (K16 açık).
- Readiness hesaplayıcısı çözümleme zincirini kullanır; mevcut firma kayıtları değişmez (additive).
- Grup/özellik/değer eşlemesi değişince etkilenen ürünlerin hazırlığı olay tabanlı yeniden hesaplanır (K15).

---

## 9. Uygulama fazları (her faz bağımsız deploy edilebilir, additive migration)

| Faz | Kapsam | Çıktı / kabul kriteri | Bağımlılık |
|---|---|---|---|
| **F0 Yetenek modeli** | `PlatformType.Capabilities` + seed + kanal override; `IsMarketplace` türetilmiş; admin kanal formunda yetenek görünümü (salt okunur + ezilebilir alanlar) | Tüm mevcut kanallar bugünkü davranışı korur; Satış Kanalları sayfasında yetenek rozetleri | — |
| **F1 Kapsam** | `FirmPlatform.ProductFilterDef/FillType/MinStock`, kural şemasına `SourceTypes/HasChannelPrice/MinStock`, `SyncChannelScope` komutu + olay/gece tetik, `channel_products` InScope/ScopeSource/IsExcluded; storefront + pazaryeri aday okuma **tek kümeye** geçer; kapsam sync sonrası kanal cache invalidation; `IChannelStockCalculator` (K17); Kapsam sekmesi | **Kabul:** `all` + legacy → storefront ürün kümesi değişmez (sayı karşılaştırma testi kanal başına); pazaryeri aday listesi kapsam kümesine eşit; filtre kaydet→sync→sayı önizleme ✓ | F0 |
| **F2 Listeleme durumu** | `ComputeListingStatus` + denormalize alanlar + readiness `blocked` + sebep kataloğu + "Düzelt" hedef tablosu; yeniden hesap tetikleri (K15) | Her kanal tipi için sebepler doğru üretilir (test matrisi: görselsiz/fiyatsız/satış kapalı/eşleme eksik/push hatalı/durdurulmuş) | F1 |
| **F3 Ekran** | Ürünler sekmesi yeni sütun/çip/filtre/çekmece/toplu işlemler; sunucu tarafı "filtreye uyan tümü"; Gönderimler sekmesi; yetkiler; rehber sayfası güncellemesi | K16 site-panel: pazaryeri push/yeniden dene/listeden düşür bu ekrandan; `/marketplaces` ürün sekmesi link verir | F2 |
| **F4 Dropship bayi (Y2)** | `dropship_partner` tipi + varsayılanlar, kanal↔Partner API hesabı bağı, Partner `catalog/stock` uçlarının kanal kapsam/fiyatına göre yanıtı, "şablondan kopyala"; F2b-2d sipariş POST (Dış API alanı) | Bayi token'ı ile katalog çekimi yalnız kapsamı ve kanal fiyatını döner; stok eşiği altı 0; bir negatif: fiyatı olmayan ürün dönmez (K11) | F1, Dış API |
| **F5 Satıcı kaynağı (Y3)** | `Product.SourceType`, satıcı sebepleri, tip varsayılanı `thirdPartySellerProducts`, satıcı paneline durum kesiti | Satıcı ürünü sitede kapsamda, dropship kanalında kapsam dışı (varsayılan); satıcı panelinde sebep görünür | F2, Satıcı paneli |
| **F6 Tedarik kaynağı altyapısı (Y4)** | `ISupplyConnector` + çözümleyici, `supply_sources/records/runs`, **dosya (xlsx/csv) bağlayıcısı** + sütun eşleme profili + elle yükleme, gelen-eşleme (yön alanı), ProductSubmission'a `SupplySourceId`, fiyat/stok kuralı, stale kuralı, panel Tedarik Kaynakları (liste+detay+çalıştırmalar) | Örnek Excel ile uçtan uca: yükle → gönderimler pending → eşle → onayla → ürün `SourceType=supply` → kanal kapsamı → yayında; ikinci yükleme yalnız değişenleri günceller (hash); stale → sebep | F2, F5 |
| **F7 Genel HTTP bağlayıcı + sipariş iletimi** | `http_json_generic` (URL+kimlik+JSON yol eşlemesi, zamanlayıcı), `PushOrderAsync` outbox + sipariş dosyası alternatifi, durum geri okuma | Sahte uç nokta ile katalog+stok çekimi; sipariş onayı → outbox → push/dosya; tekrar deneme | F6 |
| **F8 Eşleme sahipliği** | Platform-geneli varsayılan eşleme + çözümleme zinciri + definition kuralı (K16), olay tabanlı yeniden hesap | Yeni firma aynı pazaryerinde eşleme yapmadan readiness `ready` alabilir | F2 |

Sıra önerisi: F0 → F1 → F2 → F3 (kanal ürünleri ekranı hedefi) → F4/F5 (kaynaklar/yönler) → F6 → F7 → F8 (F8 F3'ten sonra herhangi bir ara
boşluğa alınabilir). Somut bir dış API geldiğinde F6 tamamsa yalnız `<firma>_api` bağlayıcı sınıfı yazılır.

---

## 10. Açık sorular — KAPATILDI (2026-08-23 kullanıcı yanıtları)
1. K11-K16 öneriler kabul edildi (§1 tablosu güncellendi).
2. Kanal stok hesabı → **K17**: `netStock` = satış kanallarına açık (`WarehouseSection.IsSellableOnline`) depo/kısım stoğu − rezerv; `stockQuantity = max(0, netStock − minStock + 1)`. Mağaza depoları kendi POS satışını sürdürür; bayrak açılırsa dış kanallara da sayılır. Uygulama: Inventory'de tek `IChannelStockCalculator` (kanal minStock parametreli), F1'de kapsam kriteri, F4'te Partner stok yanıtı, pazaryeri stok batch'inde aynı çağrı.
3. Bayi fiyat politikası → **K18** ayrı çalışma.
4. Y4 görselleri dış URL'den indirilip bizde saklanır (varsayılan) — kabul.
5. Önbellek notu (teknik, kullanıcı kararı gerektirmez): `ListingStatus` yalnız admin okur, storefront DTO'larına girmez → Redis sürüm artırımı gerekmez; F1'de storefront görünürlük kümesi değiştiği için kanal ürün/facet cache'leri (channelcat vb.) sync sonrası **invalidate edilir** — uygulama notu olarak F1'e eklendi.

## 11. Riskler ve sınırlar
- F1 storefront okuma yolunu değiştirir — en riskli adım; kabul testi "kanal başına ürün sayısı eşit" olmadan canlıya çıkmaz; izole 5051'de ölçülür.
- Kapsam materyalizasyonu 28K ürün × kanal satır üretir (mishar+telemania+…): indeks `(FirmPlatformId, InScope, IsActive)`; sync toplu SQL, `ANALYZE`.
- Pazaryeri adapter'ları (Trendyol gerçek istemci, Amazon) dışındakiler stub — katman 3 "push" sebepleri yalnız gerçek adapter'larda dolar.
- Y4 için somut API yok; F6 dosya bağlayıcısı ile uçtan uca kanıtlanır, genel HTTP bağlayıcı "kodsuz" iddiasının sınırı: sayfalama/kimlik doğrulama çeşitliliği (OAuth vb.) → firmaya özel sınıf.
- Satıcı paneli ve Dış API dokunuşları kendi alanlarının fazlarıdır; bu dokümanda yalnız arayüz sözleşmesi yazılır.
