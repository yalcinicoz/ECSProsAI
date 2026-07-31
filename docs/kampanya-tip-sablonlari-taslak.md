# Kampanya Tip Şablonları — Taslak (Faz 0)

**Tarih:** 2026-07-31 · **Durum:** TASLAK (gözden geçirilecek, kod yok)
**Bağlam:** `docs/kampanya-uctan-uca-plani.md` §2.6/2.7/Faz 0. Her kampanya tipi `definition.campaign_types`
satırıdır; `SettingsSchema` (aşağıdaki JSON) admin formunu üretir, platform `Campaign.Settings` ile doldurur.

---

## 1. Alan şekli — `CampaignSchemaField`

Mevcut `PlatformSchemaField` (key/labelI18n/type/required/help) yetiyordu ama kampanya
parametreleri seçim kutusu, birim, aralık ve **koşullu görünürlük** ister. Genişletilmiş şekil:

```jsonc
{
  "key": "benefitValue",
  "labelI18n": { "tr": "İndirim Değeri" },
  "type": "percent | money | integer | boolean | select",
  "required": true,
  "unit": "% | ₺ | adet",                       // opsiyonel gösterim birimi
  "min": 0, "max": 100,                          // opsiyonel aralık doğrulama
  "default": null,                               // opsiyonel varsayılan
  "options": [                                   // yalnız type=select
    { "value": "percent", "labelI18n": { "tr": "Yüzde" } },
    { "value": "amount",  "labelI18n": { "tr": "Tutar" } }
  ],
  "visibleWhen": { "field": "conditionType", "notEquals": "none" }, // opsiyonel koşul
  "helpI18n": { "tr": "Yüzde için tavan tutar (opsiyonel)." }
}
```

> Kesişen boyutlar (üye-grubu audience, kupon kapısı, **ürün seçimi = FillType/FilterDef**, etiket/
> badge, tarih, öncelik) **tip şablonunda DEĞİL**, kampanya seviyesindedir (§2.5). Şablon yalnız
> tipe özgü indirim parametrelerini tanımlar.

---

## 2. Tip şablonları

### 2.1 `discount` — Kapsam + Koşul + Fayda
Kapsar: 1, 4, 5, 7, 8, 10, 11, 17, 20. Tek parametrik tip; en çok kullanılan grubu tek şablonda toplar.

```jsonc
[
  { "key": "applyTo", "type": "select", "required": true, "default": "selected",
    "labelI18n": { "tr": "İndirim nereye" },
    "options": [
      { "value": "cart",     "labelI18n": { "tr": "Sepet toplamına" } },
      { "value": "selected", "labelI18n": { "tr": "Kapsamdaki ürünlere" } }
    ],
    "helpI18n": { "tr": "Kapsam = kampanyaya ilişkili ürünler (tümü/filtre/manuel)." } },

  { "key": "conditionType", "type": "select", "required": true, "default": "none",
    "labelI18n": { "tr": "Koşul (eşik)" },
    "options": [
      { "value": "none",        "labelI18n": { "tr": "Koşulsuz" } },
      { "value": "cartAmount",  "labelI18n": { "tr": "Sepet tutarı ≥" } },
      { "value": "cartQty",     "labelI18n": { "tr": "Sepet adedi ≥" } },
      { "value": "scopeAmount", "labelI18n": { "tr": "Kapsam tutarı ≥" } },
      { "value": "scopeQty",    "labelI18n": { "tr": "Kapsam adedi ≥" } }
    ] },
  { "key": "conditionValue", "type": "number", "required": true, "min": 0,
    "labelI18n": { "tr": "Eşik değeri" },
    "visibleWhen": { "field": "conditionType", "notEquals": "none" } },

  { "key": "benefitType", "type": "select", "required": true, "default": "percent",
    "labelI18n": { "tr": "İndirim şekli" },
    "options": [
      { "value": "percent", "labelI18n": { "tr": "Yüzde (%)" } },
      { "value": "amount",  "labelI18n": { "tr": "Tutar (₺)" } }
    ] },
  { "key": "benefitValue", "type": "number", "required": true, "min": 0, "unit": "%|₺",
    "labelI18n": { "tr": "İndirim değeri" } },
  { "key": "maxDiscountAmount", "type": "money", "required": false, "min": 0,
    "labelI18n": { "tr": "En çok indirim (₺)" },
    "visibleWhen": { "field": "benefitType", "equals": "percent" },
    "helpI18n": { "tr": "Yüzde indirimde uygulanacak tavan tutar (opsiyonel)." } }
]
```
Örnek doldurulmuş `Settings` (eski tip 17 "Seçili Ürünler %20"): `{ "applyTo": "selected",
"conditionType": "none", "benefitType": "percent", "benefitValue": 20 }`.
Eski tip 1 "Sepet 500₺ üstüne %10": `{ "applyTo": "cart", "conditionType": "cartAmount",
"conditionValue": 500, "benefitType": "percent", "benefitValue": 10 }`.

