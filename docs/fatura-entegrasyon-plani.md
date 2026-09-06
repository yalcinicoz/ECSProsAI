# Fatura Entegrasyonu Planı — Seriler, Entegratör Sözleşmeleri, Gönderim Yöntemleri, Dış Numaralı Faturalar, Takip

> Sürüm: **v1.1 — 2026-09-06** (seri TİPLİ tanımlanır — kullanıcı eki) · Durum: **TASLAK — K8 KAPANDI; K1-K7, K9-K11 kullanıcı onayı bekliyor**
> Alan: **Admin panel (pano #2) + Order/Core modülleri.** ERP (Nebim V3) yönü ekip arkadaşının
> `docs/erp-kaynak-senkron-gecis-plani.md` E7 kapısına bağlanır; o taraf bu planda yalnız arayüz sözleşmesi olarak geçer.
> İlgili: `docs/siparis-paket-kargo-kod-plani.md` (paket başına fatura), `docs/siparis-operasyon-plani.md` (OP2 paket
> kapanışında otomatik fatura), `docs/legacy-mysql-uye-siparis-fatura-iade-okuma-plani.md` (MSR/TYA eski faturalar),
> `docs/platform-servis-entegrasyonlari` kaydı (`core_firm_platform_integrations`, şifreli Credentials).

---

## 0. İlkeler (kullanıcı kararları, 2026-09-06)

1. **Entegratör sözleşmeleri firma bazlıdır.** Bir firma aynı anda birden fazla entegratörle çalışabilir. Sözleşme
   kanal düzeyinde değildir; kanal, seri üzerinden dolaylı olarak sözleşmeye ulaşır.
2. **Fatura serileri tekildir ve TİPLİDİR.** Seri kaydı tek bir üç harfli ön eki temsil eder (bugünkü e-arşiv/e-fatura/
   ihracat üçlü seti kaldırılır) ve **tanımlanırken tipi seçilir: e-arşiv, e-fatura veya ihracat**. Tip sonradan
   değiştirilemez (numara üretildiyse). **Her seri bir entegratör sözleşmesine bağlanır.**
3. **Satış kanalı serileri tiple bağlar ve tip eşleşmek zorundadır:** kanalda e-arşiv, e-fatura ve ihracat yuvalarına
   yalnız **aynı tipteki** seri bağlanabilir (e-arşiv yuvasına e-fatura serisi seçilemez; seçici zaten yalnız o tipi
   listeler, sunucu da doğrular). Aynı seri birden fazla kanalda kullanılabilir. **Gerekçe (kullanıcı, 2026-09-06):**
   pratikteki en büyük karışıklık, bir e-arşiv serisinin bir yerde e-arşiv, başka yerde e-fatura için kullanılmasıdır;
   tip seride sabitlenince bu yapısal olarak imkânsız hale gelir.
4. **Seri pasife alınırken kanal serisiz kalamaz.** Pasifleştirme, seriyi kullanan aktif kanal varsa yerine geçecek
   seri verilmeden reddedilir; verilirse tüm bağlar tek işlemde yeni seriye taşınır.
5. **Gönderim yöntemi satış kanalına özeldir:** (a) entegratöre API ile biz göndeririz, (b) ERP gönderir,
   (c) pazaryeri kendi keser/gönderir. Her durumda fatura numarası dâhil tüm bilgiler bizim veritabanımızda
   saklanır, izlenir ve sonraki işlemler (iptal, iade, cari, rapor) buradan yürür.
6. **Fatura numarası dört kaynaktan gelebilir:** bizim serimiz, ERP, pazaryeri, entegratör. Kaynağı kayıtta tutulur.
7. **Bizim ürettiğimiz numaralar her seride 1'den başlar, sıra ve tarih atlamaz.** Bu bir kabul kriteridir, "genelde
   böyle olur" değil.

## 1. Mevcut durum (2026-09-06 canlı tespiti)

| Bileşen | Durum |
|---|---|
| Fatura kaydı (`order.ord_invoices`) | ✅ Sipariş + paket bazlı; seri/yıl/sıra/numara, alıcı, tutarlar, iptal zinciri, `IntegratorStatus`/`ErpStatus` alanları var |
| Seri modeli (`order.ord_invoice_series`) | ⚠️ **Üçlü set** (EArchiveSerial/EInvoiceSerial/ExportSerial) + IsActive; firma bazlı; **1 test serisi (TST)** |
| Kanal → seri bağı | ⚠️ `FirmPlatform.InvoiceSeriesId` alanı var, **hiçbir akış okumaz**; 5 kanalın hiçbirinde dolu değil |
| Otomatik fatura | ⚠️ Paket kapanışında (OP2) firmanın **ilk aktif** serisinden e-arşiv keser; seri yoksa hata |
| Numara üretimi | ⚠️ `MAX(sıra)+1`, satır kilidi yok (eşzamanlı istekte biri unique index'e çarpar), **tarih kontrolü yok**, sıra (seri, tip) bazında |
| Kargodaki/teslim sipariş | **17 sipariş, 0 fatura** |
| Entegratör kataloğu | ❌ `definition.integration_services`'ta e-fatura entegratörü yok; `nebim` (erp) var, ayar şeması boş |
| Gönderim | ❌ Entegratöre/ERP'ye/pazaryerine gönderen hiçbir işlem yok; `send-to-integrator` ucu tasarımda listeli, yazılmamış |
| Dış numaralı fatura | ❌ Yol yok — numara daima bizim seriden |
| Panel | ⚠️ Faturalar sayfası: liste, iptal, elle PDF adresi; seri oluşturma modalı (üçlü set) |
| Eski faturalar | `LegacyInvoiceImportSlice` (MSR/TYA) üçlü seri tablosunu okur — model değişince **uyarlanmalı** (ekip arkadaşıyla) |

## 2. Hedef model

### 2.1 Entegratör kataloğu ve firma sözleşmeleri (Core)
- `definition.integration_services` → yeni `ServiceType = "einvoice"` kayıtları (ilk entegratör K1). `SettingsSchemaJson`
  ile kimlik alanları (kullanıcı/şifre veya API anahtarı, servis adresi, gönderici etiketi/GB alias, test modu).
  Definition kuralı geçerli: yalnız platform yönetimi ekler.
- **Sözleşme = mevcut `core_firm_platform_integrations` satırı**, `FirmPlatformId = NULL` (firma geneli), `ServiceType=einvoice`.
  Bir firmada aynı entegratörden birden fazla sözleşme olabilir (`Name` ayırt eder). Credentials Data Protection ile
  şifreli (mevcut kalıp). Ek kolon gerekmez; `Status/StartDate/EndDate` zaten var.

### 2.2 Tekil fatura serisi (Order)
`order.ord_invoice_series` yeniden yapılandırılır:

| Alan | Açıklama |
|---|---|
| `FirmId` | Firma (seri VKN'ye aittir) |
| `Serial` | 3 büyük harf; **firma içinde tekil** (tipten bağımsız — aynı harfler iki tipte tanımlanamaz, karışıklığın kaynağı budur) |
| `InvoiceType` | **`e_archive | e_invoice | export` — zorunlu, oluşturmada seçilir; sayaç 0'dan ileri gittiyse değiştirilemez** |
| `Name` | Serbest ad |
| `IntegrationContractId` | → `core_firm_platform_integrations.Id` (einvoice). **Zorunlu**, kanal yöntemi ERP/pazaryeri olsa bile seri bir sözleşmeye aittir (K2) |
| `IsActive`, `RetiredAt` | Pasifleştirme izi; pasif seri numara üretmez ama geçmiş faturalar ona bağlı kalır |
| `Description` | Not |

Sayaç ayrı tabloda: `order.ord_invoice_series_counters (SeriesId, Year, LastSequence, LastInvoiceDate)` — numara
üretimi bu satırı `SELECT … FOR UPDATE` ile kilitler. **Sıra (seri, yıl) bazındadır; tipten bağımsızdır** (bir seri = bir
numara akışı). Yıl değişince 1'den başlar (GİB kalıbı `ABC2026000000001`, 16 karakter).

### 2.3 Kanal bağları (Core)
`FirmPlatform.InvoiceSeriesId` kaldırılır; yerine:
- `FirmPlatform.InvoiceSendMethod`: `integrator_api | erp | marketplace | manual` (varsayılan `manual` — hiçbir şey
  gönderilmez, yalnız kayıt; go-live öncesi her kanal açıkça seçilir).
- `core.core_firm_platform_invoice_series (FirmPlatformId, InvoiceType, SeriesId)` — `InvoiceType ∈ {e_archive, e_invoice, export}`,
  (kanal, tip) tekil. Kanal yalnız kendi firmasının serilerini bağlayabilir ve **`Series.InvoiceType == bağ.InvoiceType`
  şartı sunucuda doğrulanır** (uyumsuz tip → 400 "Seri tipi yuvayla uyuşmuyor"). Panel seçicileri yuva tipine göre süzer.
- Fatura kesiminde de aynı kural: `CreateInvoice(InvoiceType, SeriesId)` çağrısında seri tipi istekle uyuşmazsa 400;
  otomatik kesim kanalın ilgili yuvasından seriyi alır, dolayısıyla tip zaten eşleşir.
- **Bütünlük kuralları:** aktif kanal + `integrator_api` yöntemi → üç tipin de serisi dolu olmalı (readiness);
  seri pasifleştirme → bağlı aktif kanal varsa `replacementSeriesId` zorunlu, taşıma tek transaction; seri silme yok.
- Panelde kanal ayarında "Faturalama" bölümü: yöntem + üç seri seçici + uyarı rozeti ("e-fatura serisi eksik").

### 2.4 Fatura kaydı genişletmesi (Order)
`ord_invoices`'a eklenen alanlar:

| Alan | Açıklama |
|---|---|
| `NumberSource` | `internal | erp | marketplace | integrator` |
| `SendMethod` | Kesim anındaki kanal yöntemi (anlık görüntü) |
| `IntegrationContractId` | Kullanılan sözleşme (seri üzerinden çözülür, anlık görüntü) |
| `Ettn` | GİB evrensel tekil tanımlayıcı (UUID) — biz üretiriz ya da dış kaynaktan gelir |
| `ExternalDocumentId`, `ExternalSource` | Entegratör/ERP/pazaryeri tarafındaki belge kimliği |
| `InvoiceSeriesId` → **nullable** | Dış numaralı faturada seri yoktur; `InvoiceSerial/Year/Sequence` dış numaradan ayrıştırılır (ayrıştırılamazsa yalnız `InvoiceNumber`) |
| `IntegratorStatus` durum makinesi | `not_applicable | pending | queued | sent | accepted | rejected | error | cancelled` |
| `ErpStatus` | `not_applicable | pending | sent | acknowledged | error` (E7 yazar) |

**Dış fatura kaydı ucu:** `POST /api/orders/{orderId}/invoices/external` — kaynak, numara, tarih, ETTN, tutarlar,
kalemler (opsiyonel; yoksa paket/siparişten türetilir). İdempotent anahtar `(ExternalSource, InvoiceNumber)`.
Pazaryeri ve ERP adaptörleri de bu komutu kullanır; elle giriş panelden aynı uca gider.

### 2.5 Numara üretimi güvencesi
- Sayaç satırı kilidi (eşzamanlı istekler sıralanır, hata yerine bekler).
- **Tarih kuralı:** `InvoiceDate ≥ LastInvoiceDate` (aynı gün serbest), gelecek tarih yasak; ihlalde açık hata (K3).
- Yıl dönümü: yeni yıl sayacı 1'den başlar; eski yıla fatura kesilemez (K3).
- İptal numarayı tüketir (mevcut davranış korunur). **Boşluk denetimi** raporu: seri/yıl bazında beklenen–gerçek fark.
- Unique index `(Serial, Year, Sequence)` kalır; ek `(ExternalSource, InvoiceNumber)` kısmi tekil indeks; seri tablosunda
  `(FirmId, Serial)` tekil (tip ne olursa olsun aynı harfler bir kez).

### 2.6 Gönderim kuyruğu ve adaptörler (Order + Integration)
- Outbox: `order.ord_invoice_dispatches (InvoiceId, Method, Action=send|cancel|status, Attempt, NextAttemptAt, Status,
  RequestSnapshot, ResponseSnapshot, Error, ContractId)`. Fatura kesildiğinde kanal yöntemi `integrator_api` ise
  `send` kaydı düşer; iptalde `cancel`.
- Worker: mevcut hosted-worker kalıbı; **`Node:Role=Worker|Both` kapısına uyar** (altyapı ekibinin deploy düzenine
  dokunulmaz, yalnız not düşülür). Üstel geri çekilme, azami deneme, ölü-mektup durumu.
- Sağlayıcı arayüzü `IEInvoiceProvider { SendAsync, CancelAsync, QueryStatusAsync, GetPdfAsync, CheckRecipientAsync }`
  — `CheckRecipientAsync`: alıcı VKN/TCKN e-fatura mükellefi mi (GİB kayıtlı kullanıcı sorgusu) → tip seçimi (K5).
- İlk adaptör K1'de seçilen entegratör; sandbox hesabıyla kabul. Diğerleri aynı arayüzle.
- `erp` yöntemi: dispatch `Method=erp` satırı **E7 outbox'ına devredilir** (ekip arkadaşı); fatura numarası Nebim'den
  dönerse `NumberSource=erp` ile dış kayıt olarak yazılır. Bizim tarafımız yalnız sözleşmeyi ve durum geri yazımını tanımlar.
- `marketplace` yöntemi: pazaryeri adaptörü (FAZ 4 Trendyol canlı) sipariş faturasını pazaryeri API'sinden çeker ya da
  bizim faturamızın PDF/URL'sini pazaryerine yükler (K7); her iki halde `NumberSource=marketplace` dış kayıt.

### 2.7 Panel (Admin)
- **Fatura Serileri** sayfası (Faturalar'dan ayrılır): tekil seri listesi (firma, seri, sözleşme, kullanan kanal sayısı,
  son numara/tarih, durum); oluşturma; **pasifleştirme diyaloğu** (kullanan kanallar listesi + zorunlu yerine-geçecek seri).
- **Entegratör Sözleşmeleri**: mevcut firma entegrasyon ekranında `einvoice` servis tipi (şemadan üretilen form, maskeli kimlik).
- **Kanal ayarı → Faturalama**: yöntem + üç seri + eksik uyarısı.
- **Faturalar**: liste filtreleri (kanal, kaynak, gönderim durumu, tarih), satır tıklanınca detay çekmecesi (kalemler,
  gönderim zaman çizelgesi, entegratör yanıtı, **Tekrar Dene**, PDF, iptal), **Hata Kuyruğu** sekmesi (ölü-mektuplar),
  **Dış Fatura Kaydet** modalı, **Boşluk Denetimi** raporu.
- Rehber sayfaları: fatura serileri, kanal faturalama, hata kuyruğu.

## 3. Fazlar

| Faz | İş | Kabul kriteri |
|---|---|---|
| **FE0** Model | Tekil **tipli** seri + sayaç + kanal bağ tablosu + `InvoiceSendMethod` + fatura kayıt genişletmesi; migration'da mevcut üçlü setler tekil serilere **ayrıştırılır** (her kolon kendi tipiyle: EArchiveSerial→e_archive, EInvoiceSerial→e_invoice, ExportSerial→export; **aynı harf birden fazla kolonda ise yalnız e_archive tipiyle taşınır, diğerleri boş bırakılıp panelde kırmızı uyarıyla elle tanımlanır** — TST/TST/TST bu durumdadır), mevcut tek fatura yeni seri Id'sine bağlanır; `LegacyInvoiceImportSlice` yeni tabloya uyarlanır (ekip arkadaşıyla) | İki DB'de migration temiz; eski `InvoiceSeriesId` alanı kaldırıldı; import dilimi dry-run `0` hata; tip uyumsuz bağ/kesim 400 |
| **FE1** Numara güvencesi + dış kayıt | Kilitli sayaç, tarih/yıl kuralları, boşluk raporu, `invoices/external` ucu, idempotent anahtar | 50 eşzamanlı kesimde 1..50 boşluksuz; geçmiş tarihli istek 400; aynı dış numara ikinci kez 409/idempotent |
| **FE2** Seri/kanal operasyonu (panel) | Seri sayfası (tip sütunu + oluşturmada tip seçimi), pasifleştirme+yerine-geçecek (**yalnız aynı tipte** seri önerilir), kanal Faturalama bölümü (her yuva kendi tipindeki serileri listeler), readiness uyarısı | Kullanılan seri yerine seri verilmeden pasife alınamaz (400); farklı tipte yerine-geçecek 400; verilince kanallar taşınır; serisiz aktif kanal panelde kırmızı |
| **FE3** Entegratör kataloğu + sözleşme + ilk adaptör | `einvoice` servis kaydı + şema, firma sözleşmesi formu, `IEInvoiceProvider`, K1 adaptörü sandbox | Sandbox'ta e-arşiv gönder/iptal/durum/PDF geçer; mükellef sorgusu doğru tip seçer |
| **FE4** Gönderim kuyruğu + takip | Outbox + worker + geri çekilme + ölü-mektup; Faturalar detay çekmecesi, Tekrar Dene, Hata Kuyruğu | Entegratör kapalıyken kesilen fatura kuyrukta bekler, açılınca gönderilir; 3 hatalı deneme sonrası ölü-mektupta görünür ve elle tekrar denenebilir |
| **FE5** Otomatik fatura politikası | Kesim tetiği (paket kapanışı / kargoya veriş — K4), 17 kargodaki siparişin geriye dönük faturalanması (K4), tip seçimi (K5), ihracat tetiği (K6) | Kargoya verilen hiçbir siparişte faturasız paket kalmaz; rapor sıfır |
| **FE6** ERP yöntemi | E7 outbox arayüz sözleşmesi (bizde dispatch→E7, E7'den numara/durum geri yazımı) | Ekip arkadaşının E7 kabulüyle birlikte; bizde yalnız arayüz testi |
| **FE7** Pazaryeri yöntemi | Trendyol fatura çekme/yükleme (K7), dış kayıt | FAZ 4 Trendyol canlıya bağlı |

Sıra: FE0 → FE1 → FE2 → FE3 → FE4 → FE5; FE6/FE7 dış bağımlılıklarla paralel. Bir faz kapanmadan sonrakine geçilmez.

## 4. Karar soruları (K)

| # | Soru | Öneri |
|---|---|---|
| K1 | İlk entegratör hangisi? (API dokümanı + sandbox hesabı gerekir; eski sistemde kullanılan entegratör varsa aynı) | Kullanıcı belirtir; eski sistemdeki entegratörle başlanır |
| K2 | ERP/pazaryeri yöntemli kanalların serisinde de sözleşme zorunlu mu? | **Evet** — seri daima bir sözleşmeye aittir (ilke 2); ERP-gönderimli kanallarda seri Nebim'in kullandığı seriyle aynı tanımlanır, numara `NumberSource=erp` gelir |
| K3 | Tarih kuralı: geçmiş tarih tamamen yasak mı, "serinin son tarihinden geri olmasın" mı? Yıl kapanınca eski yıla kesim? | Son tarihten geri olmaz, gelecek tarih yasak, eski yıla kesim yasak |
| K4 | Otomatik kesim tetiği: paket kapanışı (bugünkü OP2) mi, kargoya veriş mi? 17 kargodaki sipariş geriye dönük kesilsin mi? | Paket kapanışı kalır (kargo öncesi belge hazır); 17 sipariş FE5'te bugünün tarihiyle kesilir |
| K5 | e-fatura / e-arşiv seçimi: alıcı VKN mükellef sorgusu ile otomatik mi, operatör seçer mi? | Otomatik (entegratör mükellef sorgusu), operatör geçersiz kılabilir |
| K6 | İhracat tipi tetiği: teslimat ülkesi TR dışı → ihracat serisi | Otomatik |
| K7 | Pazaryeri-kesimli kanallarda pazaryerinden numarayı API ile mi çekeriz, elle mi girilir? Kendi faturamızı pazaryerine yükleyen kanal var mı? | Trendyol API ile; elle giriş yedek yol |
| ~~K8~~ | ~~Aynı seri birden fazla tipe bağlanabilir mi?~~ **KAPANDI (2026-09-06, kullanıcı):** seri tanımında tip zorunlu; kanal yuvası yalnız aynı tipteki seriyi kabul eder; aynı harfler firma içinde tek kayıt | Uygulandı: §0.2-3, §2.2, §2.3, FE0/FE2 |
| K9 | İptal: e-arşivde entegratör iptali; e-faturada iptal yerine iade faturası mı? | GİB kuralı: e-arşiv iptal, e-fatura için iade faturası (Order iade akışına bağlanır) |
| K10 | Firma başına aynı anda birden çok sözleşme olduğunda seri seçiminde gösterim | Seri formunda sözleşme zorunlu seçici; kanal seçicide seri yanında sözleşme adı |
| K11 | Worker deploy'u (Node rolü) ve E7 arayüzü — ekip arkadaşıyla paylaşım noktası | FE4 öncesi tek toplantı; bu plan onlara gönderilir |

## 5. Riskler

- **Eski seri modeline bağımlı kod:** `LegacyInvoiceImportSlice` (ekip arkadaşı) ve `CreatePackageInvoiceAuto` — FE0'da birlikte değişir.
- **Numara boşluğu:** kilit olmadan eşzamanlı kesim, tarih kontrolü olmadan geriye kesim mali riske dönüşür — FE1 FE5'ten önce şart.
- **Sözleşme kimlikleri:** Data Protection key ring yedekte olmalı (mevcut kural); entegratör kimliği kaybı gönderimi durdurur.
- **Pazaryeri/ERP bağımlılıkları:** FE6/FE7 dış planlara bağlı; bu plan onları bloke etmez, onlar bunu bloke etmez.
