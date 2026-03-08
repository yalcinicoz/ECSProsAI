# E-Ticaret Altyapı Projesi - API Tasarımı

**Doküman Versiyonu:** 1.0  
**Son Güncelleme:** Ocak 2025  
**API Stili:** RESTful  
**Versiyonlama:** URL-based (v1, v2, ...)

---

## 1. Genel Prensipler

### 1.1 URL Yapısı
```
/api/v1/{module}/{resource}
/api/v1/{module}/{resource}/{id}
/api/v1/{module}/{resource}/{id}/{sub-resource}
```

### 1.2 HTTP Metodları
| Metod | Kullanım |
|-------|----------|
| GET | Kayıt okuma (tekil veya liste) |
| POST | Yeni kayıt oluşturma |
| PUT | Kayıt güncelleme (tüm alanlar) |
| PATCH | Kısmi güncelleme veya durum değişikliği |
| DELETE | Kayıt silme |

### 1.3 HTTP Status Kodları
| Kod | Kullanım |
|-----|----------|
| 200 | Başarılı GET, PUT, PATCH |
| 201 | Başarılı POST (kayıt oluşturma) |
| 204 | Başarılı DELETE (içerik yok) |
| 400 | Validation hatası, geçersiz istek |
| 401 | Kimlik doğrulama gerekli |
| 403 | Yetki yok |
| 404 | Kayıt bulunamadı |
| 409 | Conflict (örn: duplicate kayıt) |
| 422 | İş kuralı hatası |
| 500 | Sunucu hatası |

---

## 2. Response Formatı

### 2.1 Başarılı Yanıtlar

**Tekil kayıt:**
```json
{
  "success": true,
  "data": {
    "id": "uuid",
    "name": "Ürün Adı",
    ...
  }
}
```

**Liste:**
```json
{
  "success": true,
  "data": [...],
  "meta": {
    "currentPage": 1,
    "pageSize": 20,
    "totalPages": 5,
    "totalCount": 100
  }
}
```

### 2.2 Hata Yanıtları
```json
{
  "success": false,
  "error": {
    "code": "PRODUCT_NOT_FOUND",
    "message": "Ürün bulunamadı",
    "details": [
      {
        "field": "id",
        "message": "Geçersiz ürün ID"
      }
    ],
    "traceId": "abc123"
  }
}
```

---

## 3. Query Parametreleri

### 3.1 Sayfalama
```
?page=1&pageSize=20
```
- `page`: Sayfa numarası (varsayılan: 1)
- `pageSize`: Sayfa boyutu (varsayılan: 20, max: 100)

### 3.2 Sıralama
```
?sort=name:asc
?sort=createdAt:desc
?sort=name:asc,createdAt:desc
```

### 3.3 Filtreleme
```
?filter[categoryId]=uuid
?filter[isActive]=true
?filter[price][gte]=100
?filter[price][lte]=500
?filter[createdAt][gte]=2025-01-01
```

Operatörler:
- `eq`: Eşit (varsayılan)
- `ne`: Eşit değil
- `gt`: Büyük
- `gte`: Büyük eşit
- `lt`: Küçük
- `lte`: Küçük eşit
- `in`: İçinde (virgülle ayrılmış)
- `contains`: İçerir (metin)

### 3.4 Arama
```
?search=tshirt
```

### 3.5 Alan Seçimi
```
?fields=id,name,price
```

### 3.6 İlişkili Veri
```
?include=category,variants
```

---

## 4. Headers

### 4.1 Request Headers
```
Authorization: Bearer {token}
Content-Type: application/json
Accept: application/json
Accept-Language: tr
X-Firm-Id: {firmId}
X-Platform-Id: {platformId}
X-Request-Id: {uniqueId}
```

### 4.2 Response Headers
```
X-Request-Id: {uniqueId}
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1640000000
```

---

## 5. Çok Dilli Destek

**Request:**
```
Accept-Language: tr
```

**Response (Admin API):**
```json
{
  "id": "uuid",
  "name": "Ürün Adı",
  "nameTranslations": {
    "tr": "Ürün Adı",
    "en": "Product Name"
  }
}
```

**Response (Store API):**
```json
{
  "id": "uuid",
  "name": "Ürün Adı"
}
```

---

## 6. Modül Endpoint'leri

### 6.1 Core Modülü

