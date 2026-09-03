# ECSProsAI — Kod Çalışmaları Devir Belgesi

> Durum tarihi: 2026-09-01  
> Workspace: `D:\NewProje\ECSProsAI`  
> Bu belge yeni **kod geliştirme sohbetinin** başlangıç kaynağıdır.

## 1. Sohbet kapsamı

Bu çalışma alanında yalnız uygulama kodu, Razor WebUI, API, worker kodu, migration kaynakları,
testler ve kod dokümantasyonu ele alınır. SSH, VM kurulumu, Nginx reload, systemd, gerçek deploy,
backup/restore ve canlı servis operasyonu sunucu sohbetinin kapsamıdır.

Yeni sohbet şu sırayla başlamalıdır:

1. `CLAUDE.md` ve repository talimatlarını oku.
2. Bu belgeyi tamamen oku.
3. `PROGRESS.md` dosyasının en güncel bölümünü oku.
4. Yapılacak işe göre aşağıdaki kaynak belgelerden yalnız ilgili olanları oku.
5. Değişiklikten önce `git status --short` ve hedef dosyaların diff'ini incele.

## 2. Değişmez güvenlik ve çalışma kuralları

- Kullanıcı açıkça istemeden GitHub'a commit/push yapılmaz.
- Yerel tracked veya untracked dosyalar silinmez. `git reset --hard`, `git checkout --`, `git clean`
  gibi yerel çalışmayı kaybettirecek komutlar kullanılmaz.
- GitHub değişiklikleri alınmadan önce tracked + untracked yerel çalışma güvenli biçimde yedeklenir.
  Doğrudan `git pull` yapılmaz; olası çakışmalar dosya dosya çözülür.
- `appsettingsTest.json` secret içerir, `.gitignore` kapsamındadır; içeriği terminale, loga, belgeye,
  commit'e veya mesaja yazılmaz.
- Production MySQL üzerinde hiçbir `INSERT`, `UPDATE`, `DELETE`, DDL veya yetki değişikliği yapılmaz.
  Gerektiğinde yalnız ayrı SELECT-only hesap ve server-side READ ONLY transaction kullanılır.
- `.59` legacy production PostgreSQL yeni sistem için sürekli veri kaynağı değildir ve değiştirilmez.
- Migration yalnız hedef database kimliği doğrulandıktan ve ayrıca onaylandıktan sonra uygulanır.
- Çalışan modüller gereksiz yeniden yazılmaz; diff istenen işle sınırlı tutulur.
- Her tamamlanan iş `PROGRESS.md` ve ilgili faz belgesine işlenir.
- AGENTS talimatı gereği etkileşimli oturumda `npm run build` çalıştırılmaz.

## 3. Git ve çalışma ağacı durumu

- Branch: `main`
- HEAD: `30dd430c9e40ad9266ce9a3a3d8b524ef0eca41b`
- Son commit tarihi: `2026-08-30`
- Çalışma ağacı **dirty** durumdadır.
- Aşağıdaki çalışmalar henüz GitHub'a gönderilmemiştir:
  - Çoklu node rolleri, health/readiness, Redis ayrımı ve deployment yardımcıları.
  - ERP/V3 MSSQL katalog ve fiyat reader/worker katmanı.
  - Production MySQL SELECT-only geçici üye/sipariş/fatura/iade importer'ı.
  - Production MySQL SELECT-only geçici stock-only worker'ı.
  - Legacy identity alanları ve CRM/Order/Integration migration kaynakları.
  - Nginx iki API upstream örnekleri ve test subdomain deploy dosyaları.
  - Kategori görseli ilk boya ve responsive `srcset` düzeltmeleri.
  - İlgili unit ve acceptance testleri ile plan/runbook dokümanları.

Tracked ve untracked dosyaların kesin listesi her oturumda `git status --short` ile yeniden alınmalıdır.
Özellikle `src/ECSPros.Api/Services/ErpSource/`, `LegacyImport/`, `LegacyStock/`, yeni migration dosyaları
ve testler untracked olabilir; kesinlikle kaybedilmemelidir.

Önemli: API1 ve API2'de çalışan son paket bu dirty yerel çalışma ağacından üretildi. Sunucudaki aktif
release ile repository HEAD tek başına aynı kodu temsil etmez. GitHub'dan değişiklik alma işlemi ancak tüm
yerel dosyalar güvenceye alındıktan sonra yapılmalıdır.

