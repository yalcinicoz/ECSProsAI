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

## ~~FAZ 0~~ KAPANDI (2026-08-27) — kullanıcı kararı: açık faz kalmayacak

0.1 DNS+nginx ✅ (satıcı paneli dışarıdan canlı) · 0.2 D3 sır döndürme+geçmiş temizliği ✅ (2026-08-26) ·
0.3 D6 ✅ karar: girişte-yükselt kalıcı · 0.4 D5 ✅ hafif yük testi yapıldı (~50-60 istek/sn doygunluk;
shm_size bulgusu — bkz. dayanıklılık planı) · 0.5 DP-key cron ✅ (Pazar 04:00, elle doğrulandı).
Kalan dış girdiler feshedilip ilgili fazların **Adım 0**'ı yapıldı (aşağıda); bağımsız küçükler F9'a taşındı.

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

- [ ] **Adım 0 (dış girdi):** K10 cutover tarihi (Legacy stok senkronu kesilecek gün — kullanıcı belirler) + termal yazıcı saha doğrulaması. *(eski 0.6, 0.7)*
- [ ] **2.1** Cutover planı: Legacy toplu stok senkronunun stok ayağının kapatılması (Legacy:Sync yapılandırması),
      geri dönüş planıyla birlikte; kapanış anına kadar Yerleştirme canlıda kullanılmaz (10 dk'lık mutlak ezme
      yeni girişleri siler).
- [ ] **2.2** Cutover sonrası izleme: Tedarik Raporu KPI'ları (teslim→sayım→satışa giriş) + stok tutarlılık
      spot kontrolleri ilk hafta günlük.
- [ ] **2.3** Etiket şablonlarının saha doğrulaması sonrası ince ayar (mm ofsetleri, yazıcı kesim payları).

## FAZ 3 — Kargo entegrasyonu (KG1 → KG4)
**Alan:** Kargo · **Plan:** `docs/kargo-entegrasyon-plani.md` · **Hazır:** PTT kimlik+barkod aralığı ✓, DHL/MNG kimlik ✓ + legacy çalışan kod

