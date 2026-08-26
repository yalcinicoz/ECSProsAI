# ECSPros — Açık İşler Yol Haritası

> **Oluşturma:** 2026-08-26 · **Kaynaklar:** PROGRESS.md panosu, plan dokümanları (`docs/*.md`), oturum hafızası
> **Kullanım:** Oturum başında pano yerine/yanında bu doküman açılır; hangi fazda çalışılacağı seçilir (K19 —
> alanlar karıştırılmaz), faz bitmeden sonrakine geçilmez, biten maddeler işaretlenir ve tarih düşülür.
> Fazların sırası öneridir; F1–F7 birbirinden büyük ölçüde bağımsızdır, F0 girdileri ilgili fazları bloklar.

---

## Durum özeti (2026-08-26)

Tamamlanan büyük bloklar: misharix Razor taşıma, sipariş operasyonu OP1-OP5, kampanya F0-F5, satıcı paneli,
pazaryeri veri yönetimi F1-F5, ürün tedarik T1-T6, satış kanalı F0-F3+F5, admin rehberi v2,
dayanıklılık Faz 0-1-2 (arama 5×). Aşağıdakiler **kalan** işlerdir.

---

## FAZ 0 — Kullanıcıdan beklenen karar ve dış girdiler (bloklayanlar)

Kod işi değil; ilgili fazları açan anahtarlar. Hazır olan işaretlenir.

| # | Girdi/Karar | Bloke ettiği iş |
|---|---|---|
| 0.1 | **DNS A kaydı** `partner.misharitalia.com` + nginx recreate | F5 satıcı paneli kabul testi |
| 0.2 | ~~**D3** — sır döndürme + git geçmiş temizliği~~ ✅ **YAPILDI 2026-08-26** (DB/Redis/JWT döndü, geçmiş filter-repo ile temiz, publish-demo geçmişten silindi, force-push) | — |
| 0.3 | **D6** — eski MD5/SHA256 üye hash'leri: zorunlu sıfırlama mı, "girişte yükselt" ile devam mı? | F8 |
| 0.4 | **D5** — yük testi istenip istenmediği (istenirse araç+senaryo öneririm) | F8 |
| 0.5 | **DP key yedek cron'u**: `0 4 * * 0 /opt/ECSProsAI/tools/ops/backup-dp-keys.sh` (crontab'a ekleme) | — |
| 0.6 | **K10 cutover kararı** — Legacy toplu stok senkronunun kapatılacağı tarih | F2 tedarik go-live (o güne dek Yerleştirme canlıda KULLANILMAZ) |
| 0.7 | **Termal yazıcı saha testi** — etiket şablonlarının gerçek yazıcıda doğrulanması | F2 |
| 0.8 | **Sipariş ONAY akışı kullanıcı testi** (onay linki /o/{token}; kart PayTR politika onayı istiyorsa auto-confirm yok) | konu kapanışı |
| 0.9 | **Gerçek Trendyol API anahtarları** | F4 pazaryeri canlı deneme |
| 0.10 | **PayTR canlı PCI-DSS onayı** (Direct API şimdilik YALNIZ test modu) | PayTR canlıya alma |
| 0.11 | **Mobil uygulama yayın kimliği** — GCP servis hesabı + paket adı (Play Integrity), iOS için App Attest kimlikleri | F6 |
| 0.12 | **GA4 / Meta / Merchant gerçek kimlikleri** — girilince reklam/analytics son doğrulama | kısa doğrulama turu |
| 0.13 | **Eldi firması elden girişleri** — kargo entegrasyon kimlikleri, bölge kuralları, SMS, fatura serisi | mishar kanalı işletimi |
| 0.14 | **Yorum fotoğrafları kaynak adresi** (319 foto, eski `GUID.jpg` sunucu/klasörü) | kısa aktarım işi |
| 0.15 | **HepsiJet resmi API dokümanı** + Sürat IP engelinin açılması | F3 kargo adapter'ları |

---

## FAZ 1 — Satış kanalı planının tamamlanması (F4 → F6 → F7 → F8)
**Alan:** Dış API + Admin panel · **Plan:** `docs/satis-kanali-ortak-kurgu.md` (K1-K18 kapalı; F0-F3+F5 canlıda)

- [ ] **1.1 F4 — Dropship bayi (Y2):** Partner API hesabının kanala bağlanması; bayinin kapsam/fiyat/stok
      çekişi (K17 stok formülü: `max(0, netStok − minStok + 1)`); F2b-2d sipariş POST akışı; bayi kanalında
      listeleme durumu. *(Kullanıcı hatırlatması: "F4'ü unutma")*
