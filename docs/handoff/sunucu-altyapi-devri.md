# ECSProsAI — Sunucu ve Altyapı Devir Belgesi

> Durum tarihi: 2026-09-02  
> Workspace: `D:\NewProje\ECSProsAI`  
> Bu belge yeni **sunucu ve altyapı sohbetinin** başlangıç kaynağıdır.

## 1. Sohbet kapsamı

Bu çalışma alanı yalnız VM/OS, private network, SSH, Nginx, API release, systemd, PostgreSQL, Redis,
health check, backup/restore, monitoring ve HA operasyonlarını kapsar. Uygulama kodu değiştirilmez;
gereken kod değişikliği kod sohbetine bulgu ve kabul kriteri olarak devredilir.

Yeni sohbet şu sırayla başlamalıdır:

1. Bu belgeyi tamamen oku.
2. `PROGRESS.md` dosyasının en güncel altyapı ve worker kayıtlarını oku.
3. `docs/runbooks/coklu-sunucu-kod-devreye-alma.md` dosyasını oku.
4. Hedef sunucuya yazmadan önce mevcut servis, symlink, disk, health ve yedek durumunu salt-okunur doğrula.

## 2. Değişmez güvenlik kuralları

- `appsettingsTest.json` içindeki password, connection string, private key veya token değerleri hiçbir
  terminal çıktısına, belgeye, mesaja veya commit'e yazılmaz.
- SSH bilgileri dosyadan process belleğine okunabilir; secret değerler ekrana basılmaz.
- Production MySQL üzerinde yalnız SELECT-only hesap + server-side READ ONLY transaction kullanılabilir.
  Yazma, DDL ve yetki değişikliği yasaktır.
- `.59` legacy production sunucusu yeni sistem için veri senkron kaynağı değildir; açık onay olmadan servis,
  config veya verisine dokunulmaz.
- PostgreSQL/Redis/Nginx üzerinde destructive işlemden önce hedef mutlak yol/instance doğrulanır, yedek alınır
  ve kullanıcıdan açık onay alınır.
- API'ler aynı anda restart edilmez. Önce API2, health başarılı olunca API1 güncellenir.
- Migration API startup'ında çalışmaz; yalnız tek migration gate ve ayrıca onayla çalıştırılır.
- Production config repository'ye kopyalanmaz; server-side environment/secret dosyası kullanılır.
- Kullanıcı açıkça istemeden GitHub'a commit/push yapılmaz.
- Sunucuda source repository üzerinden `git pull` ile deploy yapılmaz; immutable publish release kullanılır.

## 3. Mevcut topoloji

| Bileşen | Public/SSH adresi | Private servis adresi | Mevcut rol |
|---|---|---|---|
| Nginx LB | `51.178.208.56` | `192.168.0.56` | Test subdomain reverse proxy/upstream |
| API1 | `5.39.57.245` | `192.168.0.245:5050` | API + Razor WebUI, `Node=api-1` |
| API2 | `51.178.208.58` | `192.168.0.58:5050` | API + Razor WebUI, `Node=api-2` |
| PostgreSQL1 | `5.39.57.241` | `192.168.0.241:5432` | Yeni sistem primary/tek aktif PostgreSQL |
| Redis1 | `5.39.57.243` | `192.168.0.243:6379/6380` | Cache ve kritik state ayrı instance |
| Legacy production | `51.178.208.59` | `192.168.0.59` | Eski production site/DB; bağımsız tutulur |
| ERP/V3 MSSQL | Public kapalı | `192.168.0.100:1433` | Katalog ve fiyat gerçek kaynağı |

SSH kayıt adları `appsettingsTest.json` içinde `Infrastructure.SSH.Api1`, `Api2`, `Postgres1`, `Redis1`,
`LegacyProduction` ve `LegacyProductionNgnix` olarak tutulur. Dosya git dışıdır. Private key path değeri
belgeye kopyalanmaz.

Ürün görselleri ayrı görsel sunucusu/subdomain'den gelir. API release'ine ürün görsel dosyası yüklenmez.
Dosya yoksa görsel Nginx'i “Resim hazırlanıyor” cevabı döndürür.

## 4. API1 ve API2 mevcut yayın durumu

- Aktif release: `20260902T134516Z_image_pending_cleanup`
- Her iki node symlink'i:
  `/opt/ECSProsAI/current -> /opt/ECSProsAI/releases/20260902T134516Z_image_pending_cleanup`
- Systemd unit: `ecspros.service`
- ExecStart: `/usr/bin/dotnet /opt/ECSProsAI/current/ECSPros.Api.dll`
- Node env dosyaları:
  - API1: `/etc/ecspros/api-1.env`
  - API2: `/etc/ecspros/api-2.env`
