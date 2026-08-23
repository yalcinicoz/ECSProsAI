# Ürün Tedarik İş Akışı — Satın Alma → Mal Kabul → Ayrıştırma/Etiketleme → Yerleştirme → Satışa Giriş

> Sürüm: **v1 — 2026-08-23** (inceleme için taslak; uygulama başlamadı)
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
| `VariantId` | Zorunlu — ayrıştırma "bu ürün, bu varyant, bu kadar adet" demektir. Ekran mevcut varyantı barkod/arama ile bulur; yoksa **yerinde yeni varyant/ürün kartı** açtırır (mevcut ürün oluşturma komutları + EAN-13 üretici). |
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

### 2.5 Etiket basımı
- Yeni yazdırma yüzeyi: `GET /yazdir/urun-etiket?variantId=..&count=N` — termal şablon (PaketEtiket.cshtml
  kalıbı): barkod (EAN-13), ürün adı, renk/beden, fiyat?. Ayrıştırma ekranından tek tıkla N adet.
- Raf/birim etiketi: `GET /yazdir/birim-etiket?binId=..` (birim barkodu + kısım/depo adı). (K7 format açık.)

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
| **Ayrıştırma** (`/procurement/sorting`) | Operasyon ekranı (barkod okuyucu dostu): parti seç (ya da partisiz) → varyant bul (barkod/arama) ya da **yeni kart/varyant aç** → adet gir → **Etiket Bas (N)** → kaydet. Alt liste: bu partinin kayıtları. Yerleştirme bekleyenler sekmesi: kayıt seç → birim (bin) seç (barkod okutarak) → **Yerleştir** (stok girer). |
| **Tedarik Raporu** (`/procurement/report`) | Dönem + tedarikçi filtreli: SA adedi/tutarı ↔ ayrıştırılan adet/maliyet ↔ fatura tutarı; fark % (İ4 — "kesin değil" ibaresi ekranda); **fazla gönderim** tablosu; KPI kartları: teslim→ayrıştırma ort. süre, ayrıştırma→satışa giriş ort. süre, N günden uzun bekleyen yerleştirilmemiş/satışa girmemiş adetler. **Satışa girmeyenler** listesi (sebep rozetleriyle, satis-kanali F3 çekmecesine link). |

Rehber: 4 yeni sayfa (`07-stok` altına ya da yeni `11-tedarik` bölümü — K8).

---

## 4. Kararlar