```
# Diller
GET    /api/v1/core/languages
GET    /api/v1/core/languages/{id}
POST   /api/v1/core/languages
PUT    /api/v1/core/languages/{id}
DELETE /api/v1/core/languages/{id}

# Lookup Tipleri ve Değerleri
GET    /api/v1/core/lookup-types
GET    /api/v1/core/lookup-types/{code}/values
POST   /api/v1/core/lookup-types
POST   /api/v1/core/lookup-types/{code}/values
PUT    /api/v1/core/lookup-values/{id}
DELETE /api/v1/core/lookup-values/{id}

# Platform Tipleri
GET    /api/v1/core/platform-types
GET    /api/v1/core/platform-types/{id}
POST   /api/v1/core/platform-types
PUT    /api/v1/core/platform-types/{id}

# Firmalar
GET    /api/v1/core/firms
GET    /api/v1/core/firms/{id}
POST   /api/v1/core/firms
PUT    /api/v1/core/firms/{id}
DELETE /api/v1/core/firms/{id}

# Firma Platformları
GET    /api/v1/core/firms/{firmId}/platforms
GET    /api/v1/core/firm-platforms/{id}
POST   /api/v1/core/firms/{firmId}/platforms
PUT    /api/v1/core/firm-platforms/{id}
DELETE /api/v1/core/firm-platforms/{id}

# Entegrasyon Servisleri
GET    /api/v1/core/integration-services
GET    /api/v1/core/integration-services/{id}

# Firma Entegrasyonları
GET    /api/v1/core/firms/{firmId}/integrations
GET    /api/v1/core/firm-integrations/{id}
POST   /api/v1/core/firms/{firmId}/integrations
PUT    /api/v1/core/firm-integrations/{id}
DELETE /api/v1/core/firm-integrations/{id}
POST   /api/v1/core/firm-integrations/{id}/test-connection

# Kargo Kuralları
GET    /api/v1/core/firms/{firmId}/cargo-rules
POST   /api/v1/core/firms/{firmId}/cargo-rules
PUT    /api/v1/core/cargo-rules/{id}
DELETE /api/v1/core/cargo-rules/{id}

# Masraf Tipleri
GET    /api/v1/core/expense-types
POST   /api/v1/core/expense-types
PUT    /api/v1/core/expense-types/{id}

# Ödeme Yöntemleri
GET    /api/v1/core/payment-methods
PUT    /api/v1/core/payment-methods/{id}

# Bildirim Ayarları
GET    /api/v1/core/notification-types
GET    /api/v1/core/firms/{firmId}/notification-settings
PUT    /api/v1/core/firms/{firmId}/notification-settings/{notificationTypeId}
GET    /api/v1/core/notification-templates/{notificationTypeId}
PUT    /api/v1/core/notification-templates/{id}
```

### 6.2 Catalog Modülü

```
# Özellik Tipleri
GET    /api/v1/catalog/attribute-types
GET    /api/v1/catalog/attribute-types/{id}
POST   /api/v1/catalog/attribute-types
PUT    /api/v1/catalog/attribute-types/{id}
DELETE /api/v1/catalog/attribute-types/{id}

# Özellik Değerleri
GET    /api/v1/catalog/attribute-types/{typeId}/values
POST   /api/v1/catalog/attribute-types/{typeId}/values
PUT    /api/v1/catalog/attribute-values/{id}
DELETE /api/v1/catalog/attribute-values/{id}

# Ürün Grupları (Şablonlar)
GET    /api/v1/catalog/product-groups
GET    /api/v1/catalog/product-groups/{id}
POST   /api/v1/catalog/product-groups
PUT    /api/v1/catalog/product-groups/{id}
DELETE /api/v1/catalog/product-groups/{id}
GET    /api/v1/catalog/product-groups/{id}/attributes
POST   /api/v1/catalog/product-groups/{id}/attributes
DELETE /api/v1/catalog/product-group-attributes/{id}

# Kategoriler
GET    /api/v1/catalog/categories
GET    /api/v1/catalog/categories/{id}
GET    /api/v1/catalog/categories/tree
POST   /api/v1/catalog/categories
PUT    /api/v1/catalog/categories/{id}
DELETE /api/v1/catalog/categories/{id}
PUT    /api/v1/catalog/categories/{id}/reorder
POST   /api/v1/catalog/categories/{id}/products
DELETE /api/v1/catalog/categories/{id}/products/{productId}

# Ürünler
GET    /api/v1/catalog/products
GET    /api/v1/catalog/products/{id}
POST   /api/v1/catalog/products
PUT    /api/v1/catalog/products/{id}
DELETE /api/v1/catalog/products/{id}
PATCH  /api/v1/catalog/products/{id}/activate
PATCH  /api/v1/catalog/products/{id}/deactivate
POST   /api/v1/catalog/products/bulk-update
POST   /api/v1/catalog/products/import
GET    /api/v1/catalog/products/export

# Ürün Özellikleri
GET    /api/v1/catalog/products/{productId}/attributes
PUT    /api/v1/catalog/products/{productId}/attributes

# Varyantlar
GET    /api/v1/catalog/products/{productId}/variants
GET    /api/v1/catalog/variants/{id}
POST   /api/v1/catalog/products/{productId}/variants
PUT    /api/v1/catalog/variants/{id}
DELETE /api/v1/catalog/variants/{id}
POST   /api/v1/catalog/products/{productId}/variants/generate

# Varyant Görselleri
GET    /api/v1/catalog/variants/{variantId}/images
POST   /api/v1/catalog/variants/{variantId}/images
PUT    /api/v1/catalog/variant-images/{id}
DELETE /api/v1/catalog/variant-images/{id}
PUT    /api/v1/catalog/variants/{variantId}/images/reorder

# Platform Bazlı Ürün/Fiyat
GET    /api/v1/catalog/firm-platforms/{platformId}/products
GET    /api/v1/catalog/firm-platforms/{platformId}/products/{productId}
PUT    /api/v1/catalog/firm-platforms/{platformId}/products/{productId}
GET    /api/v1/catalog/firm-platforms/{platformId}/variants/{variantId}
PUT    /api/v1/catalog/firm-platforms/{platformId}/variants/{variantId}
POST   /api/v1/catalog/firm-platforms/{platformId}/variants/bulk-price-update

# Fiyat Geçmişi
GET    /api/v1/catalog/variants/{variantId}/price-history
```

### 6.3 Inventory Modülü

