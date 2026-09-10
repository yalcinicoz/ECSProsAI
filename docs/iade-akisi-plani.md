# İade Akışı Planı — v1 (2026-09-10)

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

## 4. Açık Kararlar

| # | Soru | Öneri |
|---|------|-------|
| **K1** | Teslimatsız İade **sipariş bütünü** mü, **paket bazlı** mı? (Çok paketli siparişte bir paket dönebilir; fatura paket başına.) | v1 sipariş bütünü; paket bazlı v2 (Order kısmi durum gerektirir). |
| **K2** | Kargoya verilmemiş faturalı siparişte "iade" fiziksel olarak paketin açılıp rafa dönmesi. `receive` adımı zorunlu mu, yoksa komut anında otomatik `received` mi? | `receive` zorunlu (muayene + depo seçimi tek yerde); stok girişi atlanır (§2.3/5). |
| **K3** | Faturalı siparişin teslimatsız iadesinde fatura: **iptal** mi (e-arşiv iptal), **iade faturası** mı? Müşteri iadesinde iade faturası? | Teslimatsız (mal müşteriye hiç geçmedi) → fatura **iptal**; müşteri iadesi → **iade faturası** (fatura entegrasyon planı FE5 ile). v1'de yalnız `ord_invoices.Status=cancelled` + entegratöre iptal, iade faturası FE5'e bırakılır. |
| **K4** | Faturasız `processing` iptali: toplanmış (picked) satır varken izin var mı? | İzin var; picked satırlar Fulfillment "iadeden rafa" yığınına düşer, v1'de yalnız uyarı metni + rezervasyon serbest. |
| **K5** | Geri ödeme tutarına **kargo ücreti** ve **taksit/kapıda ödeme masrafı** dahil mi? | Teslimatsız iade: tahsil edilen tutarın tamamı (kargo dahil, sipariş hiç gerçekleşmedi). Müşteri iadesi: yalnız ürün kalemleri; kargo ücreti iade edilmez (mevcut ticari uygulama; kanal ayarı gerekiyorsa v2). |
| **K6** | Kapıda ödeme tahsilatı `MarkDelivered`'da otomatik mi, yoksa kargo firmasının tahsilat mutabakatı beklenir mi? | Otomatik (teslim = tahsilat, R9'un doğrudan karşılığı); mutabakat farkı Finance'te ayrı iş. |
| **K7** | `returned` vitrin etiketi "Teslim Edilemedi" olsun mu? (Mevcut "İade Edildi".) | Evet. |
| **K8** | Müşteri iadesi panelden açılırken `shipped` şartı kaldırılıyor (E6). Kargo firmasında müşteri "reddettim" deyip iade talebi açan durum → operasyon Teslimatsız İade'yi kullanır. Onaylıyor musunuz? | Evet. |

---

## 5. Kapsam Dışı

- Legacy senkron/aktarım eşlemeleri (sonraki iş).
- Pazaryeri iade senkronu (pazaryeri modülü).
- İade faturası üretimi (fatura entegrasyon planı FE5).
- Kısmi (paket bazlı) teslimatsız iade (K1 v2).
