# Legacy MySQL Üye, Sipariş, Fatura ve İade Okuma Planı

> Son güncelleme: 2026-09-01  
> Kapsam: ECSProsAI, platform `41`  
> Kaynak: production MySQL (`juludedb`)  
> Hedef: yeni PostgreSQL `ecommerce_db`  
> Temel kural: MySQL yalnız okunur; yeni sistemde gerçek sipariş/iade işlemleri açılana kadar geçici import
> Kaynak sınırı: `.59` PostgreSQL bu importun kaynağı veya ara katmanı değildir

## 1. Amaç ve sınırlar

Production geçişine kadar eski sitede oluşan aşağıdaki veriler MySQL'den yeni PostgreSQL'e taşınacaktır:

1. Üye ve üye adresleri
2. Sipariş ve siparişin bağlı mali kalemleri
3. Fatura üst bilgisi ve fatura görüntüleme bağlantısı
4. İade, iade kalemleri ve geçmiş iade durumu

Geçici stok takibi de production MySQL'den yapılır; ancak mevcut `Legacy.Sync` stock-only dilimiyle ayrı
E4a akışı olarak yönetilir. Bu dokümandaki `LegacyReadImport` üye/sipariş/fatura/iade dilimlerine odaklanır.
Ürün, varyant, özellik ve fiyat bu importer'ın kapsamında değildir; bunların kalıcı kaynağı V3 MSSQL'dir.

Bu akış bir ticari işlem motoru değildir. Import sırasında:

- MySQL'de `INSERT`, `UPDATE`, `DELETE`, DDL veya kilitleyen bakım komutu çalıştırılmaz.
- PostgreSQL'e alınan tarihsel kayıtlar stok düşmez veya artırmaz.
- Rezervasyon, ödeme çekimi, para iadesi, e-posta/SMS, domain event veya outbound entegrasyon üretilmez.
- Yeni sistemde doğal yollarla oluşmuş kayıtlar silinmez ve legacy verisiyle ezilmez.
- Sipariş/fatura/iade ERP outbound servisleri kodlansa bile ayrı bayraklarla kapalı kalır.

## 2. 2026-09-01 salt-okunur kaynak envanteri

Tüm kaynak sorguları `SET SESSION TRANSACTION READ ONLY`, `START TRANSACTION READ ONLY` ve `ROLLBACK`
sınırında çalıştırıldı. Kişisel veri değerleri okunup rapora veya loga basılmadı.

| Veri | MySQL tablosu | Platform 41 kesin sayım | Hedef PostgreSQL mevcut durum |
|---|---|---:|---:|
| Üye | `webmembers` | 104 | `crm.crm_members`: 70 legacy üye |
| Adres | `webmemberaddresses` | 104 | Kalıcı legacy adres kimliği yok |
| Sipariş | `oporders` | 71 | `order.ord_orders`: 54 legacy sipariş |
| Sipariş satırı | `oporderlines` | 181 | Kalıcı legacy satır kimliği yok |
| Ödeme kaydı | `oporderpayments` | 72 | Kalıcı legacy ödeme kimliği yok |
| Fatura | `opinvoices` | 45 | Toplam 1 hedef fatura; legacy kimliği yok |
| İade | `opiadesiparisler` | 12 | 0 |
| İade kalemi | `opiadeurunler` | 28 | 0 |

İlk uzlaştırmaya göre en az `34` üye ve `17` legacy sipariş hedefte eksiktir. Fatura ve iade aktarımı
henüz yapılmamıştır.

Kaynak kalite kontrolü:

- Sipariş numaralarında tekrar yok.
- Siparişlerin üye ve adres ilişkilerinde eksik kayıt yok.
- Fatura numaralarında tekrar veya siparişsiz fatura yok.
- İadelerde sipariş ilişkisi eksik değil.
- Bazı tarih kolonlarında MySQL sıfır tarihi (`0000-00-00`) bulunuyor; bunlar PostgreSQL'e `null` veya
  tanımlı güvenli fallback olarak aktarılmalıdır.

## 3. Kaynak tablolar ve veri sözleşmeleri

