# Eski Sistem Sipariş Senkronu — Plan (2026-08-04)

Yeni sitenin siparişleri eski projeye (ECSGYE, MySQL) yazılır; sipariş operasyonları
ŞİMDİLİK eski panelden yürür; durum + kargo bilgisi geri senkronlanır ve müşteri yeni
sitede sipariş durumunu görür. Müşteri iptali yeni siteden yapılır, eskiye yansıtılır.

## 0. Sabit kararlar (kullanıcı, 2026-08-04)

- **Eski servis:** `https://services.misharitalia.com` — `POST /Services/SiparisOlusturFromModel`
  (`ECSGYE.ClassLibrary.Order` JSON). Yordam idempotent: `(platformId, orderNumber)` kayıtlıysa
  mevcut Id döner. Üye/adres yoksa kendisi açar (misafir çözümü).
- **Kanal → eski platformId eşlemesi** (FirmPlatform.Settings `legacyPlatformId`, geçici ayar):
  tozlu=1 (mişaroğlu), julude=2, olurbutik=12, mishar=41 (eldi). Şu an aktif tek kanal: mishar.
- **Ödeme tipi eşlemesi:** PayTR/online kart → `paymentTypeId=1` (`isPaid=true`);
  kapıda nakit → 2, kapıda kart → 3 (`isPaid=false`).
- **Eski sistemde her yeni sipariş "Onay Bekliyor" başlar** (SMS/çağrı merkezi onayı eski tarafta).
- **Durum eşleme (eski → yeni):**

  | Eski (oporders.orderStatus) | Yeni | Müşteri görünümü |
  |---|---|---|
  | Onay Bekliyor | pending | Onay Bekliyor |
  | Hazırlanıyor | processing (confirm + start-processing) | Hazırlanıyor |
  | Faturası Kesildi | processing (iç not: fatura kesildi) | Hazırlanıyor |
  | Kargoya Verildi | shipped (+ kargo adı/takip no) | Kargoda |
  | Teslim Edildi | delivered | Teslim Edildi |
  | Teslim Edilmeden İade | returned | İade |
  | İptal Edildi | cancelled | İptal |

