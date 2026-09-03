# ERP Gerçek Kaynak Senkronu ve Geçiş Planı

> Son güncelleme: 2026-09-01  
> Kapsam: ECSProsAI  
> Güvenlik durumu: repository varsayılanları kapalı; production secret repository dışında. Kalıcı ERP katalog/fiyat worker'ı aktiftir. ERP/MSSQL stok yeteneği koddan tamamen kaldırılmıştır.

## 1. Onaylanan geçiş akışı

1. `51.178.208.59` PostgreSQL yalnız başlangıç snapshot/dump kaynağıdır; restore tamamlandıktan sonra yeni sistem bu veritabanından yeni veya değişen kayıt okumaz.
2. Ürün, varyant, özellik ve fiyatın kalıcı gerçek kaynağı doğrudan V3 ERP/SQL Server'dır.
3. Stok production cutover'a kadar production MySQL'den geçici ve salt-okunur snapshot worker ile izlenir; cutover sonrasında tek otorite ECSPros admin panelidir. MSSQL hiçbir zaman stok kaynağı değildir.
4. Üye, sipariş, fatura ve iade production cutover'a kadar production MySQL'den geçici, salt-okunur ve yan etkisiz import edilir.
5. Ürün görsel dosyaları ayrı görsel sunucusu/subdomain üzerinde kalır; API diskine veya PostgreSQL'e dosya yüklenmez. Geçiş döneminde görsel metadata kaynağı production MySQL `apurunresimleri` tablosudur ve SELECT-only bağlantıyla uzlaştırılır.
6. `.59` üzerindeki mevcut senkron servisinin çalışması yalnız mevcut canlı sitenin devamlılığı içindir; yeni PostgreSQL için kaynak veya ara aktarım katmanı değildir.
7. ERP'ye sipariş/fatura/iade gönderen outbound işler yazılır ancak production cutover kararına kadar ayrı bayraklarla kapalı tutulur.

### 1.1 Kesin veri otoritesi matrisi

| Veri alanı | Cutover'a kadar kaynak | Cutover sonrası kaynak | `.59` PostgreSQL rolü |
|---|---|---|---|
| Ürün/katalog/varyant/özellik/fiyat | V3 MSSQL | V3 MSSQL | Yok; yalnız tamamlanmış başlangıç snapshot'ı |
| Stok | Production MySQL | ECSPros admin paneli | Yok |
| Üye | Production MySQL | Yeni ECSPros sistemi | Yok |
| Sipariş/fatura/iade | Production MySQL | Yeni ECSPros sistemi; gerekli outbound akışlar ayrıca açılır | Yok |
| Görsel dosyaları | Mevcut görsel sunucusu/subdomain | Mevcut görsel sunucusu/subdomain | Dosya kaynağı değil |

Yeni veri hattında `.59 PostgreSQL -> yeni PostgreSQL` sürekli veya artımlı senkronu bulunmayacaktır.

## 2. Fazlar ve durum

| Faz | İş | Durum | Kabul kriteri |
|---|---|---|---|
| E0 | Eski Quartz veri yönü ve prosedür envanteri | Tamamlandı | V3→MySQL ve MySQL→V3 işleri ayrıldı. |
| E1 | ERP reader + güvenli ayarlar + worker/lock/checkpoint | **DOĞRULANDI** | SQL Server bağlantısı ve dört procedure doğrulandı; 2 günlük örnek: 20 ürün, 9 varyant, 14 özellik. |
| E2 | ERP katalog/varyant/özellik → PostgreSQL | **AKTİF / KABUL EDİLDİ** | API1 izole worker; dry-run ve iki gerçek tur temiz, ikinci tur idempotent `0`; mapping hatası yok. |
| E3 | ERP fiyat → PostgreSQL | **AKTİF / KABUL EDİLDİ** | Mishar satış=`tozluSatisFiyati`, karşılaştırma=`tozluListeFiyati`; ikinci gerçek tur `0`. |
| E4 | ERP stok → PostgreSQL | **İPTAL / KODDAN KALDIRILDI** | ERP options, reader, model, service ve scheduler içinde stok yolu bulunmaz. |
| E4a | MySQL stock-only → PostgreSQL geçici senkron | **AKTİF / KABUL EDİLDİ** | `WorkerProfile=LegacyStock`; MySQL SELECT-only + READ ONLY snapshot, eksik dump-sonrası eşlemeler tamamlandı, `0` eşleşmeyen, ilk gerçek tur `5.901`, ikinci/final tur `0`. |
| E5 | Harici görsel metadata sözleşmesi | **KOD TAMAM / YAYIN BEKLİYOR** | İzole LegacyImport worker, görselsiz ürünleri 10 dk/25 ürün hedefli tamamlar; günlük tam uzlaştırma güvenlik ağıdır. Fiziksel CDN/S3 dosyalarına dokunmaz. `ImagesEnabled` açıkça etkinleştirilmelidir. |
| E6 | MySQL üye/sipariş/fatura/iade geçici importu | **L3-L6 ilk aktarım tamam; sürekli worker kapalı** | Sekiz kümeden yalnız 5 kaynak-yetim adres eksik. `MSR/TYA` faturalar ve kalem toplamını kullanan iadeler tam; stok/event/bildirim/refund yan etkisi yok. Ayrıntı: `legacy-mysql-uye-siparis-fatura-iade-okuma-plani.md`. |
| E7 | Sipariş/fatura/iade outbound | **V3 yazma sözleşmesi bekliyor; kapalı** | Outbox + idempotency iskeleti ancak doğrulanmış V3 create/cancel/invoice/return sözleşmesiyle yazılacak. |
| E8 | Dump/restore ve ilk uzlaştırma | **TAMAMLANDI** | 8 temel kaynak/hedef sayımı eşit; checkpoint migration ve ERP dry-run geçti. |
| E8a | Geç zenginleşen ürün için hedefli refresh | **TAMAMLANDI** | `@ItemCode` ve barkoddan ürün çözümü, periyodik tam-snapshot renk/ürün özelliği/tedarikçi zenginleştirmesi ve admin 404 toparlaması eklendi; outbound öncesi ensure+retry E7'de bağlanacak. |
| E9 | Production cutover | Yapılacak | Son delta, geçici import kapatma, outbound kontrollü açma. |