### 3.1 Üye ve adres

- `webmembers`: kimlik, platform, ad/soyad, telefon, e-posta, doğum, cinsiyet, doğrulama ve pazarlama
  izinleri, aktiflik, oluşturulma/güncellenme tarihleri.
- `webmemberaddresses`: üye kimliği, teslimat/fatura adresi, şehir/ilçe/mahalle, şirket/vergi bilgileri,
  oluşturulma/güncellenme tarihleri.

Parola güvenlik kuralı:

- Legacy parola özeti yeni BCrypt/Argon2 benzeri modern bir özeti asla ezmez.
- Yalnız hedef boşsa ve mevcut uyumluluk girişi gerçekten gerektiriyorsa legacy işaretli değer taşınır.
- Açık parola hiçbir aşamada okunmaz, loglanmaz veya saklanmaz.

### 3.2 Sipariş aggregate'i

- `oporders`: sipariş numarası, platform, kaynak/hedef numaralar, durum, ödeme tipi, üye ve adres
  bağlantıları, toplamlar, kargo/fatura alanları, kaynak sipariş bilgisi.
- `oporderlines`: sipariş, varyant/barkod, ürün kodu/adı/renk/beden, fiyat, miktar, indirim, durum.
- `oporderpayments`, `oporderexpenses`, `oporderdiscounts`, `opordertaxes`: siparişin mali alt kayıtları.

Sipariş `71`, satır `181`, ödeme `72` sayımı ilk snapshot kabul değeridir. Siparişin herhangi bir parçası
değiştiğinde yalnız üst kaydı değil bütün legacy aggregate yeniden okunur; hedefte ilgili legacy alt
kayıtlar upsert edilir.

### 3.3 Fatura

- `opinvoices`: sipariş, tarih/saat, fatura numarası, ETTN, kaynak/hedef numaraları, kargo bilgileri,
  entegratöre gönderim bayrakları, e-Arşiv durumu, URL ve fatura tipi.
- Kaynakta ayrı bir fatura satırı tablosu yoktur. Hedef `InvoiceItem` kayıtları bağlı sipariş satırları ve
  sipariş tutarlarından tarihsel görünüm olarak oluşturulur.
- `MSR` ve `TYA` gibi seri önekleri, yıl ve sıra numarası güvenli olarak ayrıştırılır; hedefte karşılık gelen
  `InvoiceSeries` yoksa import hata verir, rastgele seri oluşturmaz.
- ETTN ve kaynak metadata'sı yapılandırılmış entegratör cevabında; görüntüleme URL'si
  `IntegratorInvoiceUrl` alanında tutulur.

### 3.4 İade

- `opiadesiparisler`: sipariş, iade tarihi/tipi/durumu/tutarı, kayıt zamanı, üyeye ödeme bilgileri ve
  entegrasyon bayrağı.
- `opiadeurunler`: iade, sipariş satırı, neden, müşteri isteği ve tutar.
- `opiadelog`: sipariş/satır durumu ve işlem zamanı.
- `dfiadenedenleri`: legacy iade nedeni sözlüğü.

İade importu yalnız tarihsel kayıt oluşturur. Gerçek ödeme iadesi veya stok hareketi başlatmaz. Hedefteki
iade nedeni eşlemeleri sabit kod sözlüğüyle yapılır; bilinmeyen nedenler güvenli `legacy_unknown` koduyla
raporlanır ve kaynak metadata'sı korunur.

## 4. Hedef şema değişiklikleri

Sürekli çalışabilen ve tekrarlandığında aynı sonucu veren import için aşağıdaki nullable kolonlar ve filtreli
unique indeksler gereklidir:

| Hedef entity | Yeni alan | Kaynak |
|---|---|---|
| `Address` | `LegacyAddressId` | `webmemberaddresses.Id` |
| `OrderItem` | `LegacyOrderLineId` | `oporderlines.Id` |
| `OrderPayment` | `LegacyOrderPaymentId` | `oporderpayments.Id` |
| `Invoice` | `LegacyInvoiceId` | `opinvoices.Id` |
| `Return` | `LegacyReturnId` | `opiadesiparisler.Id` |
| `ReturnItem` | `LegacyReturnItemId` | `opiadeurunler.Id` |