## 4. Uygulama mimarisinin mevcut kod karşılığı

- `src/ECSPros.Api` hem ASP.NET Core API'yi hem Razor WebUI/storefront'u içerir.
- Nginx sonrasında ayrı bir WebUI servisi yoktur; istekler API düğümlerindeki aynı uygulamaya gider.
- API düğümleri `Node__Role=Api`, izole worker process'leri `Node__Role=Worker` kullanır.
- `Node__MigrateOnStartup=false` API düğümlerinde korunur.
- İzole worker profilleri:
  - `LegacyImport`
  - `LegacyStock`
  - `ErpSource`
  - Geriye uyumlu genel profil: `All`
- Redis cache ve kritik state kodda ayrı bağlantılar olarak desteklenir:
  - `ConnectionStrings__RedisCache`
  - `ConnectionStrings__RedisState`
- SignalR çoklu API için Redis backplane destekler.
- PostgreSQL kodu multi-host/Npgsql primary discovery ayarlarını destekler; fakat gerçek standby/failover
  altyapısı henüz kurulmuş değildir.
- Ürün görselleri API diskinde tutulmaz; ayrı görsel sunucusu/subdomain üzerinden gelir.
- Genel `Storage__Catalog__Enabled=false` kalır. Panel yüklemeleri için WebP/SFTP origin + JPEG/OVH object
  storage çift yazan `CatalogImageStorage` adapter'ı varsayılan aktiftir; bağlantı bilgileri admin katalog
  ayarlarında şifreli saklanır. Yerel adapter yalnız geliştirme/teşhis ortamında
  `CatalogImageStorage__Enabled=false` açık override'ıyla seçilir.

## 5. Kesin veri otoritesi kararları

| Veri | Geçiş dönemi kaynağı | Production cutover sonrası |
|---|---|---|
| Ürün/katalog/varyant/özellik | V3 ERP MSSQL | V3 ERP MSSQL |
| Fiyat | V3 ERP MSSQL | V3 ERP MSSQL |
| Stok | Production MySQL, geçici stock-only worker | ECSPros admin paneli |
| Üye | Production MySQL, geçici SELECT-only importer | ECSPros |
| Sipariş | Production MySQL, geçici SELECT-only importer | ECSPros |
| Fatura | Production MySQL, geçici SELECT-only importer | ECSPros |
| İade | Production MySQL, geçici SELECT-only importer | ECSPros |
| Ürün görsel dosyası | Ayrı görsel sunucusu/subdomain | Aynı harici görsel servisi |

Ek kurallar:

- MSSQL hiçbir zaman stok kaynağı değildir; ERP stok reader/worker kodu kaldırılmıştır.
- `51.178.208.59` PostgreSQL yalnız tamamlanmış başlangıç dump/restore kaynağıdır. Yeni veya değişen
  kayıtlar buradan yeni PostgreSQL'e senkronlanmaz.
- `.59` üzerindeki mevcut `LegacySyncWorker` yalnız eski production sitenin devamlılığı içindir.
- Geçici MySQL importer'ları yeni sistemde native oluşturulmuş kayıtları değiştirmez; yalnız `Legacy*Id`
  ile sahip olduğu kayıtları idempotent upsert eder.
- Fatura serileri `MSR` ve `TYA` olarak onaylanmıştır.
- İade tutarında kontrollü ilk aktarım kararı kalem toplamıdır; repository güvenli varsayılanı
  `ReturnAmountMismatchPolicy=Block` olarak kalır.

## 6. Tamamlanan ana kod çalışmaları

### 6.1 Çoklu sunucu kod fazı

- Node kimliği/rolü ve worker profile doğrulaması eklendi.
- API readiness; PostgreSQL primary yazılabilirliği, Redis state ve Data Protection key ring kontrolü yapar.
- Forwarded headers güven zinciri, Redis cache/state ayrımı, SignalR backplane ve multi-host PostgreSQL
  hazırlığı kodlandı.
- Atomik release/rollback betikleri ve iki API regresyon testleri eklendi.
- API düğümlerinde startup migration kapalıdır.

### 6.2 ERP/V3 MSSQL katalog ve fiyat

