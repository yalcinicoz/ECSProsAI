# Kampanya Uçtan Uca Akış — Tasarım ve Uygulama Planı

**Tarih:** 2026-07-31
**Durum:** PLAN (kod yazılmadan önce hizalanma dokümanı — kullanıcı "önce plan dokümanı" dedi)
**Çalışma alanı:** Ağırlıklı 🌐 Web sitesi (storefront gösterim + checkout); dokunulan katmanlar
Promotion (backend) + Catalog/Storefront (kart/detay) + Order (checkout) + Admin panel.

---

## 1. Amaç

Kullanıcının hedef akışı: **kampanya tasarla → kampanyaya ürün ilişkilendir → listelerde ve
detayda kampanya bilgisini göster → kampanyalı fiyatı hesapla → satın alma sürecine yansıt.**

Bugün bu akış **uçtan uca bağlı değil.** Bu doküman mevcut envanteri, açık tasarım kararlarını
ve fazlı uygulama planını tanımlar.

---

## 2. Mevcut Durum Envanteri (2026-07-31 kod taraması)

### Var olan
- **Veri modeli (Promotion.Domain):** `Campaign` (tip, firma, kod, i18n ad, tarih aralığı,
  `IsActive`, `Priority`, `Settings` jsonb, `ProductSelectionType`, `ProductFilter` jsonb),
  `CampaignType` (`HandlerClass`, `SettingsSchema`, `RequiresProducts`, `IsStackable`),
  `CampaignProduct` (ProductId **veya** VariantId), `CampaignExclusion`, `CampaignPlatform`.
- **Kampanya tipleri (seed, 4 adet):** `percentage_discount` (yüzde), `fixed_discount` (sabit
  tutar), `buy_x_get_y` (çok al az öde), `min_cart_discount` (sepet eşiği → yüzde; **tek
  stackable olan**).
- **Hesap motoru:** `CampaignEngine.Calculate` — 4 tipin tümü implement edilmiş; **sepet
  seviyesinde** `DiscountLine` (kampanya başına indirim tutarı + etkilenen varyantlar) üretir.
- **Uygulama sorgusu:** `CalculateDiscountsQuery` → aktif kampanyaları önceliğe göre çeker,
  stackable/non-stackable ayrımı yapar (non-stackable yalnız en yüksek öncelikli uygulanır).
- **Admin CRUD:** `CampaignsPage.tsx` (oluştur/düzenle: ad, tip, tarih, öncelik, tipe göre
  Settings alanları), `CreateCampaign`/`UpdateCampaign` komutları, `GetCampaigns`.
- **Kupon akışı (kampanyadan ayrı):** `/api/promotion/coupon/validate|use`, checkout istemciden
  gelen `CouponDiscount`'u sipariş toplamına uygular. **Kampanyalarla karıştırılmamalı.**
- **Ana sayfa vitrin:** `PageBlockSourceResolver` `campaign` blok kaynağı →
  `GetActiveCampaignProductRefsQuery` kampanyadaki ürünleri carousel'de listeler (yalnız ürün
  listesi; fiyat/rozet değil).

### Eksik (bu planın konusu)
1. **Ürün ilişkilendirme fiilen çalışmıyor:** `CampaignsPage` `productSelectionType`'ı sabit
   `'all'` gönderiyor (ekranda "ürün-özel kampanya ileri iş" notu var); `CreateCampaign`
   komutu `CampaignProduct` satırı **yazmıyor**. Backend `specific` destekliyor ama besleyen yok.
2. **Ürün-bazlı kampanya fiyatı yok:** Motor sepet indirim tutarı üretir; kart/detayda
   gösterilecek **ürün/varyant başına "kampanyalı birim fiyat"** üretmez.
3. **Storefront gösterim yok:** Kart DTO'larında ve `_UrunKarti.cshtml`'de kampanya rozeti/fiyatı
   yok; `StoreUrunDetayBuilder`'da **hiç kampanya referansı yok**.
   > Not: 2026-07-31'de eklenen çizili-fiyat indirimi `channel_variants.CompareAtPrice`'tır —
   > **kampanyadan bağımsız**. İkisinin gösterimde çakışmaması tasarlanmalı (bkz. §3.4).
4. **Checkout kampanya uygulamıyor:** `CheckoutCommand` yalnız `CouponDiscount` uygular;
   `CampaignEngine`/`CalculateDiscountsQuery` checkout'ta **hiç çağrılmıyor**. Kampanyalı fiyatla
   sipariş oluşmuyor.

