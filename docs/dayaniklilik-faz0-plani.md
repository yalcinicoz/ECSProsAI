# Dayanıklılık / Performans İyileştirme — İş Akışı (AI Analiz Raporları uygulaması)

> Sürüm: v1 — 2026-08-26. Kaynaklar: `docs/AIAnalizRaporlari/ECSPROS_DS_Degerlendirme.md` (+ canlı ölçüm §3.9)
> ve `ECSPros-Kod-Optimizasyon-Raporu.pdf`. Bu doküman raporların fazlarını iş emirlerine bağlar.
> 2026-08-26 doğrulaması: kritik bulguların TÜMÜ hâlâ geçerli (EnableRetryOnFailure=0, AsSplitQuery=0,
> /health yok, StockOps kilitsiz, appsettings.json git'te sırlı).

## Kararlar
| # | Karar | Durum |
|---|---|---|
| D1 | Faz sırası: **Faz 0 → Faz 1 → (yük testi) → Faz 2 → Faz 3**; Faz 4 sürekli | KAPALI (rapor önerisi, kullanıcı onayı 2026-08-26) |
| D2 | Stok atomikliği: **per-variant `pg_advisory_xact_lock` + açık transaction** (tüm stok mutasyon handler'ları); ExecutionStrategy sarmalı ile Faz 1 retry'ına hazır | KAPALI (uygulama tercihi) |
| D3 | Sır hijyeni Faz 0'da: izlenen dosyalardan çıkarma + compose env'e taşıma. **Sır DÖNDÜRME (DB/Redis/JWT) ve git geçmişi temizliği AYRI koordineli iş** — JWT değişimi tüm oturumları düşürür, Redis şifresi 3 yerde; kullanıcı zamanlaması ile | AÇIK — kullanıcı zamanlar |
| D4 | Varsayılan admin seed'i prod'da KALIR (kilitlenme riski) ama şifre log'a basılmaz | KAPALI |
| D5 | Yük testi (Faz 1 sonrası, Faz 2 öncesi): araç/senaryo seçimi | AÇIK |

## Faz 0 (bu çalışma) — kalemler
1. ✅git-hijyen: `docker-compose.yml` → `${POSTGRES_PASSWORD}`/`***KALDIRILDI***` (.env gitignored);
   `appsettings.json`'dan sırlar çıkarılır (prod değerleri zaten untracked `appsettings.Production.json`'da;
   dev değerleri yeni untracked `appsettings.Development.local.json` — base'e boş/placeholder kalır);
   12 `*DbContextFactory` → env `ECSPROS_DB` ?? untracked Production.json'dan okur.
2. Latent HttpClient kayıtları: `visual-search`, `play-integrity`, `TrendyolSeller`, `TrendyolReference`
   named client'ları timeout'larıyla kaydet.
3. `GlobalExceptionMiddleware`: `ValidationException` → 400 (canlıda 500 düştüğü doğrulanmış).
4. Admin şifresini stdout'a basmayı kaldır (D4).
5. **Stok atomikliği (D2):** `IInventoryDbContext.Database` + `StockTx` yardımcı sınıfı
   (sıralı variant kilitleri, ExecutionStrategy + transaction sarması); şu mutasyon noktaları sarılır:
   OrderConfirmed/OrderCancelled/OrderShipped/PickingLinePicked/PosSaleCompleted/PosSaleRefunded/
   ReturnReceived event handler'ları, AdjustStock, UpsertSupplierStock, ReceiveToBin (tedarik T5).
   Kabul: iki eşzamanlı rezervasyon/tüketim aynı serbest stoğu iki kez alamaz (izole 5051 eşzamanlılık testi).

## Faz 0 durumu — UYGULANDI (2026-08-26) ⚠️ restart bekliyor
- ✅ 1. Sır hijyeni: compose `${POSTGRES_PASSWORD}`/`***KALDIRILDI***` (.env mevcut, gitignored);
  base `appsettings.json` sırsız (DB/Redis şifreleri, Jwt Secret, Legacy şifre boş); Development+Demo
  json'ları gitignore'a alındı ve diskte sırlarıyla duruyor; 12 `*DbContextFactory` →
  `DesignTimeConnection.Resolve()` (ECSPROS_DB env ?? untracked Production.json ?? şifresiz localhost).
  ⚠️ D3 AÇIK: sır DÖNDÜRME + git geçmişi temizliği kullanıcı zamanlamasıyla ayrı iş.
- ✅ 2. Named HttpClient'lar timeout'larıyla kayıtlı (visual-search 10sn, play-integrity 10sn,
  TrendyolSeller 30sn, TrendyolReference 120sn, paytr 20sn). Düzeltilen rapor notu: kayıtsız
  CreateClient(ad) çakılmaz; sorun 100 sn default timeout idi.
- ✅ 3. `ValidationException` → 400 (mesajlarla) — GlobalExceptionMiddleware.
- ✅ 4. Admin şifresi stdout'a basılmıyor (admin seed prod'da kalır — D4).
- ✅ 5. Stok atomikliği: `StockTx.RunAsync` (ExecutionStrategy + açık tx + SIRALI
  `pg_advisory_xact_lock(42901, hashtext(variantId))` + ChangeTracker.Clear — Faz 1 retry'a hazır);
  sarılan 10 nokta: OrderConfirmed/Cancelled/Shipped, PickingLinePicked, PosSaleCompleted/Refunded,
  ReturnReceived, AdjustStock (negatif kontrol kilit ALTINDA — TOCTOU kapandı), UpsertSupplierStock,
  ReceiveToBin. Kabul testi (izole 5051): stok 10 iken 10 paralel −2 → TAM 5 başarı/5 red, final 0;
  ikinci tur 3 stokta 2 paralel −2 → tam 1 başarı. Test bulgusu (mevcut davranış, ayrı not):
  kısımsız/rafsız depoda Consume sessizce no-op (bare stok satırını görmez) — Faz 1'de ele alınmalı.

## Faz 1 durumu — UYGULANDI (2026-08-26) ⚠️ restart bekliyor
- ✅ `EnableRetryOnFailure(3, 5sn)` — 16 `UseNpgsql(dataSource…)` kaydının tamamı; kullanıcı-transaction'ları
  ExecutionStrategy'ye sarıldı: StockTx (zaten hazırdı) + `PostAccountTransactionCommand` (Clear + strateji).
- ✅ `/health` (anonim): custom DbHealthCheck (SELECT 1, 3sn) + RedisHealthCheck (yaz-oku; yapılandırılmamış/erişilemez
  → Degraded 200 — cache opsiyonel, CLAUDE.md kuralı); DB Unhealthy → 503. JSON gövde.
- ✅ Dış HTTP dayanıklılığı: `AddResilientHttpClient` (retry×3 + devre kesici) artık gerçekten kullanılıyor —
  paytr(20sn), legacy-order(30sn), visual-search(10sn), play-integrity(10sn), TrendyolSeller(30sn),
  TrendyolReference(120sn). Ölü kod olmaktan çıktı.
- ✅ App-layer rate limit: `admin-auth` (IP başına 10/dk) — Auth login/token/refresh + SupplierAuth login/refresh.
  Test: 10×401 ardından 429 ✓.
- ✅ Prod CORS daraltıldı: yalnız https origin'ler kaldı (localhost + http çıkarıldı; untracked Production.json).
- ✅ DP key yedeği: `tools/ops/backup-dp-keys.sh` (ilk yedek alındı: ~/yedekler/dp-keys-20260826). Cron önerisi:
  `0 4 * * 0 /opt/ECSProsAI/tools/ops/backup-dp-keys.sh` (kullanıcı ekler). Tuzak düzeltildi: cp -a mtime korur.
- ✅ `ShutdownTimeout=30sn`.
- ✅ Faz 0 bulgusu kapandı: kısımsız depoda Consume artık çıplak (BinId null) satırlardan son çare düşer.
- Kabul (izole 5051): /health 200 JSON (postgresql+redis Healthy) ✓; retry AÇIKKEN eşzamanlılık: 10 paralel −2
  → tam 5 başarı, final 0 ✓; login 11.-12. istek 429 ✓.
- ⚠️ **D6 AÇIK (kullanıcı kararı):** eski MD5/SHA256 üye hash'leri — zorunlu şifre sıfırlama akışı üyelere
  sürtünme yaratır; toplu re-hash düz metinsiz mümkün değil. Karar: (a) zorunlu sıfırlama, (b) mevcut
  "girişte yükselt" davranışıyla devam. Uygulanmadı.

## Faz 2 — Paket 1 / Adım 1 UYGULANDI (2026-08-26) ⚠️ restart bekliyor
Kod optimizasyon raporunun "ilk kod değişikliği bu olmalı" dediği P0: **tam-platform kanal fiyatı çekimi kaldırıldı.**
`IChannelPricingService.GetActiveVariantPricesAsync(firmPlatformId, variantIds)` overload'u eklendi; 5 çağıran
sayfadaki/sepetteki varyantlarla sınırlandı: GetStoreProducts (sayfalama SONRASI), ürün detayı, **Checkout**
(sepet varyantları — fiyat güvenlik hesabı aynı), görsel arama kartları, grup ürünleri. Tam çekim bilerek kalanlar:
ChannelScopeResolver.HasChannelPrice ve ProductFilterHelper fiyat-aralığı (küme gereği tüm platform).
**Ölçüm (izole 5051, 5 eşzamanlı × 25 istek, aynı veri):**
| Uç | Önce p50/p95 | Sonra p50/p95 |
|---|---|---|
| /urun-listesi | 1221 / 1472 ms | **394 / 443 ms (3.1×)** |
| /urun-listesi?fiyat-artan | 613 / 767 ms | 352 / 415 ms |
| /urun-listesi?q=elbise | 547 / 603 ms | 332 / 390 ms |
| / (ana sayfa, cache) | 23 / 41 ms | 34 / 45 ms (eşdeğer) |
**Eşitlik:** ilk sayfa ürün sırası ve fiyat dizisi BİREBİR aynı ✓; ürün detayı kanonik 301→200, fiyatlar dolu ✓.
Kalan Paket 1-4 maddeleri aşağıdaki Faz 2 listesinde.

## Faz 2 — Adım 2 UYGULANDI (2026-08-26) ⚠️ restart bekliyor
- ✅ **AsSplitQuery + AsNoTracking paketi (11 sorgu):** çok-Include kartesyen patlaması kesildi — mağaza ürün
  detayı, Catalog ürün detayı, sipariş detayı, kullanıcı detayı, iade detayı, firma detayı, ürün grupları,
  attribute tipleri, POS oturum özeti, ApproveProductSubmission (yalnız split, tracked kalır),
  ProductCampaignResolver. 6 Application projesine `Microsoft.EntityFrameworkCore.Relational 8.0.14` eklendi.
- ✅ **PageComposer aktif-pointer cache:** `GetActivePageSnapshot` pointer sorgusu 15 sn IMemoryCache —
  her sayfa isteğinde DB'ye giden pointer okuması kalktı (yayın en geç 15 sn gecikmeyle görünür).
- ✅ **PageComposer stampede koruması:** compose cache-miss yolu anahtar bazlı `SemaphoreSlim` ile
  tekilleştirildi (double-check'li; 10 sn kilit bekleme sınırı — alınamazsa üretim yine yapılır, doğruluk bozulmaz).
  Cache patladığında eşzamanlı N istek artık N ayrı pahalı üretim yapmaz.
**Ölçüm (izole 5051, 5×25, canlı 5000 ile AYNI ANDA aynı veri):**
| Uç | Önce p50/p95 | Sonra p50/p95 |
|---|---|---|
| / (ana sayfa) | 34 / 45 ms | **15 / 21 ms** |
| /urun-listesi | 394 / 443 ms | **288 / 335 ms** |
| ürün detayı | 199 / 256 ms (canlı) | 185 / 301 ms |
**Eşitlik:** liste ürün kodları canlı 5000 ile BİREBİR ✓; ürün detayı title + fiyat blokları md5 eşit ✓.

## Sonraki fazlar (ayrı iş emirleri)
- **Faz 1:** EnableRetryOnFailure (15 DbContext), /health + DB/Redis check + nginx probe, ResilientHttpClient'ın
  gerçek kullanımı + timeout'lar, admin/supplier auth rate-limit, eski MD5/SHA256 hash zorunlu sıfırlama,
  prod CORS daraltma, DP key yedeği, ShutdownTimeout.
- **Faz 2:** Optimizasyon raporu Paket 1-4 (iki aşamalı listeleme, DB'de sıralama/read-model, PageComposer
  pointer cache + stampede, küçük kart DTO + HTML küçültme). İlk adım: `GetActiveVariantPricesAsync(variantIds)`.
- **Faz 3:** worker leader-election/SKIP LOCKED, SignalR backplane, DP paylaşımı, dağıtık rate limit,
  migration'ı deploy adımına alma, medya object storage, kimlik sayaçları Redis.
- **Faz 4 (sürekli):** correlation id + ProblemDetails, xUnit+Testcontainers kritik akış testleri, metrik/APM,
  pg_stat_statements etkinleştirme.