- [ ] **1.2 F6 — Tedarik kaynağı altyapısı (Y4):** `ISupplyConnector` — bizim dropship SATICI olduğumuz yön;
      kaynak kanal ürünlerinin içeri akışı + stok/fiyat tazeleme iskeleti.
- [ ] **1.3 F7 — Genel HTTP bağlayıcı:** şema-tanımlı (SettingsSchema kalıbı) genel REST bağlayıcı — özel
      adapter yazılmamış karşı taraflar için.
- [ ] **1.4 F8 — Eşleme sahipliği:** kanal-ürün eşlemelerinin sahiplik/çakışma kuralları, eşleme bakım ekranı.
- [ ] **1.5 "Listeden düşür" toplu işlemi** — Kanal Ürünleri ekranındaki deactivate batch'in ucu bağlanacaksa
      kapsam netleştirilip tamamlanır *(küçük iş; F4 ile birlikte ele alınabilir)*.

## FAZ 2 — Tedarik go-live (K10 cutover)
**Alan:** Admin panel · **Plan:** `docs/urun-tedarik-is-akisi.md` (T1-T6 canlıda) · **Bloklayan:** 0.6, 0.7

- [ ] **2.1** Cutover planı: Legacy toplu stok senkronunun stok ayağının kapatılması (Legacy:Sync yapılandırması),
      geri dönüş planıyla birlikte; kapanış anına kadar Yerleştirme canlıda kullanılmaz (10 dk'lık mutlak ezme
      yeni girişleri siler).
- [ ] **2.2** Cutover sonrası izleme: Tedarik Raporu KPI'ları (teslim→sayım→satışa giriş) + stok tutarlılık
      spot kontrolleri ilk hafta günlük.
- [ ] **2.3** Etiket şablonlarının saha doğrulaması sonrası ince ayar (mm ofsetleri, yazıcı kesim payları).

## FAZ 3 — Kargo entegrasyonu (KG1 → KG4)
**Alan:** Kargo · **Plan:** `docs/kargo-entegrasyon-plani.md` · **Hazır:** PTT kimlik+barkod aralığı ✓, DHL/MNG kimlik ✓ + legacy çalışan kod