`Member.LegacyMemberId` ve `Order.LegacyOrderId` zaten vardır ve korunur. Gider, indirim ve vergi kayıtları
hedefte ayrı entity olarak tutuluyorsa bunlara da açık legacy ID eklenir; JSON veya açıklama metni doğal
anahtar olarak kullanılmaz.

Migration additive olmalıdır. Var olan kolon veya indeks silinmez; production davranışı değiştiren default
değer eklenmez.

## 5. Servis tasarımı

### 5.1 Bileşenler

- `LegacyCommerceImportWorker`: zamanlama, node rolü, advisory lock ve dilim orkestrasyonu.
- `LegacyMemberAddressReader` / `LegacyMemberAddressImporter`
- `LegacyOrderAggregateReader` / `LegacyOrderAggregateImporter`
- `LegacyInvoiceReader` / `LegacyInvoiceImporter`
- `LegacyReturnReader` / `LegacyReturnImporter`
- `LegacyImportCheckpointStore`: her dilim için son su işareti ve overlap başlangıcı.
- `LegacyImportReconciliationService`: kaynak/hedef sayım ve eksik ID raporu.

Worker yalnız `Node:Role=Worker|Both` olan node'da kaydedilir. Ayrı importer process'i
`Node:WorkerProfile=LegacyImport` kullanır; marketplace, feed, tracking, ERP, eski LegacySync ve dashboard
worker'ları bu profilde kaydedilmez. Profil verilmezse geriye uyumlu varsayılan `All` olur. Birden fazla
import worker ayağa kalksa bile her dilim PostgreSQL advisory lock ile tek örnek çalışır. API node'ları bu
işleri çalıştırmaz.

İzole servis node ayarları:

```text
Node__Role=Worker
Node__WorkerProfile=LegacyImport
Node__MigrateOnStartup=false
```

### 5.2 Güvenli varsayılan ayarlar

```json
{
  "LegacyReadImport": {
    "Enabled": false,
    "DryRun": true,
    "PlatformId": 41,
    "MembersEnabled": false,
    "OrdersEnabled": false,
    "InvoicesEnabled": false,
    "ReturnsEnabled": false,
    "ReturnAmountMismatchPolicy": "Block",
    "IntervalSeconds": 120,
    "OverlapMinutes": 30,
    "FullReconciliationHourUtc": 2,
    "ConnectionString": ""
  }
}
```

Secret yalnız environment/secret dosyası üzerinden verilir:

```text
LegacyReadImport__ConnectionString=<SELECT-only MySQL connection>
```

Bu bağlantı mevcut `Legacy:MySqlConnection` ile paylaşılmaz. İncelemede kullanılan eski hesabın `SELECT`
yanında yazma yetkilerine de sahip olduğu görüldü ve importer için kullanılmadı. Yalnız gereken şema/tablolara
`SELECT` yetkili ayrı MySQL kullanıcısı oluşturuldu; Api1 private ağ yolundan server-side READ ONLY transaction
probe'u başarıyla geçti. Gerçek importer aktivasyonu yine dilim bazlı dry-run ve açık onay gerektirir.

## 6. Checkpoint ve değişiklik yakalama

Kaynak tabloların tamamında güvenilir `updatedDate` olmadığı için tek yöntem kullanılmayacaktır:

| Dilim | Artımlı okuma | Tam uzlaştırma |
|---|---|---|
| Üye/adres | `GREATEST(createdDate, updatedDate), Id` + overlap | Her gece platform 41 ID kümesi |
| Sipariş | `GREATEST(createdDate, updatedDate), Id` + overlap | Değişen siparişin tüm aggregate'i; gece tam ID kontrolü |
| Sipariş satırı | Üst sipariş değişince tekrar oku; tarih alanlarında overlap | Gece sipariş bazlı hash/sayım |
| Fatura | `Id` high-water + yakın dönem yeniden okuma | Günlük numara/ETTN/hash karşılaştırması |
| İade | `GREATEST(kayitZamani, opiadelog.islemZamani), Id` | İade ve tüm kalemlerini gece yeniden karşılaştır |