## 3. E1-E3 ile eklenen kalıcı ERP yapısı

- `SqlServerErpSourceReader`
  - `jld_Appurunler`: oluşturulan ve güncellenen ürünler iki çağrıyla okunur, ürün koduyla birleştirilir.
    Prosedür değişiklik kümesini `cdItem`, `prItemVariant` ve `prItemAttribute.LastUpdatedDate` birleşiminden
    kurar; ayrıca `@ItemCode` ile tek ürünü tarih penceresinden bağımsız okumayı destekler.
  - `jld_AppurunVaryantlari`: ürün bazında barkod ve varyant eksenleri okunur.
  - `prItemAttribute`: yalnız yapılandırılmış attribute tipleri ürün kodlarıyla parametrelenmiş ve toplu okunur;
    `jld_ProductAttribute` prosedürünün dışarıda bıraktığı geç ürün özellikleri de bu kapsamdadır.
- `ErpSourceSyncService`
  - Katalog doğal anahtarı `Product.Code`.
  - Varyant doğal anahtarı `ProductVariant.Barcode`.
  - Barkod başka ürüne bağlıysa otomatik taşınmaz; hata raporlanır.
  - Yeni üründe grup eşleşmezse rastgele grup kullanılmaz.
  - Eşlenmiş V3 ürün özelliklerinin değerleri kararlı kaynak koduyla idempotent oluşturulur/güncellenir.
  - Fiyat yazıları idempotent upsert/update kullanır.
- `ErpSourceSyncWorker`
  - Yalnız `Node:Role=Worker|Both` üzerinde kayıtlıdır.
  - Katalog/fiyat ayrı PostgreSQL advisory lock kullanır.
  - Sonuçlar `integration.integration_logs` tablosuna yazılır.
- `integration.erp_sync_checkpoints`
  - Başarılı su işaretini dilim bazında saklar.
  - Okuma, yapılandırılan overlap kadar geriden başlar.
  - Tanım/barkod eşleşmesi engeli varsa katalog checkpoint'i ilerlemez.

### 3.1 Geç zenginleşen stok kartı ve on-demand toparlama kararı (2026-09-01)

- `dbo.julude_UrunAciklamaEkleme` salt-okunur metadata/definition denetimiyle incelendi. Bu prosedür V3
  stok kartını tamamlamaz; V3'teki ürün açıklaması, renk ve tedarikçi sözlüklerini linked-server üzerinden
  eski MySQL'e ekler. Ayrıca iptal siparişler için `ecs_OrderDelete` çağırır. ECSPros bu prosedürü çağırmaz.
- Mevcut artımlı reader geç girilen varyant ve ürün özelliklerini `LastUpdatedDate` değiştiği sürece yakalar.
  Periyodik katalog aralığı 360 dakikadan 15 dakikaya indirilmiştir. Worker, değişen ürün listesindeki her kod
  için tam snapshot okuyup renk, varyant, ürün özelliği ve tedarikçiyi birlikte günceller; admin
  on-demand refresh ise 404 yarış penceresindeki güvenlik ağı olarak kalır.
