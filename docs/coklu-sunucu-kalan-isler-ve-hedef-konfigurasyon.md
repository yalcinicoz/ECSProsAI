# ECSProsAI Çoklu Sunucu Kalan İşler ve Hedef Konfigürasyon Raporu

**Tarih:** 30 Ağustos 2026
**Kapsam:** 4.000 eşzamanlı aktif kullanıcı, iki OVH fiziksel ESXi sunucusu, uygulama ve altyapı yüksek erişilebilirliği
**Durum (30 Ağustos 2026 kod güncellemesi):** Çoklu-node kod sertleştirmelerinin büyük bölümü yerelde
uygulanmıştır; gerçek Patroni/Sentinel ve environment acceptance yapılmadığı için fiziksel
sunucu kaybına dayanıklı tam HA henüz tamamlanmamıştır. Güncel madde durumu için tek kaynak
`docs/acik-isler-yol-haritasi.md` FAZ 11'dir.

## 1. Yönetici özeti

Mevcut kod iki veya daha fazla API örneğini çalıştırmaya yönelik önemli hazırlıkları içeriyor. Data Protection anahtarlarının ortak veritabanında saklanması, Redis tabanlı güvenlik durumu, node rolü, migration başlangıç kilidi, ortak cache invalidation ve health endpoint'leri yapılmış durumda. Buna rağmen sistem bugün **tam yüksek erişilebilir** kabul edilemez.

Kesin nedenler şunlardır:

- Tek Nginx, tek PostgreSQL primary, tek Redis ve tek NFS kullanılırsa bunların her biri tek hata noktasıdır.
- OVH Load Balancer yalnız fiziksel sunucuyu kontrol ederse fiziksel sunucu açıkken içindeki Nginx veya API VM arızasını doğru tespit edemez. Health check, Nginx üzerinden API `/ready` endpoint'ine kadar gitmelidir.
- SignalR Redis backplane, Redis cache/state ayrımı ve Sentinel-aware discovery kodu eklendi; gerçek üç-Sentinel
  failover ve cross-node mesaj kabulü bekliyor.
- PostgreSQL multi-host primary targeting, typed pool ve yazılabilir-primary `/ready` kontrolü eklendi; Patroni
  switchover kabulü bekliyor.
- Altı dış etkili worker turuna PostgreSQL distributed lock, hakediş defterine idempotency unique index eklendi;
  sağlayıcı bazlı timeout-after-success/idempotency kabulü bekliyor.
- Feed kuyruğu atomik claim/lease/crash recovery durum makinesine geçirildi; izole PostgreSQL kabulü bekliyor.
- Doğrudan upload ve feed çıktıları `IFileStorage` sağlayıcı ailesine bağlandı; katalog image/video adapter'ı
  geriye uyumlu opt-in olarak hazır. Üretim kararı gereği ürün görselleri projede tutulmayacak; mevcut ayrı
  görsel sunucusundan subdomain/CDN URL'leriyle sunulacak. S3/MinIO aktivasyonu ertelendi ve katalog adapter'ı
  `Storage:Catalog:Enabled=false` kalacak.

Önerilen son yapı, iki fiziksel sunucu üzerinde simetrik **2 Nginx + 4 API + 2 PostgreSQL + 2 Redis** dağılımı ve üçüncü bağımsız bir witness'tır. Üç API teknik olarak kurulabilir fakat 2+1 asimetrik dağılım yüzünden fiziksel sunucu kaybında kapasite öngörüsü zayıftır. Bu nedenle üretim hedefi 2+2, toplam dört API olmalıdır.

## 2. Mevcut durum

| Alan | Durum | Açıklama |
|---|---|---|
| Data Protection key paylaşımı | Tamamlandı | Anahtarların PostgreSQL'de tutulması ve dosya fallback'i mevcut. |
| Node rolü | Kod tamam | `Api`, `Worker`, `Both`; geçersiz rol startup'ı durdurur. Gerçek node kabulü bekliyor. |
| Device/security state | Tamamlandı | Redis kullanımı ve fail-closed davranış mevcut. |
| Login sayaçları | Kod tamam | Dağıtık sayaç Redis'e taşındı; proxy trust/spoof unit testleri geçti. Gerçek LB zinciri bekliyor. |
| Feed job/status | Kod tamam | Atomik claim/lease/heartbeat/retry/crash recovery var; izole DB kabulü bekliyor. |
| Migration güvenliği | Tamamlandı | Başlangıç gate/kilit yaklaşımı mevcut. Linux dry-run doğrulaması gerekli. |
| Health endpoint'leri | Kod tamam | `/health/detail` authorization korumalı; LB zinciri kabulü bekliyor. |
| Cache invalidation | Tamamlandı | Redis pub/sub ile node'lar arası cache bust mevcut. |
| Deploy betiği | Kod tamam | Benzersiz release, atomik symlink, `/ready` rollback var; Linux dry-run bekliyor. |
| İkinci uygulama node'u | VM-loss kabulü geçti | İki ayrı VM'de shared state/auth SignalR ve gerçek API-A power-off sonrası API-B login/readiness/SignalR sürekliliği geçti; üretim Nginx upstream'i bekliyor. |
| SignalR backplane | Kod + iki-VM auth mesaj tamam | Aynı JWT ile authenticated A→B Redis-backplane mesajı geçti; Sentinel primary-loss/reconnect bekliyor. |
| PostgreSQL otomatik failover | Kısmi | Npgsql multi-host ve writable-primary readiness hazır; Patroni/etcd ve switchover kabulü bekliyor. |
| Redis otomatik failover | Kısmi | Sentinel-aware cache/state config hazır; Redis/Sentinel altyapısı bekliyor. |
| Node bağımsız storage | Kararla kapandı | Ürün görselleri API'de tutulmaz; mevcut harici subdomain/CDN kullanılır. S3 aktivasyonu ertelendi. |
| Otomatik test | Kısmi | 49 unit, 3 PostgreSQL, 1 Redis, iki-VM shared-state/auth SignalR ve API VM power-off geçti; fiziksel host ve stateful failover bekliyor. |
| Worker idempotency | Kısmi | Distributed cycle lock ve finans idempotency var; adapter bazlı kabul açık. |
| Kalıcı dosya depolama | Mimari karar | Ürün görselleri harici görsel sunucusunda; ürün dışı upload/feed açılırsa ortak path ayrıca sağlanır. |
| 4.000 kullanıcı testi | Bekliyor | Yük, soak ve fiziksel host failover testi yapılmadı. |

**Kapsam kararı (30 Ağustos 2026):** Küçük disposable VM test fazı API VM-loss ve authenticated SignalR kanıtıyla
kapatıldı. Patroni/Sentinel, Nginx/LB, fiziksel host ve 4.000 kullanıcı testleri küçük VM'lerde tekrarlanmayacak;
gerçek üretim öncesi yerleşimde FAZ 12 kabulü olarak çalıştırılacaktır. Güncel öncelik kalan kod işleridir.

Bu tabloya göre sistem API process/VM arızasına hazırlanmıştır; tek fiziksel hosta bağlı Nginx, DB veya Redis
arızasına karşı tamamlanmış değildir. Harici görsel sunucusunun sürekliliği bu uygulama kümesinden ayrı izlenir.

## 3. Hedef mimari

```text
Kullanıcılar
    |
Cloudflare (DNS, WAF, CDN, DDoS)
    |
OVH Load Balancer
    |-- aktif health check --> nginx-1 -> /ready -> sağlıklı API
    `-- aktif health check --> nginx-2 -> /ready -> sağlıklı API
           |                              |
        nginx-1                        nginx-2
         /    \                         /    \
      api-1  api-2                   api-3  api-4
         \      \                     /      /
          PostgreSQL primary <-> synchronous standby
          Redis primary       <-> replica + Sentinel quorum
          Harici ürün görsel sunucusu + subdomain/CDN (API yolunun dışında)

