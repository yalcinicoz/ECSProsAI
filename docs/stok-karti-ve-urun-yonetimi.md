# Ürün Kartı ve Ürün Yönetimi — Analiz ve Tasarım Kararları

> Son güncelleme: 2026-03-12
> Durum: Kararlar netleştirildi — Geliştirme başlayabilir

---

## 1. Terminoloji

| Bu Dokümanda | Karşılığı |
|---|---|
| **Ürün Kartı** | Geleneksel ERP'deki "stok kartı" — ürünü ve varyantlarını tanımlayan temel kayıt |
| **Ürün Grubu** | Ürünlerin değişmez özelliğini tanımlayan şablon yapısı (ürün tipi) |
| **Kategori** | Satış kanalı sınıflandırması — ürün gruplarından bağımsız, şu an kapsam dışı |
| **Özellik (Attribute)** | Ürün veya varyantı tanımlayan nitelik (kumaş, renk, beden, işlemci vb.) |
| **Varyant Ekseni** | Ürün varyantlarını birbirinden ayıran özellik türü (renk, beden, kapasite vb.) |

> **Kategori ile Ürün Grubu farkı:** Ürün grubu ürünün ne olduğunu tanımlar (tekstil, laptop). Kategori ürünlerin nerede listeleneceğini tanımlar (Kadın Gömlek, Sezon Sonu). Bir ürün birden fazla kategoride görünebilir. Kategoriler ürün gruplarından bağımsızdır ve bu aşamada kapsam dışıdır.

---

## 2. Veri Modeli

### 2.1 Katmanlar

```
Merkezi Özellik Havuzu (AttributeType + AttributeValue)
  ↓  grup tanımında seçilir
Ürün Grubu (ProductGroup)
  ├── Ürün Özellikleri   → tüm varyantlara ortak bilgiler (kumaş, kalıp, marka…)
  ├── Varyant Eksenleri  → bu grupta varyantı belirleyen özellikler (renk, beden…)
  │    └── primaryAxisCode  → listeleme için ana eksen (örn. "renk")
  └── variantMode: none | single | combination
  ↓  ürün oluşturulurken seçilir
Ürün (Product)
  ├── productGroupId
  ├── Ürün Özellikleri   → grubun şablonundan gelen, tüm varyantlara ortak değerler
  └── Varyantlar (ProductVariant)
       ├── sku, barcode, price
       └── Varyant Değerleri  → bu varyantı tanımlayan eksen değerleri
```

### 2.2 Merkezi Özellik Havuzu

```
AttributeType
  ├── code     örn. renk / beden / kumaş / işlemci / kapasite
  ├── nameI18n görünen ad (çok dilli)
  └── values[] AttributeValue (global havuz — select/multiselect olarak kullanılan gruplarda seçim listesi)
```

**Önemli karar:** `AttributeType` seviyesinde `dataType` ve `isVariantAxis` alanları **yoktur**. Bu bilgiler tamamen `ProductGroupAttribute` (junction) tablosunda tutulur:
- Aynı özellik (renk) bir grupta `select + varyant ekseni`, başka bir grupta `text + ürün özelliği` olabilir
- Global değer havuzu yalnızca `dataType = select | multiselect` olarak yapılandırılan grup kullanımlarında seçim listesi olarak devreye girer

**Değer filtreleme kararı:** Değerler global havuzda tutulur. İsteyen kullanıcı ürün grubuna göre filtre tanımlayabilir; filtre yoksa tüm değerler kullanılabilir. Küçük işletmeler için yönetimi basit tutar, filtre mantığı sonradan bağımsız olarak eklenebilir.

### 2.3 Ürün Grubu (ProductGroup)

| Alan | Açıklama |
|---|---|
| `code` | Tekil kod (örn. `tekstil`, `laptop`, `deterjan`) |
| `name` | Görünen ad |
| `variantMode` | `none` / `single` / `combination` |
| `primaryAxisCode` | Listeleme için ana varyant ekseni kodu (örn. `renk`) |
| `productAttributes[]` | Bu gruba ait ürün özellikleri (varyantlara ortak) |
| `variantAxes[]` | Bu grupta varyant oluşturan eksenler |

**`variantMode` açıklaması:**

| Mod | Ne zaman | Örnek |
|---|---|---|
| `none` | Varyant yok, tek SKU | Deterjan, kablo |
| `single` | Tek eksen, kombinasyon yok | Sadece kapasite farklı USB bellek |
| `combination` | Birden fazla eksen, matris | Renk × Beden (tekstil) |

**`primaryAxisCode` kararı:** Ürün grubunda bir varyant ekseni "ana eksen" olarak işaretlenebilir. Bu bilgi sonradan ürün listeleme kararlarında kullanılır (model bazlı mı, ana varyant bazlı mı listelensin). Varyant değeri düzeyinde vitrin işareti ise ileriki aşamada eklenebilir (reklam görsellerinde kullanışlı).

### 2.4 Ürün (Product) ve Varyant (ProductVariant)

