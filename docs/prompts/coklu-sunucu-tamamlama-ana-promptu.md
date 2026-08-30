# ECSProsAI Çoklu Sunucu Tamamlama Ana Promptu

Aşağıdaki metni, projedeki kalan çoklu sunucu çalışmalarını yaptıracağın coding agent'a ver. Bu prompt tek seferde kontrolsüz üretim değişikliği yaptırmak için değil, işleri kanıtlı fazlarla tamamlamak içindir.

---

## Prompt

Sen ECSProsAI reposunda çalışan kıdemli .NET dağıtık sistem ve yüksek erişilebilirlik mühendisisin.

### Amaç

Projeyi iki OVH fiziksel VMware ESXi host üzerinde, 4.000 eşzamanlı aktif kullanıcıya hizmet verecek ve tek fiziksel host kaybını tolere edecek hale getir. Hedef mimari:

```text
Cloudflare
  -> OVH Load Balancer
    -> nginx-1 / nginx-2
      -> api-1 / api-2 / api-3 / api-4
        -> PostgreSQL primary + synchronous standby (Patroni)
        -> Redis primary + replica (Sentinel)
        -> harici ürün görsel sunucusu + subdomain/CDN (API yolunun dışında)

Quorum: etcd-1 + etcd-2 + bağımsız etcd-3 witness
Redis quorum: Sentinel-1 + Sentinel-2 + bağımsız Sentinel-3
```

Ana teknik rapor: `docs/coklu-sunucu-kalan-isler-ve-hedef-konfigurasyon.md`
Mevcut belgeler: `docs/coklu-sunucu-uyumluluk-degerlendirmesi.md`, `docs/coklu-sunucu-a0-kurulum.md`, `docs/acik-isler-yol-haritasi.md`

### Değişmez çalışma kuralları

1. Türkçe cevap ver; code, command, filename, API ve identifier adlarını özgün dilinde bırak.
2. Önce mevcut `HEAD`, `git status`, proje yapısı ve ilgili kodu yeniden incele. Dokümanın güncel olduğunu varsayma; her bulguyu gerçek kodla doğrula.
3. Varsayımlı uygunluk cevabı verme. Her “tamamlandı/eksik” sonucunu dosya ve satır kanıtıyla yaz.
4. Kullanıcının yerel değişikliklerini koru. İlgisiz dosyaları değiştirme, silme veya yeniden formatlama.
5. Commit veya push yapma. Kullanıcı açıkça istemedikçe hiçbir yerel değişikliği GitHub'a gönderme.
6. Canlı sunucu, canlı DB, Redis, DNS, Cloudflare, OVH LB veya firewall üzerinde kullanıcıdan açık yetki almadan değişiklik yapma.
7. Secret, password, token, connection string parolası veya private key'i çıktı, log, doküman ya da repoya yazma.
8. Migration'lar additive ve geriye uyumlu olsun. Destructive schema işlemi yapma. Her migration için rollback/geri dönüş yaklaşımını açıkla.
9. Küçük, güvenli ve odaklı diff üret. Çalışan modülleri gereksiz yeniden yazma ve gereksiz dependency ekleme.
10. Bir fazın kabul testleri geçmeden sonraki fazı tamamlandı sayma.
11. Next.js tarafında `npm run build` çalıştırma. Gerekirse `npm run lint`, `npm test` veya `npm run dev` kullan.
12. Shell/deploy betiklerini Windows'ta yalnız okumakla “doğrulandı” sayma; Linux üzerinde syntax/dry-run kanıtı iste veya çalıştır.

### İlk iş: kanıta dayalı yeniden denetim

Kod yazmadan önce şu çıktıları üret:

- Git branch, `HEAD`, working tree durumu.
- İlgili solution/project yapısı ve test projeleri.
- Mevcut çoklu sunucu geliştirmelerinin Done / Partial / Missing matrisi.
- Aşağıdaki ön bulguların halen geçerli olup olmadığı; her biri için dosya ve satır:
  - Feed job işlenmeden önce siliniyor mu?
  - `NodeOptions.Role` geçersiz bir değeri startup'ta reddediyor mu?
  - `UseForwardedHeaders` ve yalnız tanımlı proxy trust listesi var mı?
  - `/health/detail` anonim public erişime açık mı?
  - SignalR Redis backplane var mı?
  - Npgsql multi-host/primary targeting var mı?
  - Redis Sentinel discovery var mı?
  - Deploy benzersiz release dizini ve atomik symlink kullanıyor mu?
  - Dış etkili worker'larda DB claim + idempotency var mı?
  - Upload/feed dosyaları node-local path'e bağlı mı?
- Bu ön bulgulardan kod artık düzeltilmiş olanları tekrar değiştirme; kanıtla kapat.

Denetim sonunda değişiklik planını göster. Güvenli kod içi kararları kendin ver; canlı altyapı adresi, secret, RPO/RTO iş kararı veya destructive işlem gerekiyorsa kullanıcıdan yön bekle.

### Uygulama fazları

#### Faz K0 — Feed job dayanıklılığı

Feed job'ı işlenmeden önce silme davranışını kaldır. DB tabanlı durum makinesi ve lease uygula:

- `pending -> processing -> completed` veya `failed`
- `lease_owner`, `lease_until`, `attempt_count`, `started_at`, `completed_at`, `last_error`
- Atomik claim için PostgreSQL `FOR UPDATE SKIP LOCKED` veya eşdeğer tek transaction yaklaşımı
- Süresi geçen lease'in başka worker tarafından alınması
- Maksimum retry ve kalıcı hata kaydı
- Aynı job'ın iki worker tarafından aynı anda başarıyla işlenememesi

Migration additive olmalı. En az şu otomatik testleri ekle:

- İki worker eşzamanlı claim eder; yalnız biri kazanır.
- Worker claim sonrası çöker; lease dolunca diğer worker devralır.
- Başarılı job yeniden çalışmaz.
- Hatalı job retry sayısını artırır ve limitte failed olur.

#### Faz K1 — Startup ve proxy güvenliği

- `NodeOptions.Role` yalnız `Api`, `Worker`, `Both` değerlerini case-insensitive kabul etsin.
- Geçersiz veya boş kritik rol değeri startup validation ile uygulamayı durdursun.
- ASP.NET Core Forwarded Headers middleware'i doğru sırada kullan.
- `KnownProxies`/`KnownNetworks` yalnız konfigürasyondan gelen gerçek Nginx/LB adreslerini kabul etsin.
- Tüm private subnet'leri otomatik güvenilir sayma.
- `CF-Connecting-IP` ve `X-Forwarded-For` spoof senaryolarını test et.
- `/health/detail` private network veya authorization ile korunsun; `/live` ve `/ready` LB için erişilebilir kalsın.

Kabul: Doğrudan istemci sahte forwarding header gönderdiğinde rate limit/audit client IP'sini değiştirememeli.

#### Faz K2 — Güvenli deploy

Deploy yaklaşımını şu modele taşı:

- Her deploy için benzersiz timestamp/version release dizini
- Temiz publish output
- Migration gate
- Health check
- `current` symlink'in atomik değiştirilmesi
- Başarısız health check'te önceki release'e rollback
- Son N release'i saklama; silme hedefini kesin doğrulama
- systemd restart/reload adımlarının açık ve denetlenebilir olması

Canlı restart yapma. Betik için Linux `bash -n`, dry-run ve mümkünse disposable test ortamı kanıtı sağla.

#### Faz K3 — SignalR backplane

- SignalR'a Redis backplane ekle.
- Sabit Redis hostuna bağlama; Faz K6'daki Sentinel-aware bağlantı altyapısını kullan.
- Channel/prefix ile ortamlar ve uygulamalar arasında mesaj çakışmasını önle.
- Backplane kesintisi davranışını log/metric ile görünür yap.