Checkpoint yalnız dilim tamamen başarılıysa ilerler. Mapping hatası, ilişki eksikliği veya DB hatasında
checkpoint ilerlemez. Her sorgu platform filtresiyle sınırlandırılır.

## 7. Upsert ve koruma kuralları

1. Yalnız `Legacy*Id` bulunan kayıt import tarafından güncellenebilir.
2. Yeni sistemde oluşturulmuş native adres/sipariş satırı/fatura/iade silinmez veya değiştirilmez.
3. Mevcut MigrationTool'daki adresleri topluca silip yeniden ekleme yaklaşımı sürekli senkron için kullanılmaz.
4. Sipariş satırlarını silip yeniden oluşturma yaklaşımı kullanılmaz; her satır `LegacyOrderLineId` ile upsert
   edilir, kaynaktan kalkmış legacy satır ayrıca pasifleştirilir ve raporlanır.
5. `DryRun=true` iken PostgreSQL transaction açılabilir ama hiçbir `SaveChanges`/commit yapılmaz.
6. Import operation ID ile yapılandırılmış log tutulur; ad, telefon, e-posta, adres, vergi no veya kart verisi
   loga yazılmaz.

## 8. Durum eşlemeleri

### Sipariş

Mevcut `LegacyOrderSync` ve MigrationTool eşlemeleri tek bir ortak mapper'a taşınmalıdır. Özellikle
`Teslim Edilemeden İade Geldi` durumu `Cancelled` değil `Returned` olarak standardize edilmelidir.
Bilinmeyen durum otomatik tamamlanmış sayılmaz; kaynak değer raporlanır ve güvenli bekleme durumunda tutulur.

### Fatura

- `isSentToIntegrator=1`: tarihsel gönderilmiş durumu.
- `isEArsiv=1`: e-Arşiv tipi.
- `faturaGonder=0`: gönderilmemiş/bekleyen tarihsel kayıt; import outbound gönderim başlatmaz.
- Kaynak URL ve ETTN varsa korunur; hedefte yeniden fatura üretilmez.

### İade

Kaynakta gözlenen `iadeTipi` değerleri `1/2`, `durumu` değeri `0`'dır. Bunların iş anlamı kullanıcı/operasyon
tarafından onaylanmadan nihai domain statüsüne tahmini eşleme yapılmaz. İlk import kaynak kodlarını metadata'da
korur ve güvenli başlangıç statüsü kullanır.

Üst kayıt ile kalem toplamı uyuşmazlığı için güvenli varsayılan `ReturnAmountMismatchPolicy=Block`'tur.
İşletme kararıyla kontrollü ilk aktarımda `UseItemTotal` seçilmiştir. Bu modda `RefundAmount` doğrudan kaynak
iade kalemlerinin toplamından üretilir; kaynak üst tutarı, kalem toplamı, çözümlenen tutar ve kullanılan baz
audit amacıyla metadata'da birlikte saklanır. Bu işlem gerçek para iadesi veya stok hareketi üretmez.

## 9. Fazlar