- `Services/ErpSource` katmanı eklendi.
- Katalog/fiyat dilimleri, timezone dönüşümü, checkpoint + overlap, PostgreSQL advisory lock,
  Code/Barcode idempotent upsert ve integration log tamamlandı.
- Mapping eksikliğinde checkpoint ilerlemez; sessiz ürün grubu fallback'i yoktur.
- Gerçek kaynak dry-run/real/idempotency kabulü tamamlandı.
- ERP stok kodu tamamen kaldırıldı.
- ERP outbound sipariş/fatura/iade yazımı yapılmadı; doğrulanmış write procedure/API sözleşmesi yoktur.

### 6.3 Geçici Legacy MySQL importer

- `Services/LegacyImport` altında üye/adres, sipariş aggregate, fatura, iade ve PII'siz reconciliation
  kodlandı.
- Additive legacy identity migration'ları hazırlandı ve yeni PostgreSQL'e uygulandı.
- İlk gerçek aktarım ve ikinci idempotency koşusu tamamlandı.
- Son uzlaştırma: üye `104/104`, adres `99/104` (5 kaynak yetimi), sipariş `71/71`, satır `181/181`,
  ödeme `72/72`, fatura `45/45`, iade `12/12`, iade kalemi `28/28`.
- Gerçek refund, stok hareketi, rezervasyon, domain event veya outbound çağrı üretilmez.
- Repository ayarları varsayılan kapalı/dry-run kalır; yalnız sunucu secret env kontrollü override eder.

### 6.4 Geçici Legacy stock-only worker

- `Services/LegacyStock` yalnız production MySQL'i SELECT-only + server-side READ ONLY okur.
- PostgreSQL yazıları tek transaction'dır; advisory lock, fail-closed eşleme, Redis cache invalidation içerir.
- Dump sonrası eksik mapping onarımı idempotent tamamlandı ve yeniden kapatıldı.
- Gerçek stok snapshot'ı sonrası ikinci/final tur `0` değişiklik verdi.
- Kaynak/eşleşen aktif kullanılabilir stok `253.847/253.847`; eşleşmeyen satır/adet `0/0`.
- Cutover'a kadar admin stok değişikliği sonraki worker turunda MySQL gerçeğiyle ezilir. Admin stok
  otoritesi ancak stock worker kapatıldıktan sonra devralır.

### 6.5 Kategori görsel düzeltmesi

Son değişen dosyalar:

- `src/ECSPros.Api/Services/Store/UrunGorselSrcset.cs`
- `src/ECSPros.Api/Views/ProjeElementleri/Urun/_UrunKarti.cshtml`
- `src/ECSPros.Api/Views/ProjeElementleri/UrunListesi/_UrunListesiUrunAlani.cshtml`
- `tests/ECSPros.Api.Tests/UrunGorselSrcsetTests.cs`

Uygulanan davranış:

- Lazy kart skeleton/placeholder HTML'i sunucu tarafında basılır; JavaScript öncesi kırık görsel ikonu ve
  `alt` metni parlamaz.
- İlk masaüstü satırındaki beş görsel eager başlar; yalnız ilk LCP adayı `fetchpriority=high` alır.
- Kart genişlikleri `240, 360, 480, 640, 768, 1024` olarak genişletildi.
- Kategori `sizes` değeri mobil/tablet iki kolon ve masaüstü grid'e göre düzenlendi.
- Infinite scroll kartları aynı `srcset`/`sizes` politikasını üretir.
- URL'deki `/85/` yükseklik değil CDN kalite parametresidir ve korunur.
- Eksik dosyada Nginx'in “Resim hazırlanıyor” cevabını ezebilecek frontend `onerror` eklenmedi.

### 6.6 Toplu resim yükleme harici hedefi

- Yeni benzersiz basename korunur; DB'de WebP dosya adı tutulur.
- WebP harici SFTP origin'de `/var/www/html/images/`, JPEG aynı basename ile OVH S3-compatible bucket kökünde
  saklanır. İki hedef birlikte başarılı olmadan kayıt aktifleşmez.
- Replace işlemi tüm yeni grup başarılı olduktan sonra eski metadata ve kullanılmayan iki fiziksel kopyayı siler;
  yarım yüklemede eski resimler korunur.
