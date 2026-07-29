# Sipariş No + Paket No + Kargo Entegrasyon Kodu — İş Planı

**Tarih:** 2026-07-19 · **Durum:** F1–F5 UYGULANDI (2026-07-19). Migration'lar dev DB'ye uygulandı; publish alındı, restart bekliyor. Uygulama notları: paket no serisi `fulfillment.ful_package_number_series` tablosundadır (plandaki `order.package_number_series` yerine — paket Fulfillment modülünün varlığı olduğu için); seri/aralık yönetim ekranı `/orders/number-series`.

## 1. Gereksinim Özeti

1. Sipariş numarası tek başına unique değil; siparişler farklı satış kanallarından
   (FirmPlatform) geldiği için numara çakışabilir. **Unique anahtar:
   (Satış Kanalı) + (Sipariş No) + (Paket No)** olmalı.
2. Sipariş numaraları **kanala özel serilerden** üretilmeli: makul uzunlukta numerik,
   kanala özel **karakter öneki** tanımlanabilmeli.
3. **Pazaryeri** siparişlerinde numara üretilmez; pazaryerinin verdiği sipariş
   numarası aynen kullanılır.
4. Farklı **tedarikçilerin** ürünleri tek siparişte olabilir → sipariş **paketlere
   bölünür**; fatura ve kargo süreçleri paket bazında izlenir. Müşteri tek sipariş
   görür, operasyon paketleri ayrı ayrı görür.
5. Her paketin bir **kargo entegrasyon kodu** olur; üretim kuralı **kargo şirketine
   özel**: (a) tamamen serbest, (b) uzunluk/format kurallı, (c) tahsisli barkod
   aralığından (PTT), (d) pazaryerinin verdiği kod aynen yazılır.
6. Paket no ve kargo kodu, **paket güncellenince değişebilir** → eski kodların izi
   (geçmiş) tutulmalı.

## 2. Mevcut Durumun Tespiti

| Alan | Bugünkü hali | Sorun |
|------|--------------|-------|
| `Order.OrderNumber` | `ORD-yyyyMMdd-<random>` — 3 ayrı handler'da kopyalı üretim (CreateOrder, Checkout, ConvertQuoteToOrder) | Seri yok, kanal bağımsız, random; unique index yok |
| `Order.FirmPlatformId` | Var | Satış kanalı kavramı hazır — seri buna bağlanır |
| `Shipment` (order şeması) | Kargo API gönderi kaydı; `TrackingNumber` serbest string | Kod üretim kuralı/serisi yok |
| `Package` (fulfillment) | `PackageNumber (int)`, `Barcode (string)`, `ShipmentId?` | Tedarikçi bazlı bölme yok, numara/barkod üretim kuralı yok, geçmiş yok |
| `OrderItem` | Tedarikçi bilgisi yok (yalnız `Product.SupplierId` var) | Paket bölme için kalem üzerinde tedarikçi snapshot'ı gerekli |
| `InvoiceSeries` | Kanal bazlı fatura serisi zaten var | Sipariş no serisi için hazır kalıp — aynı desen kullanılacak |
| Kargo tanımları | `definition.integration_services` (kargo taşıyıcı alanları mevcut) + `core_firm_platform_integrations` + `CargoRule` | Kod üretim stratejisi/aralık havuzu tanımı yok |

## 3. Tasarım

### 3.1 Sipariş Numarası Serileri (kanala özel)

Yeni tablo **`order.order_number_series`** (InvoiceSeries kalıbı):

```
Id, FirmPlatformId (unique), Prefix (örn. "MS"), PadLength (örn. 7),
NextValue (long), IsActive
```

- Üretim: `Prefix + NextValue.ToString().PadLeft(PadLength,'0')` → örn. `MS0001042`.
- **Atomiklik:** `UPDATE ... SET next_value = next_value + 1 RETURNING next_value`
  (tek SQL, yarış koşulu yok). Aynı anda iki checkout aynı numarayı alamaz.
- Kanalın serisi yoksa güvenli varsayılan seri otomatik açılır (prefix = kanal kodu).
- **Pazaryeri kanalları:** seri kullanılmaz; `Order.OrderNumber` = pazaryerinin
  numarası, ayrıca `Order.ExternalOrderNumber` alanına da ham hali yazılır
  (kaynak ayrımı `OrderNumberSource: internal | external` alanıyla).
- Üç handler'daki kopyalı üretim tek servise (`IOrderNumberService`) toplanır.