| Faz | İş | Durum | Kabul kriteri |
|---|---|---|---|
| L0 | Salt-okunur kaynak ve hedef envanteri | **TAMAMLANDI** | Tablo, ilişki, sayım, tarih ve yetki riski raporlandı. |
| L1 | Legacy identity kolonları ve migration | **TAMAMLANDI** | Üç additive migration yeni PostgreSQL'e uygulandı; nullable kolonlar ve filtreli unique indeksler doğrulandı. |
| L2 | Options, SELECT-only reader, checkpoint, worker/lock çatısı | **TAMAMLANDI** | Default kapalı/dry-run; server-side READ ONLY+rollback; Worker/Both ve advisory lock; acceptance geçti. |
| L3 | Üye ve adres importu | **GERÇEK İLK AKTARIM TAMAMLANDI** | Kontrollü hedef kimliği kapısı ve tablo yedeğiyle çalıştı; üye `104/104`, import edilebilir adres `99/99`. Kaynaktaki 5 adres yetim üye referansıdır. |
| L4 | Sipariş aggregate importu | **GERÇEK İLK AKTARIM TAMAMLANDI** | `71/181/72` kaynak ve hedef Legacy ID kümeleri eşit; `changed=324`, `skipped=0`, yan etki üretilmedi. |
| L5 | Fatura importu | **GERÇEK İLK AKTARIM TAMAMLANDI** | `MSR/TYA` aktif serileri hazırlandı; `45/45` fatura ve türetilen `121` kalem aktarıldı; ikinci koşu `changed=0`. |
| L6 | İade importu | **GERÇEK İLK AKTARIM TAMAMLANDI** | `12/12` iade ve `28/28` kalem, işletme kararıyla `UseItemTotal` kullanılarak aktarıldı; gerçek stok/refund yok; ikinci koşu `changed=0`. |
| L7 | Dry-run, uzlaştırma ve gözlemlenebilirlik | **KODLANDI; ACCEPTANCE GEÇTİ** | Sekiz Legacy ID kümesi PII'siz karşılaştırılıyor; hedef fingerprint değişmedi. |
| L8 | Kontrollü geçici aktivasyon | **İLK AKTARIM TAMAMLANDI** | L3-L6 kontrollü ilk aktarımı ve idempotency kabulü tamamlandı; sürekli worker hâlâ kapalı. |

## 10. Test ve kabul kriterleri

- SELECT-only MySQL hesabının `SELECT` sorguları geçer; yazma yetkisi ayrı disposable doğrulamada reddedilir.
- Dry-run öncesi/sonrası hedef tablo sayıları ve fingerprint'leri aynıdır.
- İlk snapshot beklenen kaynak sayıları: üye `104`, adres `104`, sipariş `71`, satır `181`, ödeme `72`,
  fatura `45`, iade `12`, iade kalemi `28`.
- Aynı import ikinci kez çalıştığında duplicate üretmez ve değişiklik sayısı `0` olur.
- Hata sonrası tekrar, checkpoint'ten ve overlap penceresinden güvenle devam eder.
- Outbox, domain event, stok hareketi, rezervasyon, gerçek ödeme veya gerçek refund sayımı değişmez.
- MySQL sorguları için timeout/cancellation vardır; sorgu hatasında hedef kısmi commit edilmez.
- Üç API node'u altında worker'ın tek örnek çalıştığı advisory lock testiyle doğrulanır.

### 10.1 2026-09-01 L4-L7 doğrulama sonucu

- Sipariş kaynak acceptance: `71` sipariş, `181` satır, `72` ödeme, `57` siparişte kullanılan adres.
- L4 hedef dry-run hiçbir yazı/checkpoint yapmadan `19` eksik legacy üye nedeniyle durdu. Bu bir kod/ağ
  hatası değil, L3 gerçek aktarımının L4'ten önce koşması gereken sıralama kapısıdır.
- Fatura kaynağı: `45/45` e-Arşiv; `MSR=38`, `TYA=7`; bütün numaralar `3+4+9` biçiminde 16 karakterdir.
  Hedefte yalnız `TST` serisi bulundu. Gerçek yasal seri konfigürasyonu importer tarafından otomatik
  üretilmez; `MSR` ve `TYA` aktif seri eşlemeleri aktivasyon önkoşuludur.
- İade kaynağı: `12` üst kayıt, `28` kalem, `66` log. Kullanılan nedenler `1/2/3/9`; gerekli hedef kodlar
  sırasıyla `legacy_unspecified`, `legacy_disliked`, `legacy_size`, `legacy_not_delivered` ve güvenli fallback
  `legacy_unknown` olarak sabitlendi. Hedefte şu anda aktif iade nedeni yoktur; importer tahmin yapmadan durur.
- İade üst/kalem tutarı `8` kayıtta eşit, `4` kayıtta farklıdır. Bu dört kayıt operasyonel olarak
  uzlaştırılmadan L6 gerçek moda alınmaz; üst tutar veya kalem tutarı otomatik doğru kabul edilmez.