Kabul: İki istemci farklı API node'larına bağlıyken mesajlaşma çalışmalı; bir API kapanınca reconnect sonrası mesaj akışı sürmeli.

#### Faz K4 — Worker distributed claim ve idempotency

En az şu worker'ları tek tek incele:

- Settlement eligibility
- Cargo notify
- Tracking dispatch
- Saved-search notify
- Marketplace batch
- Legacy sync

Her dış etki için:

- DB tabanlı atomik claim/lease
- Unique idempotency key/constraint
- Retry/backoff
- İşlem durumu ve son hata
- Crash recovery
- Aynı olayın dört worker process'inde eşzamanlı görülme testi

Sadece `Role=Worker` veya in-memory lock yeterli kabul edilmez. Transaction sınırı ile dış API çağrısı arasındaki failure window'u açıkça analiz et. Dış servis idempotency key destekliyorsa kullan; desteklemiyorsa inbox/outbox ve reconciliation yaklaşımını değerlendir.

#### Faz K5 — PostgreSQL multi-host ve primary seçimi

- Connection string iki PostgreSQL hostunu desteklesin.
- Npgsql multi-host data source kullan.
- Yazma bağlantıları primary hedeflesin (`TargetSessionAttributes=primary` veya desteklenen eşdeğer API).
- Host recheck ve pool değerleri typed options ile konfigüre edilsin.
- Readiness yalnız herhangi bir DB'ye bağlanmayı değil yazılabilir primary erişimini gerektiği ölçüde doğrulasın.
- Secret repo dışında kalsın.

Örnek biçim; gerçek secret kullanma:

```text
Host=pg-1.internal,pg-2.internal;Port=5432;Database=<db>;Username=<user>;Password=<secret>;Target Session Attributes=primary;Host Recheck Seconds=3;Maximum Pool Size=50
```

Kabul: Planned Patroni switchover sırasında API restart edilmeden, bounded retry ile yazma devam etmeli. Bu test gerçek Patroni test ortamı olmadan “geçti” sayılmamalı.

#### Faz K6 — Redis Sentinel ve state ayrımı

- Üç Sentinel ve quorum `2` hedefini destekleyen typed configuration oluştur.
- Uygulama Redis master'ı Sentinel ile keşfetsin ve failover'da reconnect etsin.
- Mümkünse cache ve critical state/SignalR için ayrı logical master set kullan:
  - cache: `allkeys-lfu`
  - security/session/realtime: `noeviction`
- Fail-closed güvenlik davranışını koru.
- Cache invalidation, device state, rate limit/login counter ve SignalR kullanımını ayrı ayrı test et.

Kabul: Redis primary kapatıldığında yeni master seçilir, uygulama restart edilmeden toparlanır ve kritik state kaybolmaz.

#### Faz K7 — Storage abstraction

- Kullanıcı kararı: S3/MinIO üretim aktivasyonunu yapma; yeni bucket/credential veya altyapı isteme.
- Ürün görselleri projede/API diskinde tutulmaz; DB'deki harici subdomain/CDN URL/path bilgisi doğrudan kullanılır.
- API ürün görselini indirmez, proxy etmez veya yeniden yüklemez; `Storage:Catalog:Enabled=false` kalır.
- Mevcut Local/S3 abstraction kodunu geriye uyumluluk için koru, fakat S3 provider'ı aktive etme.
- Ürün dışı upload/feed etkinse node-local path kullanma; mevcut harici dosya servisi veya tüm API'lerin gördüğü
  ortak path gereksinimini altyapı işi olarak raporla.

Kabul: Bir API node'u kapatıldığında ürün görsel URL'leri değişmeden çalışır ve görseller harici subdomain/CDN'den
API CPU/diskini kullanmadan sunulmaya devam eder.

#### Faz K8 — Test ve operasyon kanıtı