**Unique index:** `orders (FirmPlatformId, OrderNumber) UNIQUE` (soft-delete filtreli).
Mevcut `ORD-...` numaraları zaten unique olduğundan migration sorunsuz geçer;
eski numaralara dokunulmaz (dev verisi zaten geçici — go-live'da temiz aktarım).

### 3.2 Paket Modeli (tedarikçi bazlı bölme + bağımsız paket numarası serisi)

- **`OrderItem.SupplierId` (snapshot)** eklenir — sipariş anında `Product.SupplierId`
  kopyalanır (ürün sonradan tedarikçi değiştirirse sipariş etkilenmez).
- Paketleme anında sipariş kalemleri **tedarikçiye göre gruplanıp** paket önerisi
  oluşturulur; operatör bölmeyi elle de düzenleyebilir (bir tedarikçi 2 paket olabilir).
- **Paket numarası siparişten BAĞIMSIZ, sipariş numarası mantığıyla seriden üretilir**
  (karar 2026-07-19): yeni tablo **`order.package_number_series`** —
  `order_number_series` ile aynı yapı (FirmPlatformId, Prefix, PadLength ≈ 6 hane,
  NextValue, atomik artırım). Sipariş no (6-10 hane) + paket no kaynaştırılmaz;
  ikisi ayrı alanlarda taşınır.
- `Package`'a eklenecek alanlar: `SupplierId?`, `PackageNumber (string — seriden)`,
  `CargoIntegrationCode (string?)`, `CargoIntegrationCodeSource (generated|external)`.
  Mevcut `PackageNumber (int)` sipariş içi sıra olarak kalır (görsel sıralama),
  kimlik olarak kullanılmaz.
- **Unique kurallar:** `(FirmPlatformId, PackageNumber) UNIQUE` (seri zaten kanal
  bazlı ürettiği için doğal sağlanır) + üçlü kimlik
  `(Kanal, SiparişNo, PaketNo)` sorgu/entegrasyon anahtarıdır.
- **Operasyon sorgu akışı:** sipariş numarasıyla arama yapıldığında sipariş birden
  fazla pakete bölünmüşse paket numaraları **seçenek listesi** olarak sunulur,
  personel hangi paketle çalışacağını seçer (tek paketse doğrudan açılır).
- Fatura ve `Shipment` paket(ler)e bağlanır: `Shipment.PackageId?` /
  `Invoice.PackageId?`.
- **Fatura/kargo normali paket başınadır** (karar 2026-07-19): varsayılan akış her
  pakete ayrı fatura + ayrı kargo. "Paketleri birleştir / tek fatura / tek kargo"
  imkânı BULUNUR ama **kolay olmayan, bilinçli bir işlemdir**: ayrı endpoint + ayrı
  permission (`order.package_merge` benzeri) + zorunlu gerekçe alanı + onay diyaloğu;
  işlem `package_code_history`'ye gerekçesiyle yazılır. Normal paketleme ekranında
  öne çıkan bir buton olarak yer almaz.
- **Müşteri görünümü değişmez:** storefront "Siparişlerim" tek sipariş gösterir;
  paket kırılımı yalnız kargo takip satırlarında (paket başına takip linki) görünür.

### 3.3 Kargo Entegrasyon Kodu Üretim Motoru

Taşıyıcı tanımına (`definition.integration_services`, kargo tipi kayıtlar) **kod
stratejisi** alanları eklenir (definition şeması kuralına uygun: yalnız platform
yönetimi doldurur):

| Strateji | Davranış | Örnek |
|----------|----------|-------|
| `free` | Bizim formatımız: `{prefix}{siparişNo}{paketNo}` benzeri şablon | Aras vb. serbest bırakan firmalar |
| `pattern` | Şablon + kural doğrulama: `MinLength/MaxLength/Charset (numeric/alnum)/şablon` | Uzunluk kuralı koyan firmalar |
| `range` | Firma başına tahsisli **barkod aralığı havuzundan** sıradaki değer | PTT Kargo |
| `external` | Kod üretilmez; pazaryeri/taşıyıcı API'sinin döndürdüğü kod DB'ye yazılır | Trendyol vb. |

Yeni tablo **`core.core_cargo_barcode_ranges`** (range stratejisi için):

```
Id, FirmPlatformIntegrationId, RangeStart (long), RangeEnd (long),
NextValue (long), IsActive, ExhaustedAt?
```

- Atomik tahsis (yine `UPDATE ... RETURNING`); aralık biterse sıradaki aktif aralığa
  geçilir, hiç aralık kalmadıysa **anlaşılır hata** döner ("PTT barkod aralığı tükendi,
  yeni aralık tanımlayın") — sessiz fallback yok.
- Aralık doluluk göstergesi panelde görünür (örn. %90 uyarısı).
- Üretilen kod, taşıyıcının `pattern` kuralından da geçirilerek doğrulanır.

### 3.4 Kod Değişikliği ve Geçmiş İzi

Paket güncellenince (içerik/ağırlık değişimi, yeniden paketleme):

- Paket no ve/veya kargo kodu **yeniden üretilebilir**; eski değerler
  **`fulfillment.ful_package_code_history`** tablosuna yazılır:
  `PackageId, OldPackageNumber, OldCargoIntegrationCode, ChangedAt, ChangedBy, Reason`.
- **Hiçbir kod/barkod havuza geri dönmez** (karar 2026-07-19): sipariş no, paket no
  ve kargo kodu serilerinin tümünde iptal/yenileme eski değeri yakar; sayaç asla
  geri alınmaz, aralık stratejisinde barkod tekrar kullanılamaz.
- Kargoya verilmiş (label basılmış / API'ye gönderilmiş) paketin kodu
  değiştirilemez; önce kargo iptali gerekir (durum korumalı).

## 4. Uygulama Fazları

| Faz | İçerik | Dokunulan modüller |
|-----|--------|---------------------|
| **F1** | `order_number_series` tablosu + `IOrderNumberService` + 3 handler'ın buna bağlanması + `(FirmPlatformId, OrderNumber)` unique index + `ExternalOrderNumber`/`OrderNumberSource` alanları | Order |
| **F2** | `OrderItem.SupplierId` snapshot + paket bölme (tedarikçi grubu önerisi + elle düzenleme) + `package_number_series` (bağımsız paket no serisi) + `Package` yeni alanları + unique indexler + Shipment/Invoice paket bağı + paket başına fatura varsayılanı + korumalı "birleştir/tek fatura" istisna akışı | Order, Fulfillment, Catalog (okuma) |
| **F3** | Kargo kod stratejisi (definition alanları) + barkod aralık havuzu + üretim/doğrulama motoru + `external` yazma yolu | Core, Order (Shipment) |
| **F4** | Kod değişiklik akışı + `package_code_history` + durum korumaları | Fulfillment, Order |
| **F5 (K16 — panel)** | Admin ekranları: kanal başına sipariş + paket no serisi tanımı (Sipariş yönetimi alanında); taşıyıcı kod stratejisi alanları (integration-services formu); PTT aralık yönetimi + doluluk göstergesi; sipariş detayında paket kırılımı + paket seçici + kod geçmişi | admin (React) |

Her faz kendi migration'ı ile gelir, tamamı **additive** (mevcut kolon silinmez);
faz bitmeden sonrakine geçilmez.

## 5. Onaylanan Kararlar (2026-07-19, kullanıcı onayı)

1. **Paket no siparişten bağımsız seriden üretilir** (sipariş no mantığıyla,
   ~6 hane). Sipariş no ile kaynaştırılmaz (sipariş no 6-10 hane olabildiğinden
   pratik değil). Operasyonda sipariş numarası sorgulandığında çok paketli
   siparişlerde paket numaraları seçenek olarak sunulur, personel paketi seçer.
2. Mevcut veriler test amaçlı; go-live'da temizlenecek → mevcut `ORD-...`
   numaralarına geri dönük müdahale yok.
3. **Sipariş süreçlerindeki hiçbir kod/barkod havuza geri dönmez** (sipariş no,
   paket no, kargo kodu — tümü).
4. **Normal akış paket başına faturadır.** Paket birleştirme / tek fatura / tek
   kargo imkânı bulunur ama tercih edilen bir durum değildir → kolay olmayan,
   ayrı permission + gerekçe + onay diyaloğu gerektiren istisna işlemi olarak
   tasarlanır.
5. Seri tanımları ve ayarları **şimdilik Sipariş yönetimi alanında**; geliştirme
   bitince modüller arası genel yerleşim düzenlemesi ayrıca yapılacak.

## 6. Bilinen Sınırlar / Kapsam Dışı

- Pazaryeri sipariş **çekme** entegrasyonu (Trendyol API vb.) bu işin kapsamında
  değil; yalnız "dış numarayı kabul etme" altyapısı hazırlanır.
- Kargo firması **API** entegrasyonlarının kendisi (label basma, takip çekme)
  kapsam dışı — kod üretim/saklama katmanı hazırlanır (⏰ hafızadaki
  "kargo entegrasyonu gündeme gelince" notuyla birleşir).
- Fulfillment `Package` ile Order `Shipment.PackageCount` arasındaki eski
  gevşek bağ F2'de gerçek FK'ya dönüştürülür; eski davranış bozulmaz.