- L7 salt-okunur uzlaştırma hedefi değiştirmedi ve mevcut başlangıç durumunu şöyle raporladı:
  üyeler `70/104`, adresler `0/104`, siparişler `54/71`, satırlar `0/181`, ödemeler `0/72`, faturalar
  `0/45`, iadeler `0/12`, iade kalemleri `0/28`; toplam eksik Legacy ID `493`.

### 10.2 2026-09-01 kontrollü L8 uygulama sonucu

- L3 öncesi geri dönüş yedeği:
  `/var/backups/ecspros-l8/pre-l3-members-addresses-20260901-165818.dump` (`36K`, SHA-256 doğrulandı).
- L3 gerçek aktarımı hedef database `ecommerce_db` ve PostgreSQL server identity güvenlik kapıları geçtikten
  sonra tamamlandı. Uzlaştırmada üyeler `104/104`, adresler `99/104` oldu. Eksik 5 kaynak adres
  (`4238099`, `4238253`, `4240551`, `4394977`, `4474131`) MySQL'de bağlı `webmembers` kaydı bulunmayan
  yetim adreslerdir; importer sahte üye üretmedi ve bu kayıtları güvenli biçimde atladı.
- L4 öncesi geri dönüş yedeği:
  `/var/backups/ecspros-l8/pre-l4-orders-20260901-170037.dump` (`56K`, SHA-256 doğrulandı).
- L4 gerçek aktarımı `changed=324`, `skipped=0` ile tamamlandı. Toplam hedef sayımları sipariş
  `159→176`, kalem `233→290`, ödeme `0→72`; Legacy ID uzlaştırması `71/71`, `181/181`, `72/72`.
- İkinci gerçek koşu idempotency kabulü geçti. L3 `changed=0`, `skipped=5` (yalnız bilinen yetim adresler),
  toplam üye/adres/Legacy adres sayıları `139/125/99` olarak sabit kaldı. L4 `changed=0`, `skipped=0`;
  toplam sipariş/kalem/ödeme sayıları `176/290/72` olarak sabit kaldı.
- Beş teknik legacy iade nedeni idempotent seeder'a eklendi ve hedefte `5/5` aktif oluşturuldu. Öncesinde
  `/var/backups/ecspros-l8/pre-l6-return-reasons-20260901-170602.dump` yedeği alındı.
- Bu aşamadaki L5/L6 dry-run hedefi değiştirmedi. O anda faturalar aktif `MSR/TYA` seri tanımı, iadeler
  ise `197069`, `199473`, `200369`, `209811` üst/kalem tutar kararı nedeniyle blokluydu. Bu iki önkoşul
  aşağıdaki 10.3 uygulama kaydında giderildi.
- Aktivasyon bayrakları değiştirilmedi: `Enabled=false`, `DryRun=true`, bütün dilimler kapalı kaldı.

### 10.3 2026-09-01 kontrollü L5/L6 uygulama sonucu

- Kullanıcı kararı: fatura serileri `MSR` ve `TYA`; iade refund tutarı için kaynak üst tutarı değil kaynak
  kalem toplamı esas alınacaktır.
- Kodda `ReturnAmountMismatchPolicy` eklendi. Repo varsayılanı fail-closed `Block`; yalnız kontrollü L6
  koşusunda `UseItemTotal` verildi. Kaynak üst tutarı ve çözümlenen kalem toplamı metadata'da korunur.
- L5/L6 öncesi geri dönüş yedeği:
  `/var/backups/ecspros-l8/pre-l5-l6-invoices-returns-20260901-173105.dump` (`21.540` bayt), SHA-256
  `3627994072a88b66c4d182eea3a4dd44639d70945a93b4ebe26f2b7947c8379e`.
- Hedef kimliği doğrulandıktan sonra aynı firmaya aktif `MSR/MSR/MSR` ve `TYA/TYA/TYA` seri kayıtları
  eklendi. Seri hazırlama ikinci koşuda `inserted=none`; mükerrer seri oluşmadı.