- Hedefli refresh sırası uygulanmıştır: ürün kodu bilinmiyorsa barkodu V3 `prItemBarcode` üzerinden salt-okunur çöz;
  `jld_Appurunler(@ItemCode)`, `jld_AppurunVaryantlari` ve parametreli `prItemAttribute` sorgusuyla tam snapshot oku;
  tek PostgreSQL transaction'ında Code/Barcode doğal anahtarlarıyla idempotent upsert et; sonra çağıran
  işlemi bir kez yeniden dene. İkinci denemede de bulunamazsa tahmin/fallback yapmadan hata ver.
- Yeni renkler yalnız görünen ada göre eşlenmez. V3 `ColorCode` kararlı dış anahtar olarak taşınır;
  hedef `AttributeValue.ExtraData` içinde kaynak kodu tutulup ad değişiklikleri aynı kaydı günceller.
  Renk hex değeri V3 sözleşmesinde yoksa uydurulmayacak.
- Tedarikçi adı kimlik değildir. V3 attribute type `3` içindeki `AttributeCode` kararlı anahtar olarak
  kullanılır ve `SupplierAccountCodes` içinde açık bir V3-kod → `accounts.current_accounts.Code` eşlemesi
  gerekir. Eşleşmeyen tedarikçi adına bakarak yeni cari açılmaz veya mevcut cari seçilmez; eşleme yoksa mevcut
  `SupplierId` korunur, eşlenmiş hedef cari bulunamazsa refresh fail-closed olur.
- V3 ürün grubu `Kot Ceket`, hedefteki mevcut `grp_46 / Ceket` grubuna açık config eşlemesiyle bağlanır;
  örnek kaynak/hedef doğrulaması `P-00017199` üzerinden yapılmıştır.
- `DescriptionI18n` serbest ve kullanıcı kontrollü alandır. ERP reader açıklama satırı okumaz; katalog,
  hedefli refresh ve uzlaştırma yollarının hiçbiri ürün açıklamasını yazmaz veya silmez.
- Sipariş/fatura/iade V3 outbound E7 henüz yazılmadığından entegrasyon noktası şimdilik tasarım kapısıdır.
  E7 geldiğinde payload hazırlanmadan önce ürün/varyant `ensure+retry` zorunlu olacaktır.

### 3.2 Ürün özelliği ve görsel metadata uzlaştırması (2026-09-02)

- Artımlı V3 ürün listesinin alt tablolardaki geç attribute değişikliğini her zaman yansıtmadığı görüldü.
  Katalog worker'ı her tur en fazla `ProductAttributeBatchSize` (varsayılan 100) ürünü, oluşturulma tarihi
  en yeni olandan başlayan bileşik cursor ile tek parametreli V3 sorgusunda uzlaştırır. Yalnız açıkça
  eşlenmiş tipler okunur; 17=Malzeme, 20=Kalıp, 21=Astar Durumu, 22=Fermuar, 23=Esneklik ve diğer
  yapılandırılmış özellikler `catalog.product_attributes` alanına taşınır. Kaynak değer kodu
  `AttributeValue.ExtraData` içinde saklanır; iki worker'ın aynı değeri eşzamanlı oluşturmasını PostgreSQL
  advisory lock engeller. `30-Açıklama ve Uyarı` bilinçli olarak ignored'dır. Uzlaştırma açıklama, varyant,
  fiyat veya tedarikçi yazmaz; yeni tablo/alan/migration yoktur.
- İzole `LegacyImport` profiline iki görsel dilimi eklendi. `images-missing`, mevcut SELECT-only
  `LegacyReadImport:ConnectionString` ile en yeni görselsiz en fazla 25 ürünü varsayılan 10 dakikada bir
  hedefli okur ve yalnız eksik metadata'yı ekler. `images` ise günlük tam uzlaştırma güvenlik ağıdır; açılışta
  yoğun tarama oluşturmaması için varsayılan 60 dakika gecikir ve var olan %90 güvenlik freni/advisory
  transaction lock'unu kullanır. İkisi de fiziksel görsel dosyalarını silmez veya taşımaz; `ImagesEnabled=true`
  ile birlikte açılır. Görsel yazımı diğer import dilimlerinden bağımsız `ImagesDryRun` anahtarıyla yönetilir;
  varsayılan `true` olduğundan kontrollü yayında önce rapor görülmeden metadata yazımı başlamaz.
- PostgreSQL restart/failover sırasında görülen `57P01` artık advisory-lock edinme/dispose sınırında yakalanır;
  tek geçici bağlantı kesintisi `LegacyCommerceImportWorker` process'ini sonlandırmaz ve sonraki turda yeniden denenir.
