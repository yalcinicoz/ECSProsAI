# Sipariş Operasyonu Planı — Toplama / Ara Ayrıştırma / Paketleme

> Durum: **TASLAK — kullanıcı gözden geçirmesi bekleniyor** (2026-08-09)
> Alan: Admin panel + Fulfillment/Inventory/Order/Integration modülleri
> Kaynak: 2026-08-09 tasarım oturumu (kullanıcının süreç anlatımı + karar cevapları)
> Mevcut durum analizi bu dokümanın "Mevcut Altyapı" bölümünde.

---

## 0. Verilmiş Kararlar (tartışması kapandı)

| # | Karar |
|---|-------|
| K-1 | Mevcut Fulfillment modülü **genişletilecek**, v2 yazılmayacak. |
| K-2 | Lokasyon kırılımı **Depo + Raf** (ikili). Ara katmanlar (kat/kısım/koridor) raf ADINA kodlanır (örn. `KAT-1K3-0718`). Personel hiçbir şey bilmek zorunda değil — sistem söyler. |
| K-3 | İstemci: **telefon/tablet tarayıcısı + bluetooth barkod okuyucu** (HID). Özel el terminali yok. **Her masada kendi yazıcısı**; son okutma bitince fatura+etiket **otomatik** basılır (kullanıcı tetiklemez — tetiklemek süreyi 2×'e çıkarıyor). |
| K-4 | Ses: yalnız **numara seslendirilir** ("17"; "Raf 17" değil) + hata sesi + "Paketle" sesi. |
| K-5 | Çoklu toplama görevlerinde **iptal süreci durdurmaz** — iptal edilmiş sipariş paketleme aşamasında yakalanır, ürünleri depo iadesine ayrılır. Sipariş-bazlı (tek sipariş) toplamada her aşamada iptal uyarısı verilebilir. |
| K-6 | OBM (Ortak Birleştirme Masası) **minimal**: sorunlu sipariş/ürünler oraya transfer edilir, çözümü personel insiyatifiyle bulur (gerekirse eksik ürün siparişten çıkarılıp kargo çıkarılır). Genel süreci tıkamamak için var; akıllı akış kurgulanmayacak. |
| K-7 | Tek ürünlü hatta okutulan ürün **en eski ONAYLI siparişe** verilir. |
| K-8 | Personel rolleri: **Depo** ve **İdari** — depo personelinde alt sınıf yok, herkes her görevi alabilir. |
| K-9 | Kargo şirketi **sipariş anında kesinleşir** (checkout'ta `RequestedCargoIntegrationId`). Sorun çıkarsa **yönlendirme**: toplu veya tekil; API destekliyorsa eski taşıyıcıdan kayıt silinir, yenisine gönderilir. |
| K-10 | **Kargo API bildirimi varsayılanı: paket gerçekten oluştuğunda** (son kontrol hatasız + paket kapandı). Kanal politikasıyla "sipariş anında" (pazaryeri modu) seçilebilir. Etiket, kargo kod motoru (range/pattern) sayesinde API beklenmeden basılır; API çağrısı asenkron outbox+worker. |
| K-11 | **Fatura paket kapanırken kesilir**, paketle birlikte çıkar. Toptan (B2B) profili için "dönemsel faturalama" ileriki faz seçeneği. |
| K-12 | Ayrıştırma koşullarında eşitlik bozulamazsa **en eski sipariş** kazanır. "Düşük ihtimalli sipariş büyük koli numarasına" davranışının eşiği **profil parametresi**. |
| K-13 | Firma farklılıkları **Operasyon Profili** ile: ara ayrıştırma var/yok, koli başına maks sipariş (örn. 26), renk eşikleri, toptan eklentileri. |

---

## 1. Mevcut Altyapı (2026-08-09 analizi — genişletme temeli)

**Var ve kullanılacak:**
- `fulfillment` şeması: `ful_picking_plans` (PlanType: single_item/bulk/…, Status), `ful_sorting_bins` (plan × sipariş × koli no), `ful_packing_stations` (**SlotCount=20, IsObm alanı hazır**, Barcode), `ful_packages` + `ful_package_items` + paket no serileri + `ful_package_code_history`.
- `OrderItem`'da hazır bekleyen alanlar: `PickAssignedTo/At, PickedBy/At, SortingBinQuantity, FinalSortQuantity, FinalScanBy/At/Quantity` — **hiçbir komut henüz yazmıyor**, biz yazacağız.
- `Order`'da: `PickingPlanId, SortingBinId, PackingStationCode, PackingSlotNumber, RequestedCargoIntegrationId`.
- Kargo kod motoru (`ICargoCodeService`, free/pattern/range/external + PTT barkod aralığı) ve paket no servisi.
- Stok: `Stock` satırı **(VariantId, BinId)** kırılımında; rezervasyon raf seviyesinde (`StockReservation`); `inv_warehouse_sections.IsSellableOnline` + `PickingOrder`, `inv_warehouse_bins.Barcode` + `PickingOrder`.
- Event'ler: `PickingPlanCreated → Order.StartProcessing`, `OrderShipped → stok düşümü`; SignalR hub bağlı.
- Panel: PickingPlansPage / PackingStationsPage (basit), sipariş detayında paket bölümü, depo detayında Kısım+Raf CRUD.

**Boşluklar (bu planın işi):**
1. `PickingPlanLine` yok — ürün/miktar/kaynak raf/personel kırılımı yok; `ScanItem`/`ScanToBin` doğrulamasız iskelet.
2. Paket → `Shipment` → taşıyıcı API zinciri kopuk (`MarkShipped` manuel, `Package.ShipmentId` boş).
3. Stok düşümü rezervasyon rafından — fiili toplanan rafla mutabakat ve `StockMovement` izi yok.
4. Kargo seçenekleri ödeme boyutunu (COD) okumuyor; adapter'lar stub (KG planı ayrı — `docs/kargo-entegrasyon-plani.md`).
5. Operasyon ekranları (mobil toplama, ayrıştırma, koli duvarı, masa) yok.

**Not:** K-2 (ikili kırılım) mevcut üçlü yapıyla çelişmez: **Kısım katmanı kalır** (satılabilirlik + tedarikçi orada) ama operasyon ekranları yalnız **depo + raf kodu** gösterir; raf adlandırması ara katmanları taşır. Yeni veri yapısı gerekmez.

---

## 2. Kavramlar ve Veri Modeli

### 2.1 Operasyon Profili — `fulfillment.ful_operation_profiles`
Firma (veya kanal) başına tek aktif profil; yoksa varsayılanlar.

| Alan | Varsayılan | Açıklama |
|---|---|---|
| `UseIntermediateSorting` | true | Ara ayrıştırma aşaması var/yok (küçük firma: toplama → doğrudan masa) |
| `SingleItemFastLane` | true | Tek ürünlü siparişler ayrı hızlı hatta |
| `MaxOrdersPerBox` | 26 | Ara ayrıştırma kolisi başına maks sipariş |
| `StationSlotCount` | 26 | Masa son-ayrıştırma raf sayısı (PackingStation.SlotCount ile eşleşir) |
| `BoxGreenPct` / `BoxYellowPct` | 100 / 70 | Koli kartı renk eşikleri ("tüm ürünleri kolide olan sipariş" oranı) |
| `LowChanceThresholdPct` | 20 | Siparişin toplanma oranı bu eşiğin altındaysa koli seçiminde son bölgeye atılır (K-12) |
| `BulkQuantityEntry` | false | Toptan: barkod + adet girişi (her ürünü tek tek okutma yerine) |
| `CargoNotifyAt` | `packed` | `packed` (K-10 varsayılan) / `order_created` (pazaryeri modu — kanal Settings'ten override edilebilir) |

### 2.2 Toplama görevi satırları — `fulfillment.ful_picking_plan_lines` (YENİ)
`PickingPlan` korunur; altına satır gelir:

| Alan | Açıklama |
|---|---|
| `PickingPlanId`, `OrderId`, `OrderItemId`, `VariantId` | bağlar |
| `Quantity`, `PickedQuantity` | istenen / toplanan |
| `SourceBinId?`, `SourceBinCode` | önerilen kaynak raf (rezervasyondan; rota = `Section.PickingOrder, Bin.PickingOrder`) |
| `AssignedTo?`, `AssignedAt?` | personel dağıtımı (satır bazında) |
| `PickedBy?`, `PickedAt?` | fiilen toplayan |
| `Status` | pending / assigned / picked / short (bulunamadı) / returned |

- Plan üretilirken siparişin **rezervasyon rafları** satırlara yazılır → personel listesi raf koduna göre sıralı gelir (rota).
- `OrderItem.PickAssignedTo/PickedBy...` alanları satırla senkron güncellenir (mevcut alanlar boşa gitmez).
- **Görev tipi otomatik**: filtreye takılan siparişler tek/çok ürünlüye ayrılır → `single_item` / `bulk` iki ayrı plan (kullanıcı isterse yalnız birini oluşturur).

### 2.3 Görev oluşturma filtresi
Sipariş havuzu = durumları `confirmed` (+ henüz plana bağlanmamış). Filtreler:
- **Satış kanalı** (çoklu)
- **Depo** — siparişin TÜM kalemlerinin rezervasyonu o depodaysa (karma depolu sipariş depo filtresinde listelenmez; "karma depolu" ayrı sekmede görünür ki unutulmasın)
- **Sipariş ürün sayısı** (tek / 2-N aralık)
- **Kargo şirketi** (RequestedCargoIntegrationId)
- **Şehir** (teslimat adresi)
- Tarih aralığı + sipariş no arama (ikincil)

### 2.4 Ara ayrıştırma kolisi — `ful_sorting_bins` genişletmesi
Mevcut satır (plan × sipariş × koli no) korunur; koli "oturumu" için yeni tablo **`ful_sorting_boxes`**: `PickingPlanId, BoxNumber, Generation` (aynı numara yeniden kullanılınca +1), `Status` (open / taken / closed), `TakenBy?/TakenAt?` (zimmet), `StationId?` (masaya alındıysa), `ClosedAt`. `ful_sorting_bins`'e `SortingBoxId` eklenir — sipariş hangi koli-oturumunda, kesin bilinir.

**Ayrıştırma algoritması** (okutulan barkod → sipariş seçimi), sırayla:
1. En az 1 ürünü ayrıştırılmış (kolisi belli) siparişler önce
2. Tüm ürünleri toplanmış olanlar önce
3. Tüm ürünleri ayrıştırılan yığında olanlar önce
4. Ayrıştırma sonrası en az ürüne ihtiyacı kalanlar önce
5. En az ürün içerenler önce
6. **Eşitlikte en eski sipariş** (K-12)

Seçilen sipariş kolisizse: toplanma oranı ≥ `LowChanceThresholdPct` → **en küçük numaralı uygun koli** (doluluk < `MaxOrdersPerBox`); altındaysa en büyük numaralı açık koli. Siparişin tüm ürünleri AYNI koliye gider (koli = `ful_sorting_bins` kaydı). Hiçbir siparişin ihtiyacı yoksa → hata sesi + "depo iadesi" yığını (`ful_picking_plan_lines.Status=returned` benzeri iade kaydı).

### 2.5 Paketleme masası ve son ayrıştırma
- `PackingStation` **sanal**: personel "masa aç" deyince en küçük boş numara verilir (mevcut entity; `SlotCount` profilden). Masaya koli bağlanır (`ful_sorting_boxes.StationId`).
- Son ayrıştırma rafı = masa slotu. Slot ataması `Order.PackingStationCode + PackingSlotNumber`'a yazılır (mevcut alanlar). Okutma akışı:
  - Ürün → kolideki siparişlerden seçim (1. koşul: en az 1 ürünü rafta olan; yoksa yeni siparişe en küçük boş slot) → **slot numarası seslendirilir** → `OrderItem.FinalSortQuantity` artar.
  - Sipariş tamamlanıyorsa → **"Paketle" + slot numarası** → son kontrol: raftaki tüm ürünler tek tek okutulur (`FinalScanBy/At/Quantity`) → hepsi doğruysa: **paket oluştur → fatura kes → etiket bas (otomatik) → slot boşalt**.
  - Son kontrolde yabancı ürün → hata sesi → "masa askıda" yığınına (koli kapanınca aidiyeti kesinleşir — K-6 gereği OBM'ye gider).
- Koli kapanışı: kolideki tüm siparişler paketlendi VEYA kalanlar OBM'ye transfer edildi → koli `closed`, numara ve masa numarası yeniden kullanılabilir; personel boşa çıkar.

### 2.6 Tek ürünlü hızlı hat
Toplanan yığın doğrudan masaya gelir (ayrıştırma yok). Barkod okut → görevdeki **en eski onaylı** eşleşen sipariş → `PickedQuantity`+`FinalScan*` birlikte işaretlenir → paket + fatura + etiket otomatik → tamam. Eşleşen sipariş yoksa hata sesi + iade yığını. Görev bitişini beklemeden sipariş sipariş akar.

### 2.7 Paket kapanışı → kargo & fatura & stok
Paket kapanış tek transaction'da:
1. **Fatura** kesilir (mevcut sipariş fatura altyapısı; paket başına fatura kuralı geçerli).
2. **Kargo kodu** `ICargoCodeService`'ten (zaten üretilmişse korunur) → **etiket + fatura sunucudan masaya bağlı yazıcıya otomatik push**.
3. `Shipment` kaydı paketle İLİŞKİLİ oluşturulur (`Package.ShipmentId` dolar — mevcut kopukluk kapanır) ve **`ful_cargo_notify_outbox`** kuyruğuna düşer; worker taşıyıcı API'sine gönderir (retry'lı; `CargoNotifyAt=order_created` kanallarında outbox kaydı sipariş onayında düşer). 21:00 fiziki teslim mutabakat koşusu KG planındaki gibi bu kuyruğun üstünde çalışır.
4. **Stok fiili raftan düşer**: satırın `PickedBy` yazıldığı anda rezervasyon `picked` + `StockMovement` izi; paket kapanışında kalan mutabakat tamamlanır. (`OrderShippedEvent` handler'ı "rezervasyon rafından düş" yerine "picked rezervasyonları kapat" olarak düzeltilir.)
5. Sipariş tüm paketleri kapanınca `shipped`'e geçer (mevcut manuel `POST /ship` korunur ama operasyonda otomatik tetiklenir).

### 2.8 Kargo yönlendirme (K-9)
Ekran: taşıyıcı bazlı bekleyen paket/sipariş listesi → hedef taşıyıcı seç → toplu veya tekil yönlendir. Paket bilgisi gönderilmişse: eski adapter'da `CancelShipmentAsync` (destekliyorsa) → kargo kodu yeniden üretilir (`PackageCodeHistory` izi mevcut) → yeni taşıyıcıya outbox kaydı. Sipariş henüz paketlenmemişse yalnız `RequestedCargoIntegrationId` güncellenir.

### 2.9 Donanım entegrasyonu
- **Barkod okuyucu**: HID (klavye) modu — ekranlarda global "okutma kutusu" odağı; okuma `Enter` ile biter. Ekstra sürücü yok.
- **Yazıcı**: masa kaydına yazıcı adresi (ağ IP/kuyruk) tanımlanır; **sunucu, PDF'i doğrudan ağ yazıcısına (IPP/RAW 9100) push eder** — tarayıcı diyaloğu yok, otomatik baskı garantisi. (Alternatif QZ Tray istemcisi; ilk sürümde sunucu-push önerilir.)
- **Ses**: 0-999 numara + "hata" + "paketle" önceden üretilmiş ses dosyaları (TTS ile bir kere üretilir, `wwwroot/audio/`); tarayıcı Web Audio ile anında çalar (canlı TTS gecikmesi/uyumsuzluğu yok).

---

## 3. Ekran Kurguları (panel)

### 3.1 Görev Oluşturma — `/fulfillment/tasks/new`
```
Filtreler: [Kanal ▾çoklu] [Depo ▾] [Ürün sayısı: ●Tek ●Çok(2-99)] [Kargo ▾] [Şehir ▾] [Tarih]
──────────────────────────────────────────────────────────────
Eşleşen: 1.842 sipariş (1.214 tek ürünlü / 628 çok ürünlü) | Karma depolu (filtre dışı): 37
[✓] Tek ürünlü görev oluştur   [✓] Çok ürünlü görev oluştur          [Görev(ler)i Oluştur]
Önizleme tablosu: sipariş no, kanal, ürün sayısı, depo, kargo, şehir, tarih
```

### 3.2 Görev Listesi — `/fulfillment/tasks` (mevcut PickingPlansPage genişler)
```
[Bekleyen] [Toplanıyor] [Tamamlanan]
┌ Görev PLN-000312  ÇOK ÜRÜNLÜ   ⚠ DAĞITIM EKSİK (48/120 satır atandı)  [Dağıt]
┌ Görev PLN-000311  TEK ÜRÜNLÜ   ● Dağıtım tamam — toplanıyor (%64)     [İzle]
```
Dağıtım durumu rozetleri: kırmızı "dağıtım yapılmadı", sarı "kısmen", yeşil "tamamı atandı" (kullanıcının vurguladığı dikkat çekicilik).

### 3.3 Personel Dağıtım — görev detayında
Satırlar raf-rota sırasında; çoklu seç → personel ata. "Kalanları eşit paylaştır [P1][P2][P3]" kısayolu.

### 3.4 Personel Toplama (mobil) — `/fulfillment/my-picking`
```
Görev PLN-000312 — bana atanan: 37 satır (12 toplandı)
► KAT-1K3-0718   SKU 8683... Elbise Mavi 38   1 adet   [barkod okut]
Sonraki: KAT-1K3-0719, KAT-1K4-0021...          [Bulunamadı işaretle]
```
Raf sırasına dizili; okutulan satır yeşillenir, sayaç ilerler. "Bulunamadı" → satır `short`.

### 3.5 Ara Ayrıştırma Okutma (tablet) — `/fulfillment/sorting/{planId}`
```
[■ büyük okutma kutusu]
Son okunan: SKU 8683...  →  🔊 "7"  (KOLİ 7 — Sipariş MIS0000418, 3/5 ürün)
Hatalı okutma → 🔊 hata + "Depo iadesine ayır"
Alt şerit: bu oturumda okutulan: 214 | iade: 3
```

### 3.6 Koli Duvarı — `/fulfillment/sorting-wall/{planId}`
```
┌KOLİ 1  🟢─┐ ┌KOLİ 2  🟡─┐ ┌KOLİ 3  🔴─┐ ┌KOLİ 4 🟢 MASADA(11)─┐
│ 26 sip     │ │ 19 sip    │ │ 8 sip     │ │ Ali Kaya           │
│ 71/71 ürün │ │ 44/61     │ │ 9/37      │ │ paketlenen: 12/26  │
│ %100       │ │ %72       │ │ %24       │ │ son okutma: 14:02  │
│ [Zimmete Al]│ └───────────┘ └──────────┘ └────────────────────┘
```
Renk: kolideki siparişlerin "tüm ürünleri kolide" oranı ≥%100 yeşil, ≥%70 sarı, altı kırmızı (profil eşikleri). Kart: sipariş sayısı, ürün (giren/gereken), tamamlanma %'si (koli içi gerçek ihtiyaçtan), masa + personel + paketlenen sipariş + son okutma.

### 3.7 Masa (Son Ayrıştırma) — `/fulfillment/station/{id}` (tablet)
```
MASA 11 — KOLİ 4 (gen.2)   Slotlar: [1:MIS0418 ●3/5] [2:MIS0502 ●1/2] [3+: boş]
[■ okutma kutusu]  →  🔊 "2"    |  sipariş tamamsa: 🔊 "PAKETLE — 2"
Paketleme modu: slot 2'nin ürünlerini sırayla okut → 5/5 ✓ → 🖨 otomatik fatura+etiket → slot boşaldı
Askıda ürünler: 2  |  [Kalanları OBM'ye aktar + Koliyi kapat]
```

### 3.8 İzleme sayfaları
- **Görev izleme**: satır/personel ilerlemesi, iade yığını, short satırlar.
- **Masa izleme**: koli duvarına benzer masa kartları (açık masalar, koli, personel, slot doluluğu, paketlenen).
- **Kargo yönlendirme** `/fulfillment/cargo-reroute`: taşıyıcı → bekleyen paket listesi → hedef taşıyıcıya toplu/tekil taşı.

---

## 4. Fazlama

| Faz | Kapsam | Çıktı |
|---|---|---|
| **OP1** | Veri modeli (profil + plan satırları + koli oturumu + outbox tabloları, migration'lar) + görev oluşturma filtresi + tek/çok otomatik ayrım + görev listesi & dağıtım ekranları + `PickingPlanLine` üretiminde rezervasyon rafı/rota | Görev oluşturulup personele dağıtılabilir |
| **OP2** | Personel toplama (mobil) + ses altyapısı + **tek ürünlü hızlı hat uçtan uca** (paket+fatura+etiket otomatik baskı, sunucu-push yazıcı) + stok fiili-raf düşümü | Tek ürünlü siparişler uçtan uca operasyonla çıkar |
| **OP3** | Ara ayrıştırma motoru (koşul algoritması) + okutma ekranı + koli duvarı + koli zimmet | Çok ürünlü akışın orta aşaması |
| **OP4** | Masa son ayrıştırma + son kontrol + paket kapanışı (çok ürünlü) + OBM transferi + masa/görev izleme | Çok ürünlü siparişler uçtan uca |
| **OP5** | Kargo outbox + `CargoNotifyAt` kanal politikası + yönlendirme ekranı + `MarkShipped` otomasyonu (+ KG1 gerçek adapter'larla birleşme) | Kargo zinciri kapanır |
| **OP6+** | Toptan profili (adet-gir, istek listesi görevi, dönemsel fatura), performans raporları, sipariş-bazlı toplamada anlık iptal uyarısı | Toptan + raporlama |

Her faz kendi içinde canlıya alınabilir; OP1-OP2 tek ürünlü operasyonu tek başına çalıştırır (en hızlı değer).

---

## 5. Açık (küçük) sorular
1. Yazıcı push yöntemi: sunucudan ağ yazıcısına doğrudan (önerilen) mı, QZ Tray istemcisi mi? Masadaki yazıcılar ağ yazıcısı mı USB mi?
2. Ses dosyaları: sayı aralığı 0-999 yeterli mi (slot/koli numaraları için fazlasıyla), Türkçe tek ses seti mi?
3. Operasyon profili yönetimi: Ayarlar altında ayrı sayfa mı, kanal ayarlarının içinde mi? (Önerim: Ayarlar → Operasyon Profili, firma bazlı tek kayıt.)