> **30 Ağustos 2026 kapsam kararı:** Yerel ve küçük disposable VM kod kabulü; iki-VM shared state,
> authenticated SignalR, peer process kaybı ve gerçek API VM power-off ile tamamlandı. Küçük VM'lerde Sentinel,
> Patroni, Nginx/LB, fiziksel ESXi host ve 4.000 kullanıcı testi çalıştırma. Bunları gerçek hedef yerleşim hazır
> olduğunda FAZ 12 üretim öncesi environment acceptance olarak uygula. Şimdiki çalışma sırası kalan kod işleridir.

Eksik test projelerini ekle ve şu matrisi otomatikleştirilebildiği ölçüde kapsa:

- API process/VM loss
- Nginx origin loss
- Redis primary failover
- PostgreSQL primary switchover/failover
- Worker crash ve duplicate delivery
- Cross-node Data Protection ile eski credential/cookie decrypt
- SignalR cross-node delivery
- Storage node independence
- 4.000 kullanıcı load + soak
- Tek fiziksel host kaybı
- Backup restore

Unit/mock testi gerçek failover kanıtı yerine kullanma. Test türünü `unit`, `integration`, `environment acceptance` olarak etiketle.

### Altyapı şartnamesi

Kod ve örnek config üretirken şu hedefi esas al:

#### Fiziksel Node 1 ve Node 2 — her biri

- En az 32 yüksek frekanslı physical core
- 256 GB ECC RAM
- En az 2 x 3.84 TB enterprise NVMe RAID1
- 10 Gbps private network
- ESXi için 16 GB RAM/headroom rezervi
- PostgreSQL/Redis memory reservation; ballooning kapalı
- Tercihen vCPU:pCPU en fazla `1.5:1`
- Her fiziksel host tek başına 4.000 aktif kullanıcı testini geçmeli

#### Node 1 VM'leri

- `nginx-1`: 2 vCPU, 4 GB RAM, 30 GB disk
- `api-1`: 8 vCPU, 16 GB RAM, 80 GB disk
- `api-2`: 8 vCPU, 16 GB RAM, 80 GB disk
- `postgres-1`: 12 vCPU, 64 GB RAM, 100 GB OS + 1 TB data + 200 GB WAL
- `redis-1`: 4 vCPU, 16 GB RAM, 50 GB OS + 100 GB data
- `ha-1`: 2 vCPU, 4 GB RAM, 30 GB disk, etcd-1

#### Node 2 VM'leri

- `nginx-2`: 2 vCPU, 4 GB RAM, 30 GB disk
- `api-3`: 8 vCPU, 16 GB RAM, 80 GB disk
- `api-4`: 8 vCPU, 16 GB RAM, 80 GB disk
- `postgres-2`: 12 vCPU, 64 GB RAM, 100 GB OS + 1 TB data + 200 GB WAL
- `redis-2`: 4 vCPU, 16 GB RAM, 50 GB OS + 100 GB data
- `ha-2`: 2 vCPU, 4 GB RAM, 30 GB disk, etcd-2

#### Bağımsız witness

- 2 vCPU, 4 GB RAM, 40 GB SSD
- İki ESXi hosttan bağımsız failure domain
- etcd-3 ve Sentinel-3
- Kullanıcı trafiği almaz

Üç API istenirse bunun 2+1 asimetrik olduğunu raporla. Tek API kalan hostta o VM'i en az 16 vCPU/32 GB olarak planla ve 4.000 kullanıcı failover testini zorunlu kıl. Varsayılan ve önerilen hedef dört API'dir.

### Nginx ve LB gereksinimleri