```
Product
  ├── code, name (çok dilli), brand
  ├── productGroupId
  ├── productAttributes[]  → {attributeTypeId, value}   // tüm varyantlara ortak
  └── variants[]
       └── ProductVariant
            ├── sku
            ├── barcode       // otomatik veya manuel — bkz. §4
            ├── price
            ├── comparePrice
            └── variantAttributes[]  → {attributeTypeId, value}  // bu varyantı ayırt eden değerler
```

---

## 3. Özellik Tanımlama Zamanlaması — Hibrit

| Yol | Açıklama |
|---|---|
| **Önceden tanımlı** | Renk, Beden, Marka gibi yaygın özellikler global havuzda hazır gelir |
| **Inline oluşturma** | Ürün grubu formunda "Yeni Özellik Ekle" → modal açılır, havuza eklenir ve gruba bağlanır |

Akış:
```
Ürün Grubu Düzenle
  → Özellik Ekle
      ├── Havuzdan Seç  (mevcut AttributeType'lar arasında arama)
      └── Yeni Özellik Tanımla (modal)
           ├── kod, ad, veri tipi
           ├── Değerler (select/multiselect ise)
           └── "Varyant Ekseni mi?" toggle
```

---

## 4. Barkod — Hibrit Üretim

- Kullanıcı varyant oluştururken barkod girebilir (manuel).
- Barkod girilmemiş varyantlar için **"Barkod Oluştur"** butonu: boş barkodlu tüm varyantlara otomatik barkod üretir ve yazar.
- İkisi bir arada kullanılabilir; bir üründe bazı varyantlarda manuel, diğerlerinde otomatik barkod olabilir.

---

## 5. Varyant Kombinasyonları

**Pasif kombinasyon yok.** Matris oluşturulduğunda tüm kombinasyonlar otomatik aktif gelir. Checkpoint (pasif/aktif seçimi) gereksizdir — yeni parti stok geldiğinde pasif kombinasyonlar sorun yaratır.

Kombinasyon matrisi sadece **görsel rehber** olarak sunulur:

|        | S | M | L | XL |
|--------|---|---|---|----|
| Kırmızı | ✅ | ✅ | ✅ | ✅ |
| Siyah   | ✅ | ✅ | ✅ | ✅ |
| Beyaz   | ✅ | ✅ | ✅ | ✅ |

Tüm hücreler oluşturulur, hiçbiri devre dışı bırakılamaz. Stok girişi yapılmamış kombinasyonlar "stok yok" olarak görünür.

---

## 6. Ürün Oluşturma Sayfası — Sekme Yapısı

**Temel ilke:** Önce ürün grubu seçilir, seçim sonrası özellik sekmeleri gruba göre dinamik oluşturulur.

```
[1. Temel Bilgi] → [2. Varyantlar] → [3. Ürün Özellikleri] → [4. Varyant Özellikleri] → [5. Fiyatlandırma] → [6. Görseller]
```

| # | Sekme | İçerik | Koşul |
|---|---|---|---|
| 1 | **Temel Bilgi** | Ad (çok dilli), **Ürün Grubu seçimi**, Marka, Açıklama | Her zaman |
| 2 | **Varyantlar** | `variantMode`'a göre değişir (bkz. aşağıda) | Her zaman |
| 3 | **Ürün Özellikleri** | Grubun ürün özellikleri — tüm varyantlara ortak | Grup seçilince dinamik |
| 4 | **Varyant Özellikleri** | Her varyant için grubun varyant ekseni değerleri | `variantMode != none` |
| 5 | **Fiyatlandırma** | Platform bazlı fiyatlar, KDV oranı, karşılaştırma fiyatı | Her zaman |
| 6 | **Görseller** | Ürün görseli, varyant–görsel eşleştirme | Her zaman |

> ⚠️ Ürün Grubu değiştirildiğinde Sekme 3 ve 4 yeniden oluşturulur. Girilen özellik değerleri temizlenebilir; kullanıcıya uyarı gösterilir.

### Sekme 2 — Varyant Girişi (variantMode'a göre)

**`none`:** Varyant sekmesi içeriği basitleşir — tek SKU + barkod + fiyat alanları.

**`single`:** Tablo listesi:
```
[SKU]  [Barkod]  [Değer]  [Fiyat]  [Sil]
+ Varyant Ekle
[Barkod Oluştur] (boş barkodları doldurur)
```

**`combination`:** Eksen değerleri seçilir → Oluştur butonuyla tüm kombinasyonlar otomatik üretilir:
```
Eksen 1 (Renk):  [Kırmızı ×] [Siyah ×] [Beyaz ×] + Değer Ekle
Eksen 2 (Beden): [S ×] [M ×] [L ×] [XL ×] + Değer Ekle

[Kombinasyonları Oluştur]
→ Tablo: SKU | Barkod | Renk | Beden | Fiyat
[Barkod Oluştur]
```

---

## 7. Fiyat Geçmişi

Fiyat geçmişi bir log yapısıdır; muhasebe işlemi değildir.