- Ortak L5/L6 dry-run başarılı oldu ve hedef fingerprint'i değişmedi: fatura potansiyel değişiklik `166`,
  iade potansiyel değişiklik `40`, iki dilimde `skipped=0`.
- L5 gerçek aktarımı `changed=166`, `skipped=0`: toplam hedef fatura `1→46`, fatura kalemi `1→122`.
  Bunların Legacy kaynak eşleşmesi `45/45`; seri dağılımı `MSR:38`, `TYA:7`.
- L6 gerçek aktarımı `changed=40`, `skipped=0`: hedef iade `0→12`, iade kalemi `0→28`.
  On iki kaydın tamamında `RefundAmount = kaynak kalem toplamı`; tutar uyuşmazlığı `0`.
- İkinci gerçek koşu idempotency kabulü geçti: L5 `changed=0`, sayımlar `46/122`; L6 `changed=0`,
  sayımlar `12/28`. Gerçek refund, stok hareketi, domain event veya outbound çağrı üretilmedi.
- Son tam uzlaştırma: üye `104/104`, adres `99/104` (5 bilinen kaynak yetimi), sipariş `71/71`, satır
  `181/181`, ödeme `72/72`, fatura `45/45`, iade `12/12`, iade kalemi `28/28`; toplam eksik yalnız `5`.
- SSH tünelleri test sonunda kapatıldı. Sürekli worker ve bütün repo dilim bayrakları kapalıdır;
  `Enabled=false`, `DryRun=true`, `ReturnAmountMismatchPolicy=Block` korunur.

## 11. Aktivasyon ve geri dönüş

1. L1-L7 tamamlanır; tüm bayraklar kapalı kalır.
2. Ayrı SELECT-only MySQL kullanıcısı ve ağ erişimi hazırlanır.
3. `DryRun=true` ile dört dilimin uzlaştırma raporu onaylanır.
4. Önce üyeler/adresler, sonra siparişler, faturalar ve en son iadeler ayrı ayrı gerçek moda alınır.
5. Her dilimden sonra sayım, eksik legacy ID, son tarih ve hata oranı kontrol edilir.
6. Sorunda yalnız ilgili `*Enabled=false` yapılır; import kayıtları legacy ID ile ayırt edildiği için kontrollü
   geri alma planı uygulanabilir.
7. Yeni sistem sipariş/iade kaynağı olduğunda geçici MySQL reader kapatılır; outbound servisleri ayrı cutover
   kararı ve idempotency anahtarlarıyla açılır.

## 12. Mevcut çalışma durumu

- `.59` üzerindeki mevcut `LegacySyncWorker` 2026-09-01'de mevcut canlı sitenin kendi devamlılığı için yeniden etkinleştirildi; servis aktiftir.
- İlk başarılı turda `products`, `images`, `pricestock`, `orders` ve `order-status` dilimleri tamamlandı.
- Bu worker `.59` PostgreSQL'i güncelleyen mevcut production akışıdır; bu dokümandaki yeni hedef import worker'ı
  değildir. Yeni sistem `.59` PostgreSQL'den yeni/değişen kayıt okumaz.
- Mevcut worker ile yeni importer aynı şey değildir. Yeni importer kodunda L1-L7 ve L8 kontrollü L3-L6
  ilk aktarımı tamamlandı. Sürekli importer API1 üzerinde ayrı process ve izole `LegacyImport` profiliyle
  devreye alınmıştır; mevcut API process'i ve `.59` worker'ı değiştirilmemiştir.

### 12.1 L1-L2 uygulama kaydı

- CRM migration: `20260901133647_AddLegacyAddressIdentity`
- Order migration: `20260901133654_AddLegacyCommerceIdentities`
- Integration migration: `20260901133932_AddLegacyImportCheckpoints`
- Migration'lar yalnız yeni PostgreSQL `ecommerce_db` üzerine uygulandı; `.59` PostgreSQL değiştirilmedi.
- `LegacyReadImport` yapılandırması repoda `Enabled=false`, `DryRun=true` ve dört dilim kapalıdır.
- Kaynak reader her bağlantıda pooling'i kapatır, MySQL session'ını `TRANSACTION READ ONLY` yapar, işlemi
  READ ONLY transaction içinde yürütür ve daima rollback eder.