Quorum: ha-1 + ha-2 + bağımsız witness (etcd-3 ve Sentinel-3)
```

Trafik akışı şu olmalıdır:

`Cloudflare -> OVH Load Balancer -> Nginx VM -> API -> PostgreSQL/Redis`

Ürün resmi trafiği API'den geçmez. PostgreSQL yalnız harici görsel sunucusundaki subdomain/CDN URL veya path
bilgisini taşır; API dosyayı indirmez, proxy etmez ve kendi diskine yazmaz. Ürün dışı özel upload özellikleri
açılırsa bunlar tüm API'lerin gördüğü ortak path veya mevcut harici dosya servisiyle ayrıca çözülmelidir.

## 4. Fiziksel sunucu konfigürasyonu

İki fiziksel sunucu da diğer fiziksel sunucu tamamen kapandığında 4.000 aktif kullanıcı yükünü tek başına taşıyacak kapasitede olmalıdır. Normal çalışma yüzde 40-50 kaynak bandını hedeflemelidir; failover anında kalan hostun yüzde 80-85'i aşmaması gerekir.

| Kaynak | Node 1 | Node 2 | Not |
|---|---:|---:|---|
| CPU | En az 32 yüksek frekanslı fiziksel core | En az 32 yüksek frekanslı fiziksel core | Tek host tüm üretim yükünü taşımalı. |
| RAM | 256 GB ECC | 256 GB ECC | DB/Redis için memory reservation uygulanmalı. |
| Disk | En az 2 x 3.84 TB enterprise NVMe, RAID1 | En az 2 x 3.84 TB enterprise NVMe, RAID1 | Mümkünse PostgreSQL data ve WAL ayrı disk grubunda. |
| Network | 10 Gbps private + yedekli public uplink | 10 Gbps private + yedekli public uplink | Replikasyon ve kullanıcı trafiği ayrılmalı. |
| Hypervisor | VMware ESXi | VMware ESXi | Aynı patch seviyesi ve NTP. |
| ESXi rezervi | 16 GB RAM ve yeterli CPU headroom | 16 GB RAM ve yeterli CPU headroom | Hypervisor kaynaksız bırakılmamalı. |

Kurallar:

- PostgreSQL ve Redis VM'lerinde memory ballooning kapatılmalı, RAM reservation verilmelidir.
- Toplam vCPU:pCPU oranı üretimde tercihen `1.5:1` değerini geçmemelidir.
- PostgreSQL diskleri thin provision yerine performansı öngörülebilir bir yapı ile verilmelidir.
- VM snapshot'ı veritabanı yedeği sayılmamalıdır.
- İki host aynı güç, switch veya tek storage failure domain'ine bağlıysa fiziksel HA tamamlanmış sayılmaz.

## 5. Önerilen VM yerleşimi

### 5.1 Node 1

| VM | vCPU | RAM | Disk | Görev |
|---|---:|---:|---:|---|
| `nginx-1` | 2 | 4 GB | 30 GB | Reverse proxy, TLS origin, upstream health zinciri |
| `api-1` | 8 | 16 GB | 80 GB | API/WebUI, `Role=Api` |
| `api-2` | 8 | 16 GB | 80 GB | API/WebUI, `Role=Api` |
| `postgres-1` | 12 | 64 GB | 100 GB OS + 1 TB data + 200 GB WAL | İlk primary, Patroni üyesi |
| `redis-1` | 4 | 16 GB | 50 GB OS + 100 GB data | Redis master/replica rolü, Sentinel-1 |
| `ha-1` | 2 | 4 GB | 30 GB | etcd-1 |

### 5.2 Node 2

| VM | vCPU | RAM | Disk | Görev |
|---|---:|---:|---:|---|
| `nginx-2` | 2 | 4 GB | 30 GB | Reverse proxy, TLS origin, upstream health zinciri |
| `api-3` | 8 | 16 GB | 80 GB | API/WebUI, `Role=Api` |
| `api-4` | 8 | 16 GB | 80 GB | API/WebUI, `Role=Api` |
| `postgres-2` | 12 | 64 GB | 100 GB OS + 1 TB data + 200 GB WAL | Synchronous standby, Patroni üyesi |
| `redis-2` | 4 | 16 GB | 50 GB OS + 100 GB data | Redis replica/master rolü, Sentinel-2 |
| `ha-2` | 2 | 4 GB | 30 GB | etcd-2 |

### 5.3 Bağımsız witness

| VM | vCPU | RAM | Disk | Yer | Görev |
|---|---:|---:|---:|---|---|
| `witness-1` | 2 | 4 GB | 40 GB SSD | İki ESXi hosttan ve mümkünse aynı OVH failure domain'inden bağımsız | etcd-3 ve Sentinel-3 |

Witness kullanıcı trafiği taşımaz. Görevi ağ bölünmesinde quorum sağlayarak çift-primary/split-brain riskini azaltmaktır. İki fiziksel host üzerinde yalnız iki oy kullanıcılı Patroni/Sentinel yapısı otomatik failover için yeterli güvenliği vermez.

### 5.4 Üç API alternatifi

Zorunlu olarak üç API kullanılacaksa dağılım `2 + 1` olur. Tek API kalan fiziksel host senaryosunda bu VM en az **16 vCPU / 32 GB RAM** olmalı ve 4.000 aktif kullanıcı failover yük testini tek başına geçmelidir. Simetrik kapasite, bakım kolaylığı ve öngörülebilir failover için öneri yine dört API'dir.

## 6. Ağ, load balancer ve firewall

En az dört mantıksal ağ/VLAN ayrılmalıdır:

| Ağ | Erişebilenler | Amaç |
|---|---|---|
| APP | LB, Nginx, API | HTTP/HTTPS uygulama trafiği |
| DATA | API, PostgreSQL, Redis | DB ve cache/state erişimi |
| REPLICATION | PostgreSQL/Redis/etcd üyeleri | WAL, Redis replication ve quorum |
| MGMT | VPN/bastion ve yöneticiler | SSH, ESXi, izleme ve bakım |

Güvenlik ilkeleri:

- PostgreSQL `5432`, Redis portları ve etcd portları internete açılmamalıdır.
- API VM portlarına doğrudan internet erişimi olmamalı; yalnız Nginx VM'leri erişmelidir.
- Origin Nginx yalnız Cloudflare/OVH LB kaynaklarından trafik kabul etmelidir.
- `KnownProxies`/`KnownNetworks` yalnız gerçek LB ve Nginx adreslerini içermelidir. Tüm RFC1918 ağlarını güvenilir saymak kaldırılmalıdır.
- OVH LB health check, fiziksel ESXi IP'sini değil her Nginx VM'de Nginx üzerinden API `/ready` yolunu test etmelidir.
- `/live` yalnız process canlılığını, `/ready` DB/Redis ve zorunlu bağımlılıkların hazır oluşunu ölçmelidir.
- `/health/detail` internete anonim açık olmamalı; private network veya yetkilendirme ile korunmalıdır.

## 7. Servis bazında hedef ayarlar

### 7.1 Nginx

Başlangıç değerleri:

```nginx
worker_processes auto;
worker_rlimit_nofile 65535;

