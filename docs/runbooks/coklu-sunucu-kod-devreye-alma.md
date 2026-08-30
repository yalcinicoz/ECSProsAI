# Çoklu Sunucu Kod Devreye Alma Runbook'u

> Kapsam: yalnız uygulama kodunun beklediği ayarlar ve kabul kontrolleri. Bu belge canlıya otomatik işlem yapmaz.
> Parolalar repository'ye veya `appsettings.json` dosyasına yazılmaz; environment/secret store kullanılır.

## 1. Node rolleri

Her API VM için benzersiz kimlik ve `Api`, worker VM için `Worker` verilir:

```text
Node__Id=api-1
Node__Role=Api
Node__MigrateOnStartup=false
```

Migration yalnız bakım penceresindeki tek migration job/deploy gate tarafından çalıştırılır. Birden fazla API'nin
startup migration çalıştırmasına izin verilmez.

## 2. PostgreSQL multi-host

```text
ConnectionStrings__DefaultConnection=Host=pg-1.internal,pg-2.internal;Port=5432;Database=ecommerce_db;Username=ecommerce;Password=<secret>;SSL Mode=Require
Postgres__RequirePrimary=true
Postgres__HostRecheckSeconds=10
Postgres__LoadBalanceHosts=true
Postgres__MinPoolSize=5
Postgres__MaxPoolSize=200
Postgres__TimeoutSeconds=5
Postgres__CommandTimeoutSeconds=30
```

`Host` tek adreste local/standalone davranış korunur. İki veya daha fazla adreste Npgsql primary discovery kullanır.
Pool değeri node başınadır: üç API + bir worker için teorik toplam üst sınır `4 × MaxPoolSize` olur; PostgreSQL
`max_connections`/PgBouncer kapasitesi buna göre doğrulanmadan production değeri yükseltilmez.

## 3. Redis cache, kritik state ve SignalR

Cache ile kritik state farklı eviction politikaları yüzünden ayrı logical Redis setlerine yönlendirilir:

```text
ConnectionStrings__RedisCache=redis-cache-s1:26379,redis-cache-s2:26379,redis-cache-s3:26379,password=<secret>,ssl=true
Redis__Cache__Mode=Sentinel
Redis__Cache__ServiceName=ecspros-cache-primary
Redis__Cache__InstanceName=ECSPros:

ConnectionStrings__RedisState=redis-state-s1:26379,redis-state-s2:26379,redis-state-s3:26379,password=<secret>,ssl=true
Redis__State__Mode=Sentinel
Redis__State__ServiceName=ecspros-state-primary

Redis__SignalR__Enabled=true
Redis__SignalR__ChannelPrefix=production:ECSPros:signalr
```

Geçiş süresince yalnız `ConnectionStrings__Redis` verilirse hem cache hem state eski tek Redis'e düşer. Bu
geriye uyumluluk yoludur; nihai production ayrımı değildir. SignalR backplane tüm API node'larında aynı prefix ile
açılmalıdır.

## 4. Object storage

Yorum/iade/talep/vitrin medyası için S3 veya path-style MinIO/OVH Object Storage seçilir:

```text
Storage__Provider=S3
Storage__PublicBaseUrl=https://media.example.com
Storage__S3__ServiceUrl=https://s3.example.internal
Storage__S3__Bucket=ecspros-media
Storage__S3__AccessKey=<secret>
Storage__S3__SecretKey=<secret>
Storage__S3__Region=us-east-1
Storage__S3__ForcePathStyle=true
Storage__S3__AllowHttp=false
Storage__Catalog__Enabled=false
```

`AccessKey`/`SecretKey` repository'ye yazılmaz. Provider streaming upload, delete ve en fazla yedi günlük private
signed read URL üretir. Yorum/iade/talep/vitrin aynı provider'ı kullanır. Katalog image/video adapter'ı hazırdır
fakat mevcut `ImageServer.CdnBaseUrl` ve origin path doğrulanmadan `Storage__Catalog__Enabled=true` yapılmaz.
Opt-in açıldığında object key'ler `catalog/images/products/*` ve `catalog/videos/products/*` altındadır. Worker feed'i
önce atomik yerel temp çıktıya üretir, S3'e yükler; feed endpoint'i doğrulanmış `feedKey` sonrasında 15 dakikalık
signed URL'ye yönlendirir. `feeds/*` bucket policy'de public yapılmaz. Bucket lifecycle/versioning, CDN cache ve
private/public prefix ayrımı altyapı kabulünde yapılır.