- Başarıyla yüklenen kaynak dosya tarayıcı izin veriyorsa kullanıcının yerel `yuklenenler/` klasörüne taşınır.
- Kod default açıktır ve iki hedeften biri başarısızsa fail-closed davranır; düz secret değerler repoya veya DB'ye yazılmaz. Panelde girilen secret'lar DB'de
  Data Protection ciphertext olarak saklanır. Migration yoktur. Gerçek harici hedef kabulü yayın fazına aittir.
- SFTP ve OVH S3 bağlantı değerleri admin `/catalog/settings` ekranındaki `ImageServer.Sftp*` ve
  `ImageServer.S3*` key-value kayıtlarından her upload başında okunur; yeni kolon/migration yoktur. SFTP şifresi,
  S3 access key ve secret key Data Protection ile şifreli saklanır, GET yanıtında `•••` olarak maskelenir.
- Kullanıcıya yönelik eski `ImageServer.Ftp*` ve `VideoServer.Ftp*` alanları ayar formundan kaldırıldı; kullanıcı
  FTP istemcisi kullanmaz. Eski DB key'leri veri silmemek için yerinde bırakılır ve form Kaydet akışına girmez.

## 7. Son test durumu

- `UrunGorselSrcsetTests`: `2/2` başarılı.
- Acceptance dışı testler: `104/104` başarılı; dual-target upload testleri `4/4` başarılı.
- Admin `npx tsc --noEmit -p tsconfig.app.json`: başarılı.
- Toplu resim sayfasının tek-dosya ESLint kontrolü önceden mevcut 11 `any/@ts-ignore` ihlalinde kırmızı;
  bu çalışmanın yeni satırlarında lint hatası yok.
- Release `dotnet publish -c Release --no-restore` başarılı; var olan derleyici uyarıları yeni hata değildir.
- Tam acceptance çalışmasında private MSSQL/MySQL/PostgreSQL bağlantısı gerektiren 17 test yerel ağ
  erişimi olmadığı için başarısız olmuştu; bu görsel değişikliğinin regresyonu değildir.

Temel yerel komutlar:

```powershell
git status --short
dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj --no-restore `
  --filter "FullyQualifiedName!~.Acceptance."
