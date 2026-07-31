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

---

## 2.5 Mimari İlkeler (kullanıcı yönü, 2026-07-31) ⭐

Bu ilkeler şema ve akış tasarımını belirler:

1. **Kampanya TİPİ = platformdan bağımsız yetenek (definition katmanı).** Tip/yapı/şema
   tanımları global; yalnız geliştirici firma (platform yönetimi) tanımlar. → `CampaignType`
   `definition` şemasına taşınır; `definition.*` altın kuralı geçerli (veri aktarımı/platform
   bu tabloya kayıt EKLEYEMEZ). Bkz. `feedback_definition_schema`.
2. **Kampanyayı PLATFORMLAR uygular.** Minimum sepet tutarı, indirim oranı, hangi ürünler, geçerlilik
   zaman aralığı → hepsini platform (kiracı) belirler. → `Campaign` platform örneğidir
   (FirmPlatformId/`CampaignPlatform`), tip şablonunu doldurur.
3. **Kampanya–ürün ilişkilendirme = kategori–ürün ilişkilendirme ile BİREBİR AYNI.** Kategori
   mekanizması: `FillType` ∈ **manual | filter | mixed**, `FilterDef` jsonb (CategoryFilterRules),
   manuel liste materyalize (`channel_category_products`). Kampanya da aynısını kullanır:
   `Campaign.FillType` + `Campaign.FilterDef` + `campaign_products` (materyalize). **Aynı
   `ProductFilterHelper`/filtre motoru paylaşılır** — ikinci bir filtre dili yazılmaz.
4. **Tip tanımı yapıldığı an parametre giriş ŞABLONU üretilir; platform tipi aktifleştirince
   şablonu doldurur.** → `CampaignType.SettingsSchema` (JSON alan tanımı) admin formunu üretir;
   platform `Campaign.Settings` (jsonb) ile doldurur. **Bu, projede zaten var olan
   `SettingsSchemaJson`/`PlatformSchemaField` deseninin aynısıdır** (entegrasyon servisleri
   firma formu böyle üretiliyor — CLAUDE.md). Aynı desen kampanyaya uygulanır.
5. **Yatay genişlemeyen (vertical) yapı.** Eski `plkampanyalar` her parametre için ayrı kolon
   tutuyordu (`indirimOrani`, `indirimTutari`, `alinacakUrunSayisi`, …) → yeni tip eklenince
   yetersiz. Yeni yapıda parametreler **tip şablonunda (SettingsSchema) + örnekte (Settings)
   JSON** olarak durur. Yeni tip = yeni definition satırı + yeni handler; **şema migration'ı
   gerekmez.**

---

## 2.6 Eski Sistem Kampanya Tipleri (envanter) ve Birleştirme

Eski MySQL'de `dfindirimtipleri` (25 tip tanımı) + `plkampanyalar` (parametreler, yatay kolonlar).
Canlı kullanım (plkampanyalar, en çok kullanılan): **17** Seçili Ürünler % (60), **3** Sepet→indirimli
ürün (17), **20** Tüm ürünler % (15), **1** Sepet %/tutar (14), **7** Seçili sepette % (5),
**13** al-x-öde-y (4), **24** Kargo (4), **22** Resimli yorum (3), **19** Üye grubu (1), **23** (1),
**25** Kredi kartı kargo (1). Filtre 57/131 kampanyada, üye-tipi 129/131'de kullanılmış.

**Birleştirme (25 eski tip → ~6 parametrik yeni tip + 3 kesişen boyut):**

| Yeni tip (definition) | Kapsadığı eski tipler | SettingsSchema parametreleri (özet) |
|---|---|---|
| **`discount`** (kapsam+koşul+fayda) | 1,4,5,7,8,10,11,17,20 | `scope`(cart\|products), `condition`{type: none\|cart_amount\|cart_qty\|scope_qty\|scope_amount, value}, `benefit`{type: percent\|amount, value, maxAmount?} |
| **`buy_x_get_y`** (al X, Y bedava/indirimli) | 3,6,9,12,13,14 | `buyQty`, `getQty`, `getBenefit`{percent(100=bedava)\|amount}, `sameProduct`, `giftScope` |
| **`cross_group_gift`** (grup al → grup hediye) | 15,23 | `buyGroups`, `buyThreshold`, `giftGroups`, `giftQty/amount`, `giftBenefit` |
| **`bundle`** (kombin) | 16 | `bundleGroups[]`, `bundlePrice` \| `bundleDiscount` |
| **`free_shipping`** (kargo) | 24,25 | `threshold`(cart_amount), `paymentMethods[]?`(kredi kartı), `regions?` |
| **`review_reward`** (resimli yorum) | 22 | `trigger`=photo_review, `benefit` |