### 2.2 `buy_x_get_y` — Al X, Y bedava/indirimli
Kapsar: 3, 6, 9, 12, 13, 14. ("3 al 2 öde", "1 alana 1 bedava", "ikincisi %50", "x adet y lira".)

```jsonc
[
  { "key": "buyQuantity",  "type": "integer", "required": true, "min": 1, "unit": "adet",
    "labelI18n": { "tr": "Alınacak adet (X)" } },
  { "key": "getQuantity",  "type": "integer", "required": true, "min": 1, "unit": "adet",
    "labelI18n": { "tr": "İndirimli/bedava adet (Y)" } },
  { "key": "getBenefitType", "type": "select", "required": true, "default": "free",
    "labelI18n": { "tr": "Y ürünlerine uygulanan" },
    "options": [
      { "value": "free",    "labelI18n": { "tr": "Bedava (%100)" } },
      { "value": "percent", "labelI18n": { "tr": "Yüzde indirim" } },
      { "value": "amount",  "labelI18n": { "tr": "Sabit fiyat/tutar" } }
    ] },
  { "key": "getBenefitValue", "type": "number", "required": true, "min": 0,
    "labelI18n": { "tr": "Y indirim değeri" },
    "visibleWhen": { "field": "getBenefitType", "notEquals": "free" } },
  { "key": "sameProduct", "type": "boolean", "default": true,
    "labelI18n": { "tr": "Aynı üründen olmalı" },
    "helpI18n": { "tr": "Eski urunlerAyniOlmali. Kapalıysa kapsamdaki farklı ürünler karışabilir." } },
  { "key": "cheapestGetsBenefit", "type": "boolean", "default": true,
    "labelI18n": { "tr": "En ucuz olan indirimli" } }
]
```

### 2.3 `cross_group_gift` — Grup al → başka grup hediye/indirimli
Kapsar: 15, 23. **İki ürün kümesi gerekir** (alım kümesi = kampanya ürün seçimi; **hediye kümesi
ayrı**). Hediye kümesi kampanya seviyesinde ikinci bir seçim bloğu (`giftFillType`/`giftFilterDef`/
`giftProducts`) — form kapsam sekmesinde iki bölüm.

```jsonc
[
  { "key": "buyThresholdType", "type": "select", "required": true, "default": "qty",
    "labelI18n": { "tr": "Alım koşulu" },
    "options": [
      { "value": "qty",    "labelI18n": { "tr": "Adet ≥" } },
      { "value": "amount", "labelI18n": { "tr": "Tutar ≥" } }
    ] },
  { "key": "buyThresholdValue", "type": "number", "required": true, "min": 1,
    "labelI18n": { "tr": "Alım eşiği" } },
  { "key": "giftQuantity", "type": "integer", "required": true, "min": 1, "unit": "adet",
    "labelI18n": { "tr": "Hediye/indirimli adet" } },
  { "key": "giftBenefitType", "type": "select", "required": true, "default": "free",
    "labelI18n": { "tr": "Hediye grubuna uygulanan" },
    "options": [
      { "value": "free",    "labelI18n": { "tr": "Bedava" } },
      { "value": "percent", "labelI18n": { "tr": "Yüzde" } },
      { "value": "amount",  "labelI18n": { "tr": "Tutar" } }
    ] },
  { "key": "giftBenefitValue", "type": "number", "required": false, "min": 0,
    "labelI18n": { "tr": "Hediye indirim değeri" },
    "visibleWhen": { "field": "giftBenefitType", "notEquals": "free" } }
]
```

### 2.4 `bundle` — Kombin
Kapsar: 16. Kombindeki ürün kümesi = kampanya ürün seçimi (min ürün sayısı ile).

```jsonc
[
  { "key": "minBundleItems", "type": "integer", "required": true, "min": 2, "unit": "adet",
    "labelI18n": { "tr": "Kombin minimum ürün" } },
  { "key": "bundleBenefitType", "type": "select", "required": true, "default": "percent",
    "labelI18n": { "tr": "Kombin fiyatı" },
    "options": [
      { "value": "fixedPrice", "labelI18n": { "tr": "Sabit paket fiyatı" } },
      { "value": "percent",    "labelI18n": { "tr": "Yüzde indirim" } },
      { "value": "amount",     "labelI18n": { "tr": "Tutar indirim" } }
    ] },
  { "key": "bundleBenefitValue", "type": "number", "required": true, "min": 0,
    "labelI18n": { "tr": "Kombin değeri" } }
]
```