- Sözleşmeyi sabitleyen acceptance testleri `ErpSource_KatalogOkumaProsedurleri_SaltOkunurRaporlanir`,
  `ErpSource_HedefliUrunSnapshotVeBarkodCozumu_Okunur` ve attribute envanter testleridir; V3'ü salt okur.

## 4. Güvenli yapılandırma

Repository'deki `appsettings.json` yalnız güvenli varsayılanları içerir:

```json
{
  "ErpSource": {
    "Enabled": false,
    "DryRun": true,
    "CatalogEnabled": true,
    "PriceEnabled": true,
    "ConnectionString": ""
  }
}
```

Bağlantı dizesi yalnız secret/environment üzerinden verilir:

```text
ErpSource__ConnectionString=<secret>
```

Aktivasyon sırası:

1. `AddErpSyncCheckpoints` migration uygulanır.
2. ERP bağlantısı Worker VM'den salt-okuma kullanıcıyla doğrulanır.
3. Ürün grup ve attribute eşleme sözlükleri doldurulur.
4. `Enabled=true`, `DryRun=true` ile katalog/fiyat raporu alınır.
5. Kaynak/hedef örnekleri iş sahibiyle karşılaştırılır.
6. Katalog ve fiyat için `DryRun=false` kontrollü açılır.
7. Cutover'a kadar stok ayrı MySQL stock-only worker'dan izlenir.
8. Cutover'da MySQL stock-only worker kapatılır; stok sayımı ve düzeltmesi yalnız ECSPros admin panelinden yapılır.

Geçici MySQL stok yapılandırması ERP'den tamamen ayrıdır:

```json
{
  "Node": {
    "Role": "Worker",
    "WorkerProfile": "LegacyStock",
    "MigrateOnStartup": false
  },
  "LegacyStockSync": {
    "Enabled": false,
    "DryRun": true,
    "IntervalSeconds": 300,
    "StockStorageType": 1,
    "MinimumSourceRows": 1000,
    "BlockOnUnmappedQuantity": true,
    "MaximumUnmappedRows": 0,
    "MaximumUnmappedQuantity": 0,
    "RepairMissingMappings": false,
    "MappingRepairDryRun": true
  }
}
```

Production MySQL için ayrı `SELECT`-only kullanıcı hazırlandı. 2026-09-01'de MySQL private IP/3306 erişimi
önce Api1 VM'den doğrulandı; ardından Api1 kaynaklı geçici SSH tüneli üzerinden uygulamanın server-side
READ ONLY transaction probe'u `1/1` geçti. Geçici stok, üye, sipariş, fatura ve iade dilimleri yine ayrı
doğrulanmadan açılmaz. Kalıcı katalog/fiyat akışı MySQL'den değil V3 MSSQL'den gelir.

## 5. Açık bilgi ihtiyaçları

- V3 SQL Server'da TLS 1.2+ sertifika/protokol geçişi. Mevcut process'e özel TLS 1.0 uyumluluğu geçicidir.
- E7 için eski `EcsPros.QuartzService` kaynak dosyaları veya V3'e yeni müşteri/sipariş/fatura/iade yazan
  kesin procedure/API sözleşmeleri, zorunlu alanlar ve başarı/tekrar anahtarı. Yalnız adından hareketle V3 DML
  prosedürü seçilmeyecektir.

### 5.1 E7 salt-okunur sözleşme denetimi

V3 `sys.procedures/sys.parameters` metadata'sı salt okunur incelendi. Projeye özel adaylar arasında katalog
reader'ları dışında `ecs_OrderDelete(@OrderNumber)`, `sp_TicimaxInvoice(@EInvoiceNumber)`,
`sp_TicimaxInvoiceNew(@SiparisNo)` ve `sp_TicimaxtrOrderHeader(@DocumentNumber)` bulundu. Bunların hiçbiri
yeni ECSPros sipariş/fatura/iade payload'ını alan doğrulanmış bir create/upsert sözleşmesi değildir;
`ecs_OrderDelete` adı gereği özellikle kullanılmayacaktır. Eski Quartz kaynak kodu bu workspace'te bulunmadığı
için E7 adapter'ı tahminle yazılmadı. Bu eksik, geçici MySQL E6 importer'ının çalışmasına engel değildir.

Bu bilgiler doğrulanmadan tahmine dayalı mapping veya canlı yazma yapılmaz.

## 6. 2026-09-01 doğrulama sonucu