- Ortak production config link kaynağı: `/opt/ECSProsAI/config/appsettings.Production.json`
- İki serviste de `active`; son kontrolde warning/error kaydı yok.
- API1 `/ready`: PostgreSQL, Redis state ve Data Protection Healthy.
- API2 `/ready`: PostgreSQL, Redis state ve Data Protection Healthy.
- Bu release dirty yerel çalışma ağacından üretilmiştir; GitHub HEAD tek başına release'i temsil etmez.

2026-09-02 çoklu-node kabulü:

- Storefront migration geçmişi `20260902071409_EnforceSinglePendingProductQuestion` seviyesindedir;
  `product_questions` tablosu ve `UX_product_questions_single_pending` partial unique index'i mevcuttur.
- API01/API02 iki-process soru yarışı geçici process'lerde ve yayınlanmış gerçek process'lerde ayrı ayrı
  `10/10` geçti; her turda bir istek kazandı, diğeri kontrollü iş kuralı cevabıyla reddedildi.
- `marketplace_ref.mp_sync_runs` iki-process yarışı `1 kazanan + 1 unique reddi` verdi.
- Tüm sentetik soru/koşu kayıtları temizlendi; kalan test kaydı `0`.
- Rolling yayın API2 canary -> API1 sırasıyla health gate üzerinden tamamlandı. İki node `active`,
  `NRestarts=0`, `/ready=200`; direct/public ana sayfa ve kategori kontrolleri `200`, son 15 dakika warning yok.

Son kategori release kabulü:

- API1 direct kategori HTTP `200`.
- API2 direct kategori HTTP `200`.
- Public `https://multi-test.misharitalia.com/kadin-yeni-gelenler` HTTP `200`.
- Public HTML: `fetchpriority=high:1`, `1024px srcset:24`, server skeleton `19`, `src` olmadan kalan
  lazy ürün görseli `0`.
- Cloudflare response `DYNAMIC`; kategori HTML cache'lenmiyor.

## 5. Nginx mevcut durum

- Nginx host: `nginxlb`, native Nginx `1.24.0`.
- Test domain: `multi-test.misharitalia.com`.
- Upstream'ler:
  - `192.168.0.245:5050` (API1)
  - `192.168.0.58:5050` (API2)
- Site/API/SignalR/Swagger/PayTR callback ve health rotaları `ecspros_api` upstream'ini kullanır.
- İki denemeli pasif failover ve 2 saniyelik connect timeout doğrulandı.
- API1 veya API2 tek başına devre dışıyken diğer düğüm üzerinden 80/80 istek kabulü geçmişti.
- Production domain'e çoklu API config henüz uygulanmamıştır; yalnız test subdomain aktif kabul edilmelidir.
- Nginx yedeği: `/root/nginx-backups/ecspros-multi-test-20260831T202542Z`.
- Yerel referanslar:
  - `docker/nginx/conf.d/upstream-ecspros.conf`
  - `docker/nginx/conf.d/upstream-ecspros.conf.example`
  - `docker/nginx/conf.d/default.conf`
  - `docker/nginx/conf.d/locations.inc`
  - `docker/nginx/conf.d/satici-locations.inc`
  - `tools/deploy/ecspros-multi-test.nginx.conf`

## 6. PostgreSQL mevcut durum

- PostgreSQL1 private adresi `192.168.0.241:5432`.
- Yeni sistem database'i `ecommerce_db`.
- Başlangıç verisi `.59` custom dump/restore ile bir defa taşındı ve kaynak/hedef sayımları doğrulandı.
- `.59 -> yeni PostgreSQL` sürekli replication/senkron yoktur ve kurulmayacaktır.
- Yeni ERP checkpoint ve Legacy identity/import migration'ları yeni database'e uygulanmıştır.
- PostgreSQL2/standby henüz kurulmamıştır.
- Otomatik failover/Patroni/etcd/PgBouncer henüz devrede değildir.
- Uygulama readiness şu anda tek primary'nin yazılabilirliğini doğrular.

Önemli geri dönüş yedeklerinden bazıları:

- `/var/backups/ecspros-erp/pre-generated-description-cleanup-20260902T151532Z.dump`
- `/var/backups/ecspros-stock/pre-mapping-repair-20260901T1923Z.dump`
- `/var/backups/ecspros-stock/pre-real-stock-sync-20260901T1926Z.dump`
- `/var/backups/ecspros-erp/pre-continuous-erp-20260901T175719Z.dump`
- `/var/backups/ecspros-l8/pre-l3-members-addresses-20260901-165818.dump`
- `/var/backups/ecspros-l8/pre-l4-orders-20260901-170037.dump`
- `/var/backups/ecspros-l8/pre-l5-l6-invoices-returns-20260901-173105.dump`
- `/var/backups/ecspros-l8/pre-continuous-worker-20260901T161612Z.dump`