- [ ] **Adım 0 (dış girdi):** HepsiJet resmi API dokümanı + Sürat IP engelinin açılması — yalnız 3.5'i bloklar, PTT/DHL beklemez. *(eski 0.15)*
- [ ] **3.1 KG1** Gönderim kaydı modeli + **PTT adapter** (test ortamı teyidi açık) + **DHL/MNG adapter**
      (cancelOrder + Query sayfaları eksik; enum'lar `DHLMNGEnums.txt`).
- [ ] **3.2 KG2** Panel: gönderim oluşturma/iptal/sorgu ekranları, kanal-kargo eşleştirme.
- [ ] **3.3 KG3** Bildirimler: kargo hareketi → SMS/e-posta (tetik: sipariş onayı; 21:00 fiziki teslim kontrolü).
- [ ] **3.4 KG4** Site: kargo takip görünümleri (`/uyeliksiz-kargo-takip` zenginleştirme).
- [ ] **3.5** Sürat (IP engeli açılınca) + HepsiJet (resmi doküman gelince) adapter'ları. *(→ 0.15)*

## FAZ 4 — Pazaryeri canlıya alma
**Alan:** Admin panel · **Plan:** `docs/pazaryeri-entegrasyon-veri-yonetimi.md` (F1-F5 canlıda) · **Bloklayan:** 0.9

- [ ] **Adım 0 (dış girdi):** Gerçek Trendyol API anahtarları (kullanıcı temin eder). *(eski 0.9)*
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

- [ ] **Adım 0 (dış girdi):** Mobil yayın kimliği: GCP servis hesabı + paket adı (Play Integrity), iOS App Attest kimlikleri. *(eski 0.11)*
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

## ~~FAZ 8~~ KAPANDI (2026-08-27) — güvenlik/dayanıklılık kapanışı
8.1 D3 ✅ (2026-08-26) · 8.2 D6 ✅ (girişte-yükselt kalıcı, ek iş yok) · 8.3 D5 ✅ (yük testi; kalan tek uygulama 9.0e shm_size).
Faz olmayan kalıntılar aşağıya taşındı:

> **Sürekli bakım (faz değil — fırsat buldukça, tek tek):** correlation id + ProblemDetails · kritik akış
> testleri (xUnit+Testcontainers) · metrik/APM · pg_stat_statements. Kaynak: dayanıklılık planı "Faz 4".
>
> **Koşullu (tetiği: çoklu instance'a geçiş — bugün ihtiyaç YOK, yük testi doğruladı):** worker
> leader-election/SKIP LOCKED · SignalR backplane · dağıtık rate limit · migration'ın deploy adımına
> alınması · medya object storage. Kaynak: dayanıklılık planı "Faz 3".

## FAZ 9 — Veri kalitesi ve teknik borç (bağımsız küçük/orta işler)

- [ ] **9.0a (eski 0.8):** Sipariş ONAY akışı kullanıcı kabul testi (onay linki /o/{token}).
- [ ] **9.0b (eski 0.10):** PayTR canlı PCI-DSS onayı → Direct API'nin test modundan çıkarılması.
- [ ] **9.0c (eski 0.12):** Gerçek GA4/Meta/Merchant kimlikleri girilince reklam/analytics son doğrulama.
- [ ] **9.0d (eski 0.13):** Eldi firması elden girişleri (kargo kimlikleri, bölge kuralları, SMS, fatura serisi).
- [ ] **9.0e (2026-08-27 yük testi bulgusu):** `sudo docker compose up -d postgres` ile shm_size=1g'nin uygulanması (kısa DB kesintisi — kullanıcı zamanlar).

- [x] **9.1 B-09** ✅ ZATEN DÜZELTİLMİŞTİ (2026-07-18 Faz 21, 10.482 ürün; rapor başlığı 'DÜZELTİLDİ') — 2026-08-27 güncel DB doğrulandı: Pantolon 3.109 (13.572 değil). Yol haritasına bayat hafıza notundan girmişti.
- [x] **9.2 M2/M3 değer aktarımı** ✅ KAPANDI (2026-08-27, analizle — veri değişikliği YOK): M2 kanal seçimi legacy `plurunler.satista` → ChannelVariant.IsActive olarak ZATEN aktarılıyor (+mishar sürekli senkron); `satisaAcik` kolonu plurunler'de yok (ürün seviyesi apurunler→IsSaleOpen M1'de). M3 durdurma penceresinin legacy karşılığı YOK — `yayinda` pazaryeri yükleme/yayın durumudur (site kanallarında satıştaki ürünlerde 0; 2/9 durum kodları), SaleStopped'a eşlenemez. Not: pazaryeri `yayinda` durumu F4'te ilk-durum aktarımı için değerlendirilebilir.
- [x] **9.3 B10** ✅ KAPANDI (2026-08-27): ayrışma ölçüldü (kanal fiyatlı varyantların %6,8'i farklı, ort. ~290 TL) — sıralama ve kategori sayfaları zaten efektif/kanal fiyatındaydı; kalan iki yer düzeltildi: arama/mağaza listesi FİYAT FİLTRESİ (GetStoreProducts) ve seçim-duyarlı facet fiyat uygulaması artık kartta gösterilen efektif fiyatla (kanal min ?? BasePrice; 2 dk cache'li provider). Doğrulama: kanal 149,99/Base 269,99 ürünü ≤200 filtresinde artık listede; filtresiz çıktılar birebir. Bilinen sınır: fiyat kaydırıcısının min/max SINIRLARI hâlâ BasePrice agregasyonundan (kozmetik fark; filtreleme davranışı doğru).
- [x] **9.4 Kampanya kalanları** ✅ KAPANDI (2026-08-27):
      (a) **Motor handler'ları eklendi:** birleşik `discount` tipi (applyTo cart/selected × koşullar cartAmount/cartQty/scopeAmount/scopeQty × percent/amount + tavan) ve `bundle` (min FARKLI ürün, percent/amount/fixedPrice; CartLineItem'a additive ProductId) — panel şeması vardı, motoru yoktu, bu tipte kampanyalar sessizce çalışmıyordu. İzole motor testi 18/18 ✓.
      (b) **GÜVENLİK: checkout kupon doğrulaması sunucuya alındı** — önceden istemcinin gönderdiği `couponDiscount` tutarı yalnız clamp'lenip kabul ediliyordu (kod olmadan keyfî indirim mümkündü). Artık `couponCode` sunucuda yeniden doğrulanır (`ICouponValidator` portu → ValidateCoupon), tutar SUNUCUDA hesaplanır, istemci tutarı yok sayılır; kullanım kaydı (UseCoupon) sunucu değerleriyle atılır; geçersiz kodla sipariş oluşturulmaz.
      (c) **Kupon+kampanya birlikte kuralı belgelendi (mevcut davranış korunarak):** ikisi de kampanya-öncesi brüt üzerinden hesaplanır ve toplamdan birlikte düşülür (subtotal tabanı; toplam indirim subtotal'ı aşamaz).
      **Bilinçli ertelenenler:** `cross_group_gift` (şemada hediye-grubu alanı YOK — önce şema/panel tasarımı gerekir), `free_shipping` (siparişte kargo ücreti mekanizması hiç yok — kargo fazı F3'ün konusu; aktif KARGOTEST kampanyası işlevsiz, pasife alınması önerilir), `review_reward` (tetiği satın alma değil — ayrı akış).
- [ ] **9.5 Ürün kartı F3:** otomatik sinyaller (en düşük fiyat, sosyal kanıt, Sepette/Plus) — ayrı veri işleri.
- [ ] **9.6 Yorum fotoğrafları aktarımı** (319 adet) — kaynak adresi gelince (0.14) kısa geçiş.
- [ ] **9.7 Deprecated temizlik:** `Warehouse.IsSellableOnline`, `inv_stocks.LocationId`,
      `inv_warehouse_locations` — Inventory refactoru gerektirir, aceleye getirilmez.
- [ ] **9.8 erp_variant_data Phase11 düzeltmesi** — ERP aktarımı ertelenmişti; FAZ R (ERP ekranları) ile birlikte.
- [x] **9.9 Telemania demo filtreleri** ✅ DOĞRULANDI (2026-08-27): demo canlı — kozmetikte 12 filtre grubu (Cilt Tipi/Cinsiyet/Hacim/Paket Adedi/Renk/Saç Rengi/Saç Tipi/SPF/Form/Yaş Grubu + Kategori/Fiyat), kategoriye duyarlı (saç-bakım 8 grup).

---

## FAZ 10 — Çoklu sunucu uyumluluğu, Kademe A "HA-lite" (ONAYLANDI 2026-08-30)

Kaynak: `docs/coklu-sunucu-uyumluluk-degerlendirmesi.md` §3. **Kullanıcı kararı (2026-08-30):**
Kademe A başlar; **Sentinel/Patroni/S3 (Kademe B altyapı kalemleri B3/B4/B5) ERTELENDİ.**
A1-A4 ve A7-A9 tek sunucuda da çalışır, A0 beklenmez; A5/A6 mount ile devreye alınır.

- [ ] **10.A0 (dış girdi, kullanıcı):** ikinci VM, nginx upstream (`ip_hash`), paylaşımlı dizin `/srv/ecspros-shared`.
- [x] **10.A1** ✅ UYGULANDI (2026-08-30) ⚠️ restart bekliyor — DP key ring DB'de (`iam.data_protection_keys`,
      migration canlı DB'ye uygulandı); dosya deposu salt-okunur geri dönüş yolu + açılışta idempotent dosya→DB
      aktarımı (canlı anahtar DB'de, izole 5051 ✓). `~/.ecspros/dp-keys` yedeği bir sürüm daha korunur.
- [x] **10.A2** ✅ UYGULANDI (2026-08-30) ⚠️ restart bekliyor — `Node:Id/Role`; 10 worker yalnız Worker/Both'ta
      (DashboardMetricsWorker bilinçli her düğümde — düğüm-yerel hub yayını, tek yayıncı B1'de); Serilog NodeId;
      `/health/detail` nodeId+rol+worker listesi.
- [ ] **10.A3** Device state → Redis (`IDeviceStateStore`; challenge/nonce `SET NX EX`, secret `SET EX`; Redis yoksa fail-closed).
- [ ] **10.A4** Login sayacı → Redis + `CF-Connecting-IP` yalnız güvenilir proxy'den (ForwardedHeaders known-proxies).
- [ ] **10.A5** `IFileStorage` sözleşmesi + paylaşımlı-disk adapter'ı (6 yazma noktası; `Storage:Root`).
- [ ] **10.A6** Feed tetikleme → DB job tablosu (`SKIP LOCKED`), `status.json` → DB satırı.
- [x] **10.A7** ✅ UYGULANDI (2026-08-30) ⚠️ restart bekliyor — `Node:MigrateOnStartup=false` açılış migrate+seed'i
      atlar (varsayılan true); deploy betiği entegrasyonu A10'da.
- [x] **10.A8** ✅ UYGULANDI (2026-08-30) ⚠️ restart bekliyor — `/live` (bağımlılıksız) + `/ready` (PG+DP unhealthy→503;
      Redis degraded=200, A3 ile zorunlulaşacak); nginx upstream'in `/ready` kullanması A0/A10'da.
- [ ] **10.A9** Cache bust yayını (Redis pub/sub `ECSPros:cache:bust`).
- [ ] **10.A10** Deploy betiği çoklu hedef + loglara NodeId.
- [ ] **10.A-T** Çapraz düğüm kabul testleri (KabulTestKiti).

> **Ertelenen (Kademe B):** B1 SignalR backplane · B2 worker dağıtık claim · B3 S3/MinIO ·
> B4 Patroni DB HA · B5 Redis Sentinel · B6 nginx shared-zone rate limit · B7 release testi.
> B3/B4/B5 altyapıları kullanıcı kararıyla ertelendi (2026-08-30).

## Önerilen sıra

1. **FAZ 0'daki hazır girdiler** hangileriyse önce onların açtığı fazlar (örn. DNS geldiyse F5.1 hemen).
2. Ticari etki sırası önerim: **F1 (satış kanalı F4)** → **F3 (kargo KG1)** → **F2 (tedarik cutover, 0.6 netleşince)**
   → **F4 (Trendyol canlı)** → **F7 (go-live PART B)** → F5/F6 paralel fırsat buldukça → F8/F9 araya serpiştirilir.
3. Her faz kapanışında bu dokümanda işaretle + PROGRESS panosunu güncelle (K18 kapanış raporu kuralı geçerli).
