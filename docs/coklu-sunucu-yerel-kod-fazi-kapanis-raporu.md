# Çoklu Sunucu Yerel Kod Fazı Kapanış Raporu

**Tarih:** 30 Ağustos 2026
**İncelenen taban:** `46eae97b`
**Kapsam:** Yalnız yerel kod, migration, test ve runbook hazırlığı
**Dağıtım durumu:** Kullanıcının 30 Ağustos 2026 tarihli açık talebiyle bu paket `main` branch'ine
commit/push için hazırlanmıştır. Üretim uygulama deploy'u yapılmadı; test bağlantısı için `.59` sunucusunda
private NIC ve PostgreSQL/Redis private bind yapılandırması uygulandı.

## Kesin sonuç

Çoklu API çalıştırmak için planlanan yerel kod hazırlıkları uygulanmış ve doğrulanmıştır. Sistem yine de bugün
"tam HA hazır" değildir; iki ayrı VM'de API shared-state, authenticated SignalR ve gerçek API VM power-off kabulü
geçmesine rağmen gerçek Patroni, Redis Sentinel, iki Nginx ve fiziksel host kaybı kabul testleri çalıştırılmamıştır. Worker tarafında
tur-seviyesi dağıtık kilit hazırdır ancak
dış sağlayıcının başarı verip cevabın kaybolduğu pencere için sağlayıcı bazlı idempotency/reconciliation kanıtı
tamamlanmamıştır.

## Faz sonucu

| Faz | Yerel sonuç | Kalan kesin kapı |
|---|---|---|
| K0 Feed job | Done | Üretim benzeri yükte uzun lease/heartbeat ve process kaybı gözlemi |
| K1 Node/proxy | Done | Gerçek Cloudflare → OVH LB → Nginx trust zinciri ve yetkili health testi |
| K2 Deploy | Done | Linux acceptance geçti; canlı/canary release prosedürü FAZ 12 kapsamında |
| K3 SignalR | Done | İki-VM authenticated cross-node mesaj geçti; Sentinel primary-loss/reconnect açık |
| K4 Worker güvenliği | Partial | Sağlayıcı bazlı idempotency key veya reconciliation ve timeout-after-success testi |
| K5 PostgreSQL | Done | Patroni planned switchover; uygulama restart olmadan yazma devamlılığı |
| K6 Redis | Done | Üç Sentinel quorum ve primary kaybında restart'sız reconnect |
| K7 Storage | Done / ertelendi | Kod hazır; ürün görselleri harici subdomain/CDN'den geleceği için S3/MinIO aktivasyonu kapsam dışı |
| K8 Kanıt paketi | Done (yerel) | İki-VM shared-state geçti; VM/host, DB/Redis failover ve yük senaryoları açık |

`Done`, yalnız yerel kod kapsamının tamamlandığını ifade eder; environment acceptance yerine geçmez.

## Uygulanan ana değişiklikler

- Feed işi silinen tetik modelinden atomik `pending → processing → completed/failed` lease durum makinesine
  geçirildi. `SKIP LOCKED`, heartbeat, crash takeover, gecikmeli retry ve maksimum deneme kapanışı eklendi.
- Node rolü fail-fast doğrulanıyor. Forwarded headers yalnız tanımlı proxy/ağlardan kabul ediliyor; uygulama ham
  Cloudflare/XFF başlığı okumuyor. `/health/detail` yetki korumalıdır.
- Release dizini, atomik `current` symlink, `/ready` health gate, rollback ve güvenli retention betikleri eklendi.
- SignalR Redis backplane, Redis cache/state ayrımı ve Standalone/Sentinel bağlantı üreticisi eklendi.
- Altı worker turu PostgreSQL session advisory lock ile node'lar arasında tek sahipli hale getirildi. Finansal
  defter kaydına reference tabanlı unique idempotency eklendi.
- PostgreSQL multi-host primary targeting ve sınırlandırılmış pool/timeout seçenekleri eklendi. `/ready`, primary
  zorunluysa `pg_is_in_recovery()` üzerinden yazılabilir hedefi doğruluyor.
- Upload ve feed çıktıları local/S3 `IFileStorage` katmanına alındı; katalog geçişi mevcut CDN düzenini bozmamak
  için opt-in bırakıldı.
- Unit ve güvenlik kapılı environment acceptance test paketi solution'a eklendi.
- Storefront Razor runtime compiler ile uyumsuz C# collection-expression kullanımları klasik
  `Array.Empty<T>()`/dizi sözdizimine geçirildi; ana sayfa, kategori filtresi ve ürün detay partial'ları
  Development runtime compilation altında açılır hale getirildi.

## Test kanıtı

- `dotnet build src/ECSPros.sln --no-restore`: başarılı, 0 hata.
- `dotnet test tests/ECSPros.Api.Tests/ECSPros.Api.Tests.csproj --no-build --no-restore`: 53 geçti,
  0 kaldı, 1 skipped. Toplam: 49 unit + 3 PostgreSQL acceptance + 1 Redis acceptance başarılıdır.
- Skipped test: yalnız S3 acceptance; ürün görsellerinin harici subdomain/CDN'den sunulması kararı nedeniyle
  S3 ortamı etkinleştirilmedi.
- Sonraki hedefli Redis koşusu SSH tüneli üzerinden geçti: iki bağlantı, TTL state yazma/okuma, pub/sub ve
  cleanup 1/1 başarılı. Sentinel failover bu testin kapsamı değildir.
- Boş PostgreSQL acceptance DB oluşturuldu ve tüm modül migration'ları bağımlılık sırasıyla uygulandı. Bu koşu
  dört context'teki migration assembly keşif hatasını ortaya çıkardı; Storefront, Accounts, Requests ve
  Procurement DI kayıtları düzeltildi. History tabloları ve Accounts idempotency index'i doğrulandı.
- PostgreSQL acceptance paketi 3/3 geçti: writable primary, feed lease/claim/retry ve fiziksel bağlantı kaybında
  advisory-lock release. Patroni switchover ve gerçek worker process kill ayrıca beklenmektedir.
- Gerçek worker lock kill kabulü iki ayrı Linux `psql` process'iyle geçti: ilk process advisory lock'u aldı,
  ikincisi çakışma sırasında alamadı; ilk process `SIGKILL` ile kapatılınca yeni process lock'u devraldı. Parola
  yalnız stdin'den aktarıldı, Windows CRLF sonlandırması normalize edildi, holder idle DB session olarak tutuldu
  ve uzak geçici dizin doğrulanarak temizlendi. Bu sonuç sağlayıcı timeout-after-success idempotency kanıtı değildir.
- S3 acceptance testi opsiyonel olarak korunur; ürün görsellerinin mevcut ayrı sunucu/subdomain üzerinden
  sunulması kararıyla S3/MinIO üretim aktivasyonu ve acceptance koşusu ertelendi. `Storage:Catalog:Enabled=false`
  kalır; API ürün görseli saklamaz veya proxy etmez.
- Deploy ve kabul betikleri `bash -n` kontrolünden geçti.
- Deploy activation regresyonu Windows Git Bash'te gerçek POSIX symlink bulunmadığını algılayıp `SKIP` etti;
  ardından disposable Ubuntu Linux VM'de çalıştırıldı: activation, retention ve health-failure rollback geçti;
  uzak geçici dizin doğrulanarak temizlendi ve gerçek servise dokunulmadı.
- Disposable Ubuntu VM'e yalnız ASP.NET Core 8 runtime kuruldu ve iki gerçek Release API process'i geçici
  acceptance dizininde çalıştırıldı. `Node:Role=Api`, `MigrateOnStartup=false` ile migration/seed ve dış etkili
  worker'lar kapalıydı. Farklı node ID, writable-primary PostgreSQL, Redis-state ve Data Protection readiness;
  API-A→API-B tek kullanımlık Redis challenge tüketimi ve API-A kapandıktan sonra API-B sürekliliği geçti.
  SignalR Redis backplane açıkken iki API başladı.
  API process'leri, uzak test dizini ve yerel publish paketi temizlendi.
- İkinci bir disposable Ubuntu VM hazırlanarak gerçek iki-VM koşusu yapıldı. API-A `192.168.0.242:25101`, API-B
  `192.168.0.243:25102` üzerinde private ağa bind edildi; iki node'da writable-primary PostgreSQL, Redis-state,
  Data Protection ve genel readiness `Healthy` oldu. API-A'nın challenge'ı API-B'de atomik olarak bir kez
  tüketildi ve tekrar kullanım reddedildi. API-A process'i kapatıldıktan sonra API-B sağlıklı kaldı. Bu test VM
  power-off/fiziksel host kaybı değildir. Sonraki hedefli koşuda aynı JWT secret ile iki node'un yetkili
  DashboardHub istemcileri bağlandı; API-B hem yerel hem Redis backplane üzerinden API-A kaynaklı farklı zaman
  damgalı `MetricsUpdated` olayını aldı. Böylece authenticated A→B mesaj teslimi kanıtlandı. İki VM'deki acceptance dizinleri,
  API process'leri ve yerel publish arşivi doğrulanarak temizlendi; servis restartı yapılmadı.
  Yalnız acceptance DB'de açılan geçici IAM kullanıcısı ve login oturumu test sonunda silindi.
- Gerçek VM-loss koşusunda başlangıçtaki iki-node authenticated SignalR testi geçtikten sonra API-A VM
  (`5.39.57.242`) `systemctl poweroff` ile tamamen kapatıldı. API-A localhost tüneli ve public SSH erişimi düştü;
  API-B (`5.39.57.243`) `Healthy` kaldı. Kalan node üzerinden yeni login, JWT, DashboardHub bağlantısı ve
  `MetricsUpdated` teslimi geçti. VM2 test process/dizini, geçici IAM kullanıcısı ve iki login oturumu temizlendi.
  VM1 yeniden açıldıktan sonra private ping ve SSH doğrulandı; public SSH henüz cevap vermediği için VM2 güvenli
  jump host olarak kullanıldı ve yalnız doğrulanmış acceptance paket dizini kaldırıldı. Test artığı kalmadı. Bu test
  API VM kaybıdır; fiziksel ESXi host, Patroni primary veya Redis Sentinel primary kaybı değildir.
- Yerel storefront smoke testi gerçek uzak PostgreSQL/Redis'e SSH tüneliyle bağlanan Windows API sürecinde
  çalıştırıldı. `Node:Role=Api`, `Node:MigrateOnStartup=false` ve dış etkili worker kapılarıyla migration/seed
  çalıştırılmadı. `/`, `/kadin-yeni-gelenler`, Swagger, `/health` ve `/ready` HTTP 200 döndü; PostgreSQL
  writable-primary, Redis cache/state ve Data Protection kontrolleri `Healthy` oldu. Redis'in aktif parolası
  kullanıcının açık onayıyla çalışan container metadata'sından yalnız süreç belleğine alındı; ekrana, repoya
  veya rapora yazılmadı.
- `.59` test bağlantısı için `ens37` arayüzüne `192.168.0.59/24` kalıcı private adresi verildi. Compose'ta
  PostgreSQL `192.168.0.59:5432` ve Redis `192.168.0.59:6379` private bind'leri localhost bind'leri korunarak
  eklendi; yalnız bu iki container yeniden oluşturuldu ve healthy doğrulandı. Uygulama/Nginx restart edilmedi.
  `.242` üzerinden PostgreSQL writable-primary erişimi doğrulandı.
- Operasyon bulgusu: `/opt/ECSProsAI/publish/appsettings.Production.json`, `/opt/ECSProsAI/.env` ve çalışan
  Redis container'ın aktif parolası birbiriyle drift etmiş durumdadır. Bu rapor secret içermez. Canlı bakım
  penceresinde tek kaynak belirlenip config/container kontrollü olarak eşitlenmelidir.
- `git diff --check`: içerik/whitespace hatası yok; yalnız mevcut Git CRLF bilgilendirme uyarıları var.

## Migration ve geri dönüş

- `AddFeedJobLeases`: additive kolonlar ve active-job partial unique index ekler. Geri dönüşte önce yeni worker'lar
  durdurulmalı; eski sürüm yalnız şema geriye uyumlu kaldığı sürece aktive edilmelidir.
- `AddAccountTransactionIdempotency`: reference alanına filtreli unique index ekler. Uygulama öncesi duplicate
  preflight yapılmalıdır; constraint kaldırılmadan eski sürüme dönmek veri tekilleştirme garantisini azaltır.
- Canlı migration bu çalışmada uygulanmadı.

## Sonraki zorunlu sıra

1. Kod fazında K4 worker/provider timeout-after-success, aynı idempotency key ile retry ve reconciliation işi kapatılır.
2. Kalan kod/static/unit kontrolleri tamamlanarak 11.T kapatılır.
3. Harici ürün görsel subdomain/CDN erişimi üretim öncesi ortamda doğrulanır.
4. Gerçek sunucu yerleşimi hazır olduğunda Patroni/Sentinel, Nginx/LB, backup restore ve fiziksel host kabulü yapılır.
5. Üretime yakın boyuttaki staging ortamında 4.000 kullanıcı load/soak testi ölçülür.

Uygulama komutları ve secret-safe ortam değişkenleri `docs/runbooks/coklu-sunucu-kod-devreye-alma.md` içinde,
tek kaynak durum listesi `docs/acik-isler-yol-haritasi.md` FAZ 11 bölümündedir.