Yedek dosyaları silinmez veya restore edilmez; restore ancak ayrı plan, ayrı hedef ve açık onayla yapılır.

## 7. Redis mevcut durum

Redis1 üzerinde iki ayrı instance bulunur:

| Amaç | Port | Bellek | Eviction |
|---|---:|---:|---|
| Cache | `6379` | `10 GB` | `allkeys-lfu` |
| Kritik state | `6380` | `4 GB` | `noeviction` |

- Private bind, parola, AOF `everysec`, RDB ve systemd kalıcılığı doğrulandı.
- API1/API2 hem cache hem state bağlantı testlerini geçti.
- Cache ve State parolaları şu an aynı olabilir; kod bunları ayrı connection olarak kullanır.
- Redis2 replica ve üç Sentinel henüz kurulmadı; Redis HA yoktur.

## 8. İzole worker servisleri

### ERP katalog/fiyat

- Unit: `ecspros-erp-source.service`
- Yer: API1.
- Rol/profile: `Worker / ErpSource`.
- Yalnız private `192.168.0.100:1433` MSSQL kaynağını okur.
- Aktif release: `/opt/ECSProsAI/erp-worker-releases/20260902T151532Z_erp_attributes`.
- Katalog aralığı `15 dk`, fiyat aralığı `10 dk`, overlap `30 dk`.
- Worker Data Protection fallback yolu: `/opt/ECSProsAI/shared/dp-keys` (systemd `ReadWritePaths` içinde).
- Geç zenginleşen ürünlerde ürün özellikleri, renk/varyant ve eşlenmiş tedarikçi güncellenir; ürün açıklaması
  okunmaz veya yazılmaz. En fazla 100 ürünlük newest-first özellik uzlaştırması çalışır. Final worker `/ready`
  `200`, restart `0`.
- ERP stok yeteneği yoktur.
- Kaynak eski TLS kullandığından yalnız bu process'e özel uyumluluk uygulanmıştır; kalıcı iş ERP TLS 1.2+
  yükseltmesidir.

### Legacy üye/sipariş/fatura/iade

- Unit: `ecspros-legacy-import.service`
- Yer: API1, ayrı process.
- Symlink: `/opt/ECSProsAI/worker-current`.
- Env: `/etc/ecspros/legacy-import-worker.env` (`root:ecspros`, `0640`).
- Listener: `127.0.0.1:5060`.
- Production MySQL yalnız SELECT-only + READ ONLY okunur.
- Kapsam: members, orders, invoices, returns.
- Son idempotent turlarda yeni değişiklik `0`; bilinen 5 yetim adres güvenli atlanır.

### Legacy stock-only

- Unit: `ecspros-legacy-stock.service`.
- Yer: API1, ayrı process.
- Symlink: `/opt/ECSProsAI/stock-worker-current`.
- Son release kaydı: `20260901T192909Z_stock_final_30dd430c`.
- Aralık: `300 saniye`.
- `DryRun=false`, mapping repair kapalı, eşleşmeme toleransı sıfır.
- MySQL gerçek stok kaynağıdır; admin paneli cutover öncesi stok otoritesi değildir.

Worker env dosyalarının içeriği gösterilmez veya başka node'a körlemesine kopyalanmaz. Worker release'i API
release'inden ayrı yönetilir; API deploy'u worker symlink'lerini otomatik değiştirmez.

## 9. Admin yayını

- Test Nginx `/admin/` rotası aktiftir.
- Admin release: `/usr/share/nginx/admin-releases/20260902T131010Z_catalog_image_failclosed`.
- Aktif symlink: `/usr/share/nginx/html/admin`.
- Index ve hashed JavaScript asset HTTP `200` doğrulandı.
- Admin build bu altyapı sohbetinde yeniden üretilmez; kod sohbeti hazır artefakt ve kapsam vermelidir.

## 10. Standart rolling deployment prosedürü

1. Kod sohbetinden değişen dosyalar, test sonucu ve migration gereksinimini al.
2. Her iki API'de mevcut `current`, `ecspros.service`, disk ve private `/ready` durumunu kontrol et.
3. Yerelde temiz, benzersiz `dotnet publish -c Release --no-restore` çıktısı üret.
4. Release arşivini SHA-256 ile doğrula.
5. Paketi yeni `/opt/ECSProsAI/releases/<release-id>` dizinine aç; var olan release üzerine yazma.
6. Server production config'ini release içine symlink et; secret dosyayı paketten getirme.
7. Önce API2'yi aktive et:
   `http://192.168.0.58:5050/ready` Healthy olmadan devam etme.
8. API2 direct işlevsel testini ve journal uyarılarını kontrol et.
9. Sonra API1'i aktive et:
   `http://192.168.0.245:5050/ready` Healthy olmadan tamamlandı sayma.
10. Public `multi-test.misharitalia.com` üzerinden işlevsel kabul yap.
11. Başarısız health'te `activate-release.sh` önceki current release'e otomatik dönmelidir.
12. Release kimliği ve kabul sonuçlarını `PROGRESS.md` dosyasına kaydet.

Aktivasyon aracı: `tools/deploy/activate-release.sh`.

Windows'tan SCP ile gönderilen shell betiğinde CRLF bulunabilir. Linux'ta çalıştırmadan önce betiğin satır sonu
kontrol edilir; gerekiyorsa yalnız deploy betiğinde `sed -i 's/\r$//'` uygulanır.

Migration gerekmeyen kodda migration çalıştırılmaz. Migration gereken release'te API aktivasyonundan önce tek
gate, database kimliği, yedek ve ayrıca kullanıcı onayı zorunludur.

## 11. Sağlık ve kabul adresleri

| Kontrol | Adres |
|---|---|
| API1 ready | `http://192.168.0.245:5050/ready` |
| API2 ready | `http://192.168.0.58:5050/ready` |
| API live | `/live` |
| API health | `/health` |
| Public test | `https://multi-test.misharitalia.com/` |
| Kategori kabul | `https://multi-test.misharitalia.com/kadin-yeni-gelenler` |

`/ready` API trafiğine uygunluk kapısıdır; yalnız process yaşamını ölçen `/live` load balancer ready kontrolü
olarak tek başına kullanılmaz.

## 12. Açık altyapı ve HA işleri

Mevcut yapı iki API ile yük dağıtabilir fakat tam HA değildir. Kalan ana işler:

1. PostgreSQL2 standby kurulumu, base backup/streaming replication ve kontrollü failover tasarımı.
2. Patroni/etcd veya eşdeğer quorum/witness; Npgsql multi-host failover kabulü.
3. Redis2 replica ve üç bağımsız Sentinel; cache/state reconnect ve failover testi.
4. İkinci Nginx origin veya OVH LB'nin VM/application `/ready` kontrolüne bağlanması.
5. İkinci fiziksel host üzerinde simetrik dağılım ve gerekirse API3/API4 kapasite testi.
6. Worker HA; advisory lock yanında dış etkili işlerde claim/idempotency kabulü.
7. Merkezi log/metric/alarm, disk/DB/Redis/worker lag ve backup alarmı.
8. Offsite backup ve gerçek restore tatbikatı.
9. 4.000 aktif kullanıcı yük testi ve tek fiziksel host kaybı senaryosu.
10. Test subdomain'den production domain'e planlı cutover ve geri dönüş provası.
11. ERP kaynağının TLS 1.2+ yükseltilmesi.

Bu maddeler tamamlanmadan yapı “tam yüksek erişilebilir” olarak raporlanmaz.

## 13. Sunucu sohbetinin kod sohbetine devredeceği işler

Sunucu sohbeti kod değiştirmez. Bir problem kod gerektiriyorsa şu formatta devreder:

- Etkilenen node ve aktif release.
- Endpoint, timestamp ve correlation/request bilgisi; secret/PII hariç.
- Beklenen ve gerçekleşen davranış.
- İlgili journal/Nginx hata özeti.
- Sağlıklı diğer node ile karşılaştırma.
- Kabul kriteri ve geri dönüş gereksinimi.

## 14. Ana kaynak belgeler

- `PROGRESS.md`
- `docs/runbooks/coklu-sunucu-kod-devreye-alma.md`
- `docs/coklu-sunucu-a0-kurulum.md`
- `docs/coklu-sunucu-kalan-isler-ve-hedef-konfigurasyon.md`
- `docs/erp-kaynak-senkron-gecis-plani.md`
- `docs/legacy-mysql-uye-siparis-fatura-iade-okuma-plani.md`
- `tools/deploy/deploy.sh`
- `tools/deploy/activate-release.sh`

## 15. Yeni sunucu sohbetine yapıştırılacak başlangıç mesajı

```text
Önce docs/handoff/sunucu-altyapi-devri.md, PROGRESS.md ve
docs/runbooks/coklu-sunucu-kod-devreye-alma.md dosyalarını tamamen oku.
Bu sohbet yalnız ECSProsAI sunucu, SSH, Nginx, PostgreSQL, Redis, backup ve deployment işleri içindir.
Uygulama kodunu değiştirme; gereken değişikliği kod sohbetine raporla. appsettingsTest.json içindeki
secret'ları ekrana basma. Production MySQL üzerinde hiçbir yazma/DDL yapma. İlk işlem olarak yalnız
salt-okunur şekilde API1/API2 current release, systemd ve /ready durumunu doğrula; benden yeni işlem
gelmeden restart, deploy veya config değişikliği yapma.
```
