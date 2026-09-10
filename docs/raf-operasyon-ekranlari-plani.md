# Raf / Göz Operasyon Ekranları — Kurgu v1 (2026-09-10)

Yol haritası **FAZ 15.3**. Eski panelin "Depo" grubundaki 10 sayfanın (rafa yerleştir, toplu yerleştir, iadeden rafa,
raftan rafa transfer, raf sayım, raf ürün listesi, mağaza depodan reyona, reyondan çıkar ×3) yeni panelde tek bir
**tablet okutma ekran ailesi** olarak kurgusu. K16 gereği önce bu kurgu onaylanır, sonra kod yazılır.

---

## 0. Önce bilinmesi gereken kısıt — stok otoritesi

Canlıda `Legacy:Sync` fiyat/stok dilimi (`pricestock`, ~10 dk) **göz bazlı** stok adetlerini eski sistemin
`opproductlocations` tablosundan yeniden yazıyor (`inv_stocks.Quantity = eski adet`, eskide olmayan satır 0'lanır;
yalnız "İnternete Açık" depo tipi). Sonuç: yeni panelde yapılan her raf hareketi (yerleştirme, transfer, sayım
düzeltmesi) **en geç 10 dakika içinde eski sistemin değeriyle ezilir**. Eski panelde yapılan hareket ise yeni tarafa
otomatik gelir.

Bu yüzden raf ekranları **iki kipte** çalışmak zorunda:

| Kip | Ne zaman | Davranış |
|-----|----------|----------|
| **Aynalama** (bugün) | `Legacy:Sync:Stock=true` | Ekranlar SALT OKUNUR: raf içeriği, raf ürün listesi, sayım *raporu* (fark listesi) çalışır; yazan işlemler ("Yerleştir", "Taşı", "Sayımı uygula") üstte sarı şeritle kapalıdır: "Stok otoritesi eski sistemde — raf hareketleri eski panelden yapılır." |
| **Otorite** (go-live sonrası) | `Legacy:Sync:Stock=false` (ürün tedarik planı: go-live'da Legacy stok senkronu kapanır) | Tüm işlemler açık; stok hareketi yeni sistemde üretilir. |

Kipi tek yerden okuyan küçük bir uç (`GET /api/inventory/stock-authority` → `legacy | panel`) ekranların başında sorulur.
Bu kısıt olmadan ekranları "bitti" saymak K16 ihlali olur (ekran var ama işlev ezilir).

**Karar K1:** Ekranlar şimdi yapılıp aynalama kipinde mi açılsın (personel alışır, sayım raporu hemen kullanılır),
yoksa tümü go-live'a mı bırakılsın? **Öneri:** şimdi yap, aynalama kipiyle aç; yazma işlemleri go-live'da tek bayrakla açılır.

---

## 1. Mevcut altyapı (2026-09-10 kod+veri)

| Var | Durum |
|-----|-------|
| Depo → Kısım → Göz yapısı (`inv_warehouse_sections`, `inv_warehouse_bins`; göz barkodu) | MERKEZ: 13 kısım / 13.546 göz / 149.527 göz-bazlı stok satırı; MAGAZA ve AYAKKABI: 1 kısım / 1 göz (reyon = tek göz) |
| Depo Detay'da kısım/göz CRUD + Etiket Basımı'nda raf barkodu yazdırma | var |
| `ReceiveToBin` (mal kabul → göz, `purchase` hareketi) | var; yalnız satın alma kaynağı |
| `StockOps.ReceiveAsync/ConsumeAsync` (göz seçimi: satışa açık kısım önce; iade → kapalı kısım) | var |
| `AdjustStock` (depo bazlı düzeltme) | var, **göz bilmiyor** |
| Transferler (`inv_transfer_requests`) | depo→depo; kalemde eski `LocationId`, göz yok |
| Ürün barkodu → varyant (`GET /api/catalog/variants/by-barcode/{barcode}`) | var |
| Stoklar listesi grid'i (`binId`/`sectionId` filtreleri) | var |
| Okutma ekranı kalıbı (OP3 `SortingScanPage`: tek input, Enter, sonuç kartı, sayaç, hata şeridi) | var |

**Yok:** göz→göz taşıma, göze serbest yerleştirme, iadeden rafa (kapalı kısım gözü → açık göz), raf sayımı, göz
bazlı düzeltme, "göz içeriği" sorgusu (barkodla).

---

## 2. Ekran ailesi — `/inventory/shelf/*` (tablet, tek elle; permission `inventory.manage`, görüntüleme `inventory.view`)

Ortak kalıp: sayfa üstünde **depo seçici** (varsayılan: kullanıcının son seçtiği; localStorage), altında büyük tek
**okutma kutusu** (odak hep burada; göz barkodu ile ürün barkodu aynı kutudan okunur, ön ekten ayırt edilir — göz
barkodları `inv_warehouse_bins.Barcode`, ürün barkodları EAN), sağda **sonuç kartı**, altta **oturum listesi**
(bu ekranda yapılanlar, geri al). Sesli/titreşimli geri bildirim OP3'teki gibi.

### R1 — Raf İçeriği (eski "Raf Ürün Listesi")
Göz barkodu okut → göz adı/kısım + içindeki ürünler (görsel, kod, renk/beden, adet, rezerve). Ürün barkodu okut →
o ürünün bu depoda hangi gözlerde olduğu. Salt okunur; aynalama kipinde de tam çalışır.

### R2 — Rafa Yerleştir (eski "Rafa Ürün Yerleştir" + "Toplu")
1. Göz okut (hedef). 2. Ürün okut → +1 (toplu kipte adet kutusu). Kaynak seçimi:
- **Serbest giriş** (varsayılan): göze `adjustment(+)` hareketi — sayım dışı stok girişi; not zorunlu.
- **Mal kabul kolisi**: koli barkodu okutulursa kalemler listelenir, ürün okuttukça koliden düşer, göze girer
  (`ReceiveToBin` mevcut). Eski panelin ana kullanımı buydu.
- **Rafsız satır**: depoda `BinId=null` eski-tarz stok varsa (bugün 0 satır) önce oradan göze taşınır.

### R3 — Raftan Rafa Transfer (eski "Raftan Rafa Ürün Transfer" + "Raf Ürün Taşı")
1. Kaynak göz okut → içerik. 2. Hedef göz okut. 3. "Tümünü taşı" ya da ürün okutarak tek tek/adetli taşı.
Aynı depo içi: `transfer` hareketi (FromBinId→ToBinId), rezervasyon satırla birlikte taşınır (K2).
Depolar arası: mevcut Transfer talebi kalıbına düşer (bu ekran değil).

### R4 — İadeden Rafa (eski "İadeden Rafa Ürün Yerleştir")
Kaynak: satışa KAPALI kısımdaki gözler (iade teslim alınınca ürün otomatik buraya girer — `ReturnReceivedEvent`).
Ürün okut → hangi kapalı gözde olduğu görünür; hedef (açık) göz okut → taşınır (`return_to_shelf` hareketi).
Muayene sonucu "defolu" ise hedef yerine **Defo** kısmı seçilir (aynı akış). İade kaydıyla ilişki: `ReferenceType=return`.

### R5 — Raf Sayım (eski "Raf Sayım" + "Raf Sayım Raporu")
1. Göz okut → **beklenen** liste (sistem adetleri) gizli, sayım oturumu açılır. 2. Ürün okuttukça **sayılan** artar.
3. **Bitir** → fark tablosu (beklenen / sayılan / fark; fazla ürün "beklenmeyen" satır). 4. **Uygula** (yetki:
`inventory.count.apply`, yeni) → fark kadar `adjustment` hareketi, sayım kaydı (`inv_bin_counts`: kim, ne zaman, fark
özeti) ve rapor. Aynalama kipinde 3'e kadar çalışır (rapor üretir, uygulamaz — eski panelde düzeltilir).
Raf Sayım Raporu: sayım kayıtları listesi (DataGrid) + Excel.

### R6 — Mağaza Reyon (eski "Depodan Reyona", "Reyondan Ürün Çıkar" MR/AR/GR)
Mağaza depoları tek gözlü olduğundan bu ekran = **depo→depo tek ürün transferi** (MAGAZA/AYAKKABI ↔ MERKEZ).
Depo seçici ile "Depodan Reyona" (kaynak MERKEZ gözü okut → hedef mağaza) ve "Reyondan Çıkar" (mağaza → MERKEZ
kabul gözü). Eski `code=MR/AR/GR` parametresi = mağaza deposu seçimi. Mevcut Transfer talebi altyapısı üstüne
"anında tamamlanan" tip (`TransferType=store_move`, otomatik `completed`).

---

## 3. Backend eklenecekler (Inventory modülü)

| Komut / sorgu | Not |
|---------------|-----|
| `GetBinContentsQuery(binBarcode | binId)` | göz + kalemler (varyant görünümü Catalog `IProductService.GetVariantDisplayAsync`) |
| `GetVariantBinsQuery(warehouseId, variantId)` | ürünün gözleri |
| `PlaceToBinCommand(binId, variantId, qty, source: free|receipt|unbinned, receiptId?, notes)` | `adjustment`/`purchase` hareketi; StockTx |
| `MoveBetweenBinsCommand(fromBinId, toBinId, items[] | all)` | aynı depo şartı; rezervasyon taşınır; `transfer` hareketi |
| `ReturnToShelfCommand(fromBinId, toBinId, variantId, qty, returnId?)` | kapalı→açık; `return_to_shelf` |
| `BinCount: Start / Scan / Finish / Apply` | `inv_bin_counts` + `inv_bin_count_lines`; Apply → adjustment hareketleri |
| `StoreMoveCommand(fromWarehouseId, toWarehouseId, variantId, qty)` | tek gözlü depolar; anında tamamlanan transfer |
| `GET /api/inventory/stock-authority` | `Legacy:Sync:Stock` bayrağından |
| Migration | `inv_bin_counts`, `inv_bin_count_lines` (yeni tablolar, eklemeli) |

Kurallar: her mutasyon `StockTx.RunAsync` kilidiyle; 0'lı satır bırakılmaz (K-14); hareket tipleri
`DurumEtiketleri`'ne benzer tek sözlük (`StokHareketTipi`); göz pasifse yazma reddedilir.

---

## 4. Kararlar (onay bekliyor)

| # | Soru | Öneri |
|---|------|-------|
| **K1** | Ekranlar şimdi aynalama kipinde açılsın mı, go-live'a mı kalsın? | Şimdi; yazma işlemleri bayrakla kapalı (§0). |
| **K2** | Rezervli (siparişe ayrılmış) adet göz değiştirebilir mi? | Evet, rezervasyon satırla taşınır (toplayıcı yeni gözü görür); depo değiştiremez. |
| **K3** | Sayım farkını kim uygular? | Ayrı yetki `inventory.count.apply` (depo sorumlusu); sayan personel yalnız "Bitir". |
| **K4** | Mağaza reyonu ayrı depo mu (bugünkü gibi) yoksa MERKEZ'in kısmı mı? | Bugünkü gibi ayrı depo; R6 depo→depo anlık transfer. |
| **K5** | Rafa Yerleştir'in varsayılan kaynağı? | Mal kabul kolisi okutulmuşsa koli, değilse serbest giriş (not zorunlu). |
| **K6** | Stüdyo akışı (15.4a) bu aileye girsin mi? | Hayır; ayrı kurgu (12 görünüm). |

---

## 5. Fazlar

| Faz | İçerik |
|-----|--------|
| R0 | Backend: sorgular + komutlar + migration + `stock-authority` ucu + testler |
| R1 | Ekran kabuğu (depo seçici, okutma kutusu, kip şeridi) + R1 Raf İçeriği + R2 Rafa Yerleştir |
| R2 | R3 Raftan Rafa + R4 İadeden Rafa |
| R3 | R5 Raf Sayım + sayım raporu grid'i |
| R4 | R6 Mağaza Reyon |
| R5 | Rehber sayfası, menü (Stok › Raf İşlemleri), kabul testi (olumsuz: pasif göz, başka depo gözü, aynalama kipinde yazma) |

Kapsam dışı: stüdyo akışı (15.4a), depolar arası çok kalemli transfer (mevcut Transferler), ERP stok kartı güncelleme.