- Worker yalnız Worker/Both rolünde kaydedilir. İzole `LegacyImport` profili yalnız bu importer'ı başlatır.
  L3-L6 handler'larının tamamı aynı advisory-lock sözleşmesine eklendi; günlük tam Legacy ID uzlaştırması
  `FullReconciliationHourUtc` saatinde PII'siz sayım loglar.
- Production MySQL salt-okunur acceptance probe'u geçti. Test yalnız database/version ve platform 41 toplam
  sayımlarını okudu; kaynakta veya hedefte yazma yapmadı.
- L3 üye/adres reader/import dilimi eklendi. Kaynak iki tablo aynı repeatable-read READ ONLY transaction'da
  okunuyor; sıfır tarihler null'a çevriliyor. Native adres silinmiyor; güçlü adres imzasıyla eski Phase22
  kayıtlarına `LegacyAddressId` backfill planlanıyor; silinmiş/anonymize üyeler ve modern parola hash'leri korunuyor.
- Acceptance dışı testler `79/79`; sipariş ve fatura/iade kaynak acceptance testleri ile L7 hedef
  fingerprint testi başarılı; API ve test projesi build başarılı.
- Ayrı SELECT-only MySQL hesabı Api1 private ağ yolundan doğrulandı; READ ONLY probe `1/1` geçti.
- Kontrollü L3-L6 gerçek ilk aktarımı tamamlandı; kapsamlı geri dönüş yedekleri sunucuda tutuluyor.
  `MSR/TYA` ve kalem toplamı kararları uygulandı. Sürekli worker API1 üzerinde ayrı release, ayrı systemd unit
  ve `Node:WorkerProfile=LegacyImport` ile yalnız bu importer'ı çalıştıracak şekilde etkinleştirildi. Repo
  varsayılanı `Enabled=false`, `DryRun=true` ve dilimler kapalı kalır; yalnız sunucu env'i açık/gerçek moda
  override eder. Kalan L8 operasyon işi alarm/izleme ve production cutover kapatma prosedürüdür.

### 12.2 Sürekli worker operasyon kaydı

- Release: `/opt/ECSProsAI/worker-releases/20260901T160952Z_cfc49cf`
- Aktif symlink: `/opt/ECSProsAI/worker-current`
- Unit: `/etc/systemd/system/ecspros-legacy-import.service`
- Env: `/etc/ecspros/legacy-import-worker.env` (`root:ecspros`, `0640`); bağlantı bilgileri dokümana yazılmaz.
- Node: `legacy-import-worker-1`, `Role=Worker`, `WorkerProfile=LegacyImport`, `MigrateOnStartup=false`.
- Listener: yalnız loopback `127.0.0.1:5060`; interval `120` saniye, overlap `30` dakika.
- Kapsam: `members`, `orders`, `invoices`, `returns`; görsel/stok/katalog ve diğer hosted worker'lar yoktur.
- İki dry-run turu aynı sonucu verdi: üyeler `0/5`; siparişler `324/0`; faturalar `166/0`; iadeler `40/0`
  (`potansiyel değişiklik/atlanan`). Dry-run hedefi değiştirmedi.
- Gerçek moda geçiş öncesi geri dönüş yedeği:
  `/var/backups/ecspros-l8/pre-continuous-worker-20260901T161612Z.dump`, `149.447` bayt,
  SHA-256 `7f2daa527457f43a6d51c3f2d88d12678d74c16aa9076b11a451b660b28eb463`.
- İlk iki gerçek tur idempotent geçti: üyeler `changed=0/skipped=5`; diğer üç dilim
  `changed=0/skipped=0`. Dört checkpoint mevcut ve `LastError` alanları boştur.
- Mevcut API unit'i yeniden başlatılmadı; PID ve `NRestarts=0` sabit kaldı. API ve worker `/live`, `/ready`
  sağlık kontrolleri Healthy. `ecspros-legacy-import.service` systemd'de `active/enabled`, worker PID'i
  sabit ve `NRestarts=0` durumundadır.
