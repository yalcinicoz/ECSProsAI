# Ürün URL'i / Satış Kanalı Mimarisi — Analiz ve Yapılacaklar

> Durum: **Analiz tamamlandı, uygulama yapılmadı.** Bu doküman `dfplatforms`, `plentegreservisler`, `plurunler`
> (eski MySQL, `juludedb`) tablolarının incelenmesi sonucu çıkan bulguları ve yeni sistemde/migration'da
> yapılması gereken değişiklikleri listeler. [[project_product_url_migration_paused]] oturumunda duraklatılan
> işin devamıdır.
>
> **2026-07-04 revizyonu (mimari düzeltme):** Kullanıcı kanal-özel ürün verisinin (fiyat override, URL/SEO,
> yayın durumu) `Catalog` şemasında değil **`Storefront` şemasında** olması gerektiğini belirtti — bu bilgi
> tamamen satış kanalına özel, ve bir firmanın birden çok satış kanalı (site, pazaryeri, bayi) olabilir.
> §3 ve §4 bu doğrultuda güncellendi; §3.1 artık mevcut `Catalog.FirmPlatformProduct`/`FirmPlatformVariant`'ın
> `Storefront`'a taşınmasını da kapsıyor.
>
> **2026-07-04 — §3.1-3.3'teki tablo taşıma UYGULANDI** (bu bölümdeki plurunler/URL/SEO alanları henüz
> eklenmedi — o kısım hâlâ §5'teki açık sorulara bağlı). Yapılanlar:
> - `Catalog.FirmPlatformProduct`/`FirmPlatformVariant` silindi; `catalog.firm_platform_products`/
>   `firm_platform_variants` tabloları migration ile drop edildi (0 satırdı, veri kaybı yok).
> - `Storefront.ChannelProduct`'a `NameI18n`/`ShortDescriptionI18n` eklendi; yeni `Storefront.ChannelVariant`
>   entity'si (`storefront.channel_variants` tablosu) eski `FirmPlatformVariant`'ın fiyat alanlarını taşıyor.
> - `SetFirmPlatformVariantPriceCommand`/`GetFirmPlatformPricingQuery` (Catalog) → `SetChannelVariantPriceCommand`/
>   `GetChannelVariantPricingQuery` (Storefront) olarak taşındı; API rotaları `CatalogController`'dan
>   `NavigationController`'a taşındı (`/api/navigation/channel-variants/...`). Admin `ProductDetailPage.tsx`
>   güncellendi.
> - **Yeni bulgu / mimari çözüm:** `GetStoreProductsQuery`/`GetStoreProductGroupProductsQuery`/
>   `GetStoreProductDetailHandler`/`ProductFilterHelper` (hepsi Catalog.Application veya Api katmanında, ama
>   platforma özel fiyatı okuyorlardı) — Catalog, Storefront'a proje referansı veremeyeceği için (döngüsel
>   referans olurdu) yeni bir **`IChannelPricingService`** portu eklendi (`Shared.Contracts`, `IStockService` ile
>   aynı desen), Storefront.Infrastructure'da `StorefrontChannelPricingService` ile implement edildi ve DI'a
>   kaydedildi. Bu servis ileride gerçek kanal fiyatı okuyan her yerde kullanılmalı — doğrudan
>   `IStorefrontDbContext`'e Catalog'dan erişmeye çalışmayın, döngüsel referans hatası verir.
> - `VariantPriceHistory` (Catalog) kasıtlı olarak taşınmadı (§3.3'teki karar) — `SetChannelVariantPriceCommand`
>   hem `IStorefrontDbContext` hem `ICatalogDbContext`'i inject ediyor, iki ayrı `SaveChangesAsync` çağrısı var
>   (tek transaction değil — kodun geri kalanında zaten var olan bir sınırlama, yeni bir risk eklenmedi).
> - `TestDataSeeder.cs` (Development, `AdminController` üzerinden erişilebilir ama Program.cs'te otomatik
>   çağrılmıyor — dead-ish kod) güncellendi; bu dosyanın TRUNCATE listesinde başka önceden var olan hatalı
>   tablo adları (`catalog_category_products` vb., "catalog_" önekiyle — gerçek tablo adları önek almıyor)
>   fark edildi ama bu görevin kapsamı dışında bırakıldı, düzeltilmedi.
>
> **2026-07-04 — Faz 14 çalıştırıldı: gerçek Firma/Site + `plurunler` aktarımı UYGULANDI.**
> `tools/MigrationTool/Program.cs`'e `Phase14_FirmsAndChannelData` eklendi ve production'da çalıştırıldı
> (`dotnet run -- 14`):
> - Seed/demo `Firm`/`FirmPlatform` (Code'u `misaroglu`/`eldi` dışında olan her şey — demo'nun 2 firması +
>   7 platformu) silindi, bağlı storefront demo verisi (`nav_menus`, `channel_categories`,
>   `channel_product_groups`, FirmPlatformId'ye göre) de temizlendi.
> - 2 gerçek firma upsert edildi: `misaroglu` (Mişaroğlu Tekstil, TaxNumber/TaxOffice `dfinvoiceinfo` id=10'dan),
>   `eldi` (Eldi Tekstil, `dfinvoiceinfo` id=8'den).
> - 3 gerçek `FirmPlatform` (hepsi `PlatformType=site`): `tozlu`→misaroglu (`tozlu.com`), `julude`→eldi
>   (`julude.com`), `mishar`→misaroglu (`misharitalia.com`). Domain, `FirmPlatform.Settings` JSONB'sine
>   `{"domain": "..."}` olarak yazıldı (ayrı bir `Domain` kolonu eklenmedi — §3.2 madde 3'teki eksik hâlâ
>   açık, bu geçici bir çözüm).
> - `plurunler` (platformId IN 1,2,41) → her platform için **361.907 `ChannelVariant`** (Price=satisFiyati>0
>   ise, CompareAtPrice=listeFiyati farklıysa, IsActive=`satista` biti) + **117.495 `ChannelProduct`**
>   (o platformda en az bir plurunler satırı olan her ürün, IsActive=true — "kanala atanmış" anlamında,
>   asıl satılabilirlik ChannelVariant.IsActive'de). ~620 satır/platform atlandı (eşleşen ürün/varyant
>   `productMap`/`variantMap`'te yoktu — 74 orphan ürün + birkaç eşleşmeyen varyant, §5 madde 3'teki
>   sorunun aynısı).
> - **`yayinda` bilinçli olarak kullanılmadı** — veri incelemesi (§2.3) `yayinda`'nın platform 1/2'de neredeyse
>   hep 0 olduğunu, sadece platform 41'de tutarlı şekilde 1 olduğunu gösteriyordu; `satista` üç platformda da
>   tutarlı ve anlamlı bir "satışa açık" sinyaliydi, `yayinda` ise `int` (0/1/2 değerler görüldü) — muhtemelen
>   ayrı bir durum alanı, basit bir yayın bayrağı değil. Bu nedenle §5 madde 2 fiilen `IsActive=satista` olarak
>   çözüldü, ayrı bir `IsPublished` alanı eklenmedi.
> - Faz idempotent — Firma/Platform Code'a göre upsert, ChannelProduct/ChannelVariant
>   `(FirmPlatformId,ProductId/VariantId)` unique index'ine göre `ON CONFLICT DO UPDATE`.
>
> **Hâlâ yapılmadı** (kapsam dışı bırakıldı, gerekirse ayrı iş): Slug/SEO/robots/`urunEtiketi` alanları
> (ChannelVariant şemasına hiç eklenmedi), KDV override, stok eşiği, `FirmPlatform.Domain` gerçek kolonu
> (şu an sadece Settings JSONB'de), Mishar'ın hangi tüzel kişilik üzerinden fatura keseceği sorusu (kod bu
> migration için invoiceInfoId=10/Mişaroğlu'nu kullandı, id=8/Eldi'yi DEĞİL — dfplatforms'taki tutarsızlığı
> göz ardı edip `firma` metin alanındaki yasal unvana güvenildi).

## 1. Kapsam kararı

- Aktif platformlar: **1 (Tozlu), 2 (Julude), 41 (Mishar)** — `dfplatforms.SiteType = 'ECS'`, `status = 1`.
- Bunlar **2 gerçek firma / 3 site**:
  - **Mişaroğlu Tekstil Paz. Dağıtım San. ve Tic. Ltd. Şti.** → platform 1 (tozlu.com) + platform 41 (misharitalia.com)
  - **Eldi Tekstil San. ve Tic. A.Ş.** → platform 2 (julude.com)
- Pazaryerleri (Trendyol, Hepsiburada, N11, Çiçeksepeti, Pazarama, PttAVM, bayilik vb. — `dfplatforms`'ta ~28 satırın geri kalanı, `SiteType IN ('PAZARYERI','BAYILIK')`) **şimdilik aktarılmıyor**.

## 2. Eski şema bulguları

### 2.1 `dfplatforms` (31 satır, kendi sitelerimiz dahil tüm platformlar)
Firma/site tanımı. İlgili sütunlar: `name`, `firma` (yasal unvan, serbest metin), `webSite`, `SiteType`,
`status`, `dilKodu`, `currency`, `invoiceInfoId`/`invoiceNumberId` (FK → `dfinvoiceinfo`).

| Id | name | firma | webSite | invoiceInfoId |
|----|------|-------|---------|----------------|
| 1 | Tozlu | MİŞAROĞLU TEKSTİL ... | tozlu.com | 10 |
| 2 | Julude | ELDİ TEKSTİL SAN. VE TİC A.Ş. | julude.com | 8 |
| 41 | Mishar | MİŞAROĞLU TEKSTİL ... | misharitalia.com | 8 (muhtemelen veri hatası — firma metni Mişaroğlu diyor ama invoiceInfoId Eldi'nin kaydını gösteriyor; gerçek fatura kesimi Tozlu üzerinden yapılıyor olabilir, teyit gerekir) |

`dfinvoiceinfo` (id 8 = Eldi, id 10 = Mişaroğlu) firma vergi bilgilerini taşıyor: `taxNumber`, `taxOffice`,
`mersisNumber`, `ticaretSicilNumber`, adres alanları. Bunlar bizim `Firm` entity'sindeki
`TaxOffice`/`TaxNumber`/`Address` alanlarını gerçek veriyle doldurmak için kullanılabilir.

### 2.2 `plentegreservisler` (platform başına entegrasyon kimlik bilgileri)
Platform 1/2/41 için `tip` değerleri: `FATURA`, `MAIL`, `SİTE`, `GSM`, `OTP` (Julude'de sadece MAIL+GSM var,
site/fatura/otp kayıtları yok — muhtemelen Tozlu ile ortak altyapı kullanıyor). Bizim `FirmIntegration`
entity'sinin karşılığı; ürün URL'i işiyle doğrudan ilgisi yok ama firma/platform seed'i tam yapılacaksa
bu bilgiler de (mail sunucusu, SMS/OTP servisi vb.) referans olarak kullanılabilir. **Bu iş için düşük öncelik.**

### 2.3 `plurunler` (platform × ürün × varyant bazlı yayın kaydı — asıl konu)
Birincil anahtar mantığı `(platformId, urunId, urunAnaVaryantId)` — bu üçlüde **duplicate yok** (kontrol edildi).

- `urunId` → mevcut `MigrationTool` içindeki `productMap` ile `catalog.products` eşleşiyor.
- `urunAnaVaryantId` → `apurunvaryantlari.Id` ile aynı; `MigrationTool.Phase6_Variants` zaten bu ID'yi
  `variantMap[oldId] = newVariantGuid` şeklinde eşliyor. Yani **URL, ürün değil VARYANT (renk) bazında**
  tutuluyor — legacy'deki "ana varyant" bizim `ProductVariant`'ın tam karşılığı.
- Önemli sütunlar:
  - `urunUrl` → slug (site domaininden sonraki relative path, örn. `pastel-silky-dream-fondoten-196301`)
  - `metaTitle`, `metaDescription` → SEO başlık/açıklama
  - `metaIndex`/`metaFollow`/`metaArchive` → robots meta; **sabit değil**, platform 2 ve 41'de satırların
    ~%75-80'i `noindex/nofollow/noarchive` (muhtemelen stok dışı/pasif ürünler), platform 1'de tamamı
    `index/follow/archive`. Gerçek veri, sabitlenmemeli.
  - `satisFiyati`, `listeFiyati`, `bayiFiyati`, `kdvOrani` → platforma özel fiyat/KDV override'ı.
    `satisFiyati ≠ listeFiyati` olan satır sayısı platform 1/2'de ~68K (indirim gösterimi), platform 41'de ~0.
    `kdvOrani` platformlar arası 0/1/10/20 olarak değişiyor (ürüne göre, platforma göre değil — üç platformda
    dağılım hemen hemen aynı).
  - `satista` (satışa açık) ve `yayinda` (yayında) — **iki bağımsız bit**, birbirinin aynısı değil
    (örn. platform 1'de 342.317 satır `satista=1, yayinda=0`; sadece 20.202 satır her ikisi de 1).
  - `stokAdedi`, `enAzStok`, `enAzFiyat` → stok/fiyat eşik kuralları (Inventory modülüyle örtüşüyor, bu iş
    kapsamı dışında bırakılabilir).
  - `platformKategoriId` → platform 1/2/41 için **hep NULL** (marketplace kategori eşleşmesi; kendi
    sitelerimizde kullanılmıyor, pazaryeri aktarımı yapılmadığı için önemsiz).
  - `urunEtiketi` → rozet ikonu path'i (`/img/badge/iade-edilemez.png`, `/img/badge/haftanin-yildizi.png`) —
    çok az kullanılan (2 farklı değer), düşük öncelik.
  - `siraNo` → platforma özel sıralama.
- **Kapsam/eksik veri**: `apurunler` toplam 117.614 üründen **74 tanesi** her üç platformda da `plurunler`
  kaydına sahip değil (aynı 74 ürün, üç platformda da). Bu ürünler için URL alanı boş kalacak — fallback
  gerekiyor (ör. `Product.Slug`'a düşme ya da kanalda hiç yayınlanmamış say).
- `urunUrl` sadece 1 satırda NULL/boş (platform başına) — ihmal edilebilir.

## 3. Mimari düzeltme: kanal-özel ürün verisi `Catalog`'da değil `Storefront`'ta olmalı

### 3.0 Neden

`Catalog` modülü ürünün **kendisini** (kod, isim, fiyat, varyant, özellik) tanımlar — kanaldan bağımsız,
tek bir gerçek. Hangi ürünün hangi satış kanalında (site/pazaryeri/bayi) hangi URL'le, hangi SEO metasıyla,
hangi fiyat override'ıyla, yayında mı/satışta mı olarak göründüğü ise **tamamen o kanala özel bir karar** —
bir firma onlarca kanala açık olabilir, her kanalın kendi yayın kuralı vardır. Bu, `Catalog`'un değil
`Storefront`'un sorumluluğu. `Storefront` zaten bu ayrımı `ChannelCategory`/`ChannelProductGroup` ile
(kategori ve grup seviyesinde) uyguluyor; şu an eksik olan **ürün ve varyant seviyesindeki** karşılığı.

### 3.1 Mevcut durum — taşınması gereken iki entity

`Catalog` modülünde hâlâ bu ayrıma aykırı iki entity var (production'da **her ikisi de 0 satır** —
veri taşıma riski yok, saf bir şema/kod taşıma işi):

| Entity | Konum | Tablo | Alanlar |
|---|---|---|---|
| `FirmPlatformProduct` | `Catalog.Domain.Entities` | `catalog.firm_platform_products` | `FirmPlatformId`, `ProductId`, `NameI18n?`, `ShortDescriptionI18n?`, `IsActive` |
| `FirmPlatformVariant` | `Catalog.Domain.Entities` | `catalog.firm_platform_variants` | `FirmPlatformId`, `VariantId`, `PriceType?`, `PriceMultiplier?`, `Price?`, `CompareAtPrice?`, `IsActive` |

Storefront'ta zaten bunların ürün-seviyesi kuzeni var ama eksik alanlarla: `Storefront.ChannelProduct`
(`storefront.channel_products`: `FirmPlatformId`, `ProductId`, `IsActive`, `SortOrder` — isim/açıklama
override'ı yok). **Varyant seviyesinde Storefront'ta hiçbir şey yok.**

### 3.2 Hedef tasarım

1. **`ChannelProduct`** (Storefront, mevcut entity genişletilecek) → `NameI18n`/`ShortDescriptionI18n`
   eklenir (Catalog'daki `FirmPlatformProduct`'tan taşınır). `Catalog.FirmPlatformProduct` tamamen kaldırılır.
2. **`ChannelVariant`** (Storefront, yeni entity) → hem mevcut `Catalog.FirmPlatformVariant`'ın fiyat
   alanlarını hem de `plurunler`'dan gelen URL/SEO/yayın alanlarını taşır:
   - `FirmPlatformId`, `VariantId` (Catalog'a serbest Guid referans — Storefront zaten `ChannelProduct.ProductId`'de
     bu şekilde çalışıyor, FK/nav property yok, `ICatalogDbContext` üzerinden Application katmanında okunuyor;
     bu iş için de aynı kalıp izlenmeli)
   - `PriceType`, `PriceMultiplier`, `Price`, `CompareAtPrice` (mevcut `FirmPlatformVariant`'tan aynen taşınır)
   - `Slug` (+ `(FirmPlatformId, Slug)` unique index)
   - `MetaTitleI18n`, `MetaDescriptionI18n` (Storefront'ta zaten `ChannelCategory`/`NavNode` üzerinde aynı
     desen var — `SeoTitleI18n`/`SeoDescriptionI18n` isimlendirmesiyle tutarlı yapılabilir)
   - Robots alanları (`MetaIndex`/`MetaFollow`/`MetaArchive` ya da tek `Robots` string — veri gerçekten
     değişiyor, sabit yazılmamalı, bkz. §2.3)
   - `SortOrder` (`plurunler.siraNo`)
   - **`IsForSale`ve `IsPublished`** iki ayrı bool (`satista`/`yayinda` bağımsız — mevcut tek `IsActive`
     yetersiz)
   - `(FirmPlatformId, VariantId)` unique index (mevcut `FirmPlatformVariant`'taki gibi)
3. **`FirmPlatform` (Core) üzerinde site domaini alanı yok** — bu değişmedi, hâlâ eksik. URL'i
   (`https://{domain}/{slug}`) kurabilmek için `Domain`/`BaseUrl` eklenmeli. Bu alan Core'da kalır (firma/
   platform tanımının bir parçası, kanal-özel ürün verisi değil).
4. **KDV override** (`kdvOrani`) ve stok eşiği (`enAzStok`/`stokAdedi`) — bu iterasyonda atlanıyor (§2.3'te
   olduğu gibi); eklenirse yeri de `Storefront.ChannelVariant` olur, `Catalog` değil.
5. **`Firm` gerçek verisi** hâlâ eksik — Core modülünde kalır, değişmedi (bkz. eski §5 karar maddeleri).

### 3.3 Etkilenen kod — tam envanter (taşınacak/güncellenecek)

Mevcut `Catalog.FirmPlatformProduct`/`FirmPlatformVariant`'ı kullanan her yer (kod taşındığında güncellenmeli):

- **Domain**: `FirmPlatformProduct.cs`, `FirmPlatformVariant.cs` (silinecek/Storefront'a taşınacak) — `Product.cs`
  ve `ProductVariant.cs`'teki `FirmPlatformProducts`/`FirmPlatformVariants` nav collection'ları kaldırılır.
- **Application (Catalog)**: `ICatalogDbContext` içindeki iki `DbSet` kaldırılır;
  `Commands/SetFirmPlatformVariantPrice/SetFirmPlatformVariantPriceCommand.cs` ve
  `Queries/GetFirmPlatformPricing/GetFirmPlatformPricingQuery.cs` → Storefront.Application'a taşınır
  (örn. `SetChannelVariantCommand`, `GetChannelVariantPricingQuery`), `IStorefrontDbContext.ChannelVariants`
  üzerinden çalışacak şekilde yeniden yazılır.
- **Infrastructure (Catalog)**: `CatalogDbContext`'teki iki `DbSet`, `CatalogConfigurations.cs`'teki
  `FirmPlatformProductConfiguration`/`FirmPlatformVariantConfiguration` kaldırılır; yeni bir migration
  (`catalog.firm_platform_products`/`firm_platform_variants` tablolarını `DROP`) eklenir. Storefront
  tarafında karşılık gelen migration (`ChannelProduct`'a kolon ekleme + yeni `channel_variants` tablosu
  `CREATE`) eklenir.
- **`VariantPriceHistory`** (Catalog) — `base_price`/`base_cost` (Catalog'un kendi) değişiklikleriyle
  `platform_price` (kanal override) değişikliklerini `PriceType` ayrımcı sütunuyla tek tabloda tutuyor.
  Bu **taşınması zorunlu değil** (esasen "bu varyantın tüm fiyat geçmişi" — modül sınırı ihlali gibi
  görünse de pratikte tek sorgu noktası avantajı var); ama tam modül ayrımı isteniyorsa `FirmPlatformId`
  taşıyan satırların Storefront'a taşınması ayrı bir karar — §5'e açık soru olarak eklendi.
- **API**: `CatalogController.cs`'teki `GET firm-platforms/{platformId}/products/{productId}/pricing` ve
  `PUT firm-platforms/{platformId}/variants/{variantId}/price` endpoint'leri Storefront'a taşınmalı (yeni/
  mevcut bir Storefront controller'ına, örn. rota `channels/{firmPlatformId}/variants/{variantId}` gibi);
  `Handlers/GetStoreProductDetailHandler.cs` artık `ICatalogDbContext.FirmPlatformVariants` yerine
  `IStorefrontDbContext.ChannelVariants` okuyacak şekilde güncellenmeli; `Extensions/TestDataSeeder.cs`
  seed kodu yeni entity'lere göre güncellenmeli.
- **Admin (`admin/src`)**: `pages/catalog/ProductDetailPage.tsx` (satır ~655, ~696) — kanal fiyatı
  okuma/yazma çağrıları yeni API rotasına taşınmalı; ileride aynı ekrana Slug/SEO/yayın-durumu alanları
  için yeni UI eklenmesi gerekecek (bu iterasyonun kapsamı dışında, ayrı görev).

### 3.4 Migration Tool'a etkisi

§4.2'deki `plurunler` → hedef tablo artık `catalog.firm_platform_variants` değil,
**`storefront.channel_variants`** (yeni entity) olacak; `productMap`/`variantMap` eşlemesi aynı kalır,
sadece INSERT hedefi değişir. §4.1'deki Firm/FirmPlatform seed'i zaten Core modülünde, değişmiyor.

## 4. Migration Tool'da yapılması gerekenler

`tools/MigrationTool/Program.cs` şu an `dfplatforms`/`plentegreservisler`/`plurunler` tablolarına hiç
dokunmuyor (grep sonucu boş). Gerekli yeni fazlar:

### 4.1 Yeni faz — Firm + FirmPlatform seed
- 2 sabit `Firm` kaydı (Mişaroğlu Tekstil, Eldi Tekstil) — kod/GUID migration'a hardcode edilir (dış
  sistemden sürekli değişmeyecek sabit veri, ID eşlemesi gerekmiyor çünkü zaten 2 tane).
- 3 sabit `FirmPlatform` kaydı (tozlu.com → Mişaroğlu, misharitalia.com → Mişaroğlu, julude.com → Eldi),
  yeni `Domain` alanıyla birlikte.
- Idempotent olmalı (yeniden çalıştırılabilir) — `Code` üzerinden upsert.

### 4.2 Yeni faz — `plurunler` → `Storefront.ChannelVariant` (platform 1, 2, 41 filtreli)

> Not (2026-07-04 revizyonu): Hedef tablo `catalog.firm_platform_variants` değil, artık
> `storefront.channel_variants` (§3.2). Aşağıdaki eşleme mantığı aynı, sadece hedef entity/tablo değişti.
- `WHERE platformId IN (1,2,41)`, `productMap`/`variantMap` ile eşle (bulunamayan `urunId`/`urunAnaVaryantId`
  atlanır, sayaç loglanır — 74 ürün + migrate edilmemiş varyantlar için beklenen davranış).
- Diğer migration fazları gibi **batch upsert / re-runnable** olmalı (kullanıcı bu migration'ın birkaç kez
  daha çalıştırılacağını belirtmişti — `DELETE FROM ... WHERE FirmPlatformId = @id` + yeniden insert veya
  `(FirmPlatformId, VariantId)` unique constraint üzerinden upsert).
- Alan eşlemesi:
  - `urunUrl` → `Slug`
  - `metaTitle`/`metaDescription` → `MetaTitleI18n["tr"]`/`MetaDescriptionI18n["tr"]`
  - `metaIndex`/`metaFollow`/`metaArchive` → robots alanları (gerçek değerle, sabit değil)
  - `satisFiyati` → `Price`, `listeFiyati` → `CompareAtPrice` (eşitse `CompareAtPrice = null`)
  - `satista` → `IsForSale`, `yayinda` → `IsPublished` (yeni alanlar, §3.2)
  - `siraNo` → `SortOrder`
  - `kdvOrani`, `stokAdedi`, `platformKategoriId`, `urunEtiketi` → **bu iterasyonda taşınmıyor** (kapsam dışı, yukarıda not edildi)
- 74 üründeki eksik `plurunler` kaydı için: o ürün/varyantlar için `FirmPlatformVariant` satırı hiç oluşturulmaz
  → panelde/storefront'ta o platformda "yayında değil" gibi davranır. `Product.Slug` fallback'i ile URL
  üretilip üretilmeyeceği ayrı bir ürün kararı (bkz. §5).

## 5. Kullanıcıya sorulması/karar bekleyen noktalar

1. Mishar (platform 41) için fatura hangi tüzel kişilik üzerinden kesiliyor — `dfinvoiceinfo` id 8 (Eldi) mi,
   yoksa gerçekte id 10 (Mişaroğlu) mü kullanılmalı? Legacy veride tutarsızlık var.
2. `IsForSale`/`IsPublished` ayrımı gerçekten ürün tarafında bir anlam taşıyor mu, yoksa migration'da
   `IsActive = satista AND yayinda` gibi tek bir bayrağa mı indirgensin?
3. 74 üründe hiçbir platformda `plurunler` kaydı yok — bu ürünler pasif/silinmiş mi, yoksa gerçekten
   yayınlanmamış mı? `Product.Slug`'a düşülsün mü yoksa hiç URL üretilmesin mi?
4. KDV override (`kdvOrani`) ve stok eşiği (`enAzStok`) bu iterasyonda gerçekten atlanabilir mi, yoksa
   şimdiden şemaya eklensin mi (ileride ayrı migration + backfill gerektirir)?
5. Robots meta (`noindex` oranı yüksek — platform 2/41'de satırların çoğu `noindex`) — bu genelde
   "artık satılmıyor ama URL sabit kalsın" durumu mu? Storefront tarafında bu meta'nın nasıl render
   edileceği (sayfa `<meta name="robots">` etiketine mi yansıyacak) ayrı bir görev.
6. **(Yeni)** `VariantPriceHistory` (Catalog) `platform_price` satırları da Storefront'a taşınsın mı, yoksa
   "bu varyantın tüm fiyat geçmişi tek yerde" pratikliği için Catalog'da mı kalsın? (§3.3)
7. **(Yeni)** `SetFirmPlatformVariantPriceCommand`/`GetFirmPlatformPricingQuery` ve ilgili
   `CatalogController` endpoint'lerinin Storefront'a taşınması — mevcut admin UI (`ProductDetailPage.tsx`)
   bu endpoint'leri kullanıyor; taşıma sırasında API rotası değişecek, admin tarafında da güncelleme
   gerekecek. Bu değişiklik ayrı bir uygulama adımı olarak mı yapılsın, yoksa Slug/SEO alanlarıyla
   birlikte tek seferde mi?
