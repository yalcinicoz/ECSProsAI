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