**Kesişen boyutlar (TİP DEĞİL — her kampanyaya uygulanabilen niteleyiciler):**
- **Hedef kitle / üye grubu** (eski `uyeTipId`, tip 19): üye grubu hedefleme kampanya seviyesinde
  bir **audience** ayarıdır, ayrı tip değil (eski tip 19 → `discount` + audience=üye-grubu).
- **Kupon kapısı** (eski 18,21): kupon akışı yeni projede zaten ayrı; kampanya opsiyonel kupon
  koşulu taşıyabilir.
- **Ürün seçimi** (manual/filter/mixed): ürün-kapsamlı tiplere uygulanır (§2.5.3).

> Mevcut 4 seed tip (`percentage_discount`/`fixed_discount`/`buy_x_get_y`/`min_cart_discount`)
> yukarıdaki `discount`+`buy_x_get_y` altında birleşir — seed ve motor buna göre yenilenir.

---

## 2.7 Hedef Şema (özet)

- **`definition.campaign_types`** (global, platformdan bağımsız): `Code`, `NameI18n`,
  `DescriptionI18n`, `SettingsSchema` jsonb (parametre şablonu — form üretir), `HandlerClass`,
  `Scope`(cart/product/shipping…), `SupportsProductSelection` bool, `IsStackable`, `IsActive`,
  `SortOrder`. Yalnız `definition.manage` yetkisi yazar.
- **`promotion.campaigns`** (platform örneği): `CampaignTypeId`, **`FirmPlatformId`** (platform
  uygular), `Code`, `NameI18n`, `StartsAt`/`EndsAt`, `IsActive`, `Priority`, `Settings` jsonb
  (şablon değerleri), **`FillType`**(manual/filter/mixed), **`FilterDef`** jsonb, `Audience` jsonb
  (üye grubu vb.), `Badge`/etiket alanları. (Mevcut `ProductSelectionType`/`ProductFilter`
  bunlara göre yeniden adlandırılır.)
- **`promotion.campaign_products`** (manuel/materyalize seçim — kategoriyle aynı): `CampaignId`,
  `ProductId`/`VariantId`, `AddedType`.
- Platform kapsamı: `campaign.FirmPlatformId` (tekil) veya çok-platform gerekiyorsa
  `campaign_platforms`; dışlama `campaign_exclusions` (motor bunları UYGULAMALI — bugün etmiyor).

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

### FAZ 0 — Tip tanım altyapısı (definition) + tip konsolidasyonu
- `CampaignType`'ı `definition.campaign_types`'a taşı; `SettingsSchema` (parametre şablonu) +
  `Scope`/`SupportsProductSelection` alanları. Seed'i §2.6 birleştirilmiş tip setiyle yenile
  (`discount`, `buy_x_get_y`, `cross_group_gift`, `bundle`, `free_shipping`, `review_reward`).
- Admin tip yönetimi `definition.manage` yetkisiyle (super_admin/platform_admin); şablon (JSON)
  editörü. Okuma herkese açık (platform kampanya formu dropdown'ı buradan beslenir).

### FAZ 1 — Kampanya oluşturma + ürün ilişkilendirme (platform tarafı)
- `Campaign` platform örneği: `FirmPlatformId`, tip seçimi, **SettingsSchema'dan üretilen form**
  (şablonu doldur → `Settings` jsonb), tarih aralığı, öncelik, etiket/badge.
- **Ürün ilişkilendirme = kategori mekanizmasının aynısı:** `FillType`(manual/filter/mixed) +
  `FilterDef` jsonb + `campaign_products` materyalize. **`ProductFilterHelper` tekrar kullanılır.**
- `CreateCampaign`/`UpdateCampaign` komutları bunları yazsın (idempotent); dışlama + platform +
  audience(üye grubu) dahil.
- Admin `CampaignsPage`: tip formu (şablondan) + **kapsam sekmesi kategori ekranıyla aynı UX**
  (manuel arama-ekle / filtre kuralları / ikisi). **K16:** önce ekran kurgusunu konuşalım.

### FAZ 2 — Kampanya çözümleme servisi (ortak çekirdek)
- Yeni servis `IProductCampaignResolver` (Promotion.Application):
  girdi = (FirmPlatformId, ürün/varyant kümesi) → çıktı = varyant/ürün başına **etkin kampanya**
  (kampanya id/kod/ad/tip + hesaplanan **kampanyalı birim fiyat** *veya* "sepette geçerli" bayrağı).
- Aktiflik + tarih + **platform (FirmPlatformId)** + **kapsam (FillType/FilterDef → aynı filtre
  motoru)** + **dışlama** + **audience(üye grubu)** + **öncelik/stackable** kurallarını burada
  topla (mevcut `CalculateDiscountsQuery`'deki eksikleri gider).
- Sepet motoru (`CampaignEngine`) bu servisle **aynı kuralları paylaşsın** (tek kural seti).
- Tipe göre handler (`HandlerClass`) — yeni tip eklenince yalnız handler + definition satırı.

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