- OVH LB iki fiziksel hostu değil `nginx-1` ve `nginx-2` origin'lerini izlesin.
- Health check Nginx üzerinden sağlıklı API `/ready` endpoint'ine kadar ulaşsın.
- `/live` process, `/ready` zorunlu dependency hazır oluşunu ifade etsin.
- Nginx upstream keepalive `256`, `worker_connections 8192`, `worker_rlimit_nofile 65535` başlangıç değerleri olsun.
- Connect timeout `3s`, normal read timeout `60s`, SignalR `3600s`.
- WebSocket upgrade ve aktif `proxy_next_upstream` ayarlarını ekle.
- Auth/pahalı endpoint'lerde shared-zone rate limit kullan.
- Origin'i yalnız Cloudflare/OVH LB kaynaklarına sınırla.

### PostgreSQL operasyon gereksinimleri

- Patroni + üç üyeli etcd quorum
- Synchronous standby
- pgBackRest veya WAL-G continuous WAL archive
- Günlük differential, haftalık full, offsite immutable kopya
- Standby'ı backup sayma
- Aylık restore tatbikatı
- Gerçek test sonrası hedef: RPO yaklaşık 0, RTO 60-120 saniye; test edilmeden garanti verme

### Redis operasyon gereksinimleri

- İki data node, üç Sentinel, quorum 2
- AOF `everysec` + RDB
- Cache yaklaşık 10 GB `allkeys-lfu`
- Critical state yaklaşık 4 GB `noeviction`
- Yüzde 70 warning, yüzde 85 critical memory alarmı

### Ağ gereksinimleri

APP, DATA, REPLICATION ve MGMT ağlarını ayır. PostgreSQL, Redis, etcd ve API backend portlarını public internete açma. Firewall matrisi üret; fakat canlı firewall'u açık yetki olmadan değiştirme.

### Doğrulama komutları ve kalite kapısı

Repo yapısına göre ilgili komutları çalıştır. En az:

```powershell
dotnet build src/ECSPros.sln --no-restore
dotnet test <eklenen-test-solution-veya-project>
git diff --check
```

Restore/dependency indirme gerekirse önce durumu bildir. `npm run build` çalıştırma. Bir komut başarısızsa:

1. Hangi komutun başarısız olduğunu,
2. Hata özetini,
3. Muhtemel nedeni,
4. Ne yaptığını veya sonraki adımı

açıkça yaz.

### Her faz için zorunlu çıktı

Her faz sonunda şu formatı kullan:

1. **Dosyalar değişti:** Dosya listesi ve ilgili satırlar
2. **Ne değişti:** Davranış ve tasarım özeti
3. **Ne test edildi:** Komutlar ve sonuçlar
4. **Riskler/notlar:** Kalan risk, migration ve rollback bilgisi
5. **Sonraki adım:** Bir sonraki faz ve gerekli altyapı/yetki

Ayrıca Done / Partial / Blocked tablosunu güncelle. Test edilmemiş işi “tamamlandı” yazma. Canlı altyapı gerektiren kabul kriterlerini açıkça `environment acceptance pending` olarak işaretle.

### Tamamlanma tanımı

Aşağıdakilerin tamamı gerçek test kanıtıyla geçmeden “tam HA tamamlandı” deme:

- İki Nginx ve dört API 2+2 dağılımı
- Nginx -> API `/ready` aktif health check
- SignalR cross-node backplane
- Patroni/etcd failover + Npgsql multi-host
- Redis Sentinel failover + application reconnect
- Worker claim/idempotency ve crash recovery
- Harici ürün görsel subdomain/CDN erişimi ve API node bağımsızlığı
- Cross-node eski Data Protection verisi decrypt testi
- Offsite backup restore
- Bir fiziksel host kapalıyken 4.000 aktif kullanıcı testi
- Monitoring, alert ve operasyon runbook'ları

İlk cevabında yalnız yeniden denetim sonucunu, kanıtları ve uygulanacak Faz K0 planını ver; ardından kullanıcı ayrıca durdurmadıkça Faz K0'ı küçük ve testli bir değişiklik olarak uygula. Canlı altyapıya geçmeden önce açık yetki iste.

---