### 2.5 `free_shipping` — Kargo kampanyası
Kapsar: 24, 25. (25 = kredi kartı ödemede kargo → `paymentMethods`.)

```jsonc
[
  { "key": "thresholdType", "type": "select", "required": true, "default": "none",
    "labelI18n": { "tr": "Koşul" },
    "options": [
      { "value": "none",       "labelI18n": { "tr": "Koşulsuz" } },
      { "value": "cartAmount", "labelI18n": { "tr": "Sepet tutarı ≥" } }
    ] },
  { "key": "thresholdValue", "type": "money", "required": true, "min": 0,
    "labelI18n": { "tr": "Sepet eşiği (₺)" },
    "visibleWhen": { "field": "thresholdType", "equals": "cartAmount" } },
  { "key": "paymentMethods", "type": "select", "required": false, "default": "all",
    "labelI18n": { "tr": "Ödeme yöntemi kısıtı" },
    "options": [
      { "value": "all",         "labelI18n": { "tr": "Tümü" } },
      { "value": "credit_card", "labelI18n": { "tr": "Kredi kartı" } }
    ] },
  { "key": "coverage", "type": "select", "required": true, "default": "full",
    "labelI18n": { "tr": "Kargo indirimi" },
    "options": [
      { "value": "full",    "labelI18n": { "tr": "Ücretsiz" } },
      { "value": "percent", "labelI18n": { "tr": "Yüzde" } },
      { "value": "amount",  "labelI18n": { "tr": "Tutar" } }
    ] },
  { "key": "coverageValue", "type": "number", "required": false, "min": 0,
    "labelI18n": { "tr": "İndirim değeri" },
    "visibleWhen": { "field": "coverage", "notEquals": "full" } }
]
```

### 2.6 `review_reward` — Resimli yorum kampanyası
Kapsar: 22. Kenar tip — üyeye özel kazanım; tetik satın alma değil, **resimli yorum**. İlk sürümde
ertelenebilir (bkz. plan §5).

```jsonc
[
  { "key": "benefitType", "type": "select", "required": true, "default": "coupon",
    "labelI18n": { "tr": "Ödül" },
    "options": [
      { "value": "coupon",  "labelI18n": { "tr": "Kupon kodu" } },
      { "value": "percent", "labelI18n": { "tr": "Sonraki alışverişe %" } },
      { "value": "amount",  "labelI18n": { "tr": "Sonraki alışverişe ₺" } }
    ] },
  { "key": "benefitValue", "type": "number", "required": true, "min": 0,
    "labelI18n": { "tr": "Ödül değeri" } }
]
```

---

## 3. Tip → görünürlük / motor bayrakları (definition satırı alanları)

Şablonun yanında her tipin `definition.campaign_types` satırında taşıyacağı bayraklar:

| Tip | Scope | SupportsProductSelection | ProductPriceDisplay* | IsStackable(vars.) |
|---|---|---|---|---|
| `discount` | cart/product | ✔ | ✔ (applyTo=selected & benefit ürün-bazlı ise) | hayır (öncelik) |
| `buy_x_get_y` | product | ✔ | ✘ (yalnız sepette "Sepette") | hayır |
| `cross_group_gift` | product | ✔ (+hediye kümesi) | ✘ | hayır |
| `bundle` | product | ✔ | ✘ (kombin sepette) | hayır |
| `free_shipping` | shipping | ✘ | ✘ | evet (fiyat indirimiyle birleşebilir) |
| `review_reward` | member | ✘ | ✘ | evet |

*ProductPriceDisplay = kartta/detayda "kampanyalı birim fiyat" gösterilebilir mi (plan §3.1 kararı).
`discount` dışındaki tipler sepet-bağımlı → kartta yalnız kampanya adı/rozeti + "Sepette" bilgisi.

---

## 4. Açık noktalar (gözden geçirmede karar)
1. `discount`'ta `applyTo=cart` + koşul kombinasyonları (eski 4/5 "sepet x adedine") gerçekten
   isteniyor mu, yoksa yalnız tutar-eşiği mi? (Adet-eşiği nadir kullanılmış.)
2. `cross_group_gift` ve `bundle` ilk sürüme girsin mi, yoksa `discount`+`buy_x_get_y`+`free_shipping`
   ile mi başlayalım? (Kullanım: cross/bundle çok düşük.)
3. `review_reward` ilk sürümde ertelensin mi? (Tetik farklı; §5.)
4. Alan tipleri `percent`/`money` ayrı mı yoksa `number`+`unit` mi? (Form doğrulaması için ayrı
   öneririm.)