### Mevcut motordaki kusurlar (planda düzeltilecek)
- **`CampaignExclusion` ve `CampaignPlatform` calculate handler'da HİÇ kullanılmıyor** → kampanya
  platform ayrımı gözetmeden ve dışlama listesine bakmadan uygulanır.
- **`FirmPlatformId` filtresi yok** → kampanya mishar kanalına özel kısıtlanamıyor (Campaign'de
  `FirmId` var, `FirmPlatformId`/`CampaignPlatform` var ama sorguda kullanılmıyor).
- **`specific` yalnız `VariantId`'ye bakıyor** → ürün seviyesinde (tüm varyantlar) seçim ve
  `ProductFilter` (kategori/marka) çözümü yok.

---

## 3. Açık Tasarım Kararları (kodlamadan önce netleşmeli)

### 3.1 Kampanya fiyatının gösterim yeri ⭐ (en kritik karar)
Referans tasarım (ptplus) kartında iki ayrı satır var: **"kampanya indirimi + hesaplanan indirimli
fiyat"** (ürün-bazlı) ve **"Sepette"** (sepete uygulanacak gerçek fiyat). Karar:
- **(A)** Ürün-bazlı gösterilebilen tipler (yüzde/sabit) kartta/detayda "kampanyalı fiyat" olarak
  gösterilir; sepet-bağımlı tipler (buy_x_get_y, min_cart) yalnız **sepette** "Sepette" rozetiyle.
- **(B)** Hiçbir kampanya kartta gösterilmez, tümü yalnız sepette.
- **Öneri: (A)** — tasarıma sadık; ama tip başına "ürün-bazlı gösterilebilir mi" bayrağı gerekir.

### 3.2 Ürün seçim granülaritesi
`ProductSelectionType`: `all` / `specific` (ürün **ve/veya** varyant) / `filter` (kategori/marka/
attribute — `ProductFilter` jsonb). İlk fazda hangileri? **Öneri:** `all` + `specific`(ürün+varyant);
`filter` sonraki iş.

### 3.3 Öncelik / stackable / dışlama / platform kuralları
- Kampanya **hangi FirmPlatformId(ler)** için geçerli? (`CampaignPlatform` doldurulmalı, sorgu
  filtrelemeli.) **Öneri:** platform seçimi zorunlu; boşsa firma geneli.
- **Dışlama** (`CampaignExclusion`) motorda uygulanmalı (kampanya kapsamı − dışlanan ürünler).
- **Stackable/öncelik:** ürün-bazlı gösterimde bir ürüne **birden çok kampanya** denk gelirse
  hangisi gösterilir? **Öneri:** en yüksek öncelikli non-stackable tek kampanya ürün fiyatını
  belirler; stackable olanlar (min_cart gibi) yalnız sepette eklenir.

### 3.4 Kanal fiyatı ↔ CompareAtPrice ↔ kampanya ilişkisi
Bugün: satış fiyatı = kanal fiyatı; "eski fiyat" (çizili) = `CompareAtPrice`. Kampanya gelince
gösterim önceliği tanımlanmalı. **Öneri:**
- Referans = kanal satış fiyatı.
- Kampanyalı fiyat = kanal fiyatına kampanya indirimi uygulanmış hâli (kartta yeni "kampanyalı"
  satır, kampanya adı/rozetiyle).
- `CompareAtPrice` (indirim öncesi) çizili kalır; kampanya ayrı bir kazanç olarak gösterilir —
  **çift indirim karışıklığı olmaması için** tek "nihai ödenecek" fiyat vurgulanır.

### 3.5 Satın almada güven (fraud)
2026-07-31'de checkout **sunucu-taraflı fiyat doğrulama** eklendi (istemci fiyatına güvenilmez).
Kampanya fiyatı da **aynı yerde sunucuda** hesaplanmalı; istemciden gelen kampanyalı fiyata
güvenilmez. (bkz. `CheckoutCommand` `IChannelPricingService` recompute bloğu.)

---

## 4. Önerilen Mimari ve Fazlar

Ortak fikir: **tek kural seti, iki görünüm.** Kampanya çözümleme mantığı (aktiflik, platform,
kapsam, dışlama, öncelik, stackable) tek serviste toplanır; hem **vitrin fiyatı görünümü**
(ürün-bazlı, F3) hem **sepet/sipariş görünümü** (F4) bu servisten beslenir. Böylece kart, detay,
sepet ve sipariş aynı sonucu verir.

