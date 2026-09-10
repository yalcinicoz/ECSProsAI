# İade Akışı Planı — v1.1 (2026-09-10) — F0-F5 UYGULANDI

Sipariş iadesi ve müşteriye geri ödeme mantığının tek yerde oturtulması. Eski sistem aktarım/senkron
kodları (LegacyOrderSyncService, LegacyOrderStatusMapper, LegacyReturnImportSlice) bu planın KAPSAMI
DIŞINDADIR; bu plan uygulandıktan sonra aktarım kodları buradaki sözlüğe göre ayrıca düzeltilecektir.

---

## 0. Verilmiş Kararlar (kullanıcı, 2026-09-10 — tartışması kapandı)

| # | Kural |
|---|-------|
| R1 | İki iade tipi vardır: **Teslimatsız İade** ve **Müşteri İadesi**. |
| R2 | **Teslimatsız İade** = paket müşteriye ulaştırılamadı / müşteri kabul etmedi; **veya** faturası kesildi ama hiç kargoya verilmedi. |
| R3 | Teslimatsız İade **yalnız** kargoya verilmiş siparişte, ya da faturası kesilmiş ama kargoya verilmemiş siparişte olur. Daha önceki hiçbir aşamada olamaz. |
| R4 | Fatura kesilmeden önceki hiçbir aşamada **iade yoktur**, yalnız **İptal** vardır. |
| R5 | Sipariş durumu olarak yalnız Teslimatsız İade kullanılır (`returned`). **Müşteri iadesi sipariş durumunu değiştirmez.** |
| R6 | Her iki tip de İadeler modülünde (`ord_returns`) takip edilir: depoya giriş + varsa müşteriye ödeme. |
| R7 | Müşteriye para iadesi **yalnız müşteriden tahsilat yapıldıysa** hesaplanır. Kart / havale / ön ödeme / hesaptan ödeme → her iade tipinde geri ödeme vardır. |
| R8 | Teslim edilmeden iade alınan **kapıda ödeme** siparişlerinde tahsilat yoktur → **kesinlikle para iadesi hesaplanmaz**. |
| R9 | Müşteriye **teslim edilmiş** kapıda ödemede tahsilat yapılmıştır → müşteri iadesinde para iadesi hesaplanır. |
| R10 | **Pazaryeri** siparişlerinde hiçbir durumda müşteriye para iadesi yapılmaz; iadeyi pazaryeri yapar. |

---

## 1. Mevcut Durum (2026-09-10 kod analizi)

**Uyumlu:**
- Müşteri iadesi (`ord_returns`: requested → approved → received → refunded / rejected) sipariş durumunu değiştirmiyor (R5 ✓).
- Vitrin/mobil iade talebi yalnız `delivered` siparişte açılıyor (`CreateStoreReturnCommand`).
- `ReturnReceivedEvent` → Inventory stok girişi (`MovementType=return`, `preferReturns`).
- Cüzdana iade cari çatı üzerinden (`PostAccountTransaction`, `return_refund`), ters kayıt telafili.

**Aykırı / eksik:**