- **Stok otoritesi ESKİ SİSTEM:** operasyon eski panelde olduğu sürece, senkron kaynaklı durum
  geçişleri yeni sistemde STOK YAN ETKİSİZ uygulanır (rezervasyon serbest bırakılır ama Quantity'ye
  dokunulmaz; gerçek stok B2 dilimiyle 10 dk'da eski sistemden gelir → çift düşüm olmaz).

## 1. Yazma yönü (yeni → eski)

**Ne zaman:** kapıda ödemeli sipariş → oluştuğu anda; kart (PayTR) → ödeme başarılı olup sipariş
`paid/confirmed` olduğu anda (başarısız/yarım kart denemeleri eskiye hiç gitmez).

**Nasıl:** `integration.legacy_order_outbox` kuyruğu (OrderId, İşTipi=create|cancel, Durum, Deneme,
SonHata) → mevcut `LegacySyncWorker`'a yeni dilim (1-2 dk kadans): kuyruktakiler için `Order` modeli
doldurulur, eski servise POST edilir, eski sipariş Id'si `ord_orders.LegacyOrderId`'ye yazılır.
Checkout HİÇBİR ZAMAN bloke olmaz (kuyruk + tekrar deneme; hatalar integration_logs'a).

**Order modeli doldurma (Trendyol kalıbı):**
- `orderNumber` = MIS numaramız; `platformId` = kanalın `legacyPlatformId`'si; `orderStatus`
  gönderilmez/varsayılan bırakılır → eski mantık "Onay Bekliyor" başlatır.
- `member`: aktarılmış üyede `LegacyMemberId`; yeni üye/misafirde ad-soyad/e-posta/telefon → eski
  taraf üye+adres açar (dönüşte eski memberId'yi saklamak İSTENİRSE ayrı faz).
- Kalemler: **adet başına 1 satır** (quantity=1, benzersiz `basketDetailId`), `productVariantId` =
  `integration.erp_variant_data` eşlemesi, `sellingPrice` = kampanyalı birim satış fiyatı; kalem
  indirim payları (`OrderItem.DiscountAmount` → `orderDiscounts`); satır KDV kaydı (ürün `TaxRate`
  üzerinden dahil-KDV ayrıştırma, gibCode 0015). Kapıda bedeli → `orderExpenses`.
- Toplamlar: productTotal/subTotal/discountTotal/taxTotal/orderTotal/paidTotal bizim
  Subtotal/TotalDiscount/GrandTotal alanlarından.
- Eşleşmeyen varyant (erp kaydı olmayan yeni ürün) → outbox `hata` durumuna düşer, log + panelde
  görünür; sipariş eskiye YAZILMAZ (operasyon kararına bırakılır).

## 2. Durum yönü (eski → yeni)

Worker dilimi (3-5 dk): `LegacyOrderId` dolu ve kapanmamış (delivered/cancelled/returned olmayan)
siparişler için eski DB'den `orderStatus, courierName, shippingBarcode, courierTrackingNumber`
SELECT edilir (mevcut MySQL bağlantısı, salt-okuma). Eşleme tablosuyla yeni duruma çevrilir;
fark varsa **stok yan etkisiz** durum geçişi uygulanır + kargo bilgisi siparişe yazılır (müşteri
"Siparişlerim"de görür). Geçişler yalnız İLERİ yönde uygulanır (eski panelde geri alma nadir —
görülürse log'a düşer, elle çözülür).

## 3. İptal yönü (yeni → eski)

Müşteri yeni sitede iptal etti (pending/confirmed) → outbox'a `cancel` işi → eski tarafta iptal
(`Services/UyeSiparisIptal` veya sipariş yazımındaki gibi servis üzerinden; memberId olarak eski
üye Id'si geçilir). Eski panelde yapılan iptal zaten durum senkronuyla bize gelir.

## 4. Fazlar

- **F1 — Kanal ayarı + outbox + yazma dilimi (dry-run):** `legacyPlatformId` ayarı (panel Kanallar
  formunda alan), outbox tablosu (migration), model doldurma + POST, LegacyOrderId kaydı.
  Dry-run: model JSON'u log'a yazılır, servise gitmez. Kabul: mishar'da örnek kapıda + kart
  siparişinin JSON'u eski şemayla birebir doğrulanır.
- **F2 — Durum + kargo geri senkronu:** eşleme tablosu, stok yan etkisiz geçişler, müşteri
  görünümü (kargo firması + takip no). Kabul: eski panelde durum ilerletilen siparişin sitede
  müşteri görünümünde ilerlemesi.
- **F3 — İptal push:** siteden iptal → eskiye yansıma; kabul: çift yönlü iptal senaryosu.
- **F4 — Canlıya alma:** mishar için dry-run kapatılır; ilk gerçek siparişler eski panelde
  operasyonla birlikte doğrulanır.

## 5. Riskler / notlar

- **Çift kayıt:** orderNumber idempotency'si + outbox tek-işleme ile iki katman koruma.
- **Yeni ürün eşleşmesi:** B1 dilimi yeni ürünleri eskiye açıyor; sipariş yazımı erp eşlemesi
  bulunmayan kalemde bilerek durur (yanlış ürün yazmaktansa bekletir).
- **Kapıda onayı:** eski panelde verilir; onay bize "Hazırlanıyor" olarak döner ve sipariş
  processing'e geçer — yeni paneldeki elle "Onayla" akışı bu siparişlerde KULLANILMAZ (çakışma
  olmasın diye LegacyOrderId'li siparişte panel onay düğmesi kapatılabilir — F2'de karar).
- **Güvenlik:** eski servis action'ları anonim görünüyor — services.misharitalia.com erişimi
  IP kısıtı/paylaşılan sır ile korunmalı (eski tarafta küçük dokunuş önerilir; bizim istekler
  tek sunucudan çıkar).
- `ECSGYE.Solution` klasörü yalnız İNCELEME kopyası — ana repoya eklenmez (.gitignore).
