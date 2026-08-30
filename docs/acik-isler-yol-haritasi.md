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
> alınması · ürün dışı upload/feed için ortak path. Ürün medyası harici subdomain/CDN'dedir. Kaynak:
> dayanıklılık planı "Faz 3".

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

- [ ] **10.A0 (dış girdi, kullanıcı):** ikinci VM + paylaşımlı dizin `/srv/ecspros-shared` + nginx upstream.
      ★ HAZIRLIK TAMAM (2026-08-30): adım adım runbook `docs/coklu-sunucu-a0-kurulum.md` · nginx şablonu
      `docker/nginx/conf.d/upstream-ecspros.conf.example` (CF-Connecting-IP hash'li yapışkanlık — CF arkasında
      ip_hash yanlış dağıtır) · firewall adımı §3'te (bekleyen teyit buraya bağlandı).
- [x] **10.A1** ✅ CANLIDA (2026-08-30 restart, canlı /ready+DP doğrulandı) — DP key ring DB'de (`iam.data_protection_keys`,
      migration canlı DB'ye uygulandı); dosya deposu salt-okunur geri dönüş yolu + açılışta idempotent dosya→DB
      aktarımı (canlı anahtar DB'de, izole 5051 ✓). `~/.ecspros/dp-keys` yedeği bir sürüm daha korunur.
- [x] **10.A2** ✅ CANLIDA (2026-08-30) — `Node:Id/Role`; 10 worker yalnız Worker/Both'ta
      (DashboardMetricsWorker bilinçli her düğümde — düğüm-yerel hub yayını, tek yayıncı B1'de); Serilog NodeId;
      `/health/detail` nodeId+rol+worker listesi.
- [x] **10.A3** ✅ CANLIDA (2026-08-30 ikinci restart; kullanıcı testleri ✓ challenge tek-kullanımlık + staging replay 200→401) — `IDeviceStateStore` (Redis: challenge tek-kullanımlık
      atomik tüketim, nonce SET NX, secret SET EX; anahtar öneki `ECSPros:device:`); Redis erişilemezse FAIL-CLOSED
      (503, bellek fallback'i bilinçli YOK); `/ready`'ye `redis-state` kontrolü eklendi (yapılandırılmış+erişilemez →
      503; yapılandırılmamış → degraded; `/health` bu kontrolü çalıştırmaz — cache degraded=200 davranışı korundu).
      İzole 5051 ✓ (challenge ikinci kullanımda 400).
- [x] **10.A4** ✅ CANLIDA (2026-08-30 ikinci restart; kullanıcı testleri ✓ 6. denemede kilit) — login sayacı `ILoginAttemptCounter` portunda:
      Redis'te INCR+PEXPIRE tek Lua turunda (`ECSPros:login:*`), Redis hatasında düğüm-yerel sayaca düşer (fail-open,
      uyarı loglu); `IstemciIpAnahtari` artık CF-Connecting-IP/XFF'i yalnız güvenilir proxy soketinden kabul eder
      (vars. loopback+RFC1918; `RateLimit:TrustedProxyNetworks`). İzole 5051 ✓ (5 denemede kilit; taze süreçte kilit
      SÜRÜYOR → sayaç Redis'te kanıtlı). Not: nginx dışı 5000 erişiminin firewall'da kapalılığı kullanıcı teyidi bekler.
- [x] **10.A5** ✅ ANALİZLE KAPANDI (2026-08-30, plan sapması — gerekçeli): 6 yazma noktasının TAMAMI zaten
      yapılandırılabilir kökten yazıyor — 4 yükleme noktası + vitrin varyantları `Store:MediaRootPath`
      (Requests/StoreAccount/Pages/StoreReviews/VitrinGorselVaryantlari), ürün görsel/video DB ayarı
      `ImageServer.LocalSavePath`+`PublicBaseUrl` (CatalogSettings), feed `Feeds:OutputPath`. HA-lite'ta A0
      mount'u gelince bu ÜÇ kök `/srv/ecspros-shared/*`'a çevrilir (yalnız config/DB ayarı — kod işi yok).
      `IFileStorage` sözleşmesi hazır; S3 aktivasyonu ertelendi. Ürün görsellerinde mevcut harici
      subdomain/CDN URL düzeni korunur ve katalog storage kapalı kalır.
- [x] **10.A6** ✅ CANLIDA (2026-08-30; kullanıcı testi ✓ "✓ Üretim tamamlandı — 37337 kalem, 7 sn" + panel tamamlanma mesajı düzeltmesi) — feed tetiği `integration.feed_jobs`
      (FOR UPDATE SKIP LOCKED sahiplenme + kanal dedupe; 10 sn poll `Feeds:PollSeconds`), durum
      `integration.feed_status` (NodeId kolonu; panel her düğümden aynı durumu okur); migration CANLI DB'ye
      uygulandı (eklemeli); eski status.json'lar açılışta bir kez DB'ye aktarılır (gereksiz toplu yeniden
      üretim yok); SKIP LOCKED sahiplenme+dedupe SQL'i rollback'li psql testinde doğrulandı.
- [x] **10.A7** ✅ CANLIDA (2026-08-30) — `Node:MigrateOnStartup=false` açılış migrate+seed'i
      atlar (varsayılan true); deploy betiği entegrasyonu A10'da.
- [x] **10.A8** ✅ CANLIDA (2026-08-30) — `/live` (bağımlılıksız) + `/ready` (PG+DP unhealthy→503;
      Redis degraded=200, A3 ile zorunlulaşacak); nginx upstream'in `/ready` kullanması A0/A10'da.
- [x] **10.A9** ✅ CANLIDA (2026-08-30; journal "Cache bust aboneliği kuruldu" doğrulandı) — `ICacheBustPublisher` (Shared.Contracts):
      yerel IMemoryCache silme + Redis pub/sub `ECSPros:cache:bust` yayını; abone HER düğümde (rol kapısız
      hosted service). Porta geçenler: 5 Storefront kanal-kapsam komutu (ChannelProductCacheKeys),
      ChannelCapabilityResolver, ChannelListingStatusService, TrackingSettingsProvider. Redis'siz yalnız
      yerel (kısa TTL güvenlik ağı). İzole 5051 ✓: PUBLISH→abone sayısı 1, "Cache bust alındı" logu.
- [x] **10.A10** ✅ UYGULANDI (2026-08-30) — `tools/deploy/deploy.sh`: temiz publish + `--migrate` (14 context)
      + `nodes.conf`'taki uzak düğümlere rsync (config drift E4 önlemi) + sıralı restart talimatı (/ready
      bekleyerek; sudo komutlarını operatör çalıştırır). `nodes.conf` A0 ikinci VM gelince doldurulur;
      loglara NodeId A2'de eklendi.
- [ ] **10.A-T** Çapraz düğüm kabul testleri — **A0 bekliyor**; ★ BETİK HAZIR (2026-08-30):
      `tools/deploy/at-kabul-testleri.sh` (T1 kimlikler · T2 /ready+DP · T3 challenge A→B · T4 login kilidi A→B
      · `--kesinti` süreklilik modu; elle kontroller runbook §7'de). Tek düğümde prova: T2-T4 ✓, T1 tasarım
      gereği "aynı kimlik" diye kalıyor. Plan sapması: API-düzeyi testler bağımsız betikte; KabulTestKiti
      (tarayıcı/UX) opsiyonel ikinci doğrulama.

> **Ertelenen (Kademe B):** B1 SignalR backplane · B2 worker dağıtık claim · B3 S3/MinIO ·
> B4 Patroni DB HA · B5 Redis Sentinel · B6 nginx shared-zone rate limit · B7 release testi.
> B3/B4/B5 altyapıları kullanıcı kararıyla ertelendi (2026-08-30).
> **Yeni karar (2026-08-30):** çoklu sunucuya geçmeden yapılabilecek kod hazırlıkları FAZ 11'e alındı;
> canlı Patroni/Sentinel ve ikinci fiziksel sunucu aktivasyonu FAZ 12 kabul kapılarıyla yapılacak.
> **Medya kararı (2026-08-30):** ürün görselleri projede/API disklerinde tutulmayacak; mevcut ayrı görsel
> sunucusundan subdomain/CDN URL'leriyle sunulacak. S3/MinIO üretim aktivasyonu süresiz ertelendi.

---

## FAZ 11 — Çoklu sunucu kod dayanıklılığı (BAŞLADI 2026-08-30)

**Alan:** API + worker + deployment · **Ana rapor:**
`docs/coklu-sunucu-kalan-isler-ve-hedef-konfigurasyon.md` · **Uygulama promptu:**
`docs/prompts/coklu-sunucu-tamamlama-ana-promptu.md`

**Kural:** Bu faz yalnız kodu ve test edilebilir örnek konfigürasyonları hazırlar. Canlı PostgreSQL, Redis,
OVH LB, Cloudflare, firewall veya ESXi üzerinde kullanıcıdan ayrı onay alınmadan değişiklik yapılmaz.
Her madde küçük diff olarak uygulanır; build/test kanıtı yazılmadan `[x]` yapılmaz.

- [x] **11.0 Planlama ve kesin yeniden denetim** ✅ TAMAMLANDI (2026-08-30): mevcut HA-lite kodu güncel
      `HEAD 46eae97b` üzerinde yeniden incelendi. İki doküman oluşturuldu: kalan işler + hedef sunucu
      konfigürasyonu raporu ve fazlı uygulama promptu. Kesin ilk açık: `FeedGeneratorWorker`, işi üretimden
      önce `DELETE ... RETURNING` ile siliyor; silme sonrası process/VM kaybında tetik geri alınamıyor.
- [ ] **11.1 K0 — Feed job atomik claim/lease ve crash recovery** 🟡 KOD+TEST DB KABULÜ TAMAM, PROCESS-KILL KABULÜ BEKLİYOR (2026-08-30):
      `integration.feed_jobs` additive alanlarla `pending → processing → completed/failed` durum makinesine
      geçirildi; `lease_owner`, `lease_until`, `attempt_count`, başlangıç/bitiş/hata alanları eklendi.
      Claim `FOR UPDATE SKIP LOCKED` ile atomik; süre aşımı sonrası başka worker devralır; başarılı iş
      yeniden çalışmaz, retry limiti aşan iş `failed` kalır. Kabul: eşzamanlı claim, lease-expiry devralma,
      başarı ve kalıcı hata senaryoları testli; migration mevcut bekleyen işleri kaybetmeden `pending` yapıyor.
      **Uygulanan:** `AddFeedJobLeases` additive migration; active kanal başına partial unique index; atomik claim;
      uzun üretimde lease heartbeat; process/VM kaybında expired-lease devralma; `MaxAttempts` + gecikmeli retry;
      son crash maksimum denemeye ulaştıysa otomatik `failed`; tamamlanan iş satırı tanı için saklanıyor; panel
      tetiği aktif pending/processing işi idempotent biçimde birleştiriyor. Config: `Feeds:LeaseSeconds=900`,
      `MaxAttempts=5`, `RetryDelaySeconds=60`. Kanıt: `dotnet build src/ECSPros.sln --no-restore` 0 hata
      (mevcut 31 uyarı); EF migration script üretimi başarılı; rollback'li regresyon betiği
      `tools/tests/feed-job-lease-regression.sql` hazır. **Açık kabul:** migration uygulanmış izole PostgreSQL'de
      betiği çalıştırma + iki gerçek worker process ile eşzamanlı claim/crash testi; canlı DB'ye uygulanmadı.
      Yerel izole DB denemesi 2026-08-30'da migration başlamadan durdu: local PostgreSQL parola istedi,
      repoda parola yoktu; test DB oluşturulmadı ve mevcut `ecommerce_db` değişmedi. Sonraki hazırlıkta
      `TestCategory=Acceptance` altında gerçek Npgsql eşzamanlı claim/lease takeover/completed ve retry-limit
      → kalıcı `failed` testi eklendi;
      yalnız adı `test|acceptance` içeren DB ve açık write onayıyla çalışır, aksi halde güvenli biçimde atlanır.
      **Gerçek test DB kanıtı:** boş acceptance DB oluşturuldu, bağımlı modül migration'ları ve
      `AddFeedJobLeases` uygulandı; eşzamanlı claim, lease-expiry takeover, completed tekrar-claim engeli ve
      retry-limit → `failed` senaryoları geçti. İki gerçek worker process kill testi environment pending.
- [ ] **11.2 K1 — Node rolü ve proxy güven zinciri** 🟡 KOD+UNIT TAMAM, NGINX/LB KABULÜ BEKLİYOR (2026-08-30): `Node:Role` yalnız `Api|Worker|Both`; typo startup'ı
      durdurur. `UseForwardedHeaders` doğru middleware sırasında; yalnız konfigüre edilmiş LB/Nginx IP/ağları
      güvenilir. Sahte `CF-Connecting-IP`/`X-Forwarded-For` client IP'yi değiştiremez. `/health/detail`
      private network veya authorization ile korunur; `/live` ve `/ready` LB için açık kalır.
      **Uygulanan:** `NodeOptions.Dogrula()` rolü canonical yapıyor, boş NodeId/geçersiz rol açılışı kesiyor;
      `ReverseProxy:KnownProxies/KnownNetworks/ForwardLimit` typed config'i ve `UseForwardedHeaders` pipeline'ın
      başına eklendi; eski geniş RFC1918 güven varsayımı kaldırıldı (güvenli varsayılan yalnız loopback).
      Rate limiter, tracking, GeoIP ve ödeme IP kaydı ham CF/XFF başlıklarını okumuyor; tek kaynak middleware
      sonrası `RemoteIpAddress`. `/health/detail` artık `AdminOnly`; `/health`, `/live`, `/ready` anonim kalıyor.
      Kanıt: solution build 0 hata; `Node__Role=typo-test` başlangıcı beklenen `InvalidOperationException` ile
      reddedildi; güvenilir proxy XFF'i uygulanırken güvenilmeyen soketten sahte XFF/CF başlıklarının client IP'yi
      değiştirmediği middleware unit testleriyle doğrulandı. **Açık kabul:** Production config'e gerçek Nginx/OVH LB dar
      IP/CIDR'leri ve doğru `ForwardLimit` girilecek; Nginx'in Cloudflare istemci IP'sini sanitize edilmiş XFF
      olarak ilettiği ve `/health/detail` 401/authorized 200 davranışı gerçek zincirde doğrulanacak.
- [x] **11.3 K2 — Atomik ve geri alınabilir deploy** ✅ KOD+LINUX KABULÜ TAMAM
      (2026-08-30): `deploy.sh` benzersiz release dizinine temiz publish/rsync ve opsiyonel migration gate
      uygular; `activate-release.sh` doğrulanmış hedefe atomik `current` symlink geçirir, `/ready` başarısızsa
      önceki release'e döner ve yalnız doğrulanmış releases kökünde retention uygular. İki betik `bash -n`
      kontrolünden geçti. `tools/tests/deploy-activation-regression.sh` disposable Linux kabulü için hazırlandı.
      Disposable Ubuntu Linux
      VM'de sahte `systemctl`/`curl` ile sağlam release aktivasyonu, eski release retention temizliği ve bozuk
      `/ready` sonrası önceki release'e rollback testi geçti; yalnız `/tmp` altında açılan uzak test dizini
      doğrulanıp temizlendi. Canlı servis restartı yapılmadı.
- [x] **11.4 K3 — SignalR Redis backplane** ✅ KOD + KÜÇÜK-VM AUTH MESAJ/VM-LOSS KAPANDI
      (2026-08-30): `Microsoft.AspNetCore.SignalR.StackExchangeRedis` backplane'i opt-in config ve ortam/uygulama
      channel prefix'i ile eklendi; kritik Redis state bağlantısını kullanır. Farklı API node'larına bağlı istemci,
      node kapanış ve reconnect testi gerçek Redis/Sentinel ortamında yapılacak. **Ara kabul:** disposable Linux
      VM'de iki gerçek API process'i SignalR backplane açıkken aynı gerçek Redis bağlantısıyla başarıyla başladı.
      Ardından iki ayrı Ubuntu VM'de private IP'lere bağlı Release API'ler aynı test PostgreSQL/Redis ile açıldı;
      Redis state VM'ler arasında tüketildi ve API-A process'i kapatıldığında API-B `Healthy` kaldı. **Authenticated
      hub kanıtı:** aynı JWT secret ile A ve B DashboardHub'a yetkili istemci bağlandı; API-B, kendi yerel
      `MetricsUpdated` yayınına ek olarak Redis backplane üzerinden API-A'nın farklı zaman damgalı yayınını aldı.
      `tools/tests/signalr-two-node-client.mjs` secret'ları yalnız stdin'den alır. Test için yalnız acceptance DB'de
      geçici IAM kullanıcısı açıldı; test sonrası kullanıcı ve oluşturulan oturum, API process/dizinleri ve publish
      paketi temizlendi. Gerçek üç-Sentinel reconnect/primary-loss testi kod fazının değil, FAZ 12 üretim öncesi
      altyapı kabulünün kapısıdır.
- [ ] **11.5 K4 — Dış etkili worker distributed claim + idempotency** 🟡 ORTAK DAĞITIK KİLİT+FİNANS
      IDEMPOTENCY TAMAM, DIŞ SERVİS KABULÜ BEKLİYOR (2026-08-30): Settlement eligibility, Cargo notify,
      Tracking dispatch, Saved-search notify, Marketplace batch ve Legacy sync tek tek ele alınır. DB lease,
      unique idempotency key, retry/backoff, crash recovery ve reconciliation uygulanır. **Uygulanan:** bu altı tur
      PostgreSQL session advisory lock ile node'lar arasında tek sahipli; process/VM kaybında bağlantıyla birlikte
      kilit bırakılır. Session lock'ın bağlantı kapanınca diğer node'a geçtiğini gerçek PostgreSQL'de sınayan opt-in
      acceptance testi eklendi ve fiziksel bağlantı semantiği için pooling kapalı koşuda geçti. Hakediş defter
      kaydı reference tabanlı idempotent ve DB unique index ile korumalı; index test DB'de doğrulandı. Açık kabul:
      iki gerçek worker, kill/recovery ve dış API timeout-after-success senaryoları; outbox sağlayıcı idempotency
      anahtarları adapter bazında doğrulanacak. İki ayrı Linux `psql` process'iyle lock sahibi/çakışma/SIGKILL
      sonrası yeniden sahiplenme testi için `tools/tests/worker-lock-process-regression.sh` eklendi; parola yalnız
      stdin'den alınır ve DB adı `test|acceptance` güvenlik kapısından geçmek zorundadır. **Linux process-kill
      kanıtı (2026-08-30):** disposable Ubuntu VM'deki ilk `psql` session advisory lock'u aldı, ikinci process
      aynı lock'u alamadı; lock sahibi `SIGKILL` ile kapatılınca yeni process lock'u devraldı. Windows CRLF stdin
      sonlandırması güvenli biçimde normalize edildi; holder gerçek worker semantiğine uygun idle DB session olarak
      FIFO ile tutuldu. Uzak test dizini doğrulanarak temizlendi. Bu kanıt dış sağlayıcı timeout-after-success
      idempotency/reconciliation kapısını kapatmaz. Yalnız `Role=Worker` veya in-memory lock kabul edilmez.
- [x] **11.6 K5 — PostgreSQL multi-host primary bağlantısı** ✅ KOD KAPANDI, PATRONI KABULÜ FAZ 12'DE
      (2026-08-30): Npgsql multi-host data source, yalnız multi-hostta primary targeting, host recheck ve doğrulanan
      typed pool/timeout seçenekleri eklendi. `/ready`, `Postgres:RequirePrimary=true` iken bağlantının recovery/
      standby değil yazılabilir primary olduğunu doğruluyor; acceptance paketi aynı koşulu sınar. Tek-host local
      config geriye uyumlu. Kabul: Patroni test ortamında planned switchover sırasında API
      restart olmadan bounded retry ile yazma devam eder; altyapı kurulana kadar `environment acceptance pending`.
      Tek-primary acceptance hedefinin yazılabilir olduğu gerçek PostgreSQL'de doğrulandı; Patroni switchover
      bununla kapanmış sayılmaz.
- [x] **11.7 K6 — Redis Sentinel-aware bağlantı ve state ayrımı** ✅ KOD KAPANDI, SENTINEL KABULÜ FAZ 12'DE
      (2026-08-30): `RedisCache` ve `RedisState` ayrı bağlantılar; typed `Standalone|Sentinel`, service name,
      timeout/client adı ve legacy `Redis` fallback'i eklendi. Üç Sentinel/quorum 2 konfigürasyonu;
      cache (`allkeys-lfu`) ile security/session/SignalR (`noeviction`) mantıksal setleri ayrılır. Kabul:
      Redis primary kaybında uygulama restart olmadan reconnect ve kritik state sürekliliği; gerçek Sentinel
      ortamı gelene kadar `environment acceptance pending`.
- [x] **11.8 K7 — Node bağımsız storage** ✅ KOD KAPANDI, S3/MINIO AKTİVASYONU MİMARİ KARARLA ERTELENDİ
      (2026-08-30): yorum/iade/talep/vitrin upload noktaları `IFileStorage` arkasına alındı; local provider
      temp+atomik move, path traversal koruması ve public base URL kullanıyor. AWS SDK v4 tabanlı S3 provider;
      AWS S3, path-style MinIO/OVH endpoint, streaming upload, public CDN URL ve süre sınırlandırılmış private
      signed URL ve delete destekliyor; endpoint/credential yalnız secret config'ten gelir. Bilinmeyen provider
      startup'ı durdurur. Katalog image/video servisleri için aynı provider adapter'ı eklendi; mevcut DB/CDN
      düzenini bozmamak için `Storage:Catalog:Enabled=false` varsayılan ve cutover açıkça opt-in. S3 seçildiğinde
      feed XML/CSV çıktıları object storage'a yükleniyor ve feed endpoint'i yalnız doğrulanmış `feedKey` sonrası
      15 dakikalık signed URL üretiyor. Yerel
      provider, key/path traversal ve katalog kategori ayrımı otomatik testli. **Üretim kararı:** ürün resmi
      projeye yüklenmez; DB yalnız mevcut harici görsel subdomain/CDN URL/path bilgisini taşır. API görseli
      indirmez, proxy etmez veya kendi diskine yazmaz; `Storage:Catalog:Enabled=false` kalır. S3 acceptance artık
      FAZ 11/12 kapanış kapısı değildir. Ürün dışı upload/feed kullanılacaksa üretimde tüm API'lerin gördüğü
      paylaşımlı path veya mevcut harici dosya servisi ayrıca konfigüre edilmelidir.
- [x] **11.9 K8 — Otomatik test ve operasyon kanıtı** ✅ YEREL/KÜÇÜK-VM KANIT PAKETİ KAPANDI
      (2026-08-30): solution'a `ECSPros.Api.Tests` MSTest projesi eklendi. Node role canonicalization/
      fail-fast, PostgreSQL option sınırları, local storage atomik yazma-delete/path traversal, katalog image-video
      key ayrımı, S3 fail-fast/HTTPS/signed-URL, Redis legacy/Sentinel mode-timeout ve proxy trust/spoof sınırlarını
      kapsayan 49 unit test geçiyor. Eklenen üç PostgreSQL acceptance testi ile S3 upload/signed-read/delete ve
      Redis cross-connection state/pub-sub testleri
      ortam değişkeni verilmediğinde dış bağlantı yapmadan atlanıyor. İzole test DB'de feed eşzamanlı claim/lease
      recovery ve session advisory-lock release kanıtını otomatik üretmek üzere hazır.
      **Redis kanıtı (2026-08-30):** SSH tüneli üzerinden gerçek test Redis'e iki bağlantı açıldı; TTL key
      yazma/diğer bağlantıdan okuma, pub/sub teslimi ve cleanup testi 1/1 geçti. Bu sonuç standalone bağlantı ve
      cross-connection davranışını kanıtlar; Sentinel primary failover kanıtı değildir.
      **PostgreSQL kanıtı (2026-08-30):** güvenlik kapılı üç acceptance testi gerçek test DB'de 3/3 geçti.
      Boş DB migration koşusu Storefront, Accounts, Requests ve Procurement context'lerinde migration assembly'nin
      startup assembly'ye kaydığını ortaya çıkardı; dört DI kaydına açık `MigrationsAssembly` eklendi ve history
      tabloları ile Accounts idempotency index'i DB üzerinden doğrulandı.
      Feed lease için rollback'li SQL regresyon betiği, iki additive migration için idempotent EF SQL üretimi ve
      çoklu-node devreye alma/kabul runbook'u hazır. Kalan testler; gerçek worker process kill, worker duplicate,
      cross-node Data Protection, SignalR, DB/Redis failover ve storage bağımsızlığı senaryoları `unit`,
      `integration`, `environment acceptance` olarak ayrılır. Linux advisory-lock process-kill/recovery koşusu
      gerçek test PostgreSQL üzerinde geçti. S3 acceptance testi kodda opsiyonel korunur fakat harici görsel
      sunucusu kararı nedeniyle çalıştırılması ve S3 config'i doldurulması ertelenmiştir. Mock/unit sonuçları
      gerçek failover kanıtı sayılmaz. **İki API process kanıtı:** Ubuntu VM'de `Node:Role=Api` ve
      `MigrateOnStartup=false` ile iki Release API açıldı; farklı node ID, writable-primary PostgreSQL,
      Redis-state ve Data Protection readiness sağlıklıydı. API-A challenge'ı API-B'de bir kez tüketildi;
      API-A durdurulunca API-B hazır kaldı. Migration/seed ve dış etkili worker çalışmadı; test process/dizinleri
      temizlendi. **İki-VM API kanıtı (2026-08-30):** `192.168.0.242` ve `192.168.0.243` üzerindeki iki gerçek
      Release API farklı node ID ile hazır oldu; PostgreSQL writable-primary, Redis-state ve Data Protection
      kontrolleri iki node'da sağlıklıydı. API-A'da üretilen challenge API-B'de yalnız bir kez tüketildi; API-A
      process'i durdurulunca API-B sağlıklı kaldı. Test process/dizinleri ve yerel publish paketi temizlendi.
      Aynı iki VM'de authenticated SignalR A→B Redis-backplane mesajı da ayrıca geçti. Bu kanıt VM/fiziksel host
      power-off, Patroni veya Sentinel failover kanıtı değildir. **VM power-off kanıtı (2026-08-30):** başlangıçta
      iki node authenticated SignalR A→B testini geçti; ardından `5.39.57.242` gerçek `systemctl poweroff` ile
      kapatıldı. API-A/SSH erişimi düştüğü halde API-B `Healthy` kaldı ve VM kaybından sonra yeni login ile
      authenticated DashboardHub `MetricsUpdated` olayı aldı. Bu API VM kaybını kanıtlar; iki VM aynı fiziksel
      ESXi host üzerindeyse fiziksel host kaybı kanıtı değildir. VM2, test IAM kullanıcısı/oturumları ve yerel paket
      temizlendi. VM1 tekrar açıldığında public SSH henüz kapalıyken private ping/SSH sağlıklıydı; VM2 jump host
      üzerinden yalnız doğrulanmış acceptance dizini temizlendi. Test artığı kalmadı.
      **Yerel storefront smoke (2026-08-30):** Windows'ta çalışan API, SSH tüneli üzerinden `.59` PostgreSQL
      ve Redis'e bağlandı; migration/seed ve dış etkili worker'lar kapalı tutuldu. Ana sayfa, kategori sayfası,
      Swagger, `/health` ve `/ready` HTTP 200; PostgreSQL writable-primary, Redis cache/state ve Data Protection
      `Healthy` geçti. Razor runtime compilation sırasında bulunan ana sayfa/kategori/ürün detay collection
      expression ve DI extension uyumsuzlukları düzeltildi. Redis secret hiçbir dosya veya loga yazılmadı.
- [ ] **11.T Kod fazı kapanış kapısı** 🟡 TEST KAPSAMI KAPANDI, KALAN KOD K4/DIŞ SAĞLAYICI GÜVENLİĞİ
      (2026-08-30): son birleşik koşuda solution build 0 hata/0 uyarı; 49 unit + 3 PostgreSQL + 1 Redis olmak
      üzere 53 test geçti, yalnız S3 acceptance mimari karar gereği skipped;
      sonraki hedefli koşuda Redis acceptance testi gerçek test Redis üzerinde 1/1 geçti;
      PostgreSQL acceptance paketi gerçek test DB üzerinde 3/3 geçti;
      iki ayrı Linux VM'deki gerçek API'ler shared DB/Redis/DP readiness, çapraz Redis state ve peer-process-stop
      testini; aynı JWT ile authenticated SignalR A→B backplane yayınını ve gerçek API-A VM power-off sonrasında
      API-B login/readiness/SignalR sürekliliğini geçti;
      disposable Linux deploy activation/rollback/retention regresyonu geçti ve uzak test dizini temizlendi;
      Integration+Accounts idempotent migration SQL üretimi başarılı; iki deploy betiği `bash -n`, tüm module
      migration project yolları ve `git diff --check` temiz. Çapraz-node betiğinin korumalı `/health/detail`
      yerine anonim `nodeId` taşıyan `/ready` kullanması düzeltildi. `Storage:Provider=S3` seçilip zorunlu endpoint/secret
      config'i verilmediğinde startup beklendiği gibi durdu. Config örnekleri, migration preflight/rollback
      yaklaşımı ve acceptance runbook'u güncel.
      Küçük-VM test fazı kullanıcı kararıyla burada kapandı. Gerçek Patroni/Sentinel, Nginx/LB, fiziksel host,
      backup restore ve 4.000 kullanıcı load/soak testleri FAZ 12 üretim öncesi altyapı kabuline taşındı ve 11.T'yi
      bloke etmez. 11.T yalnız K4 worker/provider idempotency-reconciliation kod işi kapandığında `[x]` yapılır.

## FAZ 12 — Scale-i3 üretim yerleşimi ve tam HA'ya geçiş

**Alan:** OVH/ESXi + Nginx + PostgreSQL + Redis + storage · **Bloklayan:** FAZ 11 ilgili kod kapıları,
canlı değişiklik için kullanıcı onayı ve bakım penceresi.

- [x] **12.0 İlk fiziksel sunucu kararı** ✅ KAYDEDİLDİ (2026-08-30): mevcut OVH Scale-i3 — Intel Xeon
      Gold 6438M, 32c/64t, 2.2/3.9 GHz, 256 GB ECC 4800 MHz. İlk kurulum tek fiziksel hostta yapılabilir;
      bu kapasite başlangıcıdır, fiziksel HA değildir. Önerilen başlangıç VM'leri: `nginx-1` 2 vCPU/4 GB,
      `api-1` ve `api-2` ayrı ayrı 8 vCPU/24 GB, `worker-1` 4 vCPU/12 GB, `postgres-1` 12 vCPU/64 GB,
      `redis-1` 4 vCPU/16 GB, `monitoring-1` 4 vCPU/8 GB; ESXi için en az 16 GB rezerv.
- [ ] **12.1 Disk/ağ envanteri ve son VM planı:** gerçek NVMe/RAID kapasitesi, disk endurance, private/public
      uplink ve mevcut datastore ölçülmeden disk boyutları kesinleştirilmez. PostgreSQL OS/data/WAL ayrı virtual
      disk; VM memory reservation ve vCPU:pCPU hedefi en fazla yaklaşık `1.5:1`.
- [ ] **12.2 Tek Scale-i3 yerleşimi:** VM'ler, APP/DATA/MGMT ayrımı, Nginx→iki API upstream ve monitoring.
      DB/Redis/Sentinel/Patroni otomasyonu henüz yoksa standalone çalışır; aynı fiziksel hosttaki sahte replica
      veya üç quorum VM'i gerçek HA diye adlandırılmaz.
- [ ] **12.3 Harici yedek:** PostgreSQL continuous WAL + günlük differential + haftalık full yedek, Scale-i3
      dışındaki immutable/object storage'a kopya; aylık ayrı ortam restore testi. VM snapshot yedek sayılmaz.
- [ ] **12.4 Tek-host 4.000 kullanıcı kapasite testi:** gerçek trafik dağılımına yakın load + en az 2 saat soak;
      p50/p95/p99, 5xx, API CPU/RAM/GC, DB pool/slow query, Redis memory ve worker queue age kaydı. Donanım
      yeterliliği testten önce garanti edilmez.
- [ ] **12.5 İkinci fiziksel sunucu:** aynı failure domain dışında önerilen eş kapasite; `nginx-2`, `api-3/4`,
      `postgres-2`, `redis-2`, `ha-2`. Tek fiziksel host kaybında 4.000 kullanıcı yükünü kalan host taşır.
- [ ] **12.6 Bağımsız witness:** iki ESXi hosttan bağımsız 2 vCPU/4 GB/40 GB VM; etcd-3 + Sentinel-3.
- [ ] **12.7 Stateful HA:** Patroni + 3 etcd, PostgreSQL synchronous standby; Redis primary/replica + 3 Sentinel
      quorum 2. Ürün görselleri mevcut bağımsız subdomain/CDN sunucusundan gelir; API storage'ına alınmaz.
      Planned ve unplanned failover sonuçları kayıtlı.
- [ ] **12.8 Çift Nginx ve OVH LB:** LB health check fiziksel hostu değil her Nginx üzerinden API `/ready`
      zincirini kontrol eder; origin erişimi Cloudflare/OVH LB kaynaklarıyla sınırlı; WebSocket ve upstream retry
      testli.
- [ ] **12.T Tam HA kabul kapısı:** bir API process, API VM, Nginx VM, Redis primary, PostgreSQL primary ve son
      olarak bir ESXi host kontrollü kapatılır. Tek host kapalıyken 4.000 kullanıcı SLO testi, cross-node eski
      credential/cookie decrypt, SignalR, worker idempotency, harici görsel subdomain erişimi ve offsite restore testlerinin tamamı
      geçmeden yapı “tam HA” olarak adlandırılmaz.

## Önerilen sıra

1. **FAZ 0'daki hazır girdiler** hangileriyse önce onların açtığı fazlar (örn. DNS geldiyse F5.1 hemen).
2. Ticari etki sırası önerim: **F1 (satış kanalı F4)** → **F3 (kargo KG1)** → **F2 (tedarik cutover, 0.6 netleşince)**
   → **F4 (Trendyol canlı)** → **F7 (go-live PART B)** → F5/F6 paralel fırsat buldukça → F8/F9 araya serpiştirilir.
3. Her faz kapanışında bu dokümanda işaretle + PROGRESS panosunu güncelle (K18 kapanış raporu kuralı geçerli).
4. Çoklu sunucu çalışmasında sıra: **11.1 → 11.2 → 11.3 → 11.4/11.7 → 11.5 → 11.6 → 11.8 →
   11.9/11.T → 12.1-12.4 → ikinci fiziksel sunucu geldiğinde 12.5-12.T**.
