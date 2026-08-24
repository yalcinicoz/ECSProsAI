# Ürün Tedarik İş Akışı — Satın Alma → Mal Kabul → Ayrıştırma/Etiketleme → Yerleştirme → Satışa Giriş

> Sürüm: **v1.3 — 2026-08-24** (T4 REVİZYONU — kullanıcı onayı: ayrıştırma fiziki/sistem dışı; etiket basımı
> AYRI ve KEYFÎ bir işlem, SAYIM ÜRETMEZ; gerçek sayım = depoya teslim OKUTMASI, iki modlu)
> **T4 revizyon kurgusu:** (a) Fiziki ayrıştırma + kalite kontrol → sistem dışı, ekran yok (defolu takibi ileriye).
> (b) **Etiket Basımı** `/procurement/labels` (procurement.manage): yığın listesi + deste adedi → "Tümünü Yazdır"
> tek sekmede tüm desteler (`/yazdir/etiket?items=vid:adet,...`, ≤50 ürün / ≤2000 etiket); kayıt üretmez.
> (c) **Sayım / Depoya Teslim** `/procurement/sorting` (procurement.sort): OKUTMA modu her okutma +1, ADET modu
> barkod bir kez + adet (markalı/kendi etiketli yüksek adetli ürünler — üretici barkodu varyantla eşleşiyorsa
> etiket basılmadan sayılır); sayım (parti, varyant) başına BEKLEYEN kayıtta birikir (`sorting/scan` ucu);
> oturum kapanışı YOK — parti durumu yeterli (kullanıcı kararı). K9 bildirimi sayım ekranında.
> (d) Yerleştirme T5 değişmedi. SortingEntry şeması aynen; LabelPrinted/LabelCount artık yalnız opsiyonel iz.