| # | Karar | Durum |
|---|---|---|
| K1 | Yeni **Procurement (Tedarik)** modülü (`procurement` şeması); stok etkisi Inventory'de (event kalıbı) | ÖNERİ — onay bekliyor |
| K2 | Ölü `fin_supplier_deliveries/_items` şeması terk edilir (boş; yeni model Procurement'ta). `fin_supplier_invoices` KALIR ve partiye bağlanır | ÖNERİ |
| K3 | Sayım tek gerçek (İ1): stok girişi YALNIZ yerleştirme komutundan; serbest `stocks/adjust` kalır ama tedarik girişleri için kullanımdan düşürülür (ekranda yönlendirme notu) | ÖNERİ |
| K4 | SA kalem girişi: form + panoya yapıştır (Excel). Ayrı dosya yükleme YOK (ilk sürüm) | ÖNERİ |
| K5 | Mutabakat dönemseldir; parti kapanışı elle; fark bloke etmez (İ4). Fazlalık için "fiyat revizyonu" NOTU tutulur (fatura düzeltme otomasyonu YOK — Finance ayrı iş) | ÖNERİ |
| K6 | Satışa giriş = stok + `published` (İ5); `OnSaleAt` gün hassasiyetli worker damgası | ÖNERİ |
| K7 | Etiket format(lar)ı: termal yazıcı ölçüsü, etikette fiyat olsun mu, kaç şablon? | **AÇIK — kullanıcı** |
| K8 | Faturanın cariye işlenmesi (PostAccountTransaction, B0-B4 uyumu; paralel SupplierTransaction defterinin emekliliği) bu planın PARÇASI MI, ayrı Finans işi mi? | **AÇIK — kullanıcı** (önerim: ayrı iş, F5 olarak buraya iliştirilir) |
| K9 | Ayrıştırmada yeni ürün kartı açma yetkisi: ayrıştırma personeli doğrudan mı, taslak açıp katalog onayına mı düşer? | **AÇIK — kullanıcı** (önerim: doğrudan — hız ilkesi; grup şablonu zorunlu alanları güvence verir) |
| K10 | Eski sistem stok senkronu (10 dk mutlak ezme) yeni giriş akışıyla çakışır: yeni akış canlıya alınınca ilgili depo/kısımlar için Legacy stok ezmesi nasıl sınırlanır? | **AÇIK — teknik** (önerim: senkron zaten yalnız "İnternete Açık" tipe yazıyor; go-live'da kesim planı §8) |

---

## 5. Uygulama fazları

| Faz | Kapsam | Kabul kriteri |
|---|---|---|
| **T1 Modül + SA** | Procurement modülü iskeleti (Program.cs kalıbı, migration), PurchaseOrder CRUD + panoya yapıştır, Satın Almalar ekranı, rehber | SA oluştur/kalem yapıştır/duruma al; hiçbir başka akışı etkilemez |
| **T2 Mal Kabul** | ReceiptBatch CRUD + gevşek SA bağı + kaba kalemler + fatura bağı, Mal Kabul ekranı | Parti aç (kalemsiz) → durum sorting; birden çok SA bağla/çöz; negatif: kapalı partiye kalem eklenemez |
| **T3 Ayrıştırma + Etiket** | SortingEntry + ayrıştırma ekranı (varyant bul/yeni kart aç + EAN-13), ürün etiket yazdırma şablonu | Partiden 3 farklı varyant ayrıştır → etiket bas → kayıtlar partide toplanır; partisiz ayrıştırma da çalışır; negatif: adet ≤ 0 reddedilir |
| **T4 Yerleştirme + stok** | `StockMovement.BinId`, `AdjustStock` BinId/Ref genişletmesi, `PlaceSortingEntry` (event → Inventory), ayrıştırma ekranı yerleştirme sekmesi | Yerleştir → `inv_stocks` bin bazlı artar, movement Ref=sorting_entry; kısmi yerleştirme kalanı pending bırakır; satışa açık kısımdaysa ürün sitede stoklu görünür (K17) |
| **T5 Rapor + KPI** | OnSaleAt worker damgası, Tedarik Raporu ekranı (dönem mutabakatı, KPI kartları, satışa girmeyenler + F2 sebepleri) | Dönem raporu SA/sayım/fatura üçlüsünü yan yana koyar; satışa girmeyenler sebep rozetli listelenir |
| **T6 (K8'e bağlı) Finans bağı** | Fatura → cari işleme (PostAccountTransaction), fiyat revizyonu notları | — |

Sıra: T1→T5 doğrusal; T3 asıl değer — T1/T2 bilinçli ince tutuldu ki T3'e hızlı gelinsin.

## 6. Yetkiler
`procurement.manage` (SA/parti), `procurement.sort` (ayrıştırma+yerleştirme — depo personeli), rapor: manage.

## 7. KPI tanımları (T5)
- **Teslim→ayrıştırma**: batch.ReceivedAt → entry.CreatedAt (partili kayıtlar).
- **Ayrıştırma→satışa giriş**: entry.CreatedAt → OnSaleAt.
- **Bekleyenler**: PutawayStatus=pending adetleri (yaş kovaları: 0-2, 3-7, 7+ gün); yerleşti ama `published` değil (sebep dağılımıyla).
- **Fazla gönderim**: dönem+tedarikçi: ayrıştırılan − SA edilen (pozitifler), tutar etkisi UnitCost ile.

## 8. Riskler / notlar
- **Legacy stok ezmesi (K10):** `SyncStockAsync` 10 dk'da bir mutlak yazar. Yeni girişler eski sistemde yoksa
  EZİLİR. Go-live kesim planı: yeni akış hangi depo/kısımda başlıyorsa o kısımlar senkron kapsamından çıkarılır
  (senkrona kısım-hariç listesi eklenir) ya da tam kesim tarihi belirlenir. **T4 canlıya bu karar olmadan alınmaz.**
- Transferlerin stok taşımaması (mevcut bulgu) bu plan dışı — ayrı iş emri.
- Ayrıştırma ekranı operasyon hızına duyarlı: barkod okuyucu ile klavyesiz akış hedeflenir; ilk sürümde masaüstü panel, el terminali ayrı iş.
- Fatura kalem detayı ekranı (mevcut eksik) T2'de partiden bağlanınca asgari düzeyde gösterilir; tam Finance ekranı kapsam dışı.