- [ ] **3.1 KG1** Gönderim kaydı modeli + **PTT adapter** (test ortamı teyidi açık) + **DHL/MNG adapter**
      (cancelOrder + Query sayfaları eksik; enum'lar `DHLMNGEnums.txt`).
- [ ] **3.2 KG2** Panel: gönderim oluşturma/iptal/sorgu ekranları, kanal-kargo eşleştirme.
- [ ] **3.3 KG3** Bildirimler: kargo hareketi → SMS/e-posta (tetik: sipariş onayı; 21:00 fiziki teslim kontrolü).
- [ ] **3.4 KG4** Site: kargo takip görünümleri (`/uyeliksiz-kargo-takip` zenginleştirme).
- [ ] **3.5** Sürat (IP engeli açılınca) + HepsiJet (resmi doküman gelince) adapter'ları. *(→ 0.15)*

## FAZ 4 — Pazaryeri canlıya alma
**Alan:** Admin panel · **Plan:** `docs/pazaryeri-entegrasyon-veri-yonetimi.md` (F1-F5 canlıda) · **Bloklayan:** 0.9

- [ ] **4.1** Gerçek Trendyol anahtarlarıyla uçtan uca canlı deneme (ürün gönderimi + batch takibi + hata düzeltme döngüsü).
- [ ] **4.2** Zamanlanmış senkron kadansları (fiyat/stok push periyotları, worker).
- [ ] **4.3** F6+ diğer pazaryerleri (talep geldikçe; adapter'lar stub).

## FAZ 5 — Satıcı ekosistemi
**Alan:** Dış API + Satıcı paneli · **Plan:** `docs/pazaryeri-satici-api-degerlendirmesi.md`, `docs/satici-paneli-tasarimi.md` · **Bloklayan:** 0.1

- [ ] **5.1** Satıcı paneli kullanıcı kabul testi (DNS + nginx sonrası).
- [ ] **5.2 P3b** Relay e-posta (satıcı↔müşteri yazışma köprüsü).
- [ ] **5.3 P4** Partner API rate limit + sandbox ortamı.
- [ ] **5.4 P5** Panel genişletmeleri (hakediş raporları vb.).

## FAZ 6 — Mobil uygulamaya hazırlık
**Alan:** Mobil API · **Referans:** `docs/mobil-api-referansi.md`, `tools/mobile/STAGING.md` · **Bloklayan:** 0.11

- [ ] **6.1** Mobil geliştirici staging testleri (5055, DevBypass) — süregelen destek.
- [ ] **6.2** Play Integrity gerçek config (GCP servis hesabı + paket adı) → prod attestation.
- [ ] **6.3** iOS App Attest sunucu doğrulaması.
- [ ] **6.4** Staging kapatma + DevBypass secret imhası; h7-regression suite'in token'lı çağrıya güncellenmesi.

## FAZ 7 — Go-live PART B (eski sistemle çift yönlü canlı entegrasyon)
**Alan:** Web sitesi / veri · **Referans:** `project_golive_migration_2026-07-23.md` (PART A canlıda)

- [ ] **7.1** Canlı entegrasyon + geri-yazma kapsamının netleştirilmesi (hangi veriler eskiye geri yazılır,
      hangi yönde tek doğruluk kaynağı kim).
- [ ] **7.2** Uygulama + izleme; sipariş senkronu F1-F4 canlı deneyimleriyle (virgül kültürü, varyant barkodu dersleri) uyumlu.
- [ ] **7.3** K10 cutover (FAZ 2) ile takvim koordinasyonu — stok tek kaynağa iner.

## FAZ 8 — Güvenlik/dayanıklılık kapanışı + sürekli iyileştirme
**Alan:** ortak çekirdek · **Plan:** `docs/dayaniklilik-faz0-plani.md` (Faz 0-1-2 tamam) · **Bloklayan:** 0.2-0.4

- [x] **8.1 D3 uygulaması** ✅ 2026-08-26 — bkz. `docs/dayaniklilik-faz0-plani.md` D3 bölümü.
- [ ] **8.2 D6 uygulaması:** karar doğrultusunda eski hash politikası.
- [ ] **8.3 D5:** istenirse yük testi kurulumu + Faz 2 sonrası doğrulama ölçümü.
- [ ] **8.4 Faz 4 (sürekli):** correlation id + ProblemDetails, kritik akış testleri (xUnit+Testcontainers),
      metrik/APM, pg_stat_statements.
- [ ] **8.5 Faz 3 (KOŞULLU — yalnız çoklu instance'a geçişte):** worker leader-election/SKIP LOCKED, SignalR
      backplane, dağıtık rate limit, migration'ın deploy adımına alınması, medya object storage.

## FAZ 9 — Veri kalitesi ve teknik borç (bağımsız küçük/orta işler)

- [ ] **9.1 B-09:** katalogda ~%37 ürün yanlış grupta — rapor `docs/urun-grup-eslesme-analizi-2026-07-18.md`;
      düzeltme stratejisi + toplu taşıma.
- [ ] **9.2 M2/M3 değer aktarımı:** kanal seçim/durdurma alanlarının eski sistemden değer aktarımı
      (`project_sale_visibility_model_2026-07-14.md`).
- [ ] **9.3 B10:** BasePrice ↔ kanal fiyatı ayrışması (Razor taşıma açık ucu).
- [ ] **9.4 Kampanya kalanları:** kupon+kampanya birlikte optimizasyonu; eksik benefit handler'ları.
- [ ] **9.5 Ürün kartı F3:** otomatik sinyaller (en düşük fiyat, sosyal kanıt, Sepette/Plus) — ayrı veri işleri.
- [ ] **9.6 Yorum fotoğrafları aktarımı** (319 adet) — kaynak adresi gelince (0.14) kısa geçiş.
- [ ] **9.7 Deprecated temizlik:** `Warehouse.IsSellableOnline`, `inv_stocks.LocationId`,
      `inv_warehouse_locations` — Inventory refactoru gerektirir, aceleye getirilmez.
- [ ] **9.8 erp_variant_data Phase11 düzeltmesi** — ERP aktarımı ertelenmişti; FAZ R (ERP ekranları) ile birlikte.
- [ ] **9.9 [doğrulanacak] Telemania demo filtreleri** — demo API restart'ı yapıldı mı; demo+prod'da 7 yeni
      filtre tipi görünür mü?

---

## Önerilen sıra

1. **FAZ 0'daki hazır girdiler** hangileriyse önce onların açtığı fazlar (örn. DNS geldiyse F5.1 hemen).
2. Ticari etki sırası önerim: **F1 (satış kanalı F4)** → **F3 (kargo KG1)** → **F2 (tedarik cutover, 0.6 netleşince)**
   → **F4 (Trendyol canlı)** → **F7 (go-live PART B)** → F5/F6 paralel fırsat buldukça → F8/F9 araya serpiştirilir.
3. Her faz kapanışında bu dokümanda işaretle + PROGRESS panosunu güncelle (K18 kapanış raporu kuralı geçerli).