- `ConnectionStrings:ErpSource` ile SQL Server bağlantısı ve gerekli procedure'ler doğrulandı.
- Yeni hedef PostgreSQL `ecommerce_db`: ürün `0`, seed ürün grubu `144`.
- `20260901080814_AddErpSyncCheckpoints` yeni hedefe uygulandı ve tablo doğrulandı.
- İki günlük dry-run öncesi/sonrası hedef ürün sayısı `0 -> 0`; yazma olmadı.
- Dry-run, dump öncesi seed tanımlarında production'daki harf bedenler ve renk tonlarının bulunmadığını gösterdi.
  Bu değerler elle tahmin edilip oluşturulmayacak; `.59` dump/restore sonrası gerçek definition verisiyle yeniden ölçülecek.
- ERP SQL Server bağlantısı TLS 1.0 müzakere ediyor; kaynak sunucuda TLS 1.2+ geçişi ayrı altyapı işi olarak izlenmeli.
- `.59` production `LegacySyncWorker`, 2026-09-01 12:22 UTC'de dump penceresi için
  `Legacy.Sync.Enabled=false` yapılarak durduruldu. API servisi aktif kaldı; 12:14 UTC'den sonra yeni
  `pricestock` koşusu oluşmadığı 12:26 UTC'de doğrulandı. Geri dönüş yedeği:
  `/opt/ECSProsAI/publish/appsettings.Production.json.pre-dump-20260901T1222Z.bak`.
- Legacy fiyat ve stok dilimleri kodda ayrıldı. Mevcut kurulumlarla uyumluluk için iki bayrağın varsayılanı
  `true`; yeni hedefte açıkça `Prices=false`, `Stock=true` kullanılacak.
- Stock-only acceptance güvenlik kapısı, sıfır eşleşmeme toleransı ve ayrı/varsayılan kapalı eşleme onarım
  kapısı ile çalıştırıldı. Uygulama kaydı Bölüm 9 ve 9.1'dedir.

## 7. E8 dump/restore uygulama kaydı

- Kaynak yalnız `.59` PostgreSQL `ecommerce_db` idi; production MySQL'e bağlantı veya sorgu yapılmadı.
- Kaynak PostgreSQL boyutu `2.305.784.855` bayt (`2199 MB`), sürüm `16.13`.
- Custom-format dump: `/home/yalcin/ecspros-pg-ecommerce_db-20260901T1232Z.dump`.
- Dump boyutu `141.442.360` bayt; SHA-256:
  `68eb566055b4b1629dd1c462c65646ddd947a004b6c1c066039528129d800fd2`.
- Dump, yerel bilgisayara indirilmeden tek kullanımlık SSH anahtarıyla doğrudan yeni PostgreSQL sunucusuna
  aktarıldı. Aktarım sonrası public yetki ile kaynak private/public anahtar dosyaları kaldırıldı.
- Hedef geri dönüş yedeği:
  `/var/lib/ecspros-migration/pre-restore-ecommerce_db-20260901T1245Z.dump`;
  SHA-256 `4a0087cb5f7e5612d2c64d8240c72fadb5d7b6d091dffa587718f156d4dbe926`.
- Hedef DB sahibi `ecommerce`, encoding `UTF8`, locale `en_US.UTF-8` korunarak yeniden oluşturuldu.
- Kaynak/hedef kesin sayımlar eşit:
  - ürün `29.112`
  - varyant `333.790`
  - görsel `211.603`
  - stok satırı `235.012`, toplam miktar `246.870`
  - sipariş `159`
  - ürün grubu `145`
  - kanal ürünü `86.372`
  - kanal varyantı `281.996`
- Hedef fiziksel boyutun yaklaşık `787 MB` olması logical restore'un kaynak bloat/boş sayfalarını taşımamasından
  kaynaklanır; satır ve iş toplamları birebir eşittir.
- `20260901080814_AddErpSyncCheckpoints` restore sonrasında EF ile tekrar uygulandı; tablo ve history kaydı doğrulandı.
- ERP SQL Server → restore edilmiş PostgreSQL katalog/fiyat acceptance dry-run testleri `2/2` geçti; hedef ürün
  sayısının değişmediği test içinde doğrulandı.
- Acceptance dışı API test paketi `54/54` geçti. Fiyat ve stok birlikte kapalıysa hiçbir PostgreSQL/MySQL
  bağlantısı açılmadığını doğrulayan ağsız güvenlik testi buna dahildir. Test Nginx hattında `/live`,
  `/ready`, `/health` ve `/kadin-yeni-gelenler` HTTP `200`; API1 ve API2 private `/ready` kontrolleri
  ayrı ayrı Healthy/200.