### FAZ 1 — Ürün ilişkilendirme (backend + admin UI)
- `CreateCampaign`/`UpdateCampaign` komutlarına ürün/varyant listesi + `ProductSelectionType` +
  (opsiyonel) `ProductFilter`; komut `CampaignProduct` satırlarını yazsın (idempotent update).
- `CampaignPlatform` (hangi kanallar) + `CampaignExclusion` (istisna ürünler) yazımı.
- Admin `CampaignsPage`: tip/ayar formuna ek olarak **kapsam sekmesi** — "tüm ürünler / seçili
  ürünler / filtre"; ürün arama-ekle listesi, platform seçimi, dışlama listesi.
- **K16:** önce ekran kurgusunu konuşalım (kapsam sekmesi mockup'ı).

### FAZ 2 — Kampanya çözümleme servisi (ortak çekirdek)
- Yeni servis `IProductCampaignResolver` (Promotion.Application):
  girdi = (FirmPlatformId, ürün/varyant kümesi) → çıktı = varyant/ürün başına **etkin kampanya**
  (kampanya id/kod/ad/tip + hesaplanan **kampanyalı birim fiyat** *veya* "sepette geçerli" bayrağı).
- Aktiflik + tarih + **platform** + **kapsam(specific/all/filter)** + **dışlama** + **öncelik/
  stackable** kurallarını burada topla (mevcut `CalculateDiscountsQuery`'deki eksikleri gider).
- Sepet motoru (`CampaignEngine`) bu servisle aynı kuralları paylaşsın (kod tekrarı olmadan).

### FAZ 3 — Storefront gösterim (kart + detay)
- Kart DTO'larına kampanya alanları: `ChannelCategoryProductItemDto` + `StoreProductDto` →
  `CampaignName`, `CampaignBadge`, `CampaignPrice?` (ürün-bazlı gösterilebiliyorsa), `InCartOnly`
  (sepette geçerli tip). Zenginleştirme **cache dışında** yapılabilir (puan/video gibi) → TTL
  beklemeden taze; ya da cache anahtarına kampanya sürümü eklenir.
- `_UrunKarti.cshtml`: tasarımdaki "kampanya indirimi + hesaplanan fiyat" ve "Sepette" satırları
  (ilgili `ms-urun-fiyat-indirimli` / `ms-urun-fiyat-sepette` sınıfları zaten CSS'te var).
  Continuation JSON otomatik taşır (DTO alanı → `normallestir`).
- `StoreUrunDetayBuilder`: detayda kampanya adı/rozeti + kampanyalı fiyat.

### FAZ 4 — Checkout entegrasyonu
- `CheckoutCommand` sunucu-taraflı fiyat bloğunda: her kaleme ürün-bazlı kampanya fiyatını uygula
  (F2 servisi), `OrderItem.UnitPrice`/`DiscountAmount`'a yansıt; sepet-seviyesi kampanyalar
  (buy_x_get_y, min_cart) sipariş indirimi (`TotalDiscount`) olarak eklensin.
- Sepet/ödeme özet ekranlarında kampanya satırları görünsün (mevcut kupon satırıyla tutarlı).
- Kampanya + kupon birlikte kuralları (§3.3 stackable) netleştirilip uygulanır.

---

## 5. Kapsam Dışı / Sonraki
- `ProductFilter` (kategori/marka/attribute ile dinamik kapsam) — F1'de temel, gelişmişi sonra.
- Kampanya kullanım limiti / kişi başı limit / analitik.
- Kupon akışıyla birleşik "en iyi indirim" optimizasyonu.

---

## 6. Riskler / Notlar
- **Performans:** liste kartlarında ürün başına kampanya çözümü toplu (batch) yapılmalı; N+1
  olmamalı. Puan/video zenginleştirme deseni örnek alınır.
- **Cache:** kampanya başlangıç/bitişinde vitrin fiyatı değişir; kısa TTL veya post-cache
  zenginleştirme tercih edilir (kampanya aktivasyonu anında yansısın).
- **Paylaşımlı Redis:** DTO'ya alan eklenince cache sürümü artırılmalı (2026-07-31 dersi).
- **Fraud:** kampanyalı fiyat **daima sunucuda** hesaplanır; istemciden gelen fiyata güvenilmez.