## 5. Proxy güven zinciri

`ReverseProxy__KnownProxies`/`KnownNetworks` yalnız uygulamaya doğrudan bağlanan Nginx/LB soketlerini içerir.
Cloudflare veya genel RFC1918 blokları gelişigüzel güvenilir listeye eklenmez. `/live` process, `/ready` ise DB,
Redis state ve Data Protection hazırlığını ölçer; LB upstream health check `/ready` kullanır.

## 6. Migration ön kontrolü

Finansal unique index mevcut mükerrer hareketleri silmez. Migration öncesinde aşağıdaki sorgu sıfır satır dönmelidir:

```sql
SELECT "LedgerId", "TransactionType", "ReferenceType", "ReferenceId", COUNT(*)
FROM accounts.current_account_transactions
WHERE "ReferenceId" IS NOT NULL AND "IsDeleted" = false
GROUP BY "LedgerId", "TransactionType", "ReferenceType", "ReferenceId"
HAVING COUNT(*) > 1;
```

Sonuç varsa finans ekibiyle reconciliation yapılır; otomatik delete uygulanmaz. Ardından Integration feed lease ve
Accounts idempotency migration betikleri incelenip tek migration gate ile uygulanır.

## 7. Atomik release

`tools/deploy/deploy.sh` benzersiz release üretir/dağıtır. Her node'da aktivasyon ayrı yapılır:

```bash
sudo SERVICE_NAME=ecspros.service RETAIN_RELEASES=5 bash tools/deploy/activate-release.sh \
  /opt/ECSProsAI <release-id> http://127.0.0.1:5050/ready
```

Aktivasyon `current` symlink'ini atomik değiştirir. Health başarısızsa önceki release'e döner. Önce bir API canary,
sonra diğer API'ler, en son worker aktive edilir. Aynı anda tüm API'ler restart edilmez.

Disposable Linux hostta aktivasyon/rollback/retention regresyonu:

```bash
bash tools/tests/deploy-activation-regression.sh
```

Betik yalnız `mktemp` ile açtığı test kökünde çalışır; sahte `systemctl`/`curl` kullanır ve canlı servise dokunmaz.
Gerçek POSIX symlink desteklemeyen Windows Git Bash ortamında testi açıkça `SKIP` eder; kabul Linux'ta alınır.

## 8. Zorunlu environment acceptance

Environment variable kullanmak istemeyen yerel operatör, repository kökündeki `appsettingsTest.json` dosyasını
doldurabilir. Bu dosya secret içerdiği için `.gitignore` kapsamındadır ve commit/push edilmez. Environment variable
verilmişse dosyadaki değerin önüne geçer. Dosyayı terminal çıktısına yazdırmayın ve ekran görüntüsüne almayın.

### 8.1 PostgreSQL otomatik acceptance paketi

Test yalnız adı `test` veya `acceptance` içeren, migration uygulanmış izole bir PostgreSQL veritabanında çalışır.
Bağlantı değişkeni yoksa testler dış sisteme bağlanmadan `Skipped` olur. Yazmalı test ayrıca açık onay değişkeni
ister; bir sentetik feed job oluşturur ve `finally` bloğunda yalnız kendi UUID satırını siler. Regresyon SQL'inin
diğer değişiklikleri transaction sonunda rollback edilir. Connection string'i komut satırı argümanına veya
repository dosyasına yazmayın.

PowerShell:

```powershell
$env:ECSPROS_ACCEPTANCE_POSTGRES = Read-Host "Test DB connection string" -MaskInput
$env:ECSPROS_ACCEPTANCE_ALLOW_DB_WRITE = "true"
dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj `
  --filter "TestCategory=Acceptance"
Remove-Item Env:ECSPROS_ACCEPTANCE_POSTGRES
Remove-Item Env:ECSPROS_ACCEPTANCE_ALLOW_DB_WRITE
```

Bash:

```bash
read -rsp "Test DB connection string: " ECSPROS_ACCEPTANCE_POSTGRES && echo
export ECSPROS_ACCEPTANCE_POSTGRES
export ECSPROS_ACCEPTANCE_ALLOW_DB_WRITE=true
dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj \
  --filter 'TestCategory=Acceptance'