- `.59` Legacy senkronu dump sonrasında mevcut canlı sitenin devamlılığı için yeniden etkinleştirildi. İlk turda
  `products`, `images`, `pricestock`, `orders` ve `order-status` dilimleri başarıyla tamamlandı; servis aktiftir.
  Bu servis yeni PostgreSQL'e veri taşımaz ve yeni sistem için kaynak kabul edilmez.
- Production MySQL'de yalnız salt-okuma envanter sorguları çalıştırıldı. Platform 41 için üye `104`, adres
  `104`, sipariş `71`, satır `181`, ödeme `72`, fatura `45`, iade `12`, iade kalemi `28` bulundu.
- Yeni hedef geçici importunun ayrıntılı L0-L8 planı
  `docs/legacy-mysql-uye-siparis-fatura-iade-okuma-plani.md` dosyasındadır; kodlanmadan önce additive legacy
  kimlik migration'ları ve ayrı SELECT-only kullanıcı zorunludur.

## 8. E2-E3 aktivasyon kaydı (2026-09-01, son ERP worker 2026-09-02)

- İzole worker API1 üzerinde `ecspros-erp-source.service` adıyla kuruldu. Güncel aktif release:
  `/opt/ECSProsAI/erp-worker-releases/20260901T220023Z_erp_enrich_15m_30dd430c`.
- Worker `Node:Role=Worker`, `Node:WorkerProfile=ErpSource`, `MigrateOnStartup=false` ile yalnız ERP işlerini
  çalıştırır. Mevcut API ve geçici MySQL importer process'lerinden ayrıdır.
- ERP erişimi yalnız private `192.168.0.100:1433` üzerinden yapılır; public ERP IP'si worker config'inde yoktur.
  Connection string secret'ı `/etc/ecspros/erp-source-worker.env` içinde `0640 root:ecspros` izinlidir.
- SQL Server Force Encryption uyguluyor fakat yalnız eski TLS ile anlaşabiliyor. Bağlantı `Encrypt=True` kalır;
  `/etc/ecspros/erp-openssl.cnf` yalnız bu izole process'te `OPENSSL_CONF` olarak tanımlıdır. Geçici politika
  `MinProtocol=TLSv1`, `SECLEVEL=0`; kullanıcı riski açıkça onayladı. Kalıcı iş TLS 1.2+ yükseltmesidir.
- Gerçek yazım öncesi geri dönüş dump'ı:
  `/var/backups/ecspros-erp/pre-continuous-erp-20260901T175719Z.dump`, boyut `85.862.469` bayt,
  SHA-256 `dc0684ea1367cbc51168305fe92e7e58979ffec6b9c5d3d31dd15b96fcea9e59`.
- Dry-run: kaynak `27` ürün, `291` varyant; yeni `2`, atlanan/mapping hatası `0`. Fiyat diliminde dump'taki
  `25` ürün bulundu; yeni iki ürün dry-run olduğu için doğal olarak katalogda yoktu.
- İlk gerçek tur: katalog `27`, yeni ürün `2`, varyant `291`, atlanan `0`; fiyat ürünü `27`, katalogda yok `0`,
  etkilenen kanal varyantı `291`. Katalog ve fiyat checkpoint'leri hatasız oluştu.
- Aynı pencere ikinci kez okutuldu: katalog değişiklik `0`, fiyat değişiklik `0`, kanal varyant yazımı `0`.
  Böylece Code/Barcode/attribute/kanal fiyatı akışının idempotency kabulü geçti.
- Son sayımlar: ürün `29.114`, varyant `333.857`, kanal ürünü `86.374`, kanal varyantı `282.217`;
  `PriceType=erp` varyant `291`.
- Kalıcı ayar: `DryRun=false`, `CatalogMinutes=15`, `PriceMinutes=10`, `OverlapMinutes=30`.
  Servis `active/enabled`, restart sayısı `0`; normal overlap son turu katalog/fiyat `0/0`.
- Kaynak sorgusu `0` kayıt döndürdüğünde katalog checkpoint'inin ilerlememesine neden olan erken dönüş
  düzeltildi. Yeni release'in ilk sıfır-değişiklik turunda katalog checkpoint'i `2026-09-01 18:10:39Z`, fiyat
  checkpoint'i `2026-09-01 18:11:02Z` oldu; iki kayıtta da `LastError` boştur. Böylece boş pencereler yeniden
  büyüyerek taranmaz.
- Son release sonrasında `/live`, `/ready`, `/health` uçlarının üçü de `200`; hedef kayıt sayıları değişmeden
  ürün `29.114`, varyant `333.857`, kanal ürünü `86.374`, kanal varyantı `282.217`, `PriceType=erp` varyant
  `291` kaldı.
