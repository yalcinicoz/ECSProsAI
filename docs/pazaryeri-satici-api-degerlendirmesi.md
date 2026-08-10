# Pazaryeri Satıcı API'si — Değerlendirme (2026-08-10)

> Kapsam: sitemizde KENDİ malını satan firmaların (pazaryeri satıcısı) kullanacağı dış API.
> Bu bir DEĞERLENDİRMEdir — uygulama yapılmadı. Temel referans: `docs/api-hesaplari-tasarimi.md`
> (tip/scope modeli onaylı) ve mevcut `/api/partner/v1` yüzeyi.

## 1. Mevcut durum — elimizde ne var

Senaryo tasarımda ZATEN öngörülmüş: `supplier_merchant` tipi ("Pazaryeri tedarikçisi — fiyatı
o belirler", pricing.write dahil kilitli scope paketi) ve `FulfillmentMode=supplier` bayrağı
(order.read + fulfillment.write ekler) 2026-07-21'de onaylanıp F1'de kodlandı. 10 scope'luk
katalog hazır. Eksik olan TASARIM değil, UÇLARIN çoğu.

**Bugün canlı partner uçları (6):** `GET me`, `GET groups`, `GET groups/{code}`,
`POST products` (Kapı-1 otomatik doğrulama → pending → panel Kapı-2 onayı → canlı ürün;
revizyon dahil), `GET products` (owner-scoped), `PUT products/{code}/stock`
(tedarikçiye özel depo KISMI — `WarehouseSection.SupplierId`, mutlak stok).

**Dayanabileceğimiz mevcut altyapı:**
- Sahiplik: `Product.SupplierId`, `OrderItem.SupplierId` (sipariş anı snapshot),
  **paket bölme zaten tedarikçiye göre** (OP fazları) → satıcıya "kendi paketi"ni göstermek doğal.
- Cari çatı: bakiye YALNIZ `PostAccountTransaction`, `ConceptCode`'lu defter
  (`current_account_ledgers`) → hakediş için yeni tablo AÇMADAN 'hakedis' defteri açılabilir.
- Kampanya kuralı: sepet indirimi kalemlere ağırlıklı dağıtılıp `OrderItem.DiscountAmount/Total`'a
  yazılıyor → komisyon GERÇEK ödenen fiyattan hesaplanabilir (iade de aynı değerden).
- Kargo kod motoru (free/pattern/range/external) + Shipment/OBM zinciri canlı.
- Satıcı paneli (insan yüzü) S0-S2 canlı — API (makine yüzü) ile aynı sahiplik modelini paylaşıyor.

## 2. Boşluk analizi — kullanıcı ihtiyaç listesine göre

| İhtiyaç | Durum | Eksik parça |
|---|---|---|
| Ürün yükleme | ✅ VAR (onay kapılı) | Görsel yükleme yalnız URL ile; dosya upload yok |
| Ürün güncelleme | ✅ VAR (revizyon da onaydan geçer) | Onay SLA'sı/kısmi alan güncellemesi değerlendirilebilir |
| Stok güncelleme | ✅ VAR (mutlak) | Toplu/varyant-listesi ucu yok (tek tek çağrı gerekir) |
| **Fiyat güncelleme** | ❌ YOK | `pricing.write` scope tanımlı ama UÇ yok; fiyat sahibi (BasePrice vs kanal fiyatı) sözleşmesi netleşmeli |
| **Sipariş işlemleri** | ❌ YOK | `GET /orders` (satıcıya düşen paketler), kalem kabul/red?, iptal görünürlüğü; F2b-2d "order.write hiçbir partner tipinde yok" engeli aslında SATIŞ senaryosunda sorun değil (satıcı sipariş OLUŞTURMAZ, okur+kargolar) |
| **Kargo işlemleri** | ❌ YOK | `fulfillment.write` scope tanımlı, uç yok: paket için taşıyıcı+takip no bildirme; bizim anlaşmalı kargoyla etiket üretimi KG fazlarına bağlı (taşıyıcı API erişimleri kullanıcıda BLOKE) |
| **Gelir paylaşımı** | ❌ HİÇ YOK — EN BÜYÜK BOŞLUK | Komisyon modeli kodda yok: oran tanımı, satışta hakediş kaydı, iadede ters kayıt, dönemsel mutabakat, ödeme; `account.read`/`invoice.read` uçları da yok |
| Bildirim (yeni sipariş push) | ❌ YOK | Webhook altyapısı yok → v1'de satıcı poll eder |
| Rate limit / kullanım | ❌ YOK | Plandaki F5; dışa açılmadan önce şart |
| Sandbox/test hesabı | ❌ YOK | Satıcı entegrasyonu test edecek güvenli ortam kurgusu yok |

## 3. Uygulamadan önce verilmesi gereken kararlar

- **K1 Komisyon modeli:** oran nerede tanımlanır (satıcı sözleşmesi cari kartta mı, ayrı
  `definition` kataloğu mu), eksenler (satıcı × ürün grubu/kategori × belki kampanya), KDV'nin
  komisyona etkisi. Kodda hiç karşılığı yok — sıfırdan tasarlanacak.
- **K2 Sipariş görünürlük birimi:** önerim PAKET (bölme zaten SupplierId bazlı; satıcı başka
  satıcının kalemini/müşteri toplamını görmemeli — KVKK: müşteri adres bilgisinin ne kadarı
  satıcıya açılır, FulfillmentMode=supplier ise zorunlu, değilse gizli).
- **K3 Kargo sahipliği:** satıcı kendi anlaşmasıyla mı gönderir (takip no bildirir — external
  kod tipi hazır) yoksa bizim anlaşmalı kargo etiketimizle mi (KG fazları bloke). v1 = takip no bildirme.
- **K4 Hakediş dönemi:** ödeme periyodu, iade karantinası (teslimden X gün sonra hakedişe düşme),
  negatif bakiye durumu.
- **K5 Onay kapısı sınırı:** merchant tipinde fiyat/stok ONAYSIZ (tasarım kararı zaten böyle),
  içerik onaylı — teyit edilmeli.
- **K6 Push modeli:** v1 polling (GET /orders?since=), v2 webhook (imzalı, retry'lı) — webhook
  ayrı altyapı işi.

## 4. Önerilen fazlama (kaba, onay sonrası detaylanır)

1. **P1 — Fiyat + Sipariş okuma:** `PUT products/{code}/prices`, `GET orders` (paket bazlı,
   since/status filtreli), `GET orders/{packageNo}`. Mevcut altyapıyla en hızlı kazanım.
2. **P2 — Kargo bildirimi:** `POST orders/{packageNo}/shipment` (taşıyıcı+takip no →
   Shipment zinciri + durum geri beslemesi), paket durum makinesi eşlemesi.
3. **P3 — Gelir paylaşımı:** komisyon tanımı (K1) + satış/iade anında `ConceptCode='hakedis'`
   defterine kayıt + `GET account/statement`, `GET settlements` uçları + panel mutabakat ekranı.
4. **P4 — Sertleştirme:** rate limit, kullanım/audit ekranı, sandbox hesap tipi, webhook.
5. **P5 — Panel karşılıkları (K16):** satıcı sözleşme/komisyon yönetimi, hakediş raporları,
   paket operasyon ekranlarında satıcı ayrımı.

## 5. Riskler / notlar

- Gelir paylaşımı muhasebe işidir: hakediş kayıtları YALNIZ PostAccountTransaction'dan geçmeli
  (cari çatı altın kuralı); iade/iptal ters kayıtları atlanırsa satıcıya fazla ödeme çıkar.
- Paket birimi seçilmezse (K2) müşteri mahremiyeti ve çok-satıcılı sipariş bölünmesi sorun üretir.
- Rate limit olmadan fiyat/stok uçları dışa açılmamalı (bugün hiç yok).
- Satıcı paneli (insan) ile API (makine) yetkileri ayrı kimliklerde kalmalı (tasarım §8 notu).
