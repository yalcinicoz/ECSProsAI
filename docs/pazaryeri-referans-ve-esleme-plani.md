# Pazaryeri Referans Verisi ve Eşleme Altyapısı — Plan

> **Sürüm:** v1 — 2026-08-31 · **Durum:** PLAN ONAYLANDI (kullanıcı, 2026-08-31) — **uygulama K kararları
> netleşince fazlar sırasıyla başlar**; her faz bitmeden diğerine geçilmez.
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

### RF1 — Tam kapsam senkronu (≈ 2-3 gün)
Tüm yaprak kategorilerin özellik+değerlerinin indirilmesi: oran-limitli/backoff'lu tarama, kesinti sonrası
kaldığı yerden devam, `mp_sync_runs`'a kapsam metrikleri (kaç kategori/özellik/değer, süre, hata).
Panelde referans kartına "kapsam: %X" göstergesi.
**Kabul:** yaprak kategorilerin ≥%99'unun özellik+değerleri DB'de; koşum idempotent (ikinci tam koşum
yalnız delta yazar); Trendyol oran limiti aşım hatası üretmez.

### RF2 — Zamanlanmış tazeleme + değişiklik akışı (≈ 2 gün)
Periyodik senkron (K3 kadansı; mevcut hosted-worker kalıbı — ⚠️ worker kayıtları A2 rol kapısı düzenine uyar,
altyapı ekibinin deploy düzenine dokunulmaz, yalnız not düşülür) → change_log → MappingHealth otomatik işleme →
panelde "referans güncelliği" rozeti + bayatlama eşiği uyarısı + kırık/gözden-geçir eşleme raporu.
**Kabul:** senkron sonrası değişen kategori/özellik, etkilenen eşlemeleri işaretler ve panelde görünür;
senkron hiç koşmazsa X gün sonra uyarı çıkar.

### RF3 — Merkezî dağıtım (K1 kararına göre; ≈ 2-4 gün)
Önerilen v1: **imzalı snapshot paketi** — merkezî ortamda üretilen sürümlü dışa aktarım (JSON/dump + checksum
+ üretim tarihi) ve kurulum tarafında içe aktarım komutu (`import-marketplace-ref <paket>`); kurulumlar
Trendyol'a hiç çıkmadan güncel sözlüğe kavuşur. Merkezî canlı API (K1-c) ileride bunun üstüne kurulabilir.
**Kabul:** taze kurulumda tek komutla tam sözlük; aynı paketi iki kez içe aktarmak idempotent.

### RF4 — Eşleme operasyonu + öneri aracı (≈ 3-4 gün araç + operasyon süresi K4)
~144 ECS yaprak grubunun Trendyol kategorilerine eşlenmesi kampanyası: ad benzerliği + mevcut ürün özellik
istatistiklerinden **öneri listesi** (grup başına en olası 3 kategori), toplu onay akışı, ilerleme panosu
(eşli grup %, açık zorunlu özellik sayısı, pazaryeri-hazır ürün oranı). Özellik/değer eşlemede aynı öneri
yaklaşımı (ad + değer kümesi örtüşmesi).
**Kabul:** kampanya sonunda mishar kataloğunda "eşleme eksik" sebepli ürün kalmaz (hedef oran K4'te netleşir).

### RF5 — K15 olay tabanlı readiness (≈ 1-2 gün)
Referans/eşleme değişiklik olayları → etkilenen ürünlerin readiness'ının kuyruklu yeniden hesabı (mevcut
`readiness/recompute` altyapısı üzerinden; feed A6 kuyruk deseni şablon).
**Kabul:** bir eşleme değişince etkilenen ürünlerin listeleme sebepleri elle tetiksiz güncellenir.

### RF6 — Çoklu pazaryeri genişletmesi (talep geldikçe)
Amazon/Hepsiburada indiricileri aynı arayüzle; RF1-RF3 motorları marketplace parametreli çalıştığından
yalnız indirici + oran limiti profili yazılır.

## 4. Karar soruları (K)

1. **K1 Dağıtım modeli:** (a) her kurulum kendi senkronu (bugünkü, yalnız otomatikleşmiş) · (b) **merkezî
   snapshot paketi (ÖNERİ)** · (c) merkezî canlı API. B, C'nin ön adımıdır — çöpe gitmez.
2. **K2 Kanal istisnası:** eşlemede kanal bazlı istisna opsiyonu kilitlensin mi (salt pazaryeri-düzeyi)?
   (Öneri: kilitleme — ekranda gizle; model silinmez, ileride gerekirse açılır.)
3. **K3 Tazeleme kadansı:** öneri haftalık tam tarama + günlük hafif değişiklik kontrolü.
4. **K4 Eşleme operasyonu:** kampanyayı kim yürütür (içerik ekibi / birlikte), hedef tarih ve "hazır ürün oranı" hedefi?
5. **K5 API erişimi teyidi:** 26 Temmuz senkronu satıcı anahtarı olmadan mı yapıldı? (Kategori uçları anahtarsızsa
   RF1 hemen koşabilir; anahtar gerekiyorsa FAZ 4 Adım 0'a bağlanır.)
6. **K6 Sıralama:** RF1+RF2 hemen mi başlasın, yoksa F4 dropship bayi önce mi? (İkisi bağımsız; öneri: RF1+RF2
   kısa olduğundan önce, sonra F4.)

## 5. Riskler

| Risk | Önlem |
|---|---|
| Trendyol oran limitleri tam taramayı engeller | Backoff + parça parça tarama + kaldığı yerden devam (RF1 kabulü) |
| Referans değişimi mevcut eşlemeleri kırar | RF2 sağlık raporu + K15 otomatik yeniden hesap (RF5) |
| Eşleme kampanyası insan gücü ister | RF4 öneri aracı yükü düşürür; ilerleme panosuyla görünür kılınır |
| Merkezî paket ile kurulum sürüm uyumsuzluğu | Pakete şema sürümü + içe aktarımda uyum kontrolü |
| Worker/cron eklemek altyapı ekibinin düzenine çarpar | Rol kapısı (Node:Role) düzenine uyulur; deploy değişikliği gerekirse yalnız not düşülür (sorumluluk devri 2026-08-30) |
