# Pazaryeri Referans Verisi ve Eşleme Altyapısı — Plan

> **Sürüm:** v1.1 — 2026-08-31 · **Durum:** UYGULAMA BAŞLADI (K1/K2/K3/K5/K6 kapalı — yalnız K4 açık;
> sıra: ~~RF1 ✅~~ → RF2 → F4 dropship bayi); her faz bitmeden diğerine geçilmez.
> **Alan:** Admin panel (pano #2) + Integration/marketplace_ref çekirdeği.
> **İlgili:** `docs/pazaryeri-entegrasyon-veri-yonetimi.md` (F1-F5 canlı), `docs/satis-kanali-ortak-kurgu.md`
> (listeleme/readiness), `docs/pazaryerleri-yonetim-modulu` kayıtları, rehber `92-kategori-ve-ozellik-eslestirme`.

---

## 0. İlkeler (kullanıcı, 2026-08-31)

1. **Eşleme pazaryeri düzeyindedir, mağaza düzeyinde değil.** Bir pazaryerindeki tüm mağazalar/kanallar aynı
   eşlemeyi kullanır. (Mevcut model zaten böyle: `Marketplace` anahtarı + `FirmPlatformId NULL` varsayılan.)
2. **Referans verisi (pazaryeri kategori/özellik/değer) her an hazır olmalı** — bir kategori eşlenmeden önce
   bile ağacın tamamı ve tüm özellik/değerleri indirilmiş, güncel ve sorgulanabilir durmalı.
3. **Bu varlık projeyi kullanacak firmalardan bağımsızdır** — definition felsefesinin pazaryeri karşılığı:
   merkezî üretilir/sürümlenir, kurulumlara dağıtılır; her firma kendi başına sıfırdan indirmek zorunda kalmaz.
4. **Ürün temel bilgileri her an gönderime hazır olmalı** — readiness sürekli ve olay tabanlı hesaplanır (K15).

## 1. Mevcut durum (2026-08-31 canlı tespiti)

| Bileşen | Durum |
|---|---|
| `marketplace_ref` DB (ayrı) | ✅ Var: mp_categories / mp_category_attributes / mp_attribute_values / mp_change_log / mp_sync_runs |
| Trendyol kategori ağacı | ✅ 3.857 kategori (3.351 yaprak) — **son senkron 26 Temmuz, elle** |
| Özellik/değer kapsamı | ⚠️ Yalnız eşlenen kategori için talep üzerine iniyor: 101 özellik / 2.638 değer (~%3) |
| Zamanlanmış tazeleme | ❌ Yok (panel düğmesiyle manuel) |
| Eşleme modeli | ✅ Pazaryeri-düzeyi varsayılan + kanal istisnası opsiyonu (K7) — ilkeyle uyumlu |
| Eşleme içeriği | ❌ Fiilen boş: 1 kategori + 1 özellik + 3 değer (deneme kayıtları) |
| Sağlık servisi | ✅ MappingHealthService change_log işler (ama besleyen düzenli senkron yok) |
| Readiness/tamamlama | ✅ Motor + CompletionModal + listeleme sebepleri canlı; **K15 olay tabanlı tetik AÇIK** |
| Çoklu pazaryeri | ⚠️ Yalnız Trendyol indiricisi; arayüz (IMarketplaceReferenceDownloader) genişlemeye hazır |

## 2. Hedef mimari

`marketplace_ref` = **pazaryeri sözlüğü**: firma verisinden tamamen bağımsız, merkezî üretilen, sürümlü,
her kuruluma dağıtılan referans katmanı. Üzerine dört işlev: **(a)** tam-kapsam senkron motoru,
**(b)** değişiklik izleme + eşleme sağlığı, **(c)** dağıtım (K1 kararı), **(d)** eşleme operasyonu + öneri aracı.
Readiness bu katmandan olay tabanlı beslenir.

## 3. Fazlar

### RF1 — Tam kapsam senkronu — ✅ TAMAMLANDI (2026-08-31; kabul CANLIDA sağlandı)
TESPİT: senkron motoru tam kapsamı ZATEN destekliyordu (categoryIds boş → tüm yapraklar; 150ms oran gecikmesi
`Trendyol:ReferenceRequestDelayMs`; heartbeat/ilerleme; kategori başına hata toleransı) — yalnız hiç o modda
koşulmamıştı. Eklenenler: `mp_categories.attributes_synced_at` kapsam damgası (0 özellikli kategori de damgalanır;
kolon canlı DB'ye idempotent ALTER ile uygulandı), `scope=attributes-missing` (yalnız hiç taranmamış/bayat —
`Trendyol:ReferenceStaleDays` vars. 7 gün; kesinti sonrası kaldığı yerden devam + haftalık tazeleme bu modla),
özet DTO'suna yaprak/taranmış/en-eski metrikleri, panel senkron penceresine "kapsam %X (N/M yaprak)" göstergesi
ve yeni kapsam seçeneği. İlk tam koşu: restart sonrası panelden "Özellikler — yalnız eksik/bayat" (~20-30 dk,
arka planda, ilerleme pencerede).
**Kabul ✅ (2026-08-31 canlı koşu):** kapsam **%100 (3.351/3.351 yaprak)**; **73.215 özellik + 9.933.610 değer**
(~10,0M satır) 28 dakikada, **sıfır hatayla** indirildi (20:13→20:42); oran limiti aşımı yaşanmadı. İdempotens:
attributes-missing ikinci koşuda hedef bulamaz (saniyeler içinde biter). Referans sözlüğü artık "her an hazır".

### RF2 — Zamanlanmış tazeleme + değişiklik akışı — ✅ UYGULANDI (2026-09-01) ⚠️ restart bekliyor
Periyodik senkron (K3 kadansı; mevcut hosted-worker kalıbı — ⚠️ worker kayıtları A2 rol kapısı düzenine uyar,
altyapı ekibinin deploy düzenine dokunulmaz, yalnız not düşülür) → change_log → MappingHealth otomatik işleme →
panelde "referans güncelliği" rozeti + bayatlama eşiği uyarısı + kırık/gözden-geçir eşleme raporu.
**Uygulama (2026-09-01):** `MarketplaceReferenceRefreshWorker` — günlük `MarketplaceRef:AutoSync:HourUtc`
(vars. 04 UTC) saatinde her pazaryeri için sırayla categories → attributes-missing (staleDays=7 →
TAM tarama haftaya kendiliğinden yayılır = K3 "haftalık tam + günlük delta" tek işte); koşu bitişi beklenir,
kısmi hatada gün işaretlenmez (10 dk sonra yeniden dener), 3 saat zaman aşımı; A2 rol kapısının içinde
(yalnız Worker/Both düğüm), `Enabled=false` ile kapanır; /health/detail listesine eklendi. MappingHealth
zaten her koşu sonrası motor içinde çalışıyor; kırık/gözden-geçir rozetleri MappingPage'de mevcuttu.
Panel: senkron penceresine TazelikRozeti — son koşu >8 gün (ya da hiç yok) → kırmızı "bayat", en eski
özellik taraması >14 gün → sarı uyarı, aksi yeşil "güncel".
**Kabul:** restart sonrası journal "Referans tazeleme: AKTİF ✓"; İLK OTOMATİK KOŞU ertesi sabah
mp_sync_runs'ta görülür (kullanıcı/oturum doğrulaması); senkron sonrası değişen kategori/özellik etkilenen
eşlemeleri işaretler (MappingHealth); rozet bayatlamayı gösterir.

### RF3 — Merkezî dağıtım (K1 kararına göre; ≈ 2-4 gün)
Önerilen v1: **imzalı snapshot paketi** — merkezî ortamda üretilen sürümlü dışa aktarım (JSON/dump + checksum
+ üretim tarihi) ve kurulum tarafında içe aktarım komutu (`import-marketplace-ref <paket>`); kurulumlar
Trendyol'a hiç çıkmadan güncel sözlüğe kavuşur. Merkezî canlı API (K1-c) ileride bunun üstüne kurulabilir.
**Kabul:** taze kurulumda tek komutla tam sözlük; aynı paketi iki kez içe aktarmak idempotent.

### RF4 — Eşleme operasyonu + öneri aracı — ✅ ARAÇ UYGULANDI (2026-09-01) ⚠️ restart bekliyor; operasyon K4
~144 ECS yaprak grubunun Trendyol kategorilerine eşlenmesi kampanyası: ad benzerliği + mevcut ürün özellik
istatistiklerinden **öneri listesi** (grup başına en olası 3 kategori), toplu onay akışı, ilerleme panosu
(eşli grup %, açık zorunlu özellik sayısı, pazaryeri-hazır ürün oranı). Özellik/değer eşlemede aynı öneri
yaklaşımı (ad + değer kümesi örtüşmesi).
**Araç (2026-09-01):** tekil `suggest-categories` zaten vardı (ad+yol benzerliği, eşik 40) — üzerine:
`GET mapping/suggest-all` (eşsiz TÜM gruplar × ilk 3 öneri, ürün sayısına göre; yaprak kategoriler tek yükleme),
`POST mapping/bulk-category` (≤500 öğe; her öğe mevcut tekil kayıt yolundan — doğrulama/audit aynı; kısmi hata
öğe bazında raporlanır) ve panelde **eşleme kampanyası modu**: Kategoriler sekmesinde "Eşli X/Y grup" ilerlemesi +
"Toplu öneriyle eşle (N)" — satır başına öneri hapları (en iyisi ön seçili, atla seçeneği), tek tıkla toplu kayıt.
RF2 ilk otomatik koşusu da doğrulandı (2026-09-01 10:01 categories + attributes-missing, her şey tazeydi).
**Kabul:** kampanya sonunda mishar kataloğunda "eşleme eksik" sebepli ürün kalmaz (hedef oran K4'te netleşir).

### RF5 — K15 olay tabanlı readiness (≈ 1-2 gün)
Referans/eşleme değişiklik olayları → etkilenen ürünlerin readiness'ının kuyruklu yeniden hesabı (mevcut
`readiness/recompute` altyapısı üzerinden; feed A6 kuyruk deseni şablon).
**Kabul:** bir eşleme değişince etkilenen ürünlerin listeleme sebepleri elle tetiksiz güncellenir.

### RF6 — Çoklu pazaryeri genişletmesi (talep geldikçe)
Amazon/Hepsiburada indiricileri aynı arayüzle; RF1-RF3 motorları marketplace parametreli çalıştığından
yalnız indirici + oran limiti profili yazılır.

## 4. Karar soruları (K)

1. ~~K1~~ **KAPANDI (2026-08-31): merkezî snapshot paketi** — RF3 buna göre kurgulanır.
2. ~~K2~~ **KAPANDI (2026-08-31): kilitle** — ekranda kanal istisnası gizlenir; veri modeli korunur. *(Uygulaması RF4 ekran işleriyle birlikte.)*
3. ~~K3~~ **KAPANDI (2026-08-31): haftalık tam tarama + günlük delta kontrolü.**
4. **K4 Eşleme operasyonu:** kampanyayı kim yürütür (içerik ekibi / birlikte), hedef tarih ve "hazır ürün oranı" hedefi?
5. ~~K5 API erişimi teyidi~~ **KAPANDI (2026-08-31):** TrendyolReferenceDownloader kimlik başlığı KULLANMIYOR —
   kategori/özellik uçları halka açık; 26 Temmuz senkronu da anahtarsız tamamlanmış. RF1 satıcı anahtarı beklemez.
6. ~~K6~~ **KAPANDI (2026-08-31): RF1+RF2 önce, ardından F4 dropship bayi.**

## 5. Riskler

| Risk | Önlem |
|---|---|
| Trendyol oran limitleri tam taramayı engeller | Backoff + parça parça tarama + kaldığı yerden devam (RF1 kabulü) |
| Referans değişimi mevcut eşlemeleri kırar | RF2 sağlık raporu + K15 otomatik yeniden hesap (RF5) |
| Eşleme kampanyası insan gücü ister | RF4 öneri aracı yükü düşürür; ilerleme panosuyla görünür kılınır |
| Merkezî paket ile kurulum sürüm uyumsuzluğu | Pakete şema sürümü + içe aktarımda uyum kontrolü |
| Worker/cron eklemek altyapı ekibinin düzenine çarpar | Rol kapısı (Node:Role) düzenine uyulur; deploy değişikliği gerekirse yalnız not düşülür (sorumluluk devri 2026-08-30) |
