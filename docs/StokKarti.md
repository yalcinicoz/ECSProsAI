 1. Mevcut Durum: Sekme Bazlı Analiz

  ┌─────────────────┬───────┬──────────────────────────────────────────────────────────────────────────┐
  │      Sekme      │ Durum │                                  Notlar                                  │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ Genel           │ ✅    │ Ad/açıklama (çok dilli), fiyat, KDV, tedarikçi                           │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ Özellikler      │ ✅    │ Non-variant grup özelliklerini select ile giriş, toplu kaydet            │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ Varyantlar      │ ⚠️    │ SKU/barkod/özellik OK; varyant fiyat input kaydedilmiyor                 │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ Stok            │ ✅    │ Depo/varyant bazlı görüntüleme; "Stok Hareketi" /inventory/stocks'a link │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ Satış Kanalları │ ✅    │ Manuel/çarpan fiyat, toplu uygulama, satır kaydet                        │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ Görseller       │ ✅    │ Set bazlı yükleme, kapak atama, video desteği                            │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ Etiketler       │ ✅    │                                                                          │
  ├─────────────────┼───────┼──────────────────────────────────────────────────────────────────────────┤
  │ SEO             │ ✅    │ Slug, meta alanları, Google önizleme                                     │
  └─────────────────┴───────┴──────────────────────────────────────────────────────────────────────────┘

  ProductsPage (liste) → ✅ Tam
  ProductCreatePage (hızlı oluşturma) → ✅ Tam

  ---
  2. Kritik Bug'lar (Çalışmayan Şeyler)

  #: B1
  Bug: Tedarikçi seçimi 500 veriyor
  Konum: ProductDetailPage.tsx:1113
  Düzeltme: /accounts → /finance/suppliers
  ────────────────────────────────────────
  #: B2
  Bug: Varyant fiyat input defaultValue kullanıyor
  Konum: satır 2127
  Düzeltme: Controlled state + onBlur save mutation
  ────────────────────────────────────────
  #: B3
  Bug: Varyant tabındaki stok sütunu başlangıçta 0
  Konum: enabled: activeTab === 'stok'
  Düzeltme: 'stok' || 'varyantlar' yapılmalı
  ────────────────────────────────────────
  #: B4
  Bug: Header'da "IMG" placeholder — gerçek görsel yok
  Konum: satır 1540
  Düzeltme: Product API'sinden kapak URL alınmalı

  ---
  3. Eksik Backend Endpoint'leri

  Stok kartını tam kullanabilmek için şu endpoint'ler yok:

  ┌────────┬───────────────────────────────────┬──────────────────────────────────┐
  │ Method │             Endpoint              │            Gereklilik            │
  ├────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ PUT    │ /api/catalog/variants/{id}/price  │ Varyant bazlı fiyat güncelleme   │
  ├────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ PATCH  │ /api/catalog/variants/{id}/status │ Varyant aktif/pasif toggle       │
  ├────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ DELETE │ /api/catalog/variants/{id}        │ Varyant silme                    │
  ├────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ DELETE │ /api/catalog/products/{id}        │ Ürün silme (UI var ama işlevsiz) │
  └────────┴───────────────────────────────────┴──────────────────────────────────┘

  ---
  4. Eksik Frontend İşlevleri

  #: F1
  Eksik: Varyant silme butonu + confirm dialog
  Konum: Varyantlar tabı
  ────────────────────────────────────────
  #: F2
  Eksik: Varyant aktif/pasif toggle (badge'e tıklayınca)
  Konum: Varyantlar tabı
  ────────────────────────────────────────
  #: F3
  Eksik: Varyant SKU düzenleme
  Konum: Varyantlar tabı
  ────────────────────────────────────────
  #: F4
  Eksik: "Ürünü Sil" butonunun çalışması
  Konum: Genel tabı sağ panel
  ────────────────────────────────────────
  #: F5
  Eksik: Varyantlar tabında stok hareketi inline modal
  Konum: Stok tabına yönlendiriyor — yeterli kabul edilirse ✅
  ────────────────────────────────────────
  #: F6
  Eksik: Görseller tabından sonra header thumb güncellenmeli
  Konum: Header

  ---
  5. Yan Ekranlar: Tam Stok Kartı İçin Önceden Girilmesi Gerekenler

  Bir kullanıcının ürün kartı oluşturabilmesi için bu akış izlenmelidir:

  Özellik Tipleri  →  Özellik Değerleri  →  Ürün Grubu (özellik ata, eksen belirle)
         ↓
    Kategoriler  ←  (isteğe bağlı, ürünü kategori bazlı listeme için)
         ↓
    Tedarikçi  ←  (isteğe bağlı)
         ↓
    Depo  ←  (stok hareketi yapılabilmesi için)
         ↓
    Firma + Satış Kanalı  ←  (platform fiyatı girebilmek için)
         ↓
    Görsel Seti  ←  (görsel yükleyebilmek için)
         ↓
    ★ Ürün Kartı Oluştur

  Mevcut sayfalar:

  ┌─────────────────────┬──────────────────────────────────┬────────┐
  │        Ekran        │               Rota               │ Durum  │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Özellik Tipleri     │ /catalog/attribute-types         │ ✅     │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Özellik Değerleri   │ /catalog/attribute-types/:id     │ ✅     │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Ürün Grupları       │ /catalog/product-groups + detail │ ✅     │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Kategoriler         │ /catalog/categories + detail     │ ✅     │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Tedarikçiler        │ /finance/suppliers               │ ✅     │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Depolar             │ /inventory/warehouses            │ ✅     │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Firmalar & Kanallar │ /settings/firms + detail         │ ✅     │
  ├─────────────────────┼──────────────────────────────────┼────────┤
  │ Görsel Setleri      │ —                                │ ❌ Yok │
  └─────────────────────┴──────────────────────────────────┴────────┘

  ▎ ImageSet yönetim sayfası eksik — CatalogSettingsPage.tsx var ama image set CRUD için ayrı bir ekran yok.
  ▎ ProductImagesTab içinde inline kullanılıyor; default set manuel DB'den eklenmeli.

  ---
  6. Seed Değerleri — Ne Eksik?

  DatabaseSeeder.SeedAsync() şu an yalnızca IAM + Core seed atıyor. Katalog ile çalışabilmek için şunların seed'lenmesi
  gerekiyor:

  ┌──────────────────────────┬─────────────────────────────────────────────────────────────┐
  │          Tablo           │                         Eksik Seed                          │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ catalog.attribute_types  │ Renk, Beden, Materyal, Cinsiyet (varyant eksen olabilenler) │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ catalog.attribute_values │ Renk → Kırmızı/Mavi/Siyah/Beyaz; Beden → XS/S/M/L/XL/2XL    │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ catalog.product_groups   │ Demo → "Giyim" / "Erkek Giyim"                              │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ catalog.catalog_settings │ barcode_sequence = 1 (barkod üretimi için)                  │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ catalog.image_sets       │ Default image set (isDefault=true)                          │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ core.firms               │ "Demo Firma"                                                │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ core.firm_platforms      │ "Web Sitesi" (platformTypeCode: website)                    │
  ├──────────────────────────┼─────────────────────────────────────────────────────────────┤
  │ inventory.warehouses     │ "Merkez Depo"                                               │
  └──────────────────────────┴─────────────────────────────────────────────────────────────┘

  ---
  7. Önerilen Yol Haritası (Öncelik Sıralı)

  Adım 1 — Kritik Bug'ları Düzelt (30 dk)
  - B1: Tedarikçi API fix
  - B3: Stok enabled koşulu fix

  Adım 2 — Varyant Fiyat Kaydetme (1 saat)
  - Backend: PUT /api/catalog/variants/{id}/price endpoint
  - Frontend: Controlled input + onBlur/buton ile save

  Adım 3 — Varyant Silme + Durum Toggle (1 saat)
  - Backend: DELETE variants/{id} + PATCH variants/{id}/status
  - Frontend: Sil butonu + confirm dialog, aktif/pasif toggle

  Adım 4 — Seed Değerleri (45 dk)
  - DatabaseSeeder'a AttributeType/Value, ProductGroup, ImageSet, CatalogSetting, Firma/Platform, Depo eklenmesi

  Adım 5 — Görsel Seti Yönetim Sayfası (1 saat)
  - /catalog/image-sets sayfası — liste, oluştur, varsayılan seç

  Adım 6 — Header Thumbnail (20 dk)
  - Product API response'una coverImageUrl ekle
  - Header'da IMG placeholder'ı gerçek görselle değiştir

  Adım 7 — Ürün Silme (30 dk)
  - Backend: DELETE /api/catalog/products/{id} (cascade: variants, images, attributes)
  - Frontend: Confirm dialog → yönlendirme

  ---
  Hangi adımdan başlamak istersiniz? Öneririm: Önce Adım 1 bug fix'leri + Adım 4 seed değerleri ile sistemi test
  edilebilir hale getirin, ardından Adım 2-3 (varyant yönetimi).

✻ Crunched for 3m 42s

※ recap: Products sayfası analizi tamamlandı: sekmelerin çoğu çalışıyor, ama tedarikçi API bug'u, varyant fiyat kaydetme
   eksikliği ve seed verileri gibi kritik sorunlar var. Kullanıcı hangi adımdan başlamak istediğine karar vermeli.
  (disable recaps in /config)