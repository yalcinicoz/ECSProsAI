# Çoklu Sunucu Uyumluluk Raporu — Değerlendirme ve Uygulama Planı

**Tarih:** 2026-08-29 · **Değerlendirilen rapor:** `docs/ECSProsAI_Kod_Coklu_Sunucu_Uyumluluk_Raporu.pdf` (v1.0, commit bf92edc3)
**Durum:** ✅ ONAYLANDI (2026-08-30) — Kademe A uygulanıyor (`docs/acik-isler-yol-haritasi.md` FAZ 10).
**Kullanıcı kararı:** Sentinel/Patroni/S3 işleri (B3/B4/B5) ERTELENDİ.
**İlgili:** `docs/dayaniklilik-faz0-plani.md` (Faz 3 "çoklu instance" başlığı bu dokümanla ayrıntılanır)

---

## 0. Net sonuç (tek paragraf)

Raporun **teknik bulguları doğrudur** — 8 bulgunun 8'i kaynak kodda satır düzeyinde teyit edildi (Bölüm 2). Raporun
**önerdiği hedef mimari** (3-4 aktif API + Redis Sentinel + PostgreSQL Patroni + object storage + tam dağıtık
worker sahiplenmesi) ise mevcut ölçülmüş yük profilinin çok üzerindedir: canlı insan trafiği saatte 3-30 istek,
tek sunucu doygunluğu ~50-60 istek/sn (D5 yük testi, 2026-08-27). Bu nedenle öneri, raporu **iki kademede**
uygulamaktır: **Kademe A "HA-lite"** (2 API, tek worker rolü, sticky SignalR, paylaşımlı disk — yaklaşık 8-10
iş günü, altyapı büyümesi minimum) → ihtiyaç doğduğunda **Kademe B "Tam aktif-aktif"** (raporun A-F fazları).
Kademe A tek başına "bir API sunucusu çökünce site ayakta kalır" hedefini karşılar; Kademe B "4.000 eşzamanlı
kullanıcı + otomatik DB/Redis failover" hedefi içindir ve büyük kısmı **uygulama değil altyapı** işidir.

---

## 1. Raporun önermesi hakkında

| Raporun varsayımı | Bizim ölçümümüz / durum | Değerlendirme |
|---|---|---|
| 4.000 eşzamanlı aktif kullanıcı | Canlı: saatte 3-30 istek; doygunluk ~50-60 rps ≈ 180-200K istek/saat | Hedef, mevcut trafiğin binlerce katı. Ölçek için değil **erişilebilirlik (HA)** için çoklu sunucu düşünülmeli. |
| 3-4 API aktif-aktif | Tek API + docker compose (postgres/redis/nginx) tek host | 2 API + 1 DB/Redis host'u HA için yeterli başlangıç. |
| Redis Sentinel + PostgreSQL Patroni | İkisi de yok; tek konteyner | Uygulama tarafı 1-2 gün (multi-host connstring); altyapı tarafı (3 Sentinel + 2-3 PG + Patroni/etcd) ayrı proje. |
| Object storage / CDN | Medya `./media` (nginx `:ro` mount), 28.5K ürün görseli | Object storage'a geçiş URL şeması + aktarım işi; HA-lite için paylaşımlı mount (NFS) yeterli. |

**Karar noktası (kullanıcı):** Amaç "bir sunucu düşünce kesinti olmasın" mı (→ Kademe A), yoksa "büyüme için
yatay ölçek" mi (→ Kademe A + B)? Aşağıdaki plan A'yı B'nin ön koşulu olacak şekilde kurar; A'da yapılan hiçbir iş
B'de çöpe gitmez.

---

## 2. Bulguların kodla doğrulanması

| # | Rapor bulgusu | Kodda teyit | Sonuç | Not |
|---|---|---|---|---|
| P0-1 | Device secret/challenge/nonce `IMemoryCache` | `StoreDeviceController.cs:28,46-48` (challenge), `DeviceRequestGuardMiddleware.cs:88-94` (nonce), `DeviceAttestationServices.cs:218` (secret) | ✅ DOĞRU | Yalnız mobil uygulama (Store API) etkilenir; web sitesi bu yoldan geçmez. `play-integrity-oauth` token cache'i (satır 139) düğüm-yerel kalabilir. |
| P0-2 | SignalR backplane yok | `Program.cs:254` yalnız `AddSignalR`; csproj'da StackExchangeRedis paketi yok; 3 hub (`/hubs/fulfillment`, `/hubs/notifications`, `/hubs/dashboard`) | ✅ DOĞRU | Hub'lar **admin paneli** içindir (mağaza değil). HA-lite'ta nginx `ip_hash` sticky + WebSocket ile backplane'siz çalışır; tek eksik "A düğümünde üretilen bildirim B'ye bağlı admin'e gitmez" olur. |
| P0-3 | Data Protection yerel key ring | `Program.cs:141-146` `PersistKeysToFileSystem(~/.ecspros/dp-keys)` | ✅ DOĞRU | Tek XML anahtar dosyası var. **En kritik ve en ucuz** madde: aktarılmazsa ikinci düğüm hiçbir entegrasyon kimliğini (PayTR, kargo, SMS…) çözemez. |
| P0-4 | Yerel disk: görsel/video/vitrin/iade/yorum/talep ekleri/feed | `Catalog…DependencyInjection.cs:23-24` LocalDisk servisleri; feed `FeedGeneratorWorker.cs:38,160` (`App_Data/feeds` + `status.json`); `Channel<Guid>` process-içi kuyruk (satır 31) | ✅ DOĞRU | Rapor 6 yazma noktası sayıyor; teyit. Ortak `IFileStorage` sözleşmesi zaten yok — önce sözleşme, sonra adapter (NFS → ileride S3). |
| P0-5 | Hosted worker'lar her API'de başlar | `Program.cs` + Integration DI: 11 hosted service (DashboardMetrics, MarketplaceBatch, MarketplaceOrderFetch, ChannelScopeSync, OnSaleStamp, TrackingDispatch, CargoNotify, SavedSearchNotify, SettlementEligibility, LegacySync, FeedGenerator) | ✅ DOĞRU | Hiçbirinde `SKIP LOCKED`/advisory claim yok (grep). Advisory lock deseni projede zaten var (`StockTx`, `PostAccountTransaction`) — aynı kalıp worker'lara taşınabilir. |
| P1 | Login sayacı ve rate limiter process-local | `LoginMemberCommand.cs:31-32` yorumda "tek host" açıkça yazılı; `Program.cs:449` `AddRateLimiter` | ✅ DOĞRU | `IstemciIpAnahtari` (satır 438-447) `CF-Connecting-IP`'yi **koşulsuz** kabul ediyor — rapor haklı; nginx 5000'e doğrudan erişimi kapatıyorsa risk düşük ama düzeltilmeli. |
| P1 | Tek host Npgsql datasource | `Program.cs:85-87` `NpgsqlDataSourceBuilder(...).Build()`; 15 DbContext aynı datasource'u paylaşır | ✅ DOĞRU | Tek değişiklik noktası olması avantaj. `MarketplaceRef` ikinci bağlantı dizesi de aynı işleme alınmalı. |
| — | `/health` Redis degraded=200 | `Program.cs:660-666` | ✅ DOĞRU | Bilinçli karardı (Faz 1: cache hata-güvenli). Redis **state** taşıyınca `/ready` için zorunlu olmalı. |

### Raporda OLMAYAN, bizim eklediğimiz bulgular