events {
    worker_connections 8192;
}
```

- Upstream keepalive: `256`
- Backend protokolü: HTTP/1.1
- Connect timeout: `3s`
- Normal read timeout: `60s`
- SignalR/WebSocket read timeout: `3600s`
- Maksimum request body başlangıç değeri: `30m`
- Auth ve pahalı endpoint'lerde paylaşımlı Nginx rate-limit zone kullanılmalı.
- `/hubs` için WebSocket upgrade header'ları açık olmalı.
- `proxy_next_upstream` gerçek konfigürasyonda etkin olmalı; yalnız örnek dosyada yorum olarak kalmamalı.
- SignalR backplane tamamlandıktan sonra sticky session zorunlu değildir. Backplane öncesinde sticky yalnız geçici bir önlemdir.

### 7.2 PostgreSQL

- Patroni ile primary/standby yönetimi.
- `etcd-1`, `etcd-2`, `etcd-3` ile üç üyeli quorum.
- Aynı region içindeki iki host arasında synchronous replication.
- Uygulamada `NpgsqlDataSourceBuilder.BuildMultiHost()` ve primary hedefleme.
- Örnek bağlantı yaklaşımı:

```text
Host=pg-1.internal,pg-2.internal;Port=5432;Database=...;Username=...;Password=...;Target Session Attributes=primary;Host Recheck Seconds=3;Maximum Pool Size=50
```

Parola veya secret hiçbir dokümana ya da repoya yazılmamalıdır.

Başlangıç tuning değerleri, gerçek load testten sonra ölçülerek değiştirilmelidir:

| Ayar | Başlangıç değeri |
|---|---:|
| `shared_buffers` | 16 GB |
| `effective_cache_size` | 48 GB |
| `work_mem` | 16 MB |
| `maintenance_work_mem` | 2 GB |
| `max_connections` | 300 |
| `wal_level` | replica |
| `max_wal_senders` | 10 |
| `max_replication_slots` | 10 |
| `synchronous_commit` | on |
| `checkpoint_timeout` | 15 min |
| `max_wal_size` | 16 GB |

API havuzları toplamı kontrol edilmelidir. Dört API ve diğer servislerin toplam connection ihtiyacı büyürse PgBouncer transaction pooling değerlendirilmelidir.

Yedekleme:

- Standby sunucu yedek değildir.
- pgBackRest veya WAL-G ile sürekli WAL arşivi.
- Günlük differential, haftalık full yedek.
- Farklı fiziksel lokasyonda immutable/object-lock destekli kopya.
- Aylık restore testi ve kayıtlı sonuç.
- Synchronous yapı ve başarılı tatbikat sonrasında hedef RPO yaklaşık `0`, hedef RTO `60-120 saniye` olabilir; test edilmeden garanti olarak yazılmamalıdır.

### 7.3 Redis

- `redis-1` ve `redis-2` farklı fiziksel hostlarda primary/replica.
- `Sentinel-1`, `Sentinel-2`, `Sentinel-3`; quorum `2`.
- Uygulama sabit Redis IP'sine değil Sentinel discovery'ye bağlanmalıdır.
- AOF `everysec` ve RDB snapshot birlikte kullanılmalıdır.

Mümkünse kritik durum ile kolay yeniden üretilebilir cache ayrılmalıdır:

| Mantıksal set | Önerilen port | RAM hedefi | Eviction |
|---|---:|---:|---|
| Cache | 6379 | 10 GB | `allkeys-lfu` |
| Security/session/SignalR state | 6380 | 4 GB | `noeviction` |

Bellek alarmları yüzde 70 uyarı ve yüzde 85 kritik olarak başlamalıdır. SignalR backplane kritik state/realtime setini kullanmalıdır.

### 7.4 Dosya ve medya

- Ürün görselleri mevcut ayrı görsel sunucusundan subdomain/CDN ile sunulur; proje ve API VM disklerinde tutulmaz.
- Veritabanında yalnız görsel URL/path bilgisi bulunur; API ürün görselini proxy etmez.
- `Storage:Catalog:Enabled=false` üretimde korunur; S3/MinIO provider aktive edilmez.
- Ürün dışı yorum/iade/talep eki veya feed dosyası kullanılacaksa tüm API'lerin gördüğü ortak path ya da mevcut
  harici dosya servisi gerekir. Tek node-local path çoklu API için kabul edilmez.
- Harici görsel sunucusunun kendi yedeği, erişim kontrolü ve sürekliliği ayrı sunucu operasyonu kapsamındadır.

## 8. Kalan kod işleri

İşler aşağıdaki sırayla yapılmalıdır. Her iş bir öncekinin kabul kriteri tamamlandıktan sonra kapatılmalıdır.

| No | Öncelik | İş | Sorumlu | Bağımlılık | Kabul kriteri |
|---|---|---|---|---|---|
| K0 | Kritik | Feed job için atomik claim/lease ve crash recovery | Kod | PostgreSQL migration | İş önce silinmez; `pending -> processing -> completed/failed`, lease expiry ve retry testleri geçer. |
| K1 | Kritik | Node role doğrulaması | Kod | Yok | Yalnız `Api`, `Worker`, `Both` kabul edilir; typo startup'ı durdurur. |
| K2 | Kritik | Forwarded Headers güven zinciri | Kod + Altyapı | Sabit LB/Nginx IP listesi | Client IP yalnız tanımlı proxy zincirinden alınır; spoof testi başarısız olur. |
| K3 | Kritik | SignalR Redis backplane | Kod + Redis | Sentinel/state Redis | İki farklı API'ye bağlı istemciler aynı mesajı alır; node kapatma testi geçer. |
| K4 | Kritik | Worker distributed claim ve idempotency | Kod | PostgreSQL | Aynı job dört process'te eşzamanlı tetiklense bile dış etki tam bir kez oluşur. |
| K5 | Kritik | Npgsql multi-host primary bağlantısı | Kod + PostgreSQL | Patroni DNS/IP'leri | Primary switchover sırasında API restart edilmeden yazma devam eder. |
| K6 | Kritik | Redis Sentinel-aware bağlantı | Kod + Redis | Üç Sentinel | Redis primary kapanınca istemci yeni master'a otomatik bağlanır. |
| K7 | Yüksek | Health detay endpoint'ini koruma | Kod + Nginx | Private route/auth kararı | İnternetten ayrıntı okunamaz; LB `/ready` erişmeye devam eder. |
| K8 | Yüksek | Deploy betiğinde atomik release | Kod/DevOps | systemd dizin düzeni | Benzersiz release dizini, atomik symlink, rollback ve eski release retention testi geçer. |
| K9 | Ertelendi | Storage abstraction ve S3/MinIO | Kod tamam; aktivasyon yok | Kullanıcı mimari kararı | Ürün görselleri harici subdomain/CDN'den gelir; katalog storage kapalı kalır. |
| K10 | Yüksek | Otomatik integration/failover testleri | Kod + QA | Test ortamı | DB, Redis, API, Nginx ve worker failure senaryoları CI/test raporunda geçer. |

### 8.1 K0 için kesin hata modeli

Mevcut feed worker akışında iş yapılmadan önce job kaydı silinebiliyor. Worker silmeden sonra çökerse iş kaybolur. Şema en az şu alanları taşımalıdır:

- `status`
- `lease_owner`
- `lease_until`
- `attempt_count`
- `started_at`
- `completed_at`
- `last_error`

Claim tek SQL transaction içinde `FOR UPDATE SKIP LOCKED` veya eşdeğer atomik update ile yapılmalı; süresi dolan lease başka worker tarafından geri alınabilmelidir.

### 8.2 K4 kapsamındaki worker'lar

En az aşağıdaki dış etkili işler incelenmeli ve dağıtık claim/idempotency ile korunmalıdır:

- Settlement eligibility
- Cargo notify
- Tracking dispatch
- Saved-search notify
- Marketplace batch
- Legacy sync

Sadece `Role=Worker` ile tek VM çalıştırmak geçici operasyon önlemidir; worker VM kaybında otomatik ve güvenli devralma sağlamaz. Dış API'ye yapılan çağrılarda mümkünse idempotency key, veritabanında unique constraint ve işlem durumu birlikte kullanılmalıdır.

## 9. Kalan altyapı işleri

| No | Öncelik | İş | Kabul kriteri |
|---|---|---|---|
| A0 | Kritik | İki Nginx ve dört API VM oluşturma | Her API doğrudan `/live` ve `/ready` testini geçer. |
| A1 | Kritik | OVH LB health check'i Nginx -> API `/ready` zincirine bağlama | Nginx VM veya arkasındaki API'ler bozulduğunda ilgili origin trafikten çıkar. |
| A2 | Kritik | Patroni + üç üyeli etcd kurma | Planned switchover ve unplanned primary loss testleri veri kaybı olmadan geçer. |
| A3 | Kritik | Redis replica + üç Sentinel kurma | Master loss sonrası otomatik seçim ve uygulama reconnect testi geçer. |
| A4 | Kritik | Ağ/VLAN/firewall izolasyonu | DATA/REPLICATION portları public taramada kapalıdır. |
| A5 | Yüksek | Harici görsel sunucusu ve CDN doğrulaması | Ürün görselleri API CPU/diskini kullanmadan subdomain üzerinden sunulur. |
| A6 | Yüksek | Merkezi log, metric ve alarm | Node, queue, DB lag, Redis lag/memory, HTTP latency ve error rate dashboard/alarmı vardır. |
| A7 | Kritik | Offsite PostgreSQL/Redis/config yedeği | Farklı lokasyondan başarılı restore tutanağı vardır. |
| A8 | Kritik | 4.000 kullanıcı kapasite ve soak testi | Normal ve tek fiziksel host senaryosu SLO'ları geçer. |

İzleme için asgari metrikler: p50/p95/p99 latency, 5xx oranı, request/s, API CPU/RAM/GC, DB connection/pool wait, slow query, replication lag, Redis memory/eviction/replication lag, worker queue age, Nginx upstream failure ve LB healthy member sayısıdır.

## 10. Uygulama sırası

### Faz 1 — Kod güvenlik ve dayanıklılık tabanı

K0, K1, K2, K7 ve K8 tamamlanır. Otomatik test projeleri eklenir. Bu faz canlı altyapı değişmeden test ortamında doğrulanabilir.

### Faz 2 — İki node HA-lite kabulü

İkinci Nginx/API node'ları açılır, ortak Data Protection ve cache invalidation çapraz node testleri yapılır. NFS kullanılacaksa yalnız geçici olarak ve risk kaydıyla kullanılır.

### Faz 3 — Stateful servis failover

Patroni/etcd ve Redis Sentinel kurulur. K3, K5 ve K6 devreye alınır. Planned switchover testleri tamamlanmadan otomatik failover üretimde açılmaz.

### Faz 4 — Worker ve harici medya doğrulaması

K4 tamamlanır; K9 S3/MinIO aktivasyonu kullanıcı kararıyla ertelenmiştir. Worker node kaybı, duplicate event,
lease expiry ve harici görsel subdomain erişimi test edilir.

### Faz 5 — Yük, soak ve fiziksel arıza tatbikatı

Önce kademeli yük, sonra en az 2 saat 4.000 aktif kullanıcı soak testi yapılır. Test devam ederken bir API process'i, bir API VM, bir Nginx VM, Redis primary, PostgreSQL primary ve son olarak bir ESXi host kontrollü biçimde devre dışı bırakılır.

## 11. Kabul ve arıza test matrisi

| Senaryo | Beklenen sonuç | Geçiş ölçütü |
|---|---|---|
| Bir API process kapanır | Nginx/LB trafiği sağlıklı API'lere verir | Kullanıcı hatası kısa süreli retry dışında oluşmaz; 5xx SLO içinde kalır. |
| Bir API VM kapanır | Aynı ve diğer hosttaki API'ler devam eder | Session, auth ve SignalR kaybı yoktur. |
| Bir Nginx VM kapanır | OVH LB origin'i çıkarır | Site diğer Nginx üzerinden açılır. |
| Redis primary kapanır | Sentinel replica'yı master yapar | Security state korunur, API restart gerekmez. |
| PostgreSQL primary kapanır | Patroni standby'ı primary yapar | Split-brain yoktur; yazmalar RTO içinde devam eder. |
| Worker iş ortasında kapanır | Lease süresi sonunda başka worker işi alır | İş kaybolmaz ve dış etki yinelenmez. |
| Bir fiziksel ESXi host kapanır | Diğer host tüm siteyi taşır | 4.000 kullanıcı altında SLO korunur. |
| Eski Data Protection verisi diğer node'da açılır | Credential/cookie decrypt edilir | Sadece yeni test string'i değil gerçek eski kayıt doğrulanır. |
| Harici görsel subdomain/CDN testi | Ürün medyası API node'una bağlı değildir | API node'u kapansa da görsel erişimi sürer. |
| Backup restore | Ayrı ortama veri geri döner | RPO/RTO ölçülür ve tutanak oluşur. |

Önerilen başlangıç SLO hedefleri iş tarafıyla kesinleştirilmelidir:

- Başarılı HTTP isteklerinde p95: dinamik sayfalarda `< 500 ms`
- 5xx oranı: `< %0.5`
- Kritik checkout/order API'lerinde hata: `< %0.1`
- PostgreSQL failover RTO: `60-120 saniye`
- Redis failover RTO: `< 30 saniye`
- Planlı bakım sırasında kullanıcıya görünür kesinti: `0` veya ölçülmüş kısa retry penceresi

## 12. Release kapıları ve “tam HA” tanımı

Aşağıdakilerin tamamı kanıtlanmadan yapı **tam HA** olarak adlandırılmamalıdır:

- İki Nginx origin'i ve doğru `/ready` health check zinciri çalışıyor.
- Dört API iki fiziksel hosta 2+2 dağılmış.
- SignalR backplane çapraz node testini geçmiş.
- PostgreSQL Patroni/etcd failover ve Npgsql multi-host testi geçmiş.
- Redis Sentinel failover ve uygulama reconnect testi geçmiş.
- Dış etkili worker'larda claim/idempotency testleri geçmiş.
- Ürün görsellerinin API diskinden bağımsız harici subdomain/CDN erişimi doğrulanmış.
- Bir fiziksel host kapalıyken 4.000 kullanıcı testi geçmiş.
- Offsite backup restore testi yapılmış.
- Monitoring, alarm ve operasyon runbook'ları hazır.

## 13. Riskler ve kararlar

- Dört API önerisi kapasite garantisi değildir; gerçek sorgu profili ve checkout oranı bilinmeden yalnız donanım hesabıyla garanti verilemez. Son karar load test sonucu ile verilir.
- Synchronous PostgreSQL replication aynı region için düşük RPO sağlar fakat ağ gecikmesini yazma latency'sine ekler; ölçülmelidir.
- Otomatik failover quorum olmadan açılmamalıdır. Witness küçük olabilir ancak bağımsız failure domain'de olmalıdır.
- Redis cache ile kritik security state aynı memory/eviction politikasında tutulmamalıdır.
- `Role=Worker` tek node yaklaşımı duplicate'i azaltır fakat worker HA sağlamaz.
- OVH LB'nin fiziksel health check'i tek başına VM ve uygulama sağlığını kanıtlamaz.
- Bu rapordaki secret örnekleri placeholder'dır; gerçek parola, token ve anahtarlar secret manager/environment üzerinden verilmelidir.

## 14. Sonuç

Kod tabanı çoklu API için önemli ölçüde hazırlanmıştır ancak kalan kritik kod maddeleri ve stateful servis failover işleri tamamlanmadan üretim yapısı fiziksel sunucu arızasına dayanıklı değildir. Doğru hedef iki fiziksel host üzerinde simetrik 2+2 API, çift Nginx, PostgreSQL primary/standby, Redis primary/replica ve bağımsız witness'tır. Uygulamanın “4.000 aktif kullanıcı ve tek fiziksel host kaybı” kabul testini geçmesi nihai release şartıdır.