```

Acceptance testleri yalnız doğru private ağ/tünel, izole test veritabanı ve gereken açık yazma onayıyla
çalıştırılmalıdır.

### 7.1 Yerel acceptance bağlantı yöntemi — kalıcı not

Bağlantı değerleri yeniden sorulmaz veya terminale yazdırılmaz. Testler repository kökünden çalıştırılır;
`AcceptanceTestEnvironment`, çalışma dizininden yukarı doğru `.gitignore` kapsamındaki
`appsettingsTest.json` dosyasını bulur. Öncelik environment variable, sonra aşağıdaki config anahtarıdır:

| Kaynak | Environment variable | `appsettingsTest.json` anahtarı |
|---|---|---|
| V3 ERP MSSQL | `ECSPROS_ACCEPTANCE_ERP_SOURCE` | `ConnectionStrings:ErpSource` |
| Hedef PostgreSQL | `ECSPROS_ACCEPTANCE_ERP_TARGET` | `ConnectionStrings:DefaultConnection` |
| Legacy MySQL salt-okunur import | `ECSPROS_ACCEPTANCE_LEGACY_READ` | `LegacyReadImport:ConnectionString` |
| Legacy MySQL stock salt-okunur | `ECSPROS_ACCEPTANCE_LEGACY_MYSQL` | `LegacyReadImport:ConnectionString` |

Codex/managed oturumda private veritabanı testi ilk seferden ağ erişim onayıyla çalıştırılmalıdır. Sandbox
içindeki `Named Pipes / Access is denied`, TCP timeout veya host erişilemiyor sonucu credential hatası diye
yorumlanmamalı; aynı secret tekrar aranıp yazdırılmamalıdır. V3 için doğrulanmış çalışma şekli:

```powershell
dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~ErpSource_HedefliUrunSnapshotVeBarkodCozumu_Okunur"
```

Belirli ürün/özellik salt-okunur kabulünde yalnız test seçicileri environment'a verilir; bağlantı yine
`appsettingsTest.json` içinden okunur:

```powershell
$env:ECSPROS_ACCEPTANCE_ERP_PRODUCT_CODE='P-00022932'
$env:ECSPROS_ACCEPTANCE_ERP_KEYWORD_ID='20'
$env:ECSPROS_ACCEPTANCE_ERP_KEYWORD_VALUE='Rahat Kalıp'
dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~ErpSource_HedefliUrunSnapshotVeBarkodCozumu_Okunur"
```

Bu yöntemle V3 bağlantısı ve `P-00022932 / 20 / Rahat Kalıp` snapshot kabulü `1/1` geçmiştir. Hedef
PostgreSQL config'i aynı dosyadan doğru okunmuş, fakat `192.168.0.241:5432` rota erişimi timeout vermiştir;
bu credential keşfi problemi değildir. Private rota yoksa kod sohbetinde SSH/tünel kurulmaz, erişim durumu
net raporlanır. Yazmalı acceptance için ayrıca izole test/acceptance isimli database ve açık
`ECSPROS_ACCEPTANCE_ALLOW_DB_WRITE=true` kapısı gerekir; production database'e yöneltilmez.

## 8. Açık veya bloke kod işleri

1. **GitHub entegrasyonu:** Yerel tracked + untracked çalışma kaybedilmeden upstream değişiklikleri alınmalı,
   çakışmalar çözülmeli ve 88 test yeniden çalıştırılmalı. Kullanıcı açıkça istemeden push yapılmamalı.
2. **ERP outbound E7:** Yeni sipariş/fatura/iade yazan adapter, kesin V3 write procedure/API sözleşmesi
   gelmeden yazılmamalı. İncelenen procedure'ler doğrulanmış create/upsert sözleşmesi değildir.
3. **Production cutover kod kapıları:** Geçici MySQL importer/stock worker kapatma ve yeni ECSPros outbound
   servislerini açma ayrı faz, yedek ve açık işletme onayı gerektirir.
4. **Toplu resim harici hedef kabulü:** Dual SFTP WebP + OVH JPEG kodu ve varsayılan aktivasyonu
   `20260902T131010Z_catalog_image_failclosed` release'iyle API1/API2'ye yayınlandı. Admin paneldeki şifreli
   bağlantı bilgileriyle kontrollü tek bir gerçek ürün upload/CDN kabulü henüz yapılmadı; bu son işlevsel
   kabulde başarılı yüklemenin iki hedefte oluştuğu ve CDN'de gerçek görselin geldiği doğrulanmalıdır.
5. **Gerçek tarayıcı görsel QA:** Son kod public test ortamında HTML olarak doğrulandı; mobil/desktop görsel
   gözlemi ve Network `currentSrc` kontrolü kullanıcıyla tamamlanabilir.

## 9. Kod sohbetinin sunucu sohbetine devredeceği işler

Kod sohbeti deploy yapmaz. Aşağıdaki bilgileri sunucu sohbetine verir:

- Değişen dosyalar ve migration gerekip gerekmediği.
- Geçen testler ve bilinen uyarılar.
- Oluşturulacak release'in kapsamı.
- Gerekli environment key adları; secret değerleri değil.
- Beklenen `/live`, `/ready` ve işlevsel kabul kriterleri.

## 10. Ana kaynak belgeler

- `PROGRESS.md`
- `docs/acik-isler-yol-haritasi.md`
- `docs/coklu-sunucu-kalan-isler-ve-hedef-konfigurasyon.md`
- `docs/coklu-sunucu-yerel-kod-fazi-kapanis-raporu.md`
- `docs/runbooks/coklu-sunucu-kod-devreye-alma.md`
- `docs/erp-kaynak-senkron-gecis-plani.md`
- `docs/legacy-mysql-uye-siparis-fatura-iade-okuma-plani.md`

## 11. Yeni kod sohbetine yapıştırılacak başlangıç mesajı

```text
Önce CLAUDE.md, docs/handoff/kod-calismalari-devri.md ve PROGRESS.md dosyalarını tamamen oku.
Bu sohbet yalnız ECSProsAI kod çalışmaları için kullanılacak. Sunucu/SSH/deploy işlemi yapma.
Yerel tracked ve untracked değişiklikleri koru; git reset/clean kullanma. Ben açıkça istemeden
GitHub'a commit veya push yapma. appsettingsTest.json içindeki secret'ları ekrana basma.
İlk işlem olarak git status --short ile mevcut çalışma ağacını doğrula ve bana net durumu raporla.
```