| # | Tespit | Kural |
|---|--------|-------|
| E1 | Yeni sistemde siparişi `returned` yapan **hiçbir komut yok**; yalnız legacy senkron yazıyor. Panelde buton yok. | R2, R3 |
| E2 | `Order.Cancel` yalnız `pending`/`confirmed`; `processing` (fatura kesilmemiş) iptal edilemiyor. | R4 |
| E3 | `CompleteRefundCommandHandler` tahsilat kontrolü yapmıyor: iade `received` + tutar > 0 yeterli. Kapıda ödemeli, teslim edilmemiş siparişe cüzdan alacağı yazılabilir. | R7, R8 |
| E4 | Pazaryeri ayrımı yok. Kanal tipi `core.core_platform_types.IsMarketplace`'te var, iade akışı okumuyor. | R10 |
| E5 | `Order.PaymentStatus` yalnız checkout/PayTR/mock tarafından yazılıyor. **Kapıda ödemeli sipariş teslim edilince `paid` olmuyor**, `AddOrderPayment` da `PaymentStatus`'a dokunmuyor → "tahsilat yapıldı mı" sorusunun güvenilir tek kaynağı yok. | R9 |
| E6 | Panel `CreateReturnCommand` `shipped` siparişte de müşteri iadesi açtırıyor; kargodaki siparişin iadesi ancak Teslimatsız İade olabilir. | R1, R5 |
| E7 | `Return.ReturnType` serbest metin (legacy `legacy_type_N`), sözlük yok; `RefundStatus` "geri ödeme yok" değerini bilmiyor. | R1, R7 |
| E8 | Faturalı siparişin iadesinde fatura tarafında (iptal / iade faturası) hiçbir işlem yok. | R2 |
| E9 | İade tutarı (`UnitRefundAmount`/`TotalRefundAmount`) hiçbir handler'da hesaplanmıyor, hep 0; panel elle tutar giriyor, üst sınır yok. | R7 |

Canlı veri: `returned` 4 sipariş (hepsi legacy senkron), `ord_returns` 1 kayıt (requested), kapıda ödeme `unpaid` 11 sipariş.

---

## 2. Hedef Tasarım

### 2.1 Sözlükler (DurumEtiketleri — TEK kural, panel+vitrin)

**İade tipi** (`Return.ReturnType`, yeni sözlük `IadeTipi`):

| Kod | Panel | Vitrin |
|-----|-------|--------|
| `undelivered` | Teslimatsız İade | — (müşteriye "Teslim Edilemedi" olarak sipariş durumundan görünür) |
| `customer` | Müşteri İadesi | İade |

**Geri ödeme durumu** (`Return.RefundStatus`): `pending` → `completed`; **yeni:** `not_applicable`
(Panel: "Geri ödeme yok", tooltip nedeni; Vitrin: gösterilmez). Neden `Return.RefundNotApplicableReason`
(yeni nullable kolon): `cod_not_collected` | `marketplace` | `unpaid`.

**Sipariş durumu** `returned`: Panel "Teslimatsız İade" (mevcut "İade" değişir), Vitrin "Teslim Edilemedi"
(mevcut "İade Edildi" yanıltıcı: müşteri iadesi sipariş durumunu değiştirmez, `returned` yalnız teslimatsız).

### 2.2 Sipariş durum makinesi (Order.cs)

```
pending → confirmed → processing → shipped → delivered
pending | confirmed | processing(faturasız)              → cancelled          (R4)
shipped | processing(faturalı, kargosuz)                 → returned           (R2, R3)
```

- `Cancel`: `processing` eklenir, **ön koşul** sipariş için `Status != cancelled` bir `ord_invoices` yoksa.
  Fatura kontrolü handler'da (domain fatura tablosunu bilmiyor) → `CancelOrderCommandHandler` fatura
  varsa "Faturası kesilmiş sipariş iptal edilemez, Teslimatsız İade uygulayın." döner.
- `MarkUndeliveredReturn(updatedBy, reason)`: yeni domain metodu; `shipped` ya da `processing` kabul eder,
  `Status = returned`, `OrderReturnedUndeliveredEvent(orderId, items, wasShipped)` yayar. Faturalı-mı
  kontrolü yine handler'da.
- Toplama planı `processing` iptalinde: bekleyen `ful_picking_plan_lines` iptal edilir, toplanmış
  satırlar varsa "iadeden rafa" işine düşer (Fulfillment plan §2.4 mevcut kavram). v1'de yalnız uyarı +
  rezervasyon serbest bırakma (mevcut `OrderCancelledEvent`).

### 2.3 Teslimatsız İade komutu — `MarkUndeliveredReturnCommand`

`POST /api/orders/{id}/undelivered-return` `[RequirePermission(OrdersReturnsManage)]`
Body: `{ reason, warehouseId?, notes? }`

Adımlar (tek transaction):
1. Ön koşul: `shipped` **veya** (`processing` && iptal edilmemiş fatura var && `ord_shipments` yok).
   Aksi → hata (R3).
2. `order.MarkUndeliveredReturn` → `returned`; `InternalNotes` başına `[Teslimatsız İade] {reason}`.
3. `ord_shipments` (shipped) → `Status = returned_to_sender`, `ReturnedAt = now`.
4. `ord_returns` kaydı **otomatik** açılır: `ReturnType=undelivered`, `Status=approved` (talep/onay
   adımı yok — şirket başlatıyor), tüm sipariş kalemleri (`ReturnItem.Quantity = OrderItem.Quantity`,
   `ReturnReasonId` = sistem nedeni "teslim edilemedi", seed), `RefundMethod` = siparişin ödeme
   yöntemine göre (kart → `card_refund`, havale → `bank_transfer`, cüzdan → `wallet`), **geri ödeme
   uygunluğu §2.5 kuralıyla** hesaplanır.
5. Stok: **kargoya verilmişse** stok zaten tüketilmiş → depo girişi `receive` adımında (mevcut
   `ReturnReceivedEvent`). **Kargoya verilmemişse** stok yalnız rezerve → rezervasyonlar bu adımda
   serbest bırakılır (`OrderCancelledEvent` ile aynı Inventory handler'ı), `ReturnItem.StockAlreadyIn
   = true` (yeni bool) ve `receive` adımı bu kalemler için stok girişi YAPMAZ (çift sayım önlenir).
6. Fatura: K3 kararına göre (iptal / iade faturası).
7. Push/e-posta: müşteriye "Siparişiniz teslim edilemedi, iade sürecine alındı" (`PushEtkilesim`
   mevcut `IadeDurumuAsync`).

Sonra normal akış: `receive` (depo girişi + muayene) → `refund` (yalnız uygunsa) → `refunded`.
Geri ödeme uygun değilse `receive` sonrası iade otomatik **kapanır** (`Status = closed`, yeni durum;
"refunded" yanıltıcı olur). Panel etiketi "Tamamlandı (geri ödeme yok)".

### 2.4 Müşteri iadesi

- Panel `CreateReturnCommand`: yalnız `delivered` (E6 kapanır); `ReturnType` sabit `customer`, istemciden
  alınmaz.
- Vitrin/mobil: değişmez (`delivered` şartı zaten var); `ReturnType=customer`.
- Geri ödeme uygunluğu **oluşturma anında** §2.5 kuralıyla hesaplanıp `RefundStatus`'a yazılır; panel
  iade detayında daha ilk andan "Geri ödeme yok — pazaryeri" görünür.

### 2.5 Geri ödeme uygunluğu — TEK kural `IadeOdemeKurali` (Shared.Contracts)

```csharp
public static class IadeOdemeKurali
{
    public sealed record Girdi(
        bool KanalPazaryeri,          // core_platform_types.IsMarketplace
        decimal TahsilEdilen,         // ord_order_payments Status=completed toplamı
        decimal DahaOnceIadeEdilen,   // ord_return_refunds completed toplamı (aynı sipariş)
        string? OdemeYontemi,         // kart | kapida-nakit | kapida-kart | havale | cuzdan
        bool TeslimEdildi);           // Order.Status == delivered (iade anında)

    public sealed record Sonuc(bool Uygun, string? Neden, decimal UstSinir);

    public static Sonuc Degerlendir(Girdi g)
    {
        if (g.KanalPazaryeri)            return new(false, "marketplace", 0);
        if (g.TahsilEdilen <= 0)         return new(false, KapidaOdeme(g.OdemeYontemi) ? "cod_not_collected" : "unpaid", 0);
        var kalan = g.TahsilEdilen - g.DahaOnceIadeEdilen;
        return kalan > 0 ? new(true, null, kalan) : new(false, "already_refunded", 0);
    }
}
```

- Kural **tahsilat kaydına** bakar, ödeme yöntemine değil: kapıda ödeme teslim edilip tahsilat kaydı
  atıldıysa (§2.6) uygun olur (R9); tahsilat yoksa uygun değildir (R8). Kart/havale/cüzdan ödemelerinde
  tahsilat kaydı checkout/PayTR anında zaten var (R7).
- Uygulama noktaları: (a) iade oluşturma → `RefundStatus`/`RefundNotApplicableReason`; (b)
  `CompleteRefundCommandHandler` → **ikinci savunma hattı**, uygun değilse veya `Amount > UstSinir` ise
  hata. Panel elle tutar girse bile sınır aşılamaz (E9 kapanır).
- Kalem tutarı: `ReturnItem.UnitRefundAmount = OrderItem.Total / Quantity` (kampanya dağıtımı sonrası
  gerçek ödenen — kampanya planı kararı), iade toplamı = kalemler toplamı, `UstSinir` ile kırpılır.
  Kargo ücreti/taksit farkı iadesi: K5.

### 2.6 Kapıda ödeme tahsilatı (E5 kapanır)

- `MarkDeliveredCommandHandler`: sipariş `kapida-nakit`/`kapida-kart` ise **teslimde** `ord_order_payments`
  kaydı (`Amount = GrandTotal`, `Status=completed`, `Details.source=cod_on_delivery`) + `PaymentStatus=paid`.
  Kargo entegrasyonu "teslim edildi" olayı da aynı komuttan geçtiği için tek nokta.
- `AddOrderPaymentCommandHandler`: tamamlanmış ödemeler toplamı ≥ GrandTotal → `PaymentStatus=paid`,
  aksi `underpaid` (tutarlılık).
- `PaymentStatus` **türetilmiş özet** olarak kalır; kural §2.5 her zaman ödeme satırlarından hesaplar.

### 2.7 Pazaryeri (R10)

- İade kaydı yine açılır (mal depoya döner), `RefundStatus=not_applicable/marketplace`; `refund` ucu
  reddeder. Pazaryeri iade bildirimlerinin senkronu bu planın dışında (pazaryeri modülü).

### 2.8 Panel (K16 — site/panel senkronu)

- **Sipariş detayı:** `shipped` ve faturalı-kargosuz `processing` siparişte kırmızı **"Teslimatsız İade"**
  butonu (neden zorunlu, depo seçimi). `processing` faturasızda **İptal Et** görünür; faturalıysa İptal
  gizlenir, açıklama: "Faturası kesilmiş sipariş iptal edilemez."
- **İade detayı:** tip rozeti; geri ödeme kartı `not_applicable`'da kilitli + neden metni; tutar alanı
  `UstSinir` ile sınırlı ve varsayılan dolu; `closed` durumu.
- **İade listesi:** tip filtresi sözlükten (`IadeTipi`), `refundStatus` filtresine `not_applicable`.
- **Rehber:** `docs/rehber/siparisler/iadeler.md` güncellenir (iki tip, geri ödeme kuralı).

### 2.9 Vitrin / mobil

- Sipariş `returned` → "Teslim Edilemedi" (adım 4, devam etmiyor); Hesabım › İadelerim'de teslimatsız
  iade satırı görünür (tip `undelivered`, müşteri aksiyonu yok).
- `GET /api/store/lookups` sözlükleri değişince ETag değişir (mevcut mekanizma).

---

## 3. Fazlar

| Faz | İçerik | Bağımlılık |
|-----|--------|-----------|
| **F0** | Sözlükler (`IadeTipi`, `RefundStatus.not_applicable`, `returned` etiketleri), `Return` yeni kolonlar (`RefundNotApplicableReason`, `ReturnItem.StockAlreadyIn`, `closed` durumu), migration; `IadeOdemeKurali` + `CompleteRefund` savunma hattı + kalem tutarı hesabı | — |
| **F1** | Kapıda ödeme teslimde tahsilat (§2.6) + `AddOrderPayment` PaymentStatus tutarlılığı | F0 |
| **F2** | `MarkUndeliveredReturnCommand` + domain geçişi + shipment `returned_to_sender` + stok ayrımı (§2.3) | F0, K1-K3 |
| **F3** | İptal kuralı (`processing` faturasız) + panel buton/gizleme | K4 |
| **F4** | Panel: sipariş detayı butonu, iade detayı kilit/sınır, liste filtreleri; müşteri iadesi `delivered` şartı | F2, F3 |
| **F5** | Vitrin/mobil etiketleri + İadelerim; rehber; kabul testi (olumsuz senaryolar: faturasız processing'e teslimatsız iade → hata; kapıda ödeme teslimsiz iadeye refund → hata; pazaryeri refund → hata) | F4 |

Legacy aktarım/senkron düzeltmeleri (durum eşlemesi, `legacy_type_N` → sözlük, sıra-9 üzerine yazma)
**ayrı iş**, bu plan bittikten sonra.

---

## 4. Kararlar (K1-K8 — "Öneri" sütunundaki değerlerle UYGULANDI, 2026-09-10)

> Uygulama, kullanıcının "planı incele ve uygula" talebi üzerine her karar için ÖNERİ sütunundaki değerle yapıldı.
> Farklı bir karar istenirse ilgili nokta tek yerden değiştirilir (aşağıda "Kodda" sütunu).

| # | Soru | Öneri (UYGULANAN) |
|---|------|-------|
| **K1** | Teslimatsız İade **sipariş bütünü** mü, **paket bazlı** mı? (Çok paketli siparişte bir paket dönebilir; fatura paket başına.) | v1 sipariş bütünü; paket bazlı v2 (Order kısmi durum gerektirir). |
| **K2** | Kargoya verilmemiş faturalı siparişte "iade" fiziksel olarak paketin açılıp rafa dönmesi. `receive` adımı zorunlu mu, yoksa komut anında otomatik `received` mi? | `receive` zorunlu (muayene + depo seçimi tek yerde); stok girişi atlanır (§2.3/5). |
| **K3** | Faturalı siparişin teslimatsız iadesinde fatura: **iptal** mi (e-arşiv iptal), **iade faturası** mı? Müşteri iadesinde iade faturası? | Teslimatsız (mal müşteriye hiç geçmedi) → fatura **iptal**; müşteri iadesi → **iade faturası** (fatura entegrasyon planı FE5 ile). v1'de yalnız `ord_invoices.Status=cancelled` + entegratöre iptal, iade faturası FE5'e bırakılır. |
| **K4** | Faturasız `processing` iptali: toplanmış (picked) satır varken izin var mı? | İzin var; picked satırlar Fulfillment "iadeden rafa" yığınına düşer, v1'de yalnız uyarı metni + rezervasyon serbest. |
| **K5** | Geri ödeme tutarına **kargo ücreti** ve **taksit/kapıda ödeme masrafı** dahil mi? | Teslimatsız iade: tahsil edilen tutarın tamamı (kargo dahil, sipariş hiç gerçekleşmedi). Müşteri iadesi: yalnız ürün kalemleri; kargo ücreti iade edilmez (mevcut ticari uygulama; kanal ayarı gerekiyorsa v2). |
| **K6** | Kapıda ödeme tahsilatı `MarkDelivered`'da otomatik mi, yoksa kargo firmasının tahsilat mutabakatı beklenir mi? | Otomatik (teslim = tahsilat, R9'un doğrudan karşılığı); mutabakat farkı Finance'te ayrı iş. |
| **K7** | `returned` vitrin etiketi "Teslim Edilemedi" olsun mu? (Mevcut "İade Edildi".) | Evet. |
| **K8** | Müşteri iadesi panelden açılırken `shipped` şartı kaldırılıyor (E6). Kargo firmasında müşteri "reddettim" deyip iade talebi açan durum → operasyon Teslimatsız İade'yi kullanır. Onaylıyor musunuz? | Evet. |

**Kodda:** K1 → `MarkUndeliveredReturnCommand` sipariş bütünü (tüm kalemler). K2 → iade `approved` açılır, `receive` zorunlu;
`ReturnItem.StockAlreadyIn` kalemlerde stok girişi atlanır. K3 → `FaturaIptal.Uygula` (CancelInvoice ile aynı kod; entegratöre
iptal kuyruklanır); iade faturası FE5. K4 → `CancelOrderCommandHandler`: `processing` + toplama planı varsa İç Not'a uyarı, rezervasyon
serbest (mevcut olay). K5 → teslimatsız: `RefundAmount = UstSinir` (tahsil edilenin tamamı); müşteri iadesi: kalem toplamı, üst sınırla
kırpılır. K6 → `MarkDeliveredCommandHandler` kapıda ödemede `Tahsilat.KaydetAsync(source=cod_on_delivery)`. K7 → `DurumEtiketleri.Vitrin`
`returned="Teslim Edilemedi"`. K8 → `CreateReturnCommandHandler` yalnız `delivered`, tip sabit `customer`.

---

## 6. Uygulama Durumu (2026-09-10)

| Faz | Durum | Dokunulan yerler |
|-----|-------|------------------|
| F0 | ✅ | `DurumEtiketleri` (IadeTipi, GeriOdemeDurumu, GeriOdemeYokNedeni, `closed`, `returned` etiketleri, `underpaid`); `IadeOdemeKurali` (Shared.Contracts); `ReturnConstants`; `Return.RefundNotApplicableReason`, `ReturnItem.StockAlreadyIn`, `Shipment.ReturnedAt`; migration `20260910171457_AddReturnFlowFields` (+ `ReturnType` return/refund → customer backfill); Core migration `20260910170000_SeedUndeliveredReturnReason` (sistem nedeni, sabit Id); `CompleteRefund` savunma hattı + üst sınır; kalem tutarı `IadeOdemeDegerlendirme.KalemTutari` |
| F1 | ✅ | `MarkDelivered` kapıda ödeme tahsilatı; `AddOrderPayment` PaymentStatus türetimi; **ek:** PayTR callback + mock ödeme artık `ord_order_payments` satırı yazıyor (plan "zaten var" sanıyordu; canlıda 0 satırdı) — eski siparişler için `IadeOdemeKurali.TahsilEdilen` `PaymentStatus=paid` → GrandTotal geriye dönük uyumu; `IPaymentMethodResolver` (Core ödeme yöntemi Id'si) |
| F2 | ✅ | `Order.MarkUndeliveredReturn` + `OrderReturnedUndeliveredEvent`; `MarkUndeliveredReturnCommand` (`POST /api/orders/{id}/undelivered-return`, `orders.returns.manage`); gönderi `returned_to_sender`; Inventory `OrderReturnedUndeliveredEventHandler` (kargosuz → rezervasyon serbest); `ReceiveReturn` StockAlreadyIn atlar + not_applicable → `closed`; `IChannelCapabilityResolver.IsMarketplaceAsync` |
| F3 | ✅ | `Order.CancellableStatuses` + processing; `CancelOrderCommandHandler` fatura ön koşulu; panel İptal Et gizleme + açıklama |
| F4 | ✅ | Panel: sipariş detayı "Teslimatsız İade" butonu+modal, iade detayı tip rozeti / Geri Ödeme kartı (kilit+neden / üst sınır) / tutar sınırı, iade listesi tip+geri ödeme filtreleri sözlükten, "Kapanan" sekmesi; `CreateReturn` yalnız delivered |
| F5 | ✅ | Vitrin: `returned` "Teslim Edilemedi"; İadelerim'de teslimatsız satır (tip rozeti, "Ödeme iadesi: Bulunmuyor", bilgi metinleri, `closed`); `GET /api/store/lookups` `returnType` ailesi; rehber `20-iadeler.md` + `11-siparis-detay.md`; testler `IadeOdemeKuraliTests` (kural R7-R10, domain R2-R4, sözlük) |

**Bilinen sınırlar:** Pazaryeri iade senkronu ve iade faturası (FE5) kapsam dışı. Toplama planı satırlarının iptali v1'de yalnız uyarı notu (K4). Teslimatsız iade kapıda ödemeli
misafir siparişinde `Return.MemberId = Guid.Empty` (push gitmez, panelde görünür).

### 6.1 Eski sistem senkron/aktarım düzeltmesi (2026-09-10, kullanıcı: "aynı mantık orda da var, düzelt")

**Eski veri incelemesi (ECSGYE kaynak kodu + canlı MySQL profili, 173K iade):**

| Eski alan | Anlamı | Hedef |
|-----------|--------|-------|
| `opiadesiparisler.iadeTipi` | 1 = TeslimatsizIade (sipariş bütünü, kalem nedeni 9, sipariş "Teslim Edilemeden İade Geldi", `iadeTutari` = ödenmiş tahsilat toplamı); 2 = SiparisUrunIade (kalem bazlı, sipariş durumu değişmez) | `undelivered` / `customer` (R1 ile birebir) |
| `durumu`, `uyeyeOdenenTutar`, `uyeyeOdemeTarihi`, `uyeyeOdemeTipi` | Eski kod hiç yazmıyor (tümü 0/NULL/1) | KULLANILMAZ |
| `webuyeparalari` (iadeSiparislerId) | musteriIstegi=2 para iadesi alacağı (odemeTarihi dolu = üyeye ödendi); musteriIstegi=1 Değişim (bakiye) | ödendi → `refunded`/completed; alacak var → `received`/pending; yoksa `IadeOdemeKurali` → `received` ya da `closed`/not_applicable |
| `dfplatforms.iadeOdemesiYap=0` (pazaryerleri) | Üyeye ödeme satırı hiç açılmaz | R10 ile aynı (kural kanal tipinden) |
| `dfiadenedenleri` | 1 Belirsiz, 2 Beğenmedim, 3 Beden, 4 Defo, 5 Kalitesiz, 9 Teslim Edilmedi | legacy_* (4/5 eklendi); tip 1 kalemleri sistem nedeni "Teslim Edilemedi" |
| `oporders.paymentTypeId` | 1 kart, 2 kapıda nakit, 3 kapıda kart | hedef siparişte PaymentMethod boşsa buradan (kural + geri ödeme yöntemi) |

**Kod:** `LegacyReturnMappings` (sözlük + `Durum` türetimi, TEK yer), `LegacyReturnReader` (webuyeparalari toplamları,
`barcode`, sipariş filtresi), `LegacyReturnImportSlice` (hedef sipariş tahsilatı + kanal tipi ile `IadeOdemeKurali`; kalem
eşleşmesi LegacyOrderLineId yoksa varyant barkoduyla — outbox siparişleri; teslimatsız iadede üst/kalem tutar denetimi
yok çünkü üst tutar tahsilattır; yeni `ImportForOrdersAsync`), `LegacyOrderSyncService.SyncReturnsAsync` (bağlı
siparişlerin iadeleri her turda; `Legacy:Sync:Returns`, dry-run `Legacy:Sync:OrderDryRun`), durum senkronunda `returned`
→ gönderi `returned_to_sender`, `LegacyOrderImportSlice` ödeme yöntemi `card`→`kart`, Core migration
`SeedLegacyReturnReasons`. Okuma kaynağı yoksa `Legacy:MySqlConnection`'a düşer (oturum READ ONLY).
Canlı dry-run (56 bağlı sipariş): 10 iade hazır (8 teslimatsız: 5 refunded, 3 kapıda-tahsilatsız closed; 2 müşteri: refunded), 0 engel.
Legacy iade nedenleri `core_return_reasons`'ta (lookup değil) → panel neden adı "—" gösterir (bilinen sınır).

---

## 5. Kapsam Dışı

- ~~Legacy senkron/aktarım eşlemeleri (sonraki iş).~~ → §6.1 ile yapıldı (2026-09-10).
- Pazaryeri iade senkronu (pazaryeri modülü).
- İade faturası üretimi (fatura entegrasyon planı FE5).
- Kısmi (paket bazlı) teslimatsız iade (K1 v2).