- Her fiyat değişikliği `PriceHistory` kaydı olarak tutulur.
- **Düzeltme kuralı:** Bir işlem kaydı düzeltildiğinde (örn. kabul fiyatı 100 → 90), mevcut geçmiş kaydı güncellenir. Eski değer (100) saklanmaz. Efektif değer her zaman son düzeltilmiş haldir.
- Muhasebe uyumlu "düzeltme işlemi" akışı (iptal + yeni kayıt) ileriki fazda eklenebilir; şu an altyapıyı kısıtlamaz.

---

## 8. Ürün Kartı Kavramı (Backend Katmanları)

| Kavram | Backend Karşılığı |
|---|---|
| Ürün Kartı Ana Bilgisi | `Product` + `ProductVariant` |
| Stok Miktarı | `Stock` (depo + lokasyon bazında) |
| Stok Hareketi | `StockMovement` |
| Fiyat Geçmişi | `PriceHistory` |

### Ürün Kartı Liste Sayfası (`/products`)

Filtreler: Ürün Grubu, Kategori, Marka, Stok Durumu (normal / kritik / sıfır)
Kolonlar: SKU, Ürün Adı, Grup, Varyant Sayısı, Toplam Stok, Durum
Eylemler: Düzenle, Pasife Al/Aktif Et, Stok Girişi

### Ürün Kartı Detay Sayfası (`/products/:code`)

- **Sol kolon:** Temel bilgiler, ürün özellikleri, varyant listesi (sku/barkod/fiyat)
- **Sağ kolon:** Depo bazlı stok özeti, son stok hareketleri, fiyat geçmişi
- **Sekmeler:** Genel Bilgi | Özellikler | Varyantlar | Stok | Fiyat Geçmişi | Görseller

---

## 9. Geliştirme Öncelik Sırası

### Adım 1 — Merkezi Özellik Havuzu Sayfaları
- [ ] AttributeType liste + oluştur + düzenle
- [ ] AttributeValue yönetimi (select/multiselect için değer listesi)
- [ ] `isVariantAxis` flag — bu özellik varyant ekseni mi göstergesi

### Adım 2 — Ürün Grubu Yönetimi
- [ ] Ürün Grubu liste + oluştur + düzenle
- [ ] `variantMode` seçimi (Yok / Tekil / Kombinasyon)
- [ ] `primaryAxisCode` seçimi (listeleme için ana eksen)
- [ ] Gruba özellik ekle/çıkar (havuzdan seç veya inline yeni oluştur)
- [ ] Gruba varyant ekseni ekle/çıkar

### Adım 3 — Ürün Oluşturma / Düzenleme (Yeni Akış)
- [ ] Ürün grubu seçimine göre dinamik sekme oluşturma
- [ ] `none` → basit tek-SKU formu
- [ ] `single` → satır bazlı varyant listesi
- [ ] `combination` → eksen seçimi + otomatik kombinasyon üretimi
- [ ] "Barkod Oluştur" akışı

### Adım 4 — Ürün Kartı Detay Sayfası
- [ ] Varyant listesi (sku / barkod / fiyat düzenleme)
- [ ] Depo bazlı stok özeti
- [ ] Stok hareketi modal (giriş / çıkış / düzeltme)
- [ ] Fiyat geçmişi sekmesi

### Adım 5 — Stok Hareketleri
- [ ] Hareket geçmişi listesi (tarih / tür / miktar / referans)
- [ ] Manuel stok düzeltmesi

---

## 10. Mevcut Backend Durumu

Aşağıdaki endpoint'ler kullanılabilir durumda:

| Endpoint | Durum | Not |
|---|---|---|
| `GET/POST /api/catalog/product-groups` | ✅ | Liste + oluştur |
| `PUT /api/catalog/product-groups/{id}` | ✅ | Güncelle |
| `POST /api/catalog/product-groups/{id}/attributes` | ✅ | Gruba özellik ekle |
| `GET/POST /api/catalog/attribute-types` | ✅ | Merkezi havuz |
| `POST /api/catalog/attribute-types/{id}/values` | ✅ | Seçenek ekle |
| `GET/POST /api/catalog/products` | ✅ | Ürün CRUD |
| `GET /api/catalog/products/:code` | ✅ | Detay |
| `GET /api/inventory/stocks` | ✅ | Stok listesi |
| `POST /api/inventory/stocks/adjust` | ✅ | Stok hareketi |

**İhtiyaç duyulacak ek endpoint'ler (geliştirme sırasında belirlenecek):**
- `GET /api/catalog/product-groups/:id` — grup detayı (özellikler + eksenler)
- `GET /api/catalog/products/:code/variants` — ürün varyantları ayrı endpoint
- `GET /api/inventory/stocks/:variantId/movements` — hareket geçmişi
- `variantMode` ve `primaryAxisCode` alanları backend'e eklenmeli → migration gerekecek
- `PriceHistory` entity'si henüz yok → yeni tablo ve endpoint gerekecek
