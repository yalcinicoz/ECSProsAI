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

## 3. Kararlar — KULLANICI YANITLARI İŞLENDİ (2026-08-11)

### K1 Komisyon modeli ✅ KARAR: esnek, katmanlı, ama anlaşılır
Kullanıcı kararı: ürün grubu bazında VARSAYILAN oranlar (dokümanlarda yayınlanır); satıcı
sözleşmesine ÖZEL oranlar olabilir (yine ürün grubu bazında değerlendirilir); satıcı CİROSUNA
göre OTOMATİK ayarlanabilir; KAMPANYAYA özel oranlar olabilir; gerekirse ÜRÜN bazlı oran.

**Tasarım sonucu — beş katman + tek "etkin oran" çözücüsü (öneri, onaya tabi):**
1. Ürün-bazlı özel oran (satıcı × ürün) — en özel, her şeyi ezer
2. Kampanya oranı — kampanya penceresi aktifken (kampanya × grup [× satıcı?])
3. Satıcı sözleşme oranı (satıcı × ürün grubu)
4. Ciro basamağı otomatik ayarı — 3'ü/5'i modifiye eder (basamak tablosu: dönem cirosu aralığı → oran)
5. Platform varsayılanı (ürün grubu bazlı) — dokümante edilen taban

- "Kolay anlaşılır" şartının karşılığı: her hakediş kaydına **hangi katmanın uygulandığı yazılır**
  (kural kodu + oran); satıcı panelinde "bu satışta oran neden %X" tek bakışta görünür.
- Çözücü tek serviste yaşar (etkin oran = f(satıcı, ürün, grup, tarih, kampanya, dönem cirosu));
  oran tabloları platform yönetimindedir (definition şeması kuralına uygun: satıcılar/aktarımlar yazamaz).

**Alt-karar (2026-08-11) — ciro basamağı:** dönem tipi TANIMDA SEÇİLİR — üç seçenek de
kullanılabilir: aylık / yıllık / kayan 12 ay (basamak tablosu tanımına dönem tipi alanı girer).
Basamak değişiminin yürürlüğü için önerilen varsayılan: **sonraki dönem başı** (öngörülebilirlik;
"anında" istenirse tanım bazında seçilebilir yapılır) — uygulamada netleştirilecek küçük detay.

**Alt-karar (2026-08-11) — kampanya oranı = maliyet paylaşımı + satıcı katılımı:**
- Kampanya tanımında indirim yükünün paylaşımı AÇIKÇA belirtilir: yalnız pazaryeri / kısmen
  satıcı / tamamen satıcı (oran veya yüzde paylaşımı alanı).
- Pazaryeri bir kampanya tanımladığında satıcılar KATILIM kararı verir (opt-in) ve HANGİ
  ürünlerle katılacaklarını seçebilir → Promotion modülüne "kampanya × satıcı katılımı"
  (katılım durumu + satıcının ürün listesi) kavramı eklenmeli; hakediş kaydına kampanya
  paylaşım kuralı da yazılır. Bu, satıcı paneli/API'ye "açık kampanyalar / katıl / ürün seç"
  uçlarını da getirir (P3a kapsamına, panel karşılığı P5'e).

### K2 Sipariş görünürlüğü ✅ KARAR: paket bazlı + kısıtlı müşteri verisi + relay e-posta
Kullanıcı kararı: ad, soyad, adres PAYLAŞILIR; telefon, e-posta, cinsiyet, doğum yeri/tarihi
PAYLAŞILMAZ. Trendyol modeli benimsenir: müşteri başına ÜRETİLMİŞ benzersiz (relay) e-posta
adresi satıcıya verilir; satıcının fatura entegratörü faturayı bu adrese gönderir → biz yakalayıp
(a) müşterinin Hesabım/faturalar sayfasına düşürür, (b) istenirse gerçek e-postasına iletiriz.

**Tasarım sonucu:** relay e-posta ayrı bir alt-sistemdir — kendi domainimizde inbound mail alma
(catch-all), gelen faturayı relay adresten üyeye eşleme, ek/doğrulama işleme. API sözleşmesinde
paket detayında müşteri alanları: `ad, soyad, adres satırları, il/ilçe, relayEmail` — başka alan yok.
- **Alt-karar (2026-08-11):** relay adres MÜŞTERİ BAZLI (satıcı ayrımı yok — müşteriden
  hareketle gereken her bilgiye zaten erişiliyor). Kalan teknik seçimler (relay alt alan adı,
  inbound mail altyapısı, fatura dışı posta davranışı) P3b uygulama detayı.

### K3 Kargo sahipliği ✅ KARAR: satıcı hesabında seçime bağlı ÜÇ mod
1. `platform_contract` — tüm kargolar bizim sözleşmemiz üzerinden (biz göndeririz)
2. `seller_ships` — satıcı paketlerini kendi sözleşmesiyle KENDİSİ gönderir (API'den takip no bildirir)
3. `seller_contract_we_ship` — satıcı kendi kargo sözleşme bilgilerini hesabından girer,
   gönderimi BİZ yaparız (bizim operasyon, onun anlaşma kodları/ücretlendirmesi)

**Tasarım sonucu:** mevcut `FulfillmentMode` (Yol B) ikiliden üçlüye genişler; yalnız
`seller_ships` modunda etkin scope'a `fulfillment.write` eklenir (1 ve 3'te gönderim bizde).
Mod 3, taşıyıcı entegrasyonlarının satıcı-bazlı credential ile çalışmasını gerektirir —
kargo servis şemaları (SettingsSchema) hazır, ama credential saklama bugün platform bazlı
(`core_firm_platform_integrations`); satıcı bazlı şifreli saklama YENİ iş. Mod 3 ayrıca KG
fazlarına (gerçek taşıyıcı API'leri — kullanıcıda bloke) bağımlı; mod 1-2 bağımsız başlayabilir.

### K4 Hakediş dönemi ✅ KARAR: teslimden X gün sonra — X SATICI BAZLI
Teslim + X gün modeli onaylandı; **X satıcı bazında belirlenebilir** (alt-karar 2026-08-11 —
satıcı sözleşmesinde alan; platform varsayılanı + satıcıya özel override). Ödeme çıkış
periyodu için öneri: aynı esneklik kalıbı — platform varsayılanı (örn. haftalık) + satıcı
sözleşmesinde override; uygulamada netleştirilecek.

### K5 Onay kapısı ✅ KARAR: mevcut çözüm uygun
İçerik onay kapılı; fiyat/stok merchant tipinde onaysız — teyit edildi.

### K6 Push modeli ✅ KARAR: öneri uygun
v1 polling (`GET /orders?since=`), v2 imzalı+retry'lı webhook.

## 4. Fazlama — DURUM (2026-08-11)

✅ **P1 CANLIDA** (a685760) · ✅ **P2 CANLIDA** (9b74425) · ✅ **P3a UYGULANDI** (2f1d83b,
restart bekliyor): komisyon veri modeli + 5-katmanlı çözücü + teslim tetikli hakediş satırları
(OrderDeliveredEvent YENİ) + 30 dk uygunlaşma worker'ı ('hakedis' defteri, PostAccountTransaction
AccountId hedefli) + iade tersi + kampanya maliyet paylaşımı & opt-in + partner
settlements/statement/campaigns uçları + admin /api/commission/* + panel /commission (4 sekme).
KALAN: P3b relay e-posta, P4 rate limit/sandbox/webhook, satıcı opt-in'inin SİTE kampanya
kapsamına uygulanması (şu an yalnız hakedişte etkili) ve K3 mod 3 (KG fazlarına bağlı).

## 4-eski. Önerilen fazlama (K1-K6 kararları sonrası güncellendi, 2026-08-11)

1. **P1 — Fiyat + Sipariş okuma:** `PUT products/{code}/prices`, `GET orders` (paket bazlı,
   since/status filtreli; K2 alan kısıtları relay e-posta HARİÇ uygulanır — relay P3b'de gelene
   dek e-posta alanı hiç dönmez), `GET orders/{packageNo}`. Mevcut altyapıyla en hızlı kazanım.
2. **P2 — Kargo:** K3 mod 1-2 (mod 2: `POST orders/{packageNo}/shipment` taşıyıcı+takip no →
   Shipment zinciri; mod 1: paket zaten bizim operasyonda). Mod 3 KG fazlarına bağımlı — ayrı iş.
3. **P3a — Gelir paylaşımı çekirdeği:** K1 beş-katmanlı oran tabloları + etkin-oran çözücüsü +
   satış/iade anında `ConceptCode='hakedis'` defterine kayıt (uygulanmış katman izli) + teslim+X
   uygunlaşma + `GET account/statement`, `GET settlements` uçları + panel mutabakat ekranı.
4. **P3b — Relay e-posta alt-sistemi (K2):** inbound mail + üye eşleme + Hesabım fatura görünümü
   + gerçek adrese iletim; paket detayına `relayEmail` alanı bu fazda eklenir.
5. **P4 — Sertleştirme:** rate limit, kullanım/audit ekranı, sandbox hesap tipi, webhook (K6 v2).
6. **P5 — Panel karşılıkları (K16):** satıcı sözleşme/komisyon/ciro basamağı yönetimi, hakediş
   raporları, satıcı kargo modu + (mod 3) sözleşme bilgisi ekranı, paket operasyonunda satıcı ayrımı.

## 5. Riskler / notlar

- Gelir paylaşımı muhasebe işidir: hakediş kayıtları YALNIZ PostAccountTransaction'dan geçmeli
  (cari çatı altın kuralı); iade/iptal ters kayıtları atlanırsa satıcıya fazla ödeme çıkar.
- Paket birimi seçilmezse (K2) müşteri mahremiyeti ve çok-satıcılı sipariş bölünmesi sorun üretir.
- Rate limit olmadan fiyat/stok uçları dışa açılmamalı (bugün hiç yok).
- Satıcı paneli (insan) ile API (makine) yetkileri ayrı kimliklerde kalmalı (tasarım §8 notu).