> **Uygulama durumu:** **T1 UYGULANDI (2026-08-24) ⚠️ restart bekliyor** — Procurement modülü
> (`procurement` şeması, migration `InitProcurement` canlı DB'de), PurchaseOrder+Item CRUD, durum makinesi
> (closed→receiving geri açma dahil), Excel panoya yapıştır (sütun eşleme + TR sayı biçimi), yetkiler
> `procurement.manage`/`procurement.sort` (seed; firm_admin dahil), admin: Tedarik → Satın Almalar (liste+detay),
> rehber `11-tedarik/10-satin-almalar`. İzole 5051 ✓ (kod üretimi, toplamlar, negatifler: adet 0 / kimliksiz kalem /
> kapalıya kalem / geçersiz geçiş / yetkisiz 403). **T2 Mal Kabul UYGULANDI (2026-08-24) ⚠️ restart bekliyor** —
> ReceiptBatch(+Item+PO bağı) entity'leri (migration `AddReceiptBatches` canlı DB'de), MK-#### kod, kalemsiz parti
> açma, durum makinesi (completed→sorting geri aç), gevşek çoktan-çoğa SA bağı (link'te ordered→receiving; farklı
> tedarikçi SA'sı reddedilir), fatura gevşek bağı, kaba evrak kalemleri; admin Mal Kabul liste+detay; rehber
> `20-mal-kabul`. İzole 5051 ✓ (kalemsiz sorting, idempotent link, tamamlanmışa kalem 400, çapraz tedarikçi 400).
> **T3 Etiket Şablonları UYGULANDI (2026-08-24) ⚠️ restart bekliyor** — `core.core_label_templates` (migration
> `AddLabelTemplates` canlı DB'de), Upsert/Delete/Get (varsayılan hedef tip başına tek; kod addan türetilir),
> `/yazdir/etiket` + `/yazdir/etiket-birim` ([AllowAnonymous]+GUID kalıbı; JsBarcode: 13 hane→EAN13, değilse
> CODE128; @page = şablon ölçüsü, sayfa başına 1 etiket), admin `/settings/label-templates` görsel düzenleyici
> (sürükleme, taşma uyarısı, örnek veriyle önizleme, ürün koduyla test basımı), rehber `30-etiket-sablonlari`.
> Not: plan §6 `settings.manage` demişti — böyle bir permission yok, `procurement.manage` kullanıldı.
> İzole 5051 ✓ (varsayılan devri, kod türetme, 3 kopya+3 barkod svg, bozuk JSON/ölçü/tip 400, hedef tip uyuşmazlığı 404).
> **T4 Ayrıştırma UYGULANDI (2026-08-24) ⚠️ restart bekliyor** — `sorting_entries` + `missing_card_notices`
> (migration `AddSortingEntries` canlı DB'de), varyant arama (barkod TAM → SKU TAM → içeren; renk/beden/fiyat ile),
> sayım CRUD (yerleşmiş kayıt kilitli), ilk sayımda parti received→sorting, etiket sayaç ucu, K9 kart-eksik
> bildirimi (aç/çöz), `procurement.sort` yetkisi uçlarda; admin `/procurement/sorting` barkod-dostu operasyon
> ekranı (otomatik odak, tek tam eşleşmede doğrudan seçim, Kaydet+Etiket Bas → /yazdir/etiket, partisiz mod,
> bildirim listesi) + Mal Kabul detayından geçiş butonu; rehber `40-ayristirma`. Not: UnitCost SA'dan otomatik
> ÖNERİLMİYOR (v1 elle — İ3 gevşek bağ; T6 raporu maliyeti kayıttan okur). İzole 5051 ✓ (3 varyant sayımı,
> parti otomatik sorting, etiket sayacı, adet 0 / olmayan varyant / tamamlanmış parti 400, partisiz sayım,
> bildirim aç/çöz). **Sırada T5 Yerleştirme + stok (★ K10 Legacy kesimi olmadan canlıda KULLANILMAZ).**
> Alan: 🛠 **Admin panel** (pano #2) + Inventory/Finance/Catalog çekirdek dokunuşları.
> İlgili: `docs/01-gereksinim-ve-kapsam.md` §11 (Tedarik ve Cari), `docs/03-veritabani-tasarimi.md`
> (fin_supplier_* tabloları — kodda var, akış yok), `docs/cari-cati-gecis-plani.md` (B0-B4: bakiye yalnız
> PostAccountTransaction), `docs/satis-kanali-ortak-kurgu.md` (F2 listeleme durumu — "satışa girdi"nin ölçümü),
> `docs/stok-karti-ve-urun-yonetimi.md`. Mevcut durum tespiti (2026-08-23 keşfi) §1'de.
> Hedef: her faz bir iş emri; **bir faz bitmeden diğerine geçilmez.**

---

## 0. Amaç ve ilkeler (kullanıcı kurgusu, 2026-08-23)

Bizim için **asıl süreç, teslim alınan ürünlerin ayrıştırılıp etiketlenmesiyle başlar.** Öncesi (satın alma,
teslim alma) takip edilir ama **ürünü en kısa sürede satışa sokmaya asla engel olmaz.**

Gerçeklik kabulleri (tasarımın temeli):
- **Satın alma listesi ayrıntılıdır** (model, renk, beden, fiyat, adet — yani varyant düzeyi); **teslim evrakı ve
  fatura ise kabadır ya da hiç bilgi taşımaz** ("t-shirt, 1000 adet, 15 TL"). Model/renk/beden/adet yoktur.
- Birden çok satın alma, kargo kolaylığıyla **tek parti halinde** teslim edilebilir.
- Satıcılar **istenenden fazla göndermeyi alışkanlık edinmiştir**; fazlalık iade edilmez, fiyat indirimi teklif edilir.
- Bu yüzden **satın alınan ↔ teslim alınan iki belgeyi karşılaştırarak kesin sonuç ÇIKARILAMAZ.** Gerçek miktar
  ancak **ayrıştırma + etiketleme sonrası sayımla** ortaya çıkar.
- Deneyimli personel **dönemlik** işlemlere bakarak anlamlı sonuç çıkarır → mutabakat kesin değil,
  **dönemsel/istatistikseldir**.
- En önemli ölçütler: **teslim alınan ürün ne kadar sürede satışa girdi** ve **satışa girmeyen ürünler nerede takılı**.

Tasarım ilkeleri:
| # | İlke |
|---|---|
| İ1 | **Sayım = gerçek.** Stok girişinin tek kaynağı ayrıştırma sayımıdır; SA/TA belgeleri bilgi amaçlıdır. |
| İ2 | **Hiçbir ön adım zorunlu değil.** Ayrıştırma, satın alma kaydı ya da teslim kaydı OLMADAN da başlayabilir (sonradan bağlanabilir). |
| İ3 | **Gevşek bağ, kesin bağ değil.** Parti ↔ satın alma(lar) ilişkisi çoktan-çoğa ve bilgi amaçlıdır; kalem düzeyinde eşleşme zorlanmaz. |
| İ4 | **Fark normaldir.** Fazla/eksik, durdurmaz; dönem raporunda görünür, fiyat revizyonu notuyla kapatılır. |
| İ5 | **Satışa giriş tanımı ticari gerçektir:** stok girmiş VE sitede yayında (F2 `published`) — yalnız stok girişi değil. |

---

## 1. Mevcut durum (2026-08-23 keşif özeti — ayrıntı keşif raporunda)

| Adım | Durum |
|---|---|
| Satın alma (PO) | **YOK.** Tedarikçi faturası: şema + POST + salt-okunur liste (kalem detayı ekranı yok). Fatura cariye İŞLEMİYOR (paralel `SupplierTransaction` defteri — B0-B4 ile çelişki). |
| Teslim alma | **Şema var, akış ölü:** `fin_supplier_deliveries` + kalemlerinde Expected/Received/Rejected alanları hazır; tek uç POST, ekran yok, `ReceivedQuantity` hiç yazılmıyor, stok etkisi yok. |
| Kontrol | **YOK** (alanlar hazır, mantık sıfır). Gereksinim dokümanı da gevşek bağ öngörmüş (§11.4). |
| Ayrıştırma/etiket | EAN-13 üretici ✓ (atomik sayaç), birim (bin) barkodu ✓; **ürün/raf etiketi yazdırma YOK**; Fulfillment yalnız çıkış yönlü. |
| Yerleştirme | Depo→Kısım→Birim + bin bazlı stok + `StockOps` birim seçimi ✓; **yerleştirme akışı/ekranı YOK**; `AdjustStock`'ta BinId yok; **depo transferi stok taşımıyor** (yalnız durum çevirir). |
| Eski köprü | Yalnız mutlak stok adedi ezer (10 dk); belge taşımaz. ★ Yeni giriş akışı canlıya alınırken eski sistemle çift-yazım çakışması yönetilmeli (§8 risk). |

---

## 2. Kavramlar ve veri modeli

Yeni modül: **Procurement (Tedarik)** — `procurement` şeması (K1). SA/Parti/Ayrıştırma burada; stok etkisi
Inventory'de (mevcut kalıp: domain event → Inventory handler). Finance'teki `fin_supplier_deliveries` şeması
**terk edilir** (boş tablolar; okuma geriye uyumluluğu gerekmiyor — K2).

```
SatınAlma (PurchaseOrder)  ←gevşek, çoktan-çoğa→  MalKabulPartisi (ReceiptBatch)
  kalem: varyant?*, model/renk/beden                 kaba kalemler (serbest metin+adet+fiyat, varyantsız olabilir)
  metni, fiyat, adet                                 koli sayısı, irsaliye no?, fatura bağı?
                                                        │
                                                        ▼
                                        AyrıştırmaKaydı (SortingEntry)  ← ASIL SÜREÇ
                                        parti?, varyant (eşleştirilmiş/yeni), sayılan adet,
                                        etiket basıldı, ayrıştıran, tarih
                                                        │  (YerleştirEvent)
                                                        ▼
                                        Stok girişi: bin seçimi → inv_stocks + StockMovement
                                        (type=purchase, Ref=sorting_entry, BinId) → satışa giriş izleme
```

### 2.1 `procurement.purchase_orders` + `_items` (SA — hafif)
| Alan | Not |
|---|---|
| PO: `Code` (SA-YYYYAAGG-0001), `SupplierId` (cari), `OrderDate`, `ExpectedDate?`, `Status`, `Notes`, `TotalAmount` (hesaplanan) | Status: `draft → ordered → receiving → closed | cancelled`. **closed elle** verilir (kesin eşleşme yok — İ3/İ4); `receiving` bilgi amaçlı. |
| Item: `VariantId?`, `ProductGroupId?`, `ModelText?`, `ColorText?`, `SizeText?`, `Quantity`, `UnitPrice`, `Notes` | **Varyant bağlamak zorunlu değil** — katalogda henüz olmayan ürün metinle yazılır (model/renk/beden serbest metin). Sonradan varyant bağlanabilir. |
| Giriş yolları | Panel formu (satır satır) + **panoya yapıştır** (Excel'den kopyala → model/renk/beden/adet/fiyat sütun eşleme — kanal kapsamındaki sütun-eşleme kalıbının küçüğü). |

### 2.2 `procurement.receipt_batches` + `_items` + `_purchase_orders` (Mal Kabul Partisi)
| Alan | Not |
|---|---|
| Batch: `Code` (MK-YYYYAAGG-0001), `SupplierId`, `ReceivedAt`, `WarehouseId`, `PackageCount?`, `DeliveryNoteNumber?`, `SupplierInvoiceId?`, `Status`, `ReceivedBy` | Status: `received → sorting → completed`. **"received" anında hiçbir kalem bilgisi zorunlu değil** — koli geldi, kayıt açıldı, ayrıştırma başlayabilir (İ2). completed = personel "bu partide ayrıştırılacak bir şey kalmadı" der. |
| Item (opsiyonel, kaba): `DescriptionText`, `Quantity?`, `UnitPrice?` | Teslim evrakındaki kaba satırlar ("t-shirt 1000 ad 15 TL") — mutabakat raporuna girdi; ayrıştırmayı hiçbir şekilde kısıtlamaz. |
| `_purchase_orders`: (BatchId, PurchaseOrderId) | Çoktan-çoğa gevşek bağ (İ3) — "bu partide şu SA'lar var (sanıyoruz)". |

### 2.3 `procurement.sorting_entries` (Ayrıştırma kaydı — sistemin kalbi)
| Alan | Not |
|---|---|
| `BatchId?` | Partisiz ayrıştırma da mümkün (İ2; sonradan bağlanabilir). |
| `VariantId` | Zorunlu — ayrıştırma "bu ürün, bu varyant, bu kadar adet" demektir. Ekran mevcut varyantı barkod/arama ile bulur. **K9: ürün kartı ayrıştırma ÖNCESİ açılmış olur** (katalog sorumlusunun işi; EAN-13 üretimi kart açma sürecinin parçası) — ayrıştırma personeli kart açmaz. Varyant bulunamazsa kayıt yapılamaz; ekran tek tıkla **"kart eksik" bildirimi** düşer (parti üzerinde bekleyen bildirim listesi + raporda görünür), kart açılınca sayım yapılır. |
| `Quantity` | Sayılan adet (birden çok kayıt aynı varyanta olabilir; toplamı sayımdır). |
| `UnitCost?` | Alış maliyeti (SA'dan önerilir, elle ezilebilir) — maliyet/fiyat revizyonu raporuna girdi. |
| `LabelPrinted`, `LabelCount` | Etiket basımı işareti. |
| `PutawayStatus`: `pending → placed` | Yerleştirme durumu; `placed` olunca stok girişi yapılmıştır. |
| `PlacedBinId?`, `PlacedAt?`, `StockMovementId?` | İzlenebilirlik: sayım → hangi birime, hangi hareketle girdi. |
| `CreatedBy/At` | "Teslim→ayrıştırma süresi" KPI'sının damgası. |

### 2.4 Stok girişi (Inventory dokunuşları)
- `AdjustStockCommand`'a **`BinId?`** + **`ReferenceType/ReferenceId`** eklenir (koddaki "ileride" notu kapanır);
  `StockMovement`'a **`BinId`** kolonları (From/To) eklenir — izlenebilirlik kırığı kapanır (mevcut hareketlere
  dokunulmaz, additive).
- Yerleştirme komutu: `PlaceSortingEntryCommand(entryId, binId, quantity?)` → stok artar (movement:
  `purchase`, Ref=`sorting_entry`), entry `placed` olur. Kısmi yerleştirme = entry bölünür (kalan `pending`).
- **Depo transferinin stok taşımaması bu planın kapsamında DEĞİL** — ayrı iş emri olarak not edildi (§8).

### 2.5 Etiket basımı — kullanıcı tasarımlı şablonlar (K7)
**Sabit format YOK; kullanıcı etiketi kendisi tasarlar.**
- `core.core_label_templates`: `Code`, `Name`, `TargetType` (`product` | `bin`), `WidthMm`, `HeightMm`,
  `ElementsJson`, `IsDefault`, `IsActive`. Eleman: tip (`barcode` | `field` | `text` | `price`), veri kaynağı
  (ürün adı, renk, beden, SKU, barkod, satış fiyatı, serbest metin), konum/boyut (mm), yazı boyutu (pt),
  hizalama, kalınlık.
- **Etiket Şablonları ekranı** (`/settings/label-templates`): şablon listesi + görsel düzenleyici — kağıt ölçüsü
  girilir, elemanlar canlı önizlemede sürükle/da sayısal konumla yerleştirilir; örnek ürünle önizleme; varsayılan
  şablon işaretlenir. Hedef tip `bin` olan şablonlar birim/raf etiketi için aynı editörü kullanır.
- Yazdırma yüzeyi: `GET /yazdir/etiket?templateId=..&variantId=..&count=N` (ve `binId=..`) — JSON şablonu
  mm→px çevirip basar; barkod çizimi PaketEtiket kalıbındaki üreteçle ortak. Ayrıştırma ekranı şablon seçtirir
  (son seçim hatırlanır) ve tek tıkla N adet basar.

### 2.6 Satışa giriş izleme (İ5 — F2 listeleme durumu ile bağ)
- "Satışa girdi" = yerleştirildi (stok>0) **ve** site kanalında F2 durumu `published`.
- `sorting_entries` üzerinden görünüm: her kayıt için `OnSaleAt?` (ilk `published` görüldüğü an — günlük bir
  worker turu damgalar; geriye dönük kesinlik iddiası yok, gün hassasiyeti yeter).
- **"Satışa girmeyenler" listesi** = yerleştirilmiş ama `published` olmayanlar + sebep (görsel yok, fiyat 0,
  kanal kararı…) — sebepler hazır: `ChannelListingStatusService` (satis-kanali F2) aynen kullanılır.

---

## 3. Ekranlar (admin; sol menü "Tedarik" bölümü)

| Ekran | İçerik |
|---|---|
| **Satın Almalar** (`/procurement/purchase-orders`) | Liste (kod/tedarikçi/tarih/tutar/durum) + detay: kalemler (metin veya varyant), panoya yapıştırarak kalem ekleme, durum aksiyonları. Salt kayıt — hiçbir akışı kilitlemez. |
| **Mal Kabul** (`/procurement/receipts`) | Liste + detay: parti bilgisi, gevşek SA bağları (+ ekle/çıkar), kaba evrak kalemleri, fatura bağı; "Ayrıştırmaya Başla" → ayrıştırma ekranına partiyle gider. Partinin ayrıştırma özeti (kaç kayıt, kaç adet, kaç yerleşti). |
| **Ayrıştırma** (`/procurement/sorting`) | Operasyon ekranı (barkod okuyucu dostu): parti seç (ya da partisiz) → varyant bul (barkod/arama; **yalnız mevcut kartlar — K9**; bulunamazsa tek tık "kart eksik" bildirimi) → adet gir → **Etiket Bas (N)** (şablon seçimi, son seçim hatırlanır) → kaydet. Alt liste: bu partinin kayıtları + bekleyen kart-eksik bildirimleri. Yerleştirme bekleyenler sekmesi: kayıt seç → birim (bin) seç (barkod okutarak) → **Yerleştir** (stok girer). |
| **Tedarik Raporu** (`/procurement/report`) | Dönem + tedarikçi filtreli: SA adedi/tutarı ↔ ayrıştırılan adet/maliyet ↔ fatura tutarı; fark % (İ4 — "kesin değil" ibaresi ekranda); **fazla gönderim** tablosu; KPI kartları: teslim→ayrıştırma ort. süre, ayrıştırma→satışa giriş ort. süre, N günden uzun bekleyen yerleştirilmemiş/satışa girmemiş adetler. **Satışa girmeyenler** listesi (sebep rozetleriyle, satis-kanali F3 çekmecesine link). |

Ek ekran: **Etiket Şablonları** (`/settings/label-templates`, §2.5). Rehber: 5 yeni sayfa (`07-stok` altına ya da yeni `11-tedarik` bölümü — K8).

---

## 4. Kararlar

| # | Karar | Durum |
|---|---|---|
| K1 | Yeni **Procurement (Tedarik)** modülü (`procurement` şeması); stok etkisi Inventory'de (event kalıbı) | **KAPALI** (2026-08-24) |
| K2 | Ölü `fin_supplier_deliveries/_items` şeması terk edilir (boş; yeni model Procurement'ta). `fin_supplier_invoices` KALIR ve partiye bağlanır | **KAPALI** (2026-08-24) |
| K3 | Sayım tek gerçek (İ1): stok girişi YALNIZ yerleştirme komutundan; serbest `stocks/adjust` kalır ama tedarik girişleri için kullanımdan düşürülür (ekranda yönlendirme notu) | **KAPALI** (2026-08-24) |
| K4 | SA kalem girişi: form + panoya yapıştır (Excel). Ayrı dosya yükleme YOK (ilk sürüm) | **KAPALI** (2026-08-24) |
| K5 | Mutabakat dönemseldir; parti kapanışı elle; fark bloke etmez (İ4). Fazlalık için "fiyat revizyonu" NOTU tutulur (fatura düzeltme otomasyonu YOK — Finance ayrı iş) | **KAPALI** (2026-08-24) |
| K6 | Satışa giriş = stok + `published` (İ5); `OnSaleAt` gün hassasiyetli worker damgası | **KAPALI** (2026-08-24) |
| K7 | **Sabit etiket formatı YOK — kullanıcı tasarımlı şablonlar** (§2.5: şablon entity + görsel düzenleyici + şablonlu yazdırma; ürün ve birim/raf etiketi aynı altyapı) | **KAPALI** (2026-08-24) |
| K8 | Faturanın cariye işlenmesi (PostAccountTransaction, B0-B4 uyumu; paralel SupplierTransaction defterinin emekliliği) **AYRI İŞTİR** — bu planın kapsamı dışında, Finans alanında ayrıca planlanır | **KAPALI** (2026-08-24) |
| K9 | **Ürün kartı ayrıştırma ÖNCESİ açılmış olur; ayrıştırma personelinin kart açmayla ilgisi yoktur.** Ekran yalnız mevcut varyantı eşler; bulunamayan ürün için "kart eksik" bildirimi (katalog sorumlusuna kuyruk) | **KAPALI** (2026-08-24) |
| K10 | **Ayrıştırma devreye alınınca eski sistemden toplu stok güncellemeleri KAPATILACAK** (tam kesim; `Legacy:Sync` stok ayağı config ile kapatılır). Yerleştirme fazı canlıya alınırken go-live kontrol listesine yazılır | **KAPALI** (2026-08-24) |

---

## 5. Uygulama fazları

| Faz | Kapsam | Kabul kriteri |
|---|---|---|
| **T1 Modül + SA** | Procurement modülü iskeleti (Program.cs kalıbı, migration), PurchaseOrder CRUD + panoya yapıştır, Satın Almalar ekranı, rehber | SA oluştur/kalem yapıştır/duruma al; hiçbir başka akışı etkilemez |
| **T2 Mal Kabul** | ReceiptBatch CRUD + gevşek SA bağı + kaba kalemler + fatura bağı, Mal Kabul ekranı | Parti aç (kalemsiz) → durum sorting; birden çok SA bağla/çöz; negatif: kapalı partiye kalem eklenemez |
| **T3 Etiket Şablonları (K7)** | `core_label_templates` + Etiket Şablonları ekranı (görsel düzenleyici, önizleme, varsayılan) + şablonlu yazdırma yüzeyi (`/yazdir/etiket`, ürün + birim) | Kullanıcı 40×30 benzeri bir şablonu sıfırdan tasarlar, örnek ürünle önizler, N adet basar; birim etiketi de aynı editörden; negatif: kağıt dışına taşan eleman uyarısı |
| **T4 Ayrıştırma** | SortingEntry + ayrıştırma ekranı (yalnız MEVCUT varyant eşleme — K9; "kart eksik" bildirimi kuyruğu; şablon seçimiyle etiket basımı) | Partiden 3 farklı varyant ayrıştır → etiket bas → kayıtlar partide; partisiz ayrıştırma çalışır; negatif: adet ≤ 0 reddedilir, katalogda olmayan ürün kayıt edilemez → kart-eksik bildirimi düşer |
| **T5 Yerleştirme + stok** | `StockMovement.BinId`, `AdjustStock` BinId/Ref genişletmesi, `PlaceSortingEntry` (event → Inventory), ayrıştırma ekranı yerleştirme sekmesi; **go-live kontrolü: K10 Legacy toplu stok güncellemesi kapatma** | Yerleştir → `inv_stocks` bin bazlı artar, movement Ref=sorting_entry; kısmi yerleştirme kalanı pending bırakır; satışa açık kısımdaysa ürün sitede stoklu görünür (K17) |
| **T6 Rapor + KPI** | OnSaleAt worker damgası, Tedarik Raporu ekranı (dönem mutabakatı, KPI kartları, satışa girmeyenler + F2 sebepleri, kart-eksik bildirimleri) | Dönem raporu SA/sayım/fatura üçlüsünü yan yana koyar; satışa girmeyenler sebep rozetli listelenir |

Sıra: T1→T6 doğrusal; T4 asıl değer — T1/T2 bilinçli ince, T3 (etiket) T4'ün ön şartı (sabit format olmadığından
basım ancak şablonla mümkün). Fatura→cari (eski T6) K8 gereği **plan dışı** — Finans alanında ayrı iş.

## 6. Yetkiler
`procurement.manage` (SA/parti), `procurement.sort` (ayrıştırma+yerleştirme — depo personeli), rapor: manage; etiket şablonları: `settings.manage` (yazdırma herkese açık).

## 7. KPI tanımları (T5)
- **Teslim→ayrıştırma**: batch.ReceivedAt → entry.CreatedAt (partili kayıtlar).
- **Ayrıştırma→satışa giriş**: entry.CreatedAt → OnSaleAt.
- **Bekleyenler**: PutawayStatus=pending adetleri (yaş kovaları: 0-2, 3-7, 7+ gün); yerleşti ama `published` değil (sebep dağılımıyla).
- **Fazla gönderim**: dönem+tedarikçi: ayrıştırılan − SA edilen (pozitifler), tutar etkisi UnitCost ile.
- **Kart-eksik bildirimleri**: açık bildirim sayısı + yaşı (katalog sorumlusu kuyruğu; K9).

## 8. Riskler / notlar
- **Legacy stok ezmesi (K10 — KAPALI):** karar, ayrıştırma devreye alınınca eski sistemden toplu stok
  güncellemelerini KAPATMAK (tam kesim; `Legacy:Sync` stok ayağı config'te kapatılır). T5 go-live kontrol
  listesi maddesi: kapatma yapılmadan yerleştirme canlıda kullanılmaz; kapatma sonrası stok doğruluğunun tek
  sahibi yeni akıştır (eski sistemde süren manuel düzeltmeler artık YANSIMAZ — personele duyurulmalı).
- Transferlerin stok taşımaması (mevcut bulgu) bu plan dışı — ayrı iş emri.
- Ayrıştırma ekranı operasyon hızına duyarlı: barkod okuyucu ile klavyesiz akış hedeflenir; ilk sürümde masaüstü panel, el terminali ayrı iş.
- Fatura kalem detayı ekranı (mevcut eksik) T2'de partiden bağlanınca asgari düzeyde gösterilir; tam Finance ekranı kapsam dışı.