unset ECSPROS_ACCEPTANCE_POSTGRES ECSPROS_ACCEPTANCE_ALLOW_DB_WRITE
```

Paket şu üç kanıtı otomatik üretir:

- Bağlanılan PostgreSQL hedefinin recovery/standby değil yazılabilir primary olması.
- Feed migration regresyonu, iki eşzamanlı claim'den yalnız birinin kazanması, lease expiry takeover, completed
  işin yeniden alınmaması ve retry limiti dolan expired lease'in kalıcı `failed` durumuna geçmesi.
- PostgreSQL session advisory lock'ın ikinci bağlantıya kapalı olması ve sahip bağlantı kapanınca alınabilmesi.

### 8.2 S3/MinIO otomatik acceptance paketi

Yalnız adı `test` veya `acceptance` içeren bucket kabul edilir. Test benzersiz `acceptance/{uuid}.txt` objesi
yükler; ikinci provider instance ile signed URL üzerinden okur, siler ve kendi key'ini `finally` bloğunda tekrar
temizler. Secret değerleri repository'ye veya komut argümanına yazılmaz.

```powershell
$env:ECSPROS_ACCEPTANCE_S3_SERVICE_URL = "https://s3-test.example.internal"
$env:ECSPROS_ACCEPTANCE_S3_BUCKET = "ecspros-acceptance"
$env:ECSPROS_ACCEPTANCE_S3_ACCESS_KEY = Read-Host "S3 access key" -MaskInput
$env:ECSPROS_ACCEPTANCE_S3_SECRET_KEY = Read-Host "S3 secret key" -MaskInput
$env:ECSPROS_ACCEPTANCE_S3_ALLOW_WRITE = "true"
dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj `
  --filter "TestCategory=S3"
Remove-Item Env:ECSPROS_ACCEPTANCE_S3_SERVICE_URL, Env:ECSPROS_ACCEPTANCE_S3_BUCKET,
  Env:ECSPROS_ACCEPTANCE_S3_ACCESS_KEY, Env:ECSPROS_ACCEPTANCE_S3_SECRET_KEY,
  Env:ECSPROS_ACCEPTANCE_S3_ALLOW_WRITE
```

Path-style MinIO/OVH için `ECSPROS_ACCEPTANCE_S3_FORCE_PATH_STYLE=true`; farklı region gerekiyorsa
`ECSPROS_ACCEPTANCE_S3_REGION` verilir. HTTP endpoint yalnız test ortamında URL şemasından açıkça algılanır.

### 8.3 Redis otomatik acceptance paketi

Test yalnız açık yazma onayıyla benzersiz, bir dakika TTL'li `ecspros:acceptance:{uuid}` key'i oluşturur; ikinci
bağlantının state'i okuduğunu ve pub/sub mesajını aldığını doğrular, ardından yalnız kendi key/channel'ını temizler.

```powershell
$env:ECSPROS_ACCEPTANCE_REDIS = Read-Host "Test Redis connection string" -MaskInput
$env:ECSPROS_ACCEPTANCE_REDIS_ALLOW_WRITE = "true"
dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj `
  --filter "TestCategory=Redis"
Remove-Item Env:ECSPROS_ACCEPTANCE_REDIS
Remove-Item Env:ECSPROS_ACCEPTANCE_REDIS_ALLOW_WRITE
```

Bu test iki bağlantı arasındaki state/pub-sub temelini kanıtlar; Redis primary kapatma ve Sentinel quorum failover
testinin yerine geçmez.

### 8.4 Çok-node manuel/environment kabulleri

- İki API farklı node'a bağlıyken aynı SignalR bildirimi iki istemciye de ulaşır.
- Redis primary kapatıldığında Sentinel quorum primary seçer; uygulama restart olmadan cache/state reconnect olur.
- PostgreSQL planned switchover sırasında yazma bounded retry ile yeni primary'de sürer.
- İki worker process aynı anda açıldığında settlement/cargo/tracking/saved-search/batch/legacy turunu yalnız biri alır.
- Worker işlem ortasında öldürülünce PostgreSQL session lock bırakılır ve diğer worker sonraki turu alır.
- Feed worker lease expiry/crash recovery testi geçer.
- Deploy'da kasıtlı bozuk `/ready` ile rollback, sonra sağlam release aktivasyonu doğrulanır.

Bu testler gerçek çok-node/Sentinel/Patroni ortamı olmadan “tamamlandı” işaretlenmez.