| # | Bulgu | Kanıt | Önem |
|---|---|---|---|
| E1 | **Migration + seed her düğümde açılışta koşar** | `DatabaseSeeder.cs:909,1037,1062,1099` `MigrateAsync`; `Program.cs:688-692` seed her başlangıçta | İki düğüm aynı anda açılırsa migration yarışı → `__ef_migrations_*` kilit/duplicate hatası. Migration deploy adımına alınmalı (dayanıklılık Faz 3 listesinde zaten var). |
| E2 | **PageComposer stampede kilidi process-local** | `PageComposer.cs:76-102` `SemaphoreSlim` havuzu | Doğruluk sorunu değil; N düğümde N kez cache doldurma. Kabul edilebilir, Redis `SET NX` ile iyileştirilebilir. |
| E3 | 43 dosyada `IMemoryCache` — çoğu ayar/okuma cache'i | grep listesi | Güvenlik/doğruluk taşıyan yalnız 4'ü (P0-1 üçlüsü + login sayacı). Diğerleri düğüm-yerel kalabilir; **cache invalidation** (ör. `ChannelProductCacheKeys`, kanal kapsamı komutları) düğümler arası yayılmaz → admin değişikliği diğer düğümde TTL'e kadar eski görünür. Redis pub/sub ile "cache bust" mesajı gerekir (Kademe A'da kısa TTL kabul edilebilir). |
| E4 | Admin/rehber statik dosyaları ve `appsettings.Production.json` düğüm başına | nginx `./admin/dist` mount | Deploy betiği çoklu hedefe rsync yapmalı; config drift = "bir düğümde çalışan bir düğümde çalışmayan" hataları. |
| E5 | GeoLite2 `.mmdb`, Serilog dosya logları, `~/.ecspros` dizini düğüm-yerel | — | Salt-okunur veri deploy'a dahil; loglar düğüm etiketiyle (NodeId) ayrışmalı. |
| E6 | Ödeme bildirimi (PayTR callback) ve sipariş onay linki `/o/{token}` | DB-tabanlı | ✅ Sorun yok — stateless. |
| E7 | JWT/refresh token | HS256 + DB `UserSessions` | ✅ Sorun yok — `Jwt:Secret` her düğümde aynı olmalı (rapor 10. bölüm ile uyumlu). |

---

## 3. Uygulama planı

### Kademe A — "HA-lite" (2 API düğümü, kesintisiz yayın hedefi)