```
# Depolar
GET    /api/v1/inventory/warehouses
GET    /api/v1/inventory/warehouses/{id}
POST   /api/v1/inventory/warehouses
PUT    /api/v1/inventory/warehouses/{id}
DELETE /api/v1/inventory/warehouses/{id}
PUT    /api/v1/inventory/warehouses/reorder-priority

# Depo Lokasyonları
GET    /api/v1/inventory/warehouses/{warehouseId}/locations
GET    /api/v1/inventory/locations/{id}
POST   /api/v1/inventory/warehouses/{warehouseId}/locations
PUT    /api/v1/inventory/locations/{id}
DELETE /api/v1/inventory/locations/{id}
GET    /api/v1/inventory/locations/by-barcode/{barcode}

# Stoklar
GET    /api/v1/inventory/stocks
GET    /api/v1/inventory/stocks/{id}
GET    /api/v1/inventory/variants/{variantId}/stocks
PUT    /api/v1/inventory/stocks/{id}/adjust
POST   /api/v1/inventory/stocks/bulk-adjust

# Stok Hareketleri
GET    /api/v1/inventory/stock-movements
GET    /api/v1/inventory/variants/{variantId}/stock-movements

# Stok Rezervasyonları
GET    /api/v1/inventory/reservations
GET    /api/v1/inventory/orders/{orderId}/reservations

# Transferler
GET    /api/v1/inventory/transfers
GET    /api/v1/inventory/transfers/{id}
POST   /api/v1/inventory/transfers
PUT    /api/v1/inventory/transfers/{id}
DELETE /api/v1/inventory/transfers/{id}
PATCH  /api/v1/inventory/transfers/{id}/approve
PATCH  /api/v1/inventory/transfers/{id}/start-picking
PATCH  /api/v1/inventory/transfers/{id}/complete-picking
PATCH  /api/v1/inventory/transfers/{id}/hand-over
PATCH  /api/v1/inventory/transfers/{id}/receive
PATCH  /api/v1/inventory/transfers/{id}/complete
PATCH  /api/v1/inventory/transfers/{id}/cancel

# Transfer Kalemleri
GET    /api/v1/inventory/transfers/{transferId}/items
PUT    /api/v1/inventory/transfer-items/{id}
POST   /api/v1/inventory/transfer-items/{id}/pick
POST   /api/v1/inventory/transfer-items/{id}/receive

# Transfer Takip
GET    /api/v1/inventory/transfers/{transferId}/tracking
```

### 6.4 CRM Modülü

```
# Üyelik Grupları
GET    /api/v1/crm/member-groups
GET    /api/v1/crm/member-groups/{id}
POST   /api/v1/crm/member-groups
PUT    /api/v1/crm/member-groups/{id}
DELETE /api/v1/crm/member-groups/{id}

# Üyeler
GET    /api/v1/crm/members
GET    /api/v1/crm/members/{id}
POST   /api/v1/crm/members
PUT    /api/v1/crm/members/{id}
DELETE /api/v1/crm/members/{id}
PATCH  /api/v1/crm/members/{id}/activate
PATCH  /api/v1/crm/members/{id}/deactivate
PATCH  /api/v1/crm/members/{id}/anonymize
GET    /api/v1/crm/members/{id}/orders
GET    /api/v1/crm/members/{id}/addresses
POST   /api/v1/crm/members/import
GET    /api/v1/crm/members/export

# Adres Tanımları (Admin)
GET    /api/v1/crm/countries
POST   /api/v1/crm/countries
GET    /api/v1/crm/countries/{countryId}/cities
POST   /api/v1/crm/countries/{countryId}/cities
GET    /api/v1/crm/cities/{cityId}/districts
POST   /api/v1/crm/cities/{cityId}/districts
GET    /api/v1/crm/districts/{districtId}/neighborhoods
POST   /api/v1/crm/districts/{districtId}/neighborhoods
POST   /api/v1/crm/address-definitions/import

# Üye Adresleri
GET    /api/v1/crm/members/{memberId}/addresses
GET    /api/v1/crm/addresses/{id}
POST   /api/v1/crm/members/{memberId}/addresses
PUT    /api/v1/crm/addresses/{id}
DELETE /api/v1/crm/addresses/{id}
PATCH  /api/v1/crm/addresses/{id}/validate
PATCH  /api/v1/crm/addresses/{id}/set-default

# Sepetler
GET    /api/v1/crm/carts
GET    /api/v1/crm/carts/{id}
GET    /api/v1/crm/members/{memberId}/cart
DELETE /api/v1/crm/carts/{id}
POST   /api/v1/crm/carts/merge

# Cüzdan
GET    /api/v1/crm/members/{memberId}/wallet
GET    /api/v1/crm/members/{memberId}/wallet/transactions
POST   /api/v1/crm/members/{memberId}/wallet/adjust
POST   /api/v1/crm/members/{memberId}/wallet/withdraw

# Sadakat
GET    /api/v1/crm/members/{memberId}/loyalty
GET    /api/v1/crm/members/{memberId}/loyalty/transactions
POST   /api/v1/crm/members/{memberId}/loyalty/adjust

# Müşteri Kredisi (B2B)
GET    /api/v1/crm/members/{memberId}/credit
PUT    /api/v1/crm/members/{memberId}/credit
POST   /api/v1/crm/members/{memberId}/credit/adjust

# Müşteriye Özel Fiyatlar (B2B)
GET    /api/v1/crm/members/{memberId}/prices
POST   /api/v1/crm/members/{memberId}/prices
PUT    /api/v1/crm/member-prices/{id}
DELETE /api/v1/crm/member-prices/{id}
POST   /api/v1/crm/members/{memberId}/prices/import

# Müşteriye Özel İskontolar (B2B)
GET    /api/v1/crm/members/{memberId}/discounts
POST   /api/v1/crm/members/{memberId}/discounts
PUT    /api/v1/crm/member-discounts/{id}
DELETE /api/v1/crm/member-discounts/{id}
```

### 6.5 Order Modülü

```
# Siparişler
GET    /api/v1/orders
GET    /api/v1/orders/{id}
GET    /api/v1/orders/by-number/{orderNumber}
POST   /api/v1/orders
PUT    /api/v1/orders/{id}
PATCH  /api/v1/orders/{id}/confirm
PATCH  /api/v1/orders/{id}/cancel
PATCH  /api/v1/orders/{id}/update-status
GET    /api/v1/orders/export

# Sipariş Kalemleri
GET    /api/v1/orders/{orderId}/items
PUT    /api/v1/orders/{orderId}/items/{itemId}
PATCH  /api/v1/orders/{orderId}/items/{itemId}/update-status

# Sipariş Ödemeleri
GET    /api/v1/orders/{orderId}/payments
POST   /api/v1/orders/{orderId}/payments
PATCH  /api/v1/orders/{orderId}/payments/{paymentId}/confirm
PATCH  /api/v1/orders/{orderId}/payments/{paymentId}/cancel

# Sipariş Bildirimleri
GET    /api/v1/orders/{orderId}/notifications
POST   /api/v1/orders/{orderId}/notifications/{notificationTypeCode}/send
POST   /api/v1/orders/{orderId}/notifications/{id}/retry

# Fatura Serileri
GET    /api/v1/orders/invoice-series
POST   /api/v1/orders/invoice-series
PUT    /api/v1/orders/invoice-series/{id}

# Faturalar
GET    /api/v1/orders/invoices
GET    /api/v1/orders/invoices/{id}
GET    /api/v1/orders/{orderId}/invoices
POST   /api/v1/orders/{orderId}/invoices
PATCH  /api/v1/orders/invoices/{id}/send-to-integrator
PATCH  /api/v1/orders/invoices/{id}/cancel
GET    /api/v1/orders/invoices/{id}/pdf

# Kargolar
GET    /api/v1/orders/shipments
GET    /api/v1/orders/shipments/{id}
GET    /api/v1/orders/{orderId}/shipments
POST   /api/v1/orders/{orderId}/shipments
PATCH  /api/v1/orders/shipments/{id}/send-to-cargo
PATCH  /api/v1/orders/shipments/{id}/update-tracking
GET    /api/v1/orders/shipments/{id}/events

# Hediye Ürünler
GET    /api/v1/orders/{orderId}/gifts
POST   /api/v1/orders/{orderId}/gifts
DELETE /api/v1/orders/{orderId}/gifts/{giftId}

# İadeler
GET    /api/v1/orders/returns
GET    /api/v1/orders/returns/{id}
GET    /api/v1/orders/{orderId}/returns
POST   /api/v1/orders/{orderId}/returns
PUT    /api/v1/orders/returns/{id}
PATCH  /api/v1/orders/returns/{id}/approve
PATCH  /api/v1/orders/returns/{id}/reject
PATCH  /api/v1/orders/returns/{id}/receive
PATCH  /api/v1/orders/returns/{id}/complete-inspection
PATCH  /api/v1/orders/returns/{id}/complete

# İade Kalemleri
PUT    /api/v1/orders/return-items/{id}
PATCH  /api/v1/orders/return-items/{id}/inspect

# İade Ödemeleri
GET    /api/v1/orders/returns/{returnId}/refunds
POST   /api/v1/orders/returns/{returnId}/refunds
PATCH  /api/v1/orders/return-refunds/{id}/process

# Hediye Çekleri
GET    /api/v1/orders/gift-cards
GET    /api/v1/orders/gift-cards/{id}
GET    /api/v1/orders/gift-cards/by-code/{code}
POST   /api/v1/orders/gift-cards
PATCH  /api/v1/orders/gift-cards/{id}/activate
PATCH  /api/v1/orders/gift-cards/{id}/deactivate
GET    /api/v1/orders/gift-cards/{id}/transactions

# Teklifler (B2B)
GET    /api/v1/orders/quotes
GET    /api/v1/orders/quotes/{id}
POST   /api/v1/orders/quotes
PUT    /api/v1/orders/quotes/{id}
DELETE /api/v1/orders/quotes/{id}
PATCH  /api/v1/orders/quotes/{id}/send
PATCH  /api/v1/orders/quotes/{id}/accept
PATCH  /api/v1/orders/quotes/{id}/reject
POST   /api/v1/orders/quotes/{id}/convert-to-order
GET    /api/v1/orders/quotes/{id}/pdf

# Sipariş Onay (B2B)
GET    /api/v1/orders/pending-approval
PATCH  /api/v1/orders/{id}/approve
PATCH  /api/v1/orders/{id}/reject-approval
```

### 6.6 Fulfillment Modülü

```
# Toplama Planları
GET    /api/v1/fulfillment/picking-plans
GET    /api/v1/fulfillment/picking-plans/{id}
POST   /api/v1/fulfillment/picking-plans
POST   /api/v1/fulfillment/picking-plans/preview
PUT    /api/v1/fulfillment/picking-plans/{id}
DELETE /api/v1/fulfillment/picking-plans/{id}
PATCH  /api/v1/fulfillment/picking-plans/{id}/start
PATCH  /api/v1/fulfillment/picking-plans/{id}/complete
PATCH  /api/v1/fulfillment/picking-plans/{id}/cancel
POST   /api/v1/fulfillment/picking-plans/{id}/add-orders
DELETE /api/v1/fulfillment/picking-plans/{id}/orders/{orderId}
GET    /api/v1/fulfillment/picking-plans/{id}/orders
GET    /api/v1/fulfillment/picking-plans/{id}/items

# Toplama İşlemleri
POST   /api/v1/fulfillment/picking/assign
GET    /api/v1/fulfillment/picking/my-tasks
POST   /api/v1/fulfillment/picking/scan-item
POST   /api/v1/fulfillment/picking/report-not-found

# Ayrıştırma Kolileri
GET    /api/v1/fulfillment/sorting-bins
GET    /api/v1/fulfillment/sorting-bins/{id}
GET    /api/v1/fulfillment/sorting-bins/{id}/orders
POST   /api/v1/fulfillment/sorting/scan-to-bin

# Paketleme Masaları
GET    /api/v1/fulfillment/packing-stations
GET    /api/v1/fulfillment/packing-stations/{id}
POST   /api/v1/fulfillment/packing-stations
PUT    /api/v1/fulfillment/packing-stations/{id}
DELETE /api/v1/fulfillment/packing-stations/{id}
PATCH  /api/v1/fulfillment/packing-stations/{id}/assign
PATCH  /api/v1/fulfillment/packing-stations/{id}/release

# Son Ayrıştırma ve Paketleme
POST   /api/v1/fulfillment/final-sorting/scan-to-slot
POST   /api/v1/fulfillment/packing/final-scan
POST   /api/v1/fulfillment/packing/complete
POST   /api/v1/fulfillment/packing/move-to-obm

# Paketler
GET    /api/v1/fulfillment/packages
GET    /api/v1/fulfillment/packages/{id}
GET    /api/v1/fulfillment/packages/by-barcode/{barcode}
PATCH  /api/v1/fulfillment/packages/{id}/print-label
PATCH  /api/v1/fulfillment/packages/{id}/hand-to-cargo

# Dashboard / Raporlar
GET    /api/v1/fulfillment/dashboard/summary
GET    /api/v1/fulfillment/dashboard/picking-status
GET    /api/v1/fulfillment/dashboard/packing-status
GET    /api/v1/fulfillment/dashboard/obm-items
```

### 6.7 Finance Modülü

```
# Tedarikçiler
GET    /api/v1/finance/suppliers
GET    /api/v1/finance/suppliers/{id}
POST   /api/v1/finance/suppliers
PUT    /api/v1/finance/suppliers/{id}
DELETE /api/v1/finance/suppliers/{id}
GET    /api/v1/finance/suppliers/{id}/balance

# Alış Faturaları
GET    /api/v1/finance/supplier-invoices
GET    /api/v1/finance/supplier-invoices/{id}
POST   /api/v1/finance/supplier-invoices
PUT    /api/v1/finance/supplier-invoices/{id}
DELETE /api/v1/finance/supplier-invoices/{id}
PATCH  /api/v1/finance/supplier-invoices/{id}/confirm
PATCH  /api/v1/finance/supplier-invoices/{id}/cancel

# Teslimatlar
GET    /api/v1/finance/supplier-deliveries
GET    /api/v1/finance/supplier-deliveries/{id}
POST   /api/v1/finance/supplier-deliveries
PUT    /api/v1/finance/supplier-deliveries/{id}
PATCH  /api/v1/finance/supplier-deliveries/{id}/start-receiving
PATCH  /api/v1/finance/supplier-deliveries/{id}/complete
POST   /api/v1/finance/supplier-deliveries/{id}/items/{itemId}/receive

# Cari Hesap
GET    /api/v1/finance/suppliers/{supplierId}/transactions
GET    /api/v1/finance/suppliers/{supplierId}/statement

# Ödemeler
GET    /api/v1/finance/supplier-payments
GET    /api/v1/finance/supplier-payments/{id}
POST   /api/v1/finance/supplier-payments
PATCH  /api/v1/finance/supplier-payments/{id}/complete
PATCH  /api/v1/finance/supplier-payments/{id}/cancel

# Tedarikçiye İadeler
GET    /api/v1/finance/supplier-returns
GET    /api/v1/finance/supplier-returns/{id}
POST   /api/v1/finance/supplier-returns
PUT    /api/v1/finance/supplier-returns/{id}
PATCH  /api/v1/finance/supplier-returns/{id}/confirm
PATCH  /api/v1/finance/supplier-returns/{id}/ship
PATCH  /api/v1/finance/supplier-returns/{id}/complete

# Fiyat Geçmişi
GET    /api/v1/finance/suppliers/{supplierId}/price-history
GET    /api/v1/finance/variants/{variantId}/purchase-price-history
```

### 6.8 Promotion Modülü

```
# Kampanya Tipleri
GET    /api/v1/promotions/campaign-types
GET    /api/v1/promotions/campaign-types/{id}

# Kampanyalar
GET    /api/v1/promotions/campaigns
GET    /api/v1/promotions/campaigns/{id}
POST   /api/v1/promotions/campaigns
PUT    /api/v1/promotions/campaigns/{id}
DELETE /api/v1/promotions/campaigns/{id}
PATCH  /api/v1/promotions/campaigns/{id}/activate
PATCH  /api/v1/promotions/campaigns/{id}/deactivate
POST   /api/v1/promotions/campaigns/{id}/duplicate

# Kampanya Ürünleri
GET    /api/v1/promotions/campaigns/{id}/products
POST   /api/v1/promotions/campaigns/{id}/products
DELETE /api/v1/promotions/campaigns/{id}/products/{productId}
POST   /api/v1/promotions/campaigns/{id}/products/refresh-filter
GET    /api/v1/promotions/campaigns/{id}/exclusions
POST   /api/v1/promotions/campaigns/{id}/exclusions
DELETE /api/v1/promotions/campaigns/{id}/exclusions/{exclusionId}

# Kampanya Platform Kısıtlamaları
GET    /api/v1/promotions/campaigns/{id}/platforms
PUT    /api/v1/promotions/campaigns/{id}/platforms

# Kuponlar
GET    /api/v1/promotions/coupons
GET    /api/v1/promotions/coupons/{id}
GET    /api/v1/promotions/coupons/by-code/{code}
POST   /api/v1/promotions/coupons
PUT    /api/v1/promotions/coupons/{id}
DELETE /api/v1/promotions/coupons/{id}
PATCH  /api/v1/promotions/coupons/{id}/activate
PATCH  /api/v1/promotions/coupons/{id}/deactivate
POST   /api/v1/promotions/coupons/generate-bulk
GET    /api/v1/promotions/coupons/{id}/usages

# İndirim Hesaplama
POST   /api/v1/promotions/calculate
POST   /api/v1/promotions/validate-coupon
```

### 6.9 IAM Modülü

```
# Kimlik Doğrulama
POST   /api/v1/iam/auth/login
POST   /api/v1/iam/auth/logout
POST   /api/v1/iam/auth/refresh-token
POST   /api/v1/iam/auth/forgot-password
POST   /api/v1/iam/auth/reset-password
POST   /api/v1/iam/auth/change-password
GET    /api/v1/iam/auth/me

# Kullanıcılar
GET    /api/v1/iam/users
GET    /api/v1/iam/users/{id}
POST   /api/v1/iam/users
PUT    /api/v1/iam/users/{id}
DELETE /api/v1/iam/users/{id}
PATCH  /api/v1/iam/users/{id}/activate
PATCH  /api/v1/iam/users/{id}/deactivate
PATCH  /api/v1/iam/users/{id}/reset-password
GET    /api/v1/iam/users/{id}/roles
PUT    /api/v1/iam/users/{id}/roles
GET    /api/v1/iam/users/{id}/permissions
PUT    /api/v1/iam/users/{id}/permissions
GET    /api/v1/iam/users/{id}/sessions

# Roller
GET    /api/v1/iam/roles
GET    /api/v1/iam/roles/{id}
POST   /api/v1/iam/roles
PUT    /api/v1/iam/roles/{id}
DELETE /api/v1/iam/roles/{id}
GET    /api/v1/iam/roles/{id}/permissions
PUT    /api/v1/iam/roles/{id}/permissions

# İzinler
GET    /api/v1/iam/permissions
GET    /api/v1/iam/permissions/by-module

# Oturumlar
GET    /api/v1/iam/sessions
DELETE /api/v1/iam/sessions/{id}
DELETE /api/v1/iam/users/{userId}/sessions

# Admin Menüler
GET    /api/v1/iam/admin-menus
GET    /api/v1/iam/admin-menus/tree
GET    /api/v1/iam/admin-menus/my-menu
POST   /api/v1/iam/admin-menus
PUT    /api/v1/iam/admin-menus/{id}
DELETE /api/v1/iam/admin-menus/{id}
PUT    /api/v1/iam/admin-menus/reorder

# Audit Logs
GET    /api/v1/iam/audit-logs
GET    /api/v1/iam/audit-logs/{id}
GET    /api/v1/iam/audit-logs/by-entity/{entityType}/{entityId}
```

### 6.10 CMS Modülü

```
# Site Menüleri
GET    /api/v1/cms/platforms/{platformId}/menus
GET    /api/v1/cms/menus/{id}
POST   /api/v1/cms/platforms/{platformId}/menus
PUT    /api/v1/cms/menus/{id}
DELETE /api/v1/cms/menus/{id}

# Menü Öğeleri
GET    /api/v1/cms/menus/{menuId}/items
GET    /api/v1/cms/menus/{menuId}/items/tree
POST   /api/v1/cms/menus/{menuId}/items
PUT    /api/v1/cms/menu-items/{id}
DELETE /api/v1/cms/menu-items/{id}
PUT    /api/v1/cms/menus/{menuId}/items/reorder

# Mega Panel
GET    /api/v1/cms/menu-items/{menuItemId}/mega-panel
PUT    /api/v1/cms/menu-items/{menuItemId}/mega-panel
GET    /api/v1/cms/mega-panels/{panelId}/groups
POST   /api/v1/cms/mega-panels/{panelId}/groups
PUT    /api/v1/cms/panel-groups/{id}
DELETE /api/v1/cms/panel-groups/{id}
GET    /api/v1/cms/panel-groups/{groupId}/items
POST   /api/v1/cms/panel-groups/{groupId}/items
PUT    /api/v1/cms/panel-items/{id}
DELETE /api/v1/cms/panel-items/{id}

# Sayfa Şablonları
GET    /api/v1/cms/page-templates
GET    /api/v1/cms/page-templates/{id}

# Bölüm Tipleri
GET    /api/v1/cms/section-types
GET    /api/v1/cms/section-types/{id}

# Sayfalar
GET    /api/v1/cms/platforms/{platformId}/pages
GET    /api/v1/cms/pages/{id}
GET    /api/v1/cms/pages/by-slug/{slug}
POST   /api/v1/cms/platforms/{platformId}/pages
PUT    /api/v1/cms/pages/{id}
DELETE /api/v1/cms/pages/{id}
PATCH  /api/v1/cms/pages/{id}/publish
PATCH  /api/v1/cms/pages/{id}/unpublish
POST   /api/v1/cms/pages/{id}/duplicate

# Sayfa Bölümleri
GET    /api/v1/cms/pages/{pageId}/sections
POST   /api/v1/cms/pages/{pageId}/sections
PUT    /api/v1/cms/page-sections/{id}
DELETE /api/v1/cms/page-sections/{id}
PUT    /api/v1/cms/pages/{pageId}/sections/reorder

# Bölüm Öğeleri
GET    /api/v1/cms/page-sections/{sectionId}/items
POST   /api/v1/cms/page-sections/{sectionId}/items
PUT    /api/v1/cms/section-items/{id}
DELETE /api/v1/cms/section-items/{id}
PUT    /api/v1/cms/page-sections/{sectionId}/items/reorder

# Ürün Listeleri
GET    /api/v1/cms/platforms/{platformId}/product-lists
GET    /api/v1/cms/product-lists/{id}
POST   /api/v1/cms/platforms/{platformId}/product-lists
PUT    /api/v1/cms/product-lists/{id}
DELETE /api/v1/cms/product-lists/{id}
GET    /api/v1/cms/product-lists/{id}/items
POST   /api/v1/cms/product-lists/{id}/items
DELETE /api/v1/cms/product-lists/{id}/items/{itemId}
PUT    /api/v1/cms/product-lists/{id}/items/reorder
POST   /api/v1/cms/product-lists/{id}/refresh
```

### 6.11 Integration Modülü

```
# Pazaryeri Entegrasyonları
GET    /api/v1/integrations/marketplaces
GET    /api/v1/integrations/marketplaces/{integrationId}/status
POST   /api/v1/integrations/marketplaces/{integrationId}/sync-products
POST   /api/v1/integrations/marketplaces/{integrationId}/sync-orders
POST   /api/v1/integrations/marketplaces/{integrationId}/sync-stocks
GET    /api/v1/integrations/marketplaces/{integrationId}/logs

# Kargo Entegrasyonları
GET    /api/v1/integrations/cargo
POST   /api/v1/integrations/cargo/{integrationId}/send-shipment/{shipmentId}
POST   /api/v1/integrations/cargo/{integrationId}/get-tracking/{shipmentId}
POST   /api/v1/integrations/cargo/{integrationId}/cancel-shipment/{shipmentId}

# Fatura Entegratörü
GET    /api/v1/integrations/invoice-integrators
POST   /api/v1/integrations/invoice-integrators/{integrationId}/send-invoice/{invoiceId}
POST   /api/v1/integrations/invoice-integrators/{integrationId}/check-status/{invoiceId}
POST   /api/v1/integrations/invoice-integrators/{integrationId}/cancel-invoice/{invoiceId}
POST   /api/v1/integrations/invoice-integrators/{integrationId}/check-taxpayer

# Stok Senkronizasyon Kuyruğu
GET    /api/v1/integrations/stock-sync/queue
GET    /api/v1/integrations/stock-sync/queue/status
POST   /api/v1/integrations/stock-sync/queue/retry/{id}
DELETE /api/v1/integrations/stock-sync/queue/{id}

# Genel Entegrasyon Logları
GET    /api/v1/integrations/logs
GET    /api/v1/integrations/logs/{id}
```

### 6.12 POS Modülü (Mağaza Satış)

```
# Kasalar
GET    /api/v1/pos/registers
GET    /api/v1/pos/registers/{id}
POST   /api/v1/pos/registers
PUT    /api/v1/pos/registers/{id}
DELETE /api/v1/pos/registers/{id}

# Kasa Oturumları
GET    /api/v1/pos/sessions
GET    /api/v1/pos/sessions/{id}
GET    /api/v1/pos/sessions/current
POST   /api/v1/pos/sessions/open
POST   /api/v1/pos/sessions/{id}/close
POST   /api/v1/pos/sessions/{id}/suspend
POST   /api/v1/pos/sessions/{id}/resume
GET    /api/v1/pos/sessions/{id}/report
GET    /api/v1/pos/sessions/{id}/transactions

# Kasa Hareketleri
POST   /api/v1/pos/sessions/{id}/cash-in
POST   /api/v1/pos/sessions/{id}/cash-out

# Hızlı Satış
POST   /api/v1/pos/sales
POST   /api/v1/pos/sales/quick
GET    /api/v1/pos/products/by-barcode/{barcode}
GET    /api/v1/pos/products/by-sku/{sku}
GET    /api/v1/pos/products/search

# Hızlı Erişim Ürünleri
GET    /api/v1/pos/registers/{registerId}/quick-products
POST   /api/v1/pos/registers/{registerId}/quick-products
PUT    /api/v1/pos/quick-products/{id}
DELETE /api/v1/pos/quick-products/{id}
PUT    /api/v1/pos/registers/{registerId}/quick-products/reorder

# POS İade
POST   /api/v1/pos/returns
GET    /api/v1/pos/receipts/by-number/{receiptNumber}
GET    /api/v1/pos/orders/by-receipt/{receiptNumber}

# Müşteri (POS)
GET    /api/v1/pos/customers/search
GET    /api/v1/pos/customers/by-phone/{phone}
GET    /api/v1/pos/customers/by-card/{cardNumber}
POST   /api/v1/pos/customers/quick-add
GET    /api/v1/pos/customers/{id}/recent-orders

# Fiş/Makbuz
GET    /api/v1/pos/receipts/{id}
POST   /api/v1/pos/receipts/{id}/reprint
GET    /api/v1/pos/receipts/{id}/pdf

# POS Raporları
GET    /api/v1/pos/reports/daily-summary
GET    /api/v1/pos/reports/hourly-sales
GET    /api/v1/pos/reports/cashier-performance
GET    /api/v1/pos/reports/product-sales
```

---

## 7. Store (Storefront) API

Müşteriye dönük API'ler:

```
# Katalog
GET    /api/v1/store/categories
GET    /api/v1/store/categories/{slug}
GET    /api/v1/store/products
GET    /api/v1/store/products/{slug}
GET    /api/v1/store/products/{slug}/variants
GET    /api/v1/store/search

# Sepet
GET    /api/v1/store/cart
POST   /api/v1/store/cart/items
PUT    /api/v1/store/cart/items/{itemId}
DELETE /api/v1/store/cart/items/{itemId}
DELETE /api/v1/store/cart/clear
POST   /api/v1/store/cart/apply-coupon
DELETE /api/v1/store/cart/remove-coupon

# Checkout
POST   /api/v1/store/checkout/calculate
POST   /api/v1/store/checkout/create-order
GET    /api/v1/store/checkout/payment-methods

# Üyelik
POST   /api/v1/store/auth/register
POST   /api/v1/store/auth/login
POST   /api/v1/store/auth/logout
POST   /api/v1/store/auth/forgot-password
POST   /api/v1/store/auth/reset-password
GET    /api/v1/store/account/profile
PUT    /api/v1/store/account/profile
PUT    /api/v1/store/account/change-password

# Adresler
GET    /api/v1/store/account/addresses
POST   /api/v1/store/account/addresses
PUT    /api/v1/store/account/addresses/{id}
DELETE /api/v1/store/account/addresses/{id}

# Siparişlerim
GET    /api/v1/store/account/orders
GET    /api/v1/store/account/orders/{id}
POST   /api/v1/store/account/orders/{id}/return-request

# Cüzdan ve Sadakat
GET    /api/v1/store/account/wallet
GET    /api/v1/store/account/loyalty

# CMS
GET    /api/v1/store/menus/{menuCode}
GET    /api/v1/store/pages/{slug}
GET    /api/v1/store/home

# Adres Yardımcıları
GET    /api/v1/store/address/countries
GET    /api/v1/store/address/cities/{countryId}
GET    /api/v1/store/address/districts/{cityId}
GET    /api/v1/store/address/neighborhoods/{districtId}

# B2B - Sipariş Şablonları
GET    /api/v1/store/account/order-templates
GET    /api/v1/store/account/order-templates/{id}
POST   /api/v1/store/account/order-templates
PUT    /api/v1/store/account/order-templates/{id}
DELETE /api/v1/store/account/order-templates/{id}
POST   /api/v1/store/account/order-templates/{id}/add-to-cart

# B2B - Hızlı Sipariş
POST   /api/v1/store/cart/quick-add
POST   /api/v1/store/cart/import-csv

# B2B - Teklifler
GET    /api/v1/store/account/quotes
GET    /api/v1/store/account/quotes/{id}
POST   /api/v1/store/checkout/request-quote
PATCH  /api/v1/store/account/quotes/{id}/accept
PATCH  /api/v1/store/account/quotes/{id}/reject

# B2B - Cari Hesap
GET    /api/v1/store/account/credit
GET    /api/v1/store/account/statement
```

---

## 8. WebSocket / SignalR Endpoints

Gerçek zamanlı iletişim için:

```
/hubs/fulfillment    # Depo operasyonları
/hubs/notifications  # Bildirimler
/hubs/dashboard      # Canlı dashboard
```

**Fulfillment Hub Events:**
- `PickingTaskAssigned`
- `ItemPicked`
- `ItemNotFound`
- `OrderReadyToPack`
- `PackageCompleted`
- `VoiceCommand`

**Notification Hub Events:**
- `NewOrder`
- `StockAlert`
- `SystemAlert`

---

## 9. Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 1.0 | Ocak 2025 | İlk versiyon |
| 1.1 | Ocak 2025 | B2B endpoint'leri eklendi: müşteri kredisi, özel fiyatlar, teklifler, sipariş şablonları, hızlı sipariş |
| 1.2 | Ocak 2025 | POS endpoint'leri eklendi: kasalar, oturumlar, hızlı satış, fiş yönetimi, POS raporları |