- Mevcut `ecspros.service` ve `ecspros-legacy-import.service` PID'leri aktivasyon boyunca değişmedi.
- Acceptance dışı test paketi son kodla `83/83` geçti. GitHub'a gönderim yapılmadı.

### 8.1 Geç zenginleştirme worker yayını (2026-09-02)

- Yeni release, değişen her ürün için hedefli tam snapshot okuyarak açıklama 16-37, ürün özellikleri,
  renk/varyant ve eşlenmiş tedarikçiyi birlikte günceller. Katalog aralığı `15`, fiyat aralığı `10`, overlap
  `30` dakikadır.
- Arşiv SHA-256: `5ae715c76bcbfbb3fb1b3f9941bbf65378d126cc67e31c4243e0ef7dd76a12a8`; boyut `132.421.400` bayt.
- Aktivasyon öncesi geri dönüş dump'ı:
  `/var/backups/ecspros-erp/pre-20260901T220023Z_erp_enrich_15m_30dd430c.dump`; SHA-256
  `2578e2d04468f79399dbd08c68a3e182e8b142bd2e41f0df0b1592f929b5cf8d`; boyut `141.923.261` bayt;
  `pg_restore --list` başarılı.
- İki başarısız başlangıç sağlık kapısıyla eski release/env'e döndü: önce worker read-only filesystem altında
  varsayılan `~/.ecspros` yolu, sonra release'e production config kopyalanmaması. Final aktivasyonda
  `/opt/ECSProsAI/shared/dp-keys` override'ı ve shared production config hash-eşit kopyası kullanıldı.
- Final worker ve API readiness `200`, restart `0`, aktivasyon sonrası hata `0`. İkinci katalog turu gerçek
  15 dakikalık periyotta çalıştı. `P-00022932` için V3'teki 5 açıklama satırı hedefte 226 karakter açıklamaya
  dönüştü. V3'ün son `keywordId=20` değeri `Normal Kalıp` olduğundan hedef de `Normal Kalıp`tır.
- Acceptance dışı paket `89/89`, hedefli V3 snapshot testi son tur `1/1` geçti. Migration, API/admin yayını,
  Production MySQL yazısı veya GitHub push yapılmadı.

### 8.2 Açıklama senkronunu kaldıran özellik worker yayını (2026-09-02)

- API1 ERP worker release'i `20260902T151532Z_erp_attributes`; arşiv SHA-256
  `30a52bc1f88c2b6fcb5fea5356b5428958fcb149a4469fcd8dcd6261fdaa5f55`, boyut `131.499.105` bayt.
  Aktivasyon sonrası `/ready=200`, `active`, `NRestarts=0`; migration ve API1/API2 ana servis restart'ı yoktur.
- Temizlik öncesi `catalog.products` custom dump'ı
  `/var/backups/ecspros-erp/pre-generated-description-cleanup-20260902T151532Z.dump` yolunda alındı;
  SHA-256 `fd4a32a8e03e60a2fde4bb7960eac824516160d7ed9ef87928a7628997536477`, boyut `1.663.435`
  bayt ve `pg_restore --list` başarılıdır.
- Yalnız Türkçe açıklaması `<strong>` ile başlayan `497` aktif ürünün `tr` anahtarı tek transaction'da
  kaldırıldı; kalan `0`. Worker'ın ilk turu 100/100 özellik adayını güncelledi ve `P-00023131` kartındaki
  malzeme/kalıp/astar/fermuar/esneklik/boy/kumaş türü/sezon/cinsiyet/yaş grubu hedefte doğrulandı.
- Ayrı veri eşleme engeli kapatıldı: V3 `Kot Ceket`, hedefte salt-okunur `P-00017199` doğrulamasıyla mevcut
  `grp_46 / Ceket` grubuna bağlandı ve `20260902T152815Z_erp_kot_ceket` worker release'iyle yayınlandı. İlk
  canlı katalog turunda `P-00023146` `8` varyantla oluşturuldu (`atlanan=0`); fiyat turu da `8` kanal
  varyantını işledi (`katalogda-yok=0`). Faz kapanışındaki hedef `READ ONLY` kontrolde hem `P-00017199`
  hem `P-00023146` `grp_46 / Ceket` ve `8` varyantlı olarak doğrulandı; aktif ürün özelliği sayıları sırasıyla
  `11` ve `9`, kullanıcı kontrollü Türkçe açıklamalar boştur.

## 9. ERP stok kaldırma ve geçici MySQL stock-only aktivasyon kaydı (2026-09-01)