Amaç: bir API düğümü kapanınca site/panel/mobil çalışmaya devam etsin; altyapıda **yalnız ikinci bir API VM'i +
paylaşımlı bir dizin** eklensin (DB/Redis tek host'ta kalır, o host'un HA'sı Kademe B).

| Adım | İş | Rapor karşılığı | Dosyalar | Süre |
|---|---|---|---|---|
| A0 | Dış girdi: ikinci VM, nginx upstream (2 backend, `ip_hash`), NFS/paylaşımlı dizin (`/srv/ecspros-shared`: media, feeds, dp-keys, uploads) | — | altyapı | kullanıcı |
| A1 | **Data Protection ortak repository** — `PersistKeysToDbContext` (IAM DB'de `iam.data_protection_keys`) + mevcut XML'in aktarımı + geriye uyum (dosya anahtarı okunmaya devam) | P0-3 | `Program.cs`, IAM migration, aktarım komutu | 1 gün |
| A2 | **Node kimliği + worker rolü kapısı** — `Node:Id`, `Node:Role=Api\|Worker\|Both`; 11 hosted service yalnız `Worker/Both` düğümde başlar; `/health/detail` NodeId ve worker heartbeat döner | P0-5 (geçici koruma) | `Program.cs`, Integration DI, yeni `NodeOptions` | 1 gün |
| A3 | **Device state → Redis** — `IDeviceStateStore` (challenge `SET NX EX`, nonce `SET NX EX`, secret `SET EX`), Redis yoksa **fail-closed** | P0-1 | 3 dosya + yeni servis | 1.5 gün |
| A4 | **Login sayacı → Redis** (`INCR`+`EXPIRE`, Lua ile atomik kilit) ; `CF-Connecting-IP` yalnız güvenilir proxy'den; `ForwardedHeaders` known-proxies | P1 | `LoginMemberCommand.cs`, `Program.cs` | 1 gün |
| A5 | **`IFileStorage` sözleşmesi + paylaşımlı-disk adapter'ı** — 6 yazma noktası sözleşmeye alınır; adapter kökü `Storage:Root` (A0'daki mount); URL üretimi tek yerde | P0-4 (sözleşme kısmı) | Catalog DI, 5 controller, FeedGenerator | 2.5 gün |
| A6 | **Feed tetikleme** process-içi `Channel` yerine DB job tablosu (`tracking.feed_jobs`, `SKIP LOCKED`) ve `status.json` yerine DB satırı | P0-4/P0-5 | `FeedGeneratorWorker.cs` | 1 gün |
| A7 | **Migration/seed deploy adımına** — açılışta `MigrateAsync` yalnız `Node:MigrateOnStartup=true` ise; deploy betiği tek düğümden `dotnet ef database update` | E1 | `DatabaseSeeder.cs`, deploy script | 0.5 gün |
| A8 | **/live, /ready, /health/detail** ayrımı — `/ready`: PG read-write + Redis + DP anahtar okunabilir; nginx upstream health bu ucu kullanır | Rapor §9 | `Program.cs` | 0.5 gün |
| A9 | **Cache bust yayını** — Redis pub/sub `ECSPros:cache:bust` kanalı; admin komutları (`ChannelProductCacheKeys` vb.) yerel `IMemoryCache` temizliğini tüm düğümlere yayar | E3 | Shared.Infrastructure, ~6 komut | 1 gün |
| A10 | Deploy betiği çoklu hedef (publish → rsync N düğüm → sıralı restart, `/ready` bekleyerek); loglara NodeId | E4/E5 | `deploy.sh` | 0.5 gün |
| A-T | **Çapraz düğüm kabul testleri** (rapor §12'nin A-kapsamı): challenge A→attest B; login kilidi A→B; upload A→okuma B; PayTR kimliği B'de çözülür; A kapatılınca panel/site kesintisiz | — | KabulTestKiti | 1 gün |

**Toplam ≈ 11 iş günü.** Kademe A sonunda: raporun P0-1, P0-3, P1(login) tam; P0-2 sticky ile kabul edilmiş
sınır; P0-4 paylaşımlı disk ile karşılanmış; P0-5 rol kapısıyla karşılanmış (raporun da kabul ettiği geçici koruma).

**Kademe A'da bilinçli kabul edilen sınırlar**
- SignalR bildirimi yalnız üretildiği düğüme bağlı admin'lere gider (sticky sayesinde aynı kullanıcı hep aynı
  düğümde; çapraz bildirim kaybı olur). Dashboard sayaçları düğüm-yerel.
- Worker düğümü kapanırsa arka plan işleri (kargo bildirimi, takip, hakediş…) **durur, kaybolmaz** — DB'de
  pending kalır; düğüm dönünce sürer. İkinci düğüme `Role=Both` verilip elle devralınabilir.
- DB/Redis host'u tek nokta (Kademe B).

### Kademe B — "Tam aktif-aktif" (raporun A-F fazları)

| Faz | İş | Ön koşul | Süre (uyg.) |
|---|---|---|---|
| B1 Realtime | `AddStackExchangeRedis` backplane (`ChannelPrefix` ortam bazlı), DashboardMetrics liderlik kilidi | Redis erişilebilir | 1 gün |
| B2 Workers | 11 worker'da atomik claim: `FOR UPDATE SKIP LOCKED` + `lease_owner/lease_until` + dış çağrı idempotency key (kargo, e-posta, hakediş `ReferenceType/ReferenceId` unique) | A2 | 4-5 gün (worker başına ½ gün) |
| B3 Storage | `IFileStorage` için S3/MinIO adapter + medya aktarımı + `/media` geriye uyumlu yönlendirme + CDN origin | A5 | 3 gün + aktarım |
| B4 DB HA | `BuildMultiHost` + `Target Session Attributes=primary` + `HostRecheckSeconds`; `EnableRetryOnFailure` sınırları; `/ready` primary kontrolü | **Patroni/etcd kurulu 2-3 PG** (altyapı) | 1 gün uyg. |
| B5 Redis HA | Sentinel bağlantı dizesi (`serviceName=`), state/cache prefix ayrımı, failover davranış testi | **3 Sentinel + replica** (altyapı) | 1 gün uyg. |
| B6 Rate limit | IP limiti nginx shared zone (zaten `00-ratelimit.conf`), hesap limiti Redis (A4'te bitmiş olur) | — | 0.5 gün |
| B7 Release | 3-4 API, node-kill + failover + 4.000 kullanıcı k6 senaryosu, SLO | tümü | 2 gün |

**Toplam uygulama ≈ 13 gün + altyapı (Patroni, Sentinel, S3 — ayrı planlanır).**

---

## 4. Rapora göre farklılaşan önerilerimiz

1. **Data Protection için PostgreSQL repository** (`PersistKeysToDbContext`) — rapor "PostgreSQL-backed veya shared
   storage" diyor; DB'yi seçiyoruz: yedek zaten DB dump'ında olur, NFS bağımlılığı kalkar. Mevcut XML anahtar
   **silinmez**, DB'ye kopyalanır; `dp-keys` dizini bir sürüm daha okunur (geri dönüş yolu).
2. **Object storage'ı Kademe B'ye erteleme** — 28.5K ürün görselinin URL şeması ve nginx `/media` yolu değişmeden
   NFS ile HA sağlanır. `IFileStorage` sözleşmesi A5'te kurulduğu için S3'e geçiş sonra yalnız adapter işi.
3. **SignalR backplane'i Kademe B'ye erteleme** — hub'lar admin paneline özel; `ip_hash` + WebSocket ile
   bağlantılar zaten tek düğümde kalır. Backplane, çapraz bildirim gerektiğinde 1 günlük iş.
4. **Worker'larda önce rol kapısı, sonra dağıtık claim** — rapor da bunu geçici koruma sayıyor; 11 worker'ı
   tek tek idempotent yapmak (B2) en pahalı kalem, HA-lite için şart değil.
5. **Health'te Redis'i "zorunlu" yapma kararı A3 ile birlikte** — bugün Redis salt cache ve hata-güvenli
   (CLAUDE.md kuralı); Redis'e **güvenlik state'i** taşındığı anda `/ready` Redis'siz 503 dönmeli ama `/health`
   (cache) davranışı korunur. Mobil attestation Redis'siz **fail-closed** olur (rapor ile aynı).
6. **CF-Connecting-IP** — rapor haklı; ek olarak nginx dışından 5000 portuna erişimin firewall'da kapalı olduğu
   teyit edilmeli (bugün ikinci savunma hattı olarak tasarlanmış).

---

## 5. Riskler ve geri dönüş

| Risk | Önlem |
|---|---|
| DP anahtar aktarımında hata → tüm entegrasyon kimlikleri çözülemez | Aktarım önce izole 5051'de; XML dosyası + `~/.ecspros` yedeği (zaten kural); DB repository'de anahtar görünene kadar dosya repository okunmaya devam |
| Redis düşerse mobil attestation fail-closed → mobil uygulama giriş yapamaz | Redis HA (B5) gelene kadar mobilde net hata mesajı + uyarı; web etkilenmez |
| Paylaşımlı disk (NFS) gecikmesi görsel yüklemede | Yazma seyrek (admin); okuma nginx'ten (cache); ölçülür |
| Worker düğümü tek → arka plan işleri duraklar | İkinci düğüm `Role=Both`'a alınarak elle devralma; B2 ile kalıcı çözüm |
| İki düğüm farklı sürüm çalıştırır (deploy ortası) | Sıralı restart + `/ready` bekleme; API sözleşmesi geriye uyumlu tutulur |

---

## 6. Onay için sorular

1. Hedef **Kademe A (HA)** mı, **A+B (ölçek)** mi? (Öneri: A şimdi, B altyapı hazır olunca.)
2. A0 altyapı kalemleri (ikinci VM, NFS) ne zaman hazır olur? A1-A4, A7-A9 tek sunucuda da çalışır ve
   **A0 beklenmeden başlanabilir**; A5/A6 mount ile birlikte devreye alınır.
3. Mobil attestation'ın Redis yokken fail-closed olması kabul mü? (Alternatif: Redis yoksa tek-düğüm
   `IMemoryCache`'e düşmek — güvenlik açısından rapor bunu reddediyor, biz de önermiyoruz.)