- ERP/MSSQL stok modeli, reader metodu, SQL komut ayarları, scheduler ve sync metodu koddan tamamen çıkarıldı.
  ERP worker logu artık yalnız `Catalog=True, Price=True` bildirir; stok çalıştırabilecek bir ERP kod yolu yoktur.
- API1, API2, LegacyImport ve ErpSource process'leri stok yeteneği bulunmayan
  `20260901T_stock_diag_30dd430c` release'ine geçirildi. API1'de dört servis, API2'de API servisi aktif;
  private health kontrolleri başarılıdır.
- Geçiş dönemi için API1'e ayrı `ecspros-legacy-stock.service` kuruldu. Worker profili `LegacyStock`, bağlantı
  production MySQL SELECT-only hesabıdır; kaynak okuma server-side `READ ONLY` transaction ve rollback ile
  yapılır. PostgreSQL snapshot yazısı tek transaction'dır; eşleşmeme üst sınırı repo ve sunucuda `0/0` kalır.
- İlk dry-run `4.561` eşleşmeyen satır gösterdi. Tanı ayrımı, `308` varyantın tamamının aktif hedef ürüne ve
  `307` rafın tamamının aktif hedef depo kısmına ait olduğunu kanıtladı. MySQL adetleri otoritatif olduğundan
  bu kayıtları atlama/tolerans verme yaklaşımı iptal edildi.
- Varsayılan kapalı `RepairMissingMappings` akışı eklendi. MySQL'den yalnız stok taşıyan iş anahtarlarını
  okur; hedefe migration ile aynı SKU/barkod, raf kodu ve özellik eşleme kurallarıyla tek transaction'da,
  idempotent yazar. Dry-run `308` varyant, `307` raf, `616` varyant özelliği, `1` özellik değeri ve
  `0` eşlenemeyen özellik gösterdi. Gerçek onarım aynı sayıları yazdı; ikinci tur `0` değişiklik verdi.
- Onarım sonrası stok dry-run'ı `160.474/160.474` eşleşen kombinasyon, eşleşmeyen satır/adet `0/0` gösterdi.
  İlk gerçek stok turu `5.901` değişiklik (`512` update, `5.350` insert, `39` sıfırlama), hemen ikinci tur
  `0` değişiklik verdi. Final turda kaynak/eşleşen toplam adet `253.847/253.847`, değişiklik `0`.
- Stok worker cutover anında kapatılacak. O andan sonra stok sayımı ve düzeltmesi yalnız ECSPros admin paneli
  üzerinden yapılacak; admin stok yazımı sonrası dağıtık stok cache anahtarları temizlenir.
- Acceptance dışı test paketi `86/86` geçti. GitHub'a gönderim yapılmadı.

### 9.1 Admin yayını ve gerçek stok yazımı öncesi kapı

- Admin artefaktı `/usr/share/nginx/admin-releases/20260901T_admin` altına atomik yayımlandı;
  `/admin/` index ve hashed JavaScript asset'i Nginx üzerinden HTTP `200` döndürür.
- Eşleşmeyen kayıtları sınırsız atlamak yerine `MaximumUnmappedRows` ve `MaximumUnmappedQuantity` üst sınırları
  eklendi. Repo varsayılanı ve çalışan servis `0/0`; son üç turda eşleşmeyen satır/adet `0/0`.
- Eşleme onarımı öncesi beş hedef tablo yedeği:
  `/var/backups/ecspros-stock/pre-mapping-repair-20260901T1923Z.dump`, `68.364.205` bayt, SHA-256
  `11def3536b74c82066b00a68a2ecfd53cf4b5ff2807d9e9c7630694ab8fa7c4e`. Gerçek stok öncesi ek yedek:
  `/var/backups/ecspros-stock/pre-real-stock-sync-20260901T1926Z.dump`, `13.000.584` bayt, SHA-256
  `4e344429a3d9cf77c42f37a7eadd1666b8099f60cd56febe14035730a55be741`. İki dump'ın tablo verisi listesi
  `pg_restore --list` ile doğrulandı.
- Hedef bütünlük sonucu: aktif stok satırı `240.362`, kullanılabilir toplam `253.847`; negatif kullanılabilir
  stok, yetim/silinmiş varyant, yetim/silinmiş raf, case-insensitive varyant/raf barkod tekrarı ve aktif
  varyant-raf çifti tekrarı `0`.
- API1 stock worker final release'i `20260901T192909Z_stock_final_30dd430c`; `DryRun=false`,
  `RepairMissingMappings=false`, `MappingRepairDryRun=true`, interval `300` saniye. Admin lint mevcut 179 hata
  nedeniyle başarısızdır; frontend kaynaklarına bu çalışmada müdahale edilmedi.
