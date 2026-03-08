# E-Ticaret Altyapı Projesi - Veritabanı Tasarımı

**Doküman Versiyonu:** 1.0  
**Son Güncelleme:** Ocak 2025  
**Veritabanı:** PostgreSQL  
**Yaklaşım:** Database First

---

## 1. Genel Kurallar

### 1.1 Tablo İsimlendirme
Modül bazlı ön ekler kullanılıyor:
- `core_` → Temel tanımlar, sistem ayarları
- `catalog_` → Ürün, kategori, özellikler
- `inv_` → Stok, depo (inventory)
- `crm_` → Müşteri (customer relationship)
- `ord_` → Sipariş (order)
- `ful_` → Operasyon (fulfillment)
- `fin_` → Finans, tedarik, cari
- `prm_` → Kampanya, promosyon (promotion)
- `iam_` → Kullanıcı, yetki (identity & access)

### 1.2 Ortak Alanlar
Tüm tablolarda bulunan alanlar:
```
id (UUID, PK) → gen_random_uuid()
created_at (timestamp)
created_by (UUID, FK → iam_users, nullable)
updated_at (timestamp)
updated_by (UUID, FK → iam_users, nullable)
deleted_at (timestamp, nullable) → soft delete
deleted_by (UUID, FK → iam_users, nullable)
```

### 1.3 Çok Dilli Yapı
- Kısa alanlar: `name_i18n (JSONB)` → `{"tr": "...", "en": "..."}`
- Uzun içerikler: `core_contents` tablosu

### 1.4 Enum/Lookup Değerleri
- Tablolarda string olarak saklanır
- `core_lookup_types` ve `core_lookup_values` tablolarından yönetilir
- Uygulama katmanında doğrulama yapılır

---

## 2. Core Modülü (Temel Tanımlar)

### core_languages
Desteklenen diller.
```
id (UUID, PK)
code (varchar, unique) → "tr", "en", "de", "zh"
native_name (varchar) → "Türkçe", "English", "Deutsch", "中文" (site dil seçici için)
direction (varchar) → "ltr", "rtl"
is_default (boolean)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

Not: 
- Site dil seçicide `native_name` gösterilir
- Admin panelde dil isimleri `core_contents` tablosundan çekilir (entity_type="language", field_name="name")

### core_contents
Uzun çok dilli içerikler.
```
id (UUID, PK)
entity_type (varchar) → "product", "category", "campaign"
entity_id (UUID)
field_name (varchar) → "description", "specifications"
language_code (varchar) → "tr", "en"
content (text)
[ortak alanlar]

UNIQUE(entity_type, entity_id, field_name, language_code)
```

### core_lookup_types
Lookup tipleri.
```
id (UUID, PK)
code (varchar, unique) → "order_status", "payment_status"
name_i18n (JSONB)
description (text, nullable)
is_system (boolean) → sistem tanımlı mı
[ortak alanlar]
```

### core_lookup_values
Lookup değerleri.
```
id (UUID, PK)
lookup_type_id (UUID, FK → core_lookup_types)
code (varchar) → "pending", "confirmed"
name_i18n (JSONB)
color (varchar, nullable) → UI renk kodu
icon (varchar, nullable)
extra_data (JSONB, nullable)
is_default (boolean)
is_active (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(lookup_type_id, code)
```

### core_platform_types
Platform tipleri (sistem tanımlı).
```
id (UUID, PK)
code (varchar, unique) → "trendyol", "hepsiburada", "site"
name_i18n (JSONB)
is_marketplace (boolean)
is_active (boolean)
settings_schema (JSONB, nullable)
[ortak alanlar]
```

### core_firms
Firmalar.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
tax_office (varchar)
tax_number (varchar)
address (text)
phone (varchar)
email (varchar)
is_main (boolean)
price_type (varchar) → "manual", "multiplier"
price_multiplier (numeric, nullable)
invoice_integrator_id (UUID, FK → core_firm_integrations, nullable)
is_active (boolean)
[ortak alanlar]
```

### core_firm_platforms
Firma-platform kombinasyonları.
```
id (UUID, PK)
firm_id (UUID, FK → core_firms)
platform_type_id (UUID, FK → core_platform_types)
code (varchar, unique)
name_i18n (JSONB)
credentials (JSONB)
settings (JSONB)
price_type (varchar, nullable) → override
price_multiplier (numeric, nullable)
invoice_series_id (UUID, FK → ord_invoice_series, nullable)
is_active (boolean)
[ortak alanlar]
```

### core_integration_services
Entegrasyon servisleri (sistem tanımlı).
```
id (UUID, PK)
code (varchar, unique) → "trendyol", "yurtici_kargo", "logo_efatura"
name_i18n (JSONB)
service_type (varchar) → "marketplace", "cargo", "invoice_integrator", "payment", "sms", "erp", "other"
is_available (boolean) → plugin hazır mı
settings_schema (JSONB, nullable)
[ortak alanlar]
```

### core_firm_integrations
Firma entegrasyonları (sözleşmeler).
```
id (UUID, PK)
firm_id (UUID, FK → core_firms)
integration_service_id (UUID, FK → core_integration_services)
name (varchar, nullable)
credentials (JSONB)
settings (JSONB)
is_active (boolean)
[ortak alanlar]
```

### core_expense_types
Masraf tipleri.
```
id (UUID, PK)
code (varchar, unique) → "shipping", "cod_fee", "assembly"
name_i18n (JSONB)
is_item_level (boolean)
default_tax_rate (numeric)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### core_cargo_rules
Kargo seçim kuralları.
```
id (UUID, PK)
firm_id (UUID, FK → core_firms)
firm_integration_id (UUID, FK → core_firm_integrations)
rule_type (varchar) → "default", "neighborhood", "payment_type", "combined"
payment_type (varchar, nullable) → "prepaid", "cod_cash", "cod_card"
neighborhood_id (UUID, FK, nullable)
city_id (UUID, FK, nullable)
priority (integer)
is_active (boolean)
[ortak alanlar]
```

### core_order_statuses
Sipariş durum tanımları.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
color (varchar, nullable)
sort_order (integer)
is_active (boolean)
[ortak alanlar]
```

### core_order_item_statuses
Sipariş kalemi durum tanımları.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
color (varchar, nullable)
sort_order (integer)
is_active (boolean)
[ortak alanlar]
```

### core_payment_methods
Ödeme yöntemleri.
```
id (UUID, PK)
code (varchar, unique) → "credit_card", "cod_cash", "wallet"
name_i18n (JSONB)
is_online (boolean)
requires_confirmation (boolean)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### core_return_reasons
İade nedenleri.
```
id (UUID, PK)
code (varchar, unique) → "defective", "wrong_product", "changed_mind"
name_i18n (JSONB)
requires_inspection (boolean)
is_customer_fault (boolean)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### core_notification_types
Bildirim tipleri.
```
id (UUID, PK)
code (varchar, unique) → "order_confirmed", "shipped", "delivered"
name_i18n (JSONB)
default_channels (JSONB) → ["sms", "email"]
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### core_notification_templates
Bildirim şablonları (çok dilli).
```
id (UUID, PK)
notification_type_id (UUID, FK)
language_code (varchar)
channel (varchar) → "sms", "email", "push"
subject (varchar, nullable)
body (text)
is_active (boolean)
[ortak alanlar]

UNIQUE(notification_type_id, language_code, channel)
```

### core_firm_notification_settings
Firma bildirim ayarları.
```
id (UUID, PK)
firm_id (UUID, FK)
notification_type_id (UUID, FK)
is_enabled (boolean)
channels (JSONB) → ["sms", "email"]
sms_provider_id (UUID, FK → core_firm_integrations, nullable)
email_provider_id (UUID, FK → core_firm_integrations, nullable)
push_provider_id (UUID, FK → core_firm_integrations, nullable)
[ortak alanlar]

UNIQUE(firm_id, notification_type_id)
```

---

## 3. Catalog Modülü (Ürün Kataloğu)

### catalog_attribute_types
Özellik tipleri.
```
id (UUID, PK)
code (varchar, unique) → "color", "size", "material"
name_i18n (JSONB)
data_type (varchar) → "select", "multi_select", "text", "number", "boolean"
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### catalog_attribute_values
Özellik değerleri.
```
id (UUID, PK)
attribute_type_id (UUID, FK)
code (varchar) → "red", "blue", "s", "m"
name_i18n (JSONB)
extra_data (JSONB, nullable) → renk kodu, görsel vb.
is_active (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(attribute_type_id, code)
```

### catalog_product_groups
Ürün grupları (şablonlar).
```
id (UUID, PK)
code (varchar, unique) → "tshirt", "pants", "mobile_phone"
name_i18n (JSONB)
parent_id (UUID, FK → self, nullable)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### catalog_product_group_attributes
Grup-özellik ilişkisi.
```
id (UUID, PK)
product_group_id (UUID, FK)
attribute_type_id (UUID, FK)
is_variant (boolean) → varyant mı, bilgi mi
is_required (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(product_group_id, attribute_type_id)
```

### catalog_categories
Kategoriler (müşteriye dönük).
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
parent_id (UUID, FK → self, nullable)
fill_type (varchar) → "manual", "filter", "mixed"
filter_rules (JSONB, nullable)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### catalog_category_products
Kategori-ürün ilişkisi.
```
id (UUID, PK)
category_id (UUID, FK)
product_id (UUID, FK)
sort_order (integer)
is_pinned (boolean)
[ortak alanlar]

UNIQUE(category_id, product_id)
```

### catalog_products
Ana ürünler.
```
id (UUID, PK)
product_group_id (UUID, FK)
code (varchar, unique)
name_i18n (JSONB)
short_description_i18n (JSONB, nullable)
is_active (boolean)
[ortak alanlar]
```

### catalog_product_attributes
Ürün özellikleri (varyant olmayan).
```
id (UUID, PK)
product_id (UUID, FK)
attribute_type_id (UUID, FK)
attribute_value_id (UUID, FK, nullable)
custom_value (JSONB, nullable)
[ortak alanlar]

UNIQUE(product_id, attribute_type_id)
```

### catalog_product_variants
Varyantlar (SKU).
```
id (UUID, PK)
product_id (UUID, FK)
sku (varchar, unique)
base_price (numeric)
base_cost (numeric, nullable)
is_active (boolean)
[ortak alanlar]
```

### catalog_product_variant_attributes
Varyant özellikleri.
```
id (UUID, PK)
variant_id (UUID, FK)
attribute_type_id (UUID, FK)
attribute_value_id (UUID, FK)
[ortak alanlar]

UNIQUE(variant_id, attribute_type_id)
```

### catalog_product_variant_images
Varyant görselleri.
```
id (UUID, PK)
variant_id (UUID, FK)
image_url (varchar)
sort_order (integer)
is_main (boolean)
[ortak alanlar]
```

### catalog_firm_platform_products
Platform bazlı ürün override.
```
id (UUID, PK)
firm_platform_id (UUID, FK)
product_id (UUID, FK)
name_i18n (JSONB, nullable)
short_description_i18n (JSONB, nullable)
is_active (boolean)
[ortak alanlar]

UNIQUE(firm_platform_id, product_id)
```

### catalog_firm_platform_variants
Platform bazlı varyant fiyat.
```
id (UUID, PK)
firm_platform_id (UUID, FK)
variant_id (UUID, FK)
price_type (varchar, nullable) → "manual", "multiplier"
price_multiplier (numeric, nullable)
price (numeric, nullable)
compare_at_price (numeric, nullable)
is_active (boolean)
[ortak alanlar]

UNIQUE(firm_platform_id, variant_id)
```

### catalog_product_units
Ürün birimleri (B2B - toptan satış için).
```
id (UUID, PK)
variant_id (UUID, FK)
unit_type (varchar) → "piece", "dozen", "box", "carton", "pallet"
unit_name_i18n (JSONB) → {"tr": "Düzine", "en": "Dozen"}
pieces_per_unit (integer) → 1 düzine = 12 adet
is_default (boolean)
min_order_quantity (integer) → bu birimde minimum sipariş miktarı
price_multiplier (numeric, nullable) → birim fiyat çarpanı
[ortak alanlar]

UNIQUE(variant_id, unit_type)
```

---

## 4. Inventory Modülü (Stok/Depo)

### inv_warehouses
Depolar.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
warehouse_type (varchar) → "main", "secondary", "store", "store_warehouse", "virtual", "receiving", "studio", "tailor", "defective", "other"
address (text, nullable)
is_sellable_online (boolean)
reserve_priority (integer)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### inv_warehouse_locations
Depo lokasyonları.
```
id (UUID, PK)
warehouse_id (UUID, FK)
code (varchar)
barcode (varchar, unique)
name (varchar, nullable)
parent_id (UUID, FK → self, nullable)
location_type (varchar) → "zone", "aisle", "rack", "shelf", "bin"
reserve_priority (integer)
picking_order (integer)
is_active (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(warehouse_id, code)
```

### inv_stocks
Stok kayıtları.
```
id (UUID, PK)
variant_id (UUID, FK)
warehouse_id (UUID, FK)
location_id (UUID, FK, nullable)
stock_type (varchar) → "physical", "virtual"
quantity (integer)
reserved_quantity (integer)
available_quantity (integer) → generated (quantity - reserved_quantity)
[ortak alanlar]

UNIQUE(variant_id, warehouse_id, location_id, stock_type)
```

### inv_stock_movements
Stok hareketleri.
```
id (UUID, PK)
variant_id (UUID, FK)
from_warehouse_id (UUID, FK, nullable)
to_warehouse_id (UUID, FK, nullable)
from_location_id (UUID, FK, nullable)
to_location_id (UUID, FK, nullable)
movement_type (varchar) → "purchase", "sale", "return", "transfer", "adjustment", "defective", "donation"
quantity (integer)
reference_type (varchar, nullable)
reference_id (UUID, nullable)
notes (text, nullable)
created_at (timestamp)
created_by (UUID, FK, nullable)
```

### inv_stock_reservations
Stok rezervasyonları.
```
id (UUID, PK)
stock_id (UUID, FK)
variant_id (UUID, FK)
warehouse_id (UUID, FK)
location_id (UUID, FK, nullable)
quantity (integer)
reference_type (varchar) → "order", "transfer_request"
reference_id (UUID)
status (varchar) → "reserved", "picked", "released", "cancelled"
[ortak alanlar]
```

### inv_transfer_requests
Transfer talepleri.
```
id (UUID, PK)
code (varchar, unique)
from_warehouse_id (UUID, FK)
to_warehouse_id (UUID, FK)
transfer_type (varchar) → "studio", "tailor", "inter_warehouse", "defective", "donation", "supplier_return", "other"
status (varchar) → "draft", "pending", "picking", "picked", "in_transit", "delivered", "completed", "cancelled"
requested_by (UUID, FK)
requested_at (timestamp)
notes (text, nullable)
[ortak alanlar]
```

### inv_transfer_request_items
Transfer kalemleri.
```
id (UUID, PK)
transfer_request_id (UUID, FK)
variant_id (UUID, FK)
requested_quantity (integer)
picked_quantity (integer, default 0)
delivered_quantity (integer, default 0)
from_location_id (UUID, FK, nullable)
to_location_id (UUID, FK, nullable)
status (varchar) → "pending", "picking", "picked", "in_transit", "delivered", "completed", "cancelled"
[ortak alanlar]
```

### inv_transfer_tracking
Transfer takip (detaylı izleme).
```
id (UUID, PK)
transfer_request_id (UUID, FK)
transfer_item_id (UUID, FK, nullable)
action (varchar) → "created", "approved", "picking_started", "item_picked", "handed_to_carrier", "received", "item_placed", "completed", "cancelled"
from_user_id (UUID, FK, nullable)
to_user_id (UUID, FK, nullable)
quantity (integer, nullable)
notes (text, nullable)
created_at (timestamp)
created_by (UUID, FK, nullable)
```

---

## 5. CRM Modülü (Müşteri)

### crm_member_groups
Üyelik grupları.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
is_default (boolean)
is_wholesale (boolean) → toptan müşteri grubu mu
requires_approval (boolean) → sipariş onayı gerekli mi
show_prices_before_login (boolean) → giriş yapmadan fiyat göster
min_order_amount (numeric, nullable) → minimum sipariş tutarı
payment_terms_days (integer, nullable) → vade günü (30, 60 vb.)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### crm_members
Üyeler.
```
id (UUID, PK)
member_group_id (UUID, FK)
email (varchar, unique, nullable)
phone (varchar, unique, nullable)
password_hash (varchar, nullable)
first_name (varchar)
last_name (varchar)
gender (varchar, nullable)
birth_date (date, nullable)
tax_office (varchar, nullable)
tax_number (varchar, nullable)
company_name (varchar, nullable)
is_registered (boolean)
is_email_verified (boolean)
is_phone_verified (boolean)
is_active (boolean)
last_login_at (timestamp, nullable)
anonymized_at (timestamp, nullable)
[ortak alanlar]
```

### crm_countries
Ülkeler.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
phone_code (varchar)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### crm_cities
Şehirler.
```
id (UUID, PK)
country_id (UUID, FK)
code (varchar)
name_i18n (JSONB)
is_active (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(country_id, code)
```

### crm_districts
İlçeler.
```
id (UUID, PK)
city_id (UUID, FK)
code (varchar)
name_i18n (JSONB)
is_active (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(city_id, code)
```

### crm_neighborhoods
Mahalleler.
```
id (UUID, PK)
district_id (UUID, FK)
code (varchar)
name_i18n (JSONB)
postal_code (varchar, nullable)
is_active (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(district_id, code)
```

### crm_streets
Sokaklar.
```
id (UUID, PK)
neighborhood_id (UUID, FK)
code (varchar)
name_i18n (JSONB)
is_active (boolean)
[ortak alanlar]

UNIQUE(neighborhood_id, code)
```

### crm_buildings
Binalar.
```
id (UUID, PK)
street_id (UUID, FK)
building_number (varchar)
address_code (varchar, unique, nullable)
postal_code (varchar, nullable)
is_active (boolean)
[ortak alanlar]
```

### crm_addresses
Üye adresleri.
```
id (UUID, PK)
member_id (UUID, FK)
title (varchar)
country_id (UUID, FK, nullable) → referans için
country_name (varchar) → "Türkiye"
city_id (UUID, FK, nullable)
city_name (varchar) → "İstanbul"
district_id (UUID, FK, nullable)
district_name (varchar) → "Kadıköy"
neighborhood_id (UUID, FK, nullable)
neighborhood_name (varchar, nullable) → "Caferağa"
street_id (UUID, FK, nullable)
street_name (varchar, nullable)
building_id (UUID, FK, nullable)
building_number (varchar, nullable)
door_number (varchar, nullable)
address_code (varchar, nullable)
address_line (text, nullable)
postal_code (varchar, nullable)
recipient_name (varchar)
recipient_phone (varchar)
delivery_notes (text, nullable)
is_default (boolean)
is_validated (boolean)
validated_at (timestamp, nullable)
validated_by (UUID, FK, nullable)
[ortak alanlar]
```
Not: Hem ID (referans/raporlama için) hem name (kalıcı değer) tutulur. Tanım tablolarından silme adresi bozmaz.

### crm_carts
Sepetler.
```
id (UUID, PK)
member_id (UUID, FK, nullable)
session_id (varchar, nullable)
firm_platform_id (UUID, FK)
currency_code (varchar)
notes (text, nullable)
merged_from_cart_id (UUID, FK, nullable)
[ortak alanlar]
```

### crm_cart_items
Sepet kalemleri.
```
id (UUID, PK)
cart_id (UUID, FK)
variant_id (UUID, FK)
quantity (integer)
added_price (numeric)
added_at (timestamp)
is_available (boolean)
available_quantity (integer)
last_checked_at (timestamp)
[ortak alanlar]
```

### crm_wallets
Cüzdanlar.
```
id (UUID, PK)
member_id (UUID, FK, unique)
balance (numeric)
currency_code (varchar)
[ortak alanlar]
```

### crm_wallet_transactions
Cüzdan hareketleri.
```
id (UUID, PK)
wallet_id (UUID, FK)
transaction_type (varchar) → "refund_credit", "payment_usage", "manual_adjustment", "withdrawal"
debit (numeric, default 0)
credit (numeric, default 0)
balance_after (numeric)
reference_type (varchar, nullable)
reference_id (UUID, nullable)
description (text, nullable)
[ortak alanlar]

CHECK (debit >= 0)
CHECK (credit >= 0)
CHECK (debit = 0 OR credit = 0)
```

### crm_loyalty_accounts
Sadakat hesapları.
```
id (UUID, PK)
member_id (UUID, FK, unique)
total_points (integer)
available_points (integer)
pending_points (integer)
currency_code (varchar)
points_to_currency_rate (numeric)
[ortak alanlar]
```

### crm_loyalty_transactions
Puan hareketleri.
```
id (UUID, PK)
loyalty_account_id (UUID, FK)
transaction_type (varchar) → "earn", "redeem", "expire", "cancel", "adjustment"
points (integer)
balance_after (integer)
reference_type (varchar, nullable)
reference_id (UUID, nullable)
expires_at (timestamp, nullable)
notes (text, nullable)
[ortak alanlar]
```

### crm_member_credits
Müşteri kredi limitleri (B2B).
```
id (UUID, PK)
member_id (UUID, FK, unique)
credit_limit (numeric)
used_credit (numeric)
available_credit (numeric) → generated (credit_limit - used_credit)
currency_code (varchar)
last_review_at (timestamp, nullable)
last_review_by (UUID, FK, nullable)
notes (text, nullable)
[ortak alanlar]
```

### crm_member_prices
Müşteriye özel fiyatlar (B2B).
```
id (UUID, PK)
member_id (UUID, FK)
variant_id (UUID, FK)
price (numeric)
min_quantity (integer) → bu fiyat için minimum miktar
valid_from (timestamp)
valid_until (timestamp, nullable)
[ortak alanlar]

UNIQUE(member_id, variant_id, min_quantity)
```

### crm_member_discounts
Müşteriye özel iskontolar (B2B).
```
id (UUID, PK)
member_id (UUID, FK)
discount_type (varchar) → "category", "product_group", "brand", "all"
target_id (UUID, nullable) → category_id veya product_group_id
discount_rate (numeric)
valid_from (timestamp)
valid_until (timestamp, nullable)
[ortak alanlar]
```

### crm_order_templates
Sipariş şablonları (B2B).
```
id (UUID, PK)
member_id (UUID, FK)
name (varchar)
description (text, nullable)
is_active (boolean)
last_used_at (timestamp, nullable)
[ortak alanlar]
```

### crm_order_template_items
Sipariş şablon kalemleri.
```
id (UUID, PK)
template_id (UUID, FK)
variant_id (UUID, FK)
quantity (integer)
unit_type (varchar)
sort_order (integer)
[ortak alanlar]
```

---

## 6. Order Modülü (Sipariş)

### ord_orders
Siparişler.
```
id (UUID, PK)
order_number (varchar, unique)
firm_platform_id (UUID, FK)
member_id (UUID, FK)
cart_id (UUID, FK, nullable)
status (varchar)
payment_status (varchar)

-- Sipariş tipi (B2B)
order_type (varchar) → "retail", "wholesale", "quote_conversion"
requires_approval (boolean)
approved_at (timestamp, nullable)
approved_by (UUID, FK, nullable)
quote_id (UUID, FK, nullable) → tekliften dönüştürüldüyse

-- Vade (B2B)
payment_terms_days (integer, nullable)
payment_due_date (date, nullable)

-- POS (Mağaza satışı)
is_pos_sale (boolean, default false)
pos_session_id (UUID, FK, nullable)
pos_register_id (UUID, FK, nullable)
receipt_number (varchar, nullable)

-- Para birimi ve kur
currency_code (varchar) → "TRY", "USD", "EUR" (satış para birimi)
invoice_currency_code (varchar) → "TRY" (fatura para birimi)
exchange_rate (numeric) → 1.00 veya 32.50 (satış → fatura dönüşüm kuru)

-- Teslimat adresi
shipping_recipient_name (varchar)
shipping_recipient_phone (varchar)
shipping_country_id (UUID, FK)
shipping_city_id (UUID, FK)
shipping_district_id (UUID, FK)
shipping_neighborhood_id (UUID, FK, nullable)
shipping_address_line (text)
shipping_postal_code (varchar, nullable)
shipping_delivery_notes (text, nullable)

-- Fatura adresi
billing_same_as_shipping (boolean)
billing_recipient_name (varchar, nullable)
billing_tax_office (varchar, nullable)
billing_tax_number (varchar, nullable)
billing_company_name (varchar, nullable)
billing_country_id (UUID, FK, nullable)
billing_city_id (UUID, FK, nullable)
billing_district_id (UUID, FK, nullable)
billing_address_line (text, nullable)

-- Tutarlar
subtotal (numeric)
total_discount (numeric)
total_expense (numeric)
total_tax (numeric)
grand_total (numeric)

-- Kargo
default_cargo_firm_id (UUID, FK, nullable)

-- Müşteri istekleri
customer_notes (JSONB, nullable)
internal_notes (text, nullable)

-- Onay
confirmation_required (boolean)
confirmed_at (timestamp, nullable)
confirmed_by (UUID, FK, nullable)

-- Operasyon alanları
picking_plan_id (UUID, FK, nullable)
sorting_bin_id (UUID, FK, nullable)
packing_station_code (varchar, nullable)
packing_slot_number (integer, nullable)

[ortak alanlar]
```

### ord_order_items
Sipariş kalemleri.
```
id (UUID, PK)
order_id (UUID, FK)
variant_id (UUID, FK)
sku (varchar)
product_name (varchar)
variant_info (varchar)
quantity (integer)
unit_price (numeric)
subtotal (numeric)
discount_amount (numeric)
tax_amount (numeric)
total (numeric)
status (varchar)

-- Toplama
pick_assigned_to (UUID, FK, nullable)
pick_assigned_at (timestamp, nullable)
picked_by (UUID, FK, nullable)
picked_at (timestamp, nullable)

-- Ara ayrıştırma
sorting_bin_quantity (integer, default 0)

-- Son ayrıştırma
final_sort_quantity (integer, default 0)

-- Son okutma
final_scan_by (UUID, FK, nullable)
final_scan_at (timestamp, nullable)
final_scan_quantity (integer, default 0)

[ortak alanlar]
```

### ord_order_discounts
Sipariş indirimleri.
```
id (UUID, PK)
order_id (UUID, FK)
order_item_id (UUID, FK, nullable)
discount_type (varchar) → "campaign", "coupon", "member_group", "manual"
discount_source_id (UUID, nullable)
discount_name (varchar)
discount_amount (numeric)
[ortak alanlar]
```

### ord_order_expenses
Sipariş masrafları.
```
id (UUID, PK)
order_id (UUID, FK)
order_item_id (UUID, FK, nullable)
expense_type_id (UUID, FK)
expense_name (varchar)
amount (numeric)
tax_amount (numeric)
[ortak alanlar]
```

### ord_order_taxes
Sipariş vergileri.
```
id (UUID, PK)
order_id (UUID, FK)
order_item_id (UUID, FK, nullable)
order_expense_id (UUID, FK, nullable)
tax_type (varchar) → "kdv", "otv"
tax_rate (numeric)
tax_amount (numeric)
[ortak alanlar]

CHECK (order_item_id IS NOT NULL OR order_expense_id IS NOT NULL)
```

### ord_order_payments
Sipariş ödemeleri.
```
id (UUID, PK)
order_id (UUID, FK)
payment_method_id (UUID, FK)
amount (numeric)
currency_code (varchar)
status (varchar)
details (JSONB)
[ortak alanlar]
```

### ord_invoice_series
Fatura seri numaraları.
```
id (UUID, PK)
firm_id (UUID, FK)
name (varchar, nullable)
e_archive_serial (char(3))
e_invoice_serial (char(3))
export_serial (char(3))
is_active (boolean)
[ortak alanlar]
```

### ord_invoices
Faturalar.
```
id (UUID, PK)
order_id (UUID, FK)
invoice_series_id (UUID, FK)
invoice_type (varchar) → "e_archive", "e_invoice", "export"
invoice_serial (char(3))
invoice_year (char(4))
invoice_sequence (integer)
invoice_number (varchar) → generated
invoice_date (timestamp)

-- Alıcı
recipient_name (varchar)
recipient_tax_office (varchar, nullable)
recipient_tax_number (varchar, nullable)
recipient_company_name (varchar, nullable)
recipient_address (text)

-- Tutarlar
subtotal (numeric)
total_discount (numeric)
total_tax (numeric)
grand_total (numeric)

-- Entegratör
integrator_status (varchar)
integrator_sent_at (timestamp, nullable)
integrator_response (JSONB, nullable)
integrator_invoice_url (varchar, nullable)

-- ERP
erp_status (varchar)
erp_sent_at (timestamp, nullable)
erp_reference (varchar, nullable)

-- Durum
status (varchar)
cancelled_by_invoice_id (UUID, FK, nullable)
cancels_invoice_id (UUID, FK, nullable)

[ortak alanlar]

UNIQUE(invoice_serial, invoice_year, invoice_sequence)
```

### ord_invoice_items
Fatura kalemleri.
```
id (UUID, PK)
invoice_id (UUID, FK)
order_item_id (UUID, FK, nullable)
description (varchar)
quantity (numeric)
unit_price (numeric)
discount_amount (numeric)
tax_rate (numeric)
tax_amount (numeric)
total (numeric)
[ortak alanlar]
```

### ord_shipments
Kargo gönderileri.
```
id (UUID, PK)
order_id (UUID, FK)
firm_integration_id (UUID, FK)
shipment_number (varchar, unique)
tracking_number (varchar, nullable)
tracking_url (varchar, nullable)
status (varchar)
cargo_status_raw (varchar, nullable)
api_status (varchar)
api_request_payload (JSONB, nullable)
api_response_payload (JSONB, nullable)
api_sent_at (timestamp, nullable)
estimated_delivery_date (date, nullable)
delivered_at (timestamp, nullable)
delivery_signature (varchar, nullable)
delivery_notes (text, nullable)
package_count (integer)
total_weight (numeric, nullable)
total_desi (numeric, nullable)
[ortak alanlar]
```

### ord_shipment_items
Kargo kalemleri.
```
id (UUID, PK)
shipment_id (UUID, FK)
order_item_id (UUID, FK)
quantity (integer)
[ortak alanlar]
```

### ord_shipment_events
Kargo hareketleri.
```
id (UUID, PK)
shipment_id (UUID, FK)
event_code (varchar)
event_description (varchar)
event_location (varchar, nullable)
event_date (timestamp)
raw_data (JSONB, nullable)
created_at (timestamp)
```

### ord_order_notifications
Sipariş bildirimleri.
```
id (UUID, PK)
order_id (UUID, FK)
notification_type_id (UUID, FK)
channel (varchar)
recipient (varchar)
subject (varchar, nullable)
body (text)
status (varchar)
provider_id (UUID, FK, nullable)
provider_reference (varchar, nullable)
provider_response (JSONB, nullable)
sent_at (timestamp, nullable)
failed_at (timestamp, nullable)
failure_reason (text, nullable)
retry_count (integer, default 0)
next_retry_at (timestamp, nullable)
[ortak alanlar]
```

### ord_gift_cards
Hediye çekleri.
```
id (UUID, PK)
code (varchar, unique)
firm_id (UUID, FK)
original_amount (numeric)
remaining_amount (numeric)
currency_code (varchar)
valid_from (date)
valid_until (date, nullable)
is_single_use (boolean)
created_for_member_id (UUID, FK, nullable)
created_from_order_id (UUID, FK, nullable)
status (varchar)
[ortak alanlar]
```

### ord_gift_card_transactions
Hediye çeki hareketleri.
```
id (UUID, PK)
gift_card_id (UUID, FK)
transaction_type (varchar)
amount (numeric)
balance_after (numeric)
order_id (UUID, FK, nullable)
notes (text, nullable)
[ortak alanlar]
```

### ord_returns
İade talepleri.
```
id (UUID, PK)
return_number (varchar, unique)
order_id (UUID, FK)
member_id (UUID, FK)
return_type (varchar)
customer_notes (text, nullable)
status (varchar)
return_cargo_firm_id (UUID, FK, nullable)
return_tracking_number (varchar, nullable)
return_cargo_sent_at (timestamp, nullable)
return_cargo_received_at (timestamp, nullable)
inspection_notes (text, nullable)
inspection_completed_at (timestamp, nullable)
inspection_completed_by (UUID, FK, nullable)
refund_method (varchar)
refund_status (varchar)
refund_amount (numeric)
[ortak alanlar]
```

### ord_return_items
İade kalemleri.
```
id (UUID, PK)
return_id (UUID, FK)
order_item_id (UUID, FK)
variant_id (UUID, FK)
quantity (integer)
return_reason_id (UUID, FK)
customer_notes (text, nullable)
unit_refund_amount (numeric)
total_refund_amount (numeric)
status (varchar)
inspection_result (varchar, nullable)
inspection_notes (text, nullable)
[ortak alanlar]
```

### ord_return_refunds
İade ödemeleri.
```
id (UUID, PK)
return_id (UUID, FK)
refund_method (varchar)
amount (numeric)
status (varchar)
details (JSONB)
original_payment_id (UUID, FK, nullable)
wallet_transaction_id (UUID, FK, nullable)
processed_at (timestamp, nullable)
processed_by (UUID, FK, nullable)
[ortak alanlar]
```

### ord_quotes
Teklifler (B2B).
```
id (UUID, PK)
quote_number (varchar, unique)
firm_platform_id (UUID, FK)
member_id (UUID, FK)
status (varchar) → "draft", "sent", "viewed", "accepted", "rejected", "expired", "converted"
valid_until (timestamp)
currency_code (varchar)

subtotal (numeric)
total_discount (numeric)
total_tax (numeric)
grand_total (numeric)

notes_to_customer (text, nullable)
internal_notes (text, nullable)

sent_at (timestamp, nullable)
viewed_at (timestamp, nullable)
responded_at (timestamp, nullable)
converted_order_id (UUID, FK, nullable)

[ortak alanlar]
```

### ord_quote_items
Teklif kalemleri.
```
id (UUID, PK)
quote_id (UUID, FK)
variant_id (UUID, FK)
sku (varchar)
product_name (varchar)
variant_info (varchar)
quantity (integer)
unit_type (varchar)
unit_price (numeric)
discount_rate (numeric)
discount_amount (numeric)
tax_rate (numeric)
tax_amount (numeric)
total (numeric)
notes (text, nullable)
[ortak alanlar]
```

---

## 7. Fulfillment Modülü (Operasyon)

### ful_picking_plans
Toplama planları.
```
id (UUID, PK)
plan_number (varchar, unique)
warehouse_id (UUID, FK)
plan_type (varchar) → "single_item", "bulk", "dropshipping", "manual"
status (varchar)
planned_by (UUID, FK)
planned_at (timestamp)
started_at (timestamp, nullable)
completed_at (timestamp, nullable)
[ortak alanlar]
```

### ful_sorting_bins
Ayrıştırma kolileri (sanal).
```
id (UUID, PK)
picking_plan_id (UUID, FK)
bin_number (integer)
status (varchar)
[ortak alanlar]
```

### ful_packing_stations
Paketleme masaları.
```
id (UUID, PK)
warehouse_id (UUID, FK)
station_code (varchar)
barcode (varchar, unique)
station_name (varchar, nullable)
slot_count (integer, default 20)
is_obm (boolean)
assigned_to (UUID, FK, nullable)
status (varchar)
[ortak alanlar]
```

### ful_packages
Paketler.
```
id (UUID, PK)
order_id (UUID, FK)
shipment_id (UUID, FK, nullable)
package_number (integer)
barcode (varchar, unique)
weight (numeric, nullable)
width (numeric, nullable)
height (numeric, nullable)
length (numeric, nullable)
desi (numeric, nullable)
status (varchar)
packed_at (timestamp, nullable)
packed_by (UUID, FK, nullable)
label_printed_at (timestamp, nullable)
[ortak alanlar]
```

---

## 8. POS Modülü (Mağaza Satış)

### pos_registers
Kasalar.
```
id (UUID, PK)
warehouse_id (UUID, FK) → mağaza deposu
firm_platform_id (UUID, FK)
code (varchar, unique) → "KASA-01"
name (varchar)
receipt_prefix (varchar) → fiş numarası ön eki
receipt_sequence (integer) → son fiş numarası
is_active (boolean)
[ortak alanlar]
```

### pos_sessions
Kasa oturumları.
```
id (UUID, PK)
register_id (UUID, FK)
user_id (UUID, FK) → kasiyer
session_number (varchar, unique)
opened_at (timestamp)
closed_at (timestamp, nullable)
opening_cash (numeric)
closing_cash (numeric, nullable)
expected_cash (numeric, nullable)
cash_difference (numeric, nullable)
status (varchar) → "open", "closed", "suspended"
notes (text, nullable)
[ortak alanlar]
```

### pos_session_transactions
Kasa hareketleri.
```
id (UUID, PK)
session_id (UUID, FK)
transaction_type (varchar) → "sale", "return", "cash_in", "cash_out"
amount (numeric)
payment_method (varchar)
reference_type (varchar, nullable) → "order", "return"
reference_id (UUID, nullable)
notes (text, nullable)
created_at (timestamp)
created_by (UUID, FK)
```

### pos_quick_products
Hızlı erişim ürünleri (kasa ekranı için).
```
id (UUID, PK)
register_id (UUID, FK, nullable) → NULL ise tüm kasalar
variant_id (UUID, FK)
button_text (varchar)
button_color (varchar, nullable)
category (varchar, nullable) → gruplama için
sort_order (integer)
is_active (boolean)
[ortak alanlar]
```

### pos_receipts
Fiş/makbuz kayıtları.
```
id (UUID, PK)
session_id (UUID, FK)
order_id (UUID, FK)
receipt_number (varchar, unique)
receipt_type (varchar) → "sale", "return"
printed_at (timestamp, nullable)
printed_by (UUID, FK, nullable)
reprint_count (integer, default 0)
[ortak alanlar]
```

---

## 8. Finance Modülü (Tedarik/Cari)

### fin_suppliers
Tedarikçiler.
```
id (UUID, PK)
code (varchar, unique)
name (varchar)
tax_office (varchar, nullable)
tax_number (varchar, nullable)
phone (varchar, nullable)
email (varchar, nullable)
address (text, nullable)
contact_person (varchar, nullable)
notes (text, nullable)
is_active (boolean)
[ortak alanlar]
```

### fin_supplier_invoices
Alış faturaları.
```
id (UUID, PK)
supplier_id (UUID, FK)
invoice_number (varchar)
invoice_date (date)
due_date (date, nullable)
subtotal (numeric)
total_discount (numeric)
total_tax (numeric)
grand_total (numeric)
status (varchar)
notes (text, nullable)
[ortak alanlar]
```

### fin_supplier_invoice_items
Alış fatura kalemleri.
```
id (UUID, PK)
invoice_id (UUID, FK)
variant_id (UUID, FK, nullable)
description (varchar)
quantity (numeric)
unit_price (numeric)
discount_rate (numeric, default 0)
discount_amount (numeric, default 0)
tax_rate (numeric)
tax_amount (numeric)
total (numeric)
[ortak alanlar]
```

### fin_supplier_deliveries
Tedarikçi teslimatları.
```
id (UUID, PK)
supplier_id (UUID, FK)
invoice_id (UUID, FK, nullable)
delivery_date (date)
delivery_note_number (varchar, nullable)
warehouse_id (UUID, FK)
status (varchar)
notes (text, nullable)
received_by (UUID, FK, nullable)
received_at (timestamp, nullable)
[ortak alanlar]
```

### fin_supplier_delivery_items
Teslimat kalemleri.
```
id (UUID, PK)
delivery_id (UUID, FK)
invoice_item_id (UUID, FK, nullable)
variant_id (UUID, FK)
expected_quantity (integer)
received_quantity (integer)
rejected_quantity (integer, default 0)
location_id (UUID, FK, nullable)
notes (text, nullable)
[ortak alanlar]
```

### fin_supplier_transactions
Cari hesap hareketleri.
```
id (UUID, PK)
supplier_id (UUID, FK)
transaction_type (varchar)
debit (numeric, default 0)
credit (numeric, default 0)
balance_after (numeric)
reference_type (varchar, nullable)
reference_id (UUID, nullable)
description (text, nullable)
transaction_date (date)
[ortak alanlar]
```

### fin_supplier_payments
Tedarikçi ödemeleri.
```
id (UUID, PK)
supplier_id (UUID, FK)
payment_date (date)
amount (numeric)
payment_type (varchar)
details (JSONB)
notes (text, nullable)
status (varchar)
[ortak alanlar]
```

### fin_supplier_returns
Tedarikçiye iadeler.
```
id (UUID, PK)
supplier_id (UUID, FK)
return_number (varchar, unique)
return_date (date)
reason (varchar)
status (varchar)
notes (text, nullable)
subtotal (numeric)
total_tax (numeric)
grand_total (numeric)
cargo_firm_id (UUID, FK, nullable)
tracking_number (varchar, nullable)
shipped_at (timestamp, nullable)
[ortak alanlar]
```

### fin_supplier_return_items
Tedarikçi iade kalemleri.
```
id (UUID, PK)
return_id (UUID, FK)
variant_id (UUID, FK)
quantity (integer)
unit_price (numeric)
tax_rate (numeric)
tax_amount (numeric)
total (numeric)
notes (text, nullable)
[ortak alanlar]
```

---

## 9. Promotion Modülü (Kampanya)

### prm_campaign_types
Kampanya tipleri (sistem tanımlı).
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
description_i18n (JSONB, nullable)
handler_class (varchar)
settings_schema (JSONB, nullable)
requires_products (boolean)
is_stackable (boolean)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### prm_campaigns
Kampanyalar.
```
id (UUID, PK)
campaign_type_id (UUID, FK)
firm_id (UUID, FK, nullable)
code (varchar, unique)
name_i18n (JSONB)
description_i18n (JSONB, nullable)
starts_at (timestamp)
ends_at (timestamp, nullable)
is_active (boolean)
priority (integer)
settings (JSONB)
product_selection_type (varchar)
product_filter (JSONB, nullable)
[ortak alanlar]
```

### prm_campaign_products
Kampanya ürünleri.
```
id (UUID, PK)
campaign_id (UUID, FK)
product_id (UUID, FK, nullable)
variant_id (UUID, FK, nullable)
added_type (varchar)
[ortak alanlar]

UNIQUE(campaign_id, product_id, variant_id)
```

### prm_campaign_exclusions
Kampanya hariç tutmaları.
```
id (UUID, PK)
campaign_id (UUID, FK)
product_id (UUID, FK, nullable)
variant_id (UUID, FK, nullable)
reason (text, nullable)
[ortak alanlar]

UNIQUE(campaign_id, product_id, variant_id)
```

### prm_campaign_platforms
Kampanya platform kısıtlamaları.
```
id (UUID, PK)
campaign_id (UUID, FK)
firm_platform_id (UUID, FK)
is_included (boolean)
[ortak alanlar]

UNIQUE(campaign_id, firm_platform_id)
```

### prm_coupons
Kuponlar.
```
id (UUID, PK)
campaign_id (UUID, FK, nullable)
member_id (UUID, FK, nullable)
code (varchar, unique)
name_i18n (JSONB)
coupon_type (varchar)
discount_value (numeric)
usage_limit_total (integer, nullable)
usage_limit_per_member (integer, nullable)
usage_count (integer, default 0)
minimum_cart_total (numeric, nullable)
valid_for_first_order_only (boolean, default false)
member_group_id (UUID, FK, nullable)
starts_at (timestamp)
ends_at (timestamp, nullable)
is_active (boolean)
[ortak alanlar]
```

### prm_coupon_usages
Kupon kullanımları.
```
id (UUID, PK)
coupon_id (UUID, FK)
member_id (UUID, FK)
order_id (UUID, FK)
discount_amount (numeric)
used_at (timestamp)
[ortak alanlar]
```

---

## 10. IAM Modülü (Kullanıcı/Yetki)

### iam_users
Kullanıcılar.
```
id (UUID, PK)
username (varchar, unique)
email (varchar, unique)
password_hash (varchar)
first_name (varchar)
last_name (varchar)
phone (varchar, nullable)
avatar_url (varchar, nullable)
firm_id (UUID, FK, nullable)
department (varchar)
job_title (varchar, nullable)
is_active (boolean)
last_login_at (timestamp, nullable)
password_changed_at (timestamp, nullable)
must_change_password (boolean, default false)
preferences (JSONB, nullable)
[ortak alanlar]
```

### iam_roles
Roller.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
description_i18n (JSONB, nullable)
is_system (boolean)
is_active (boolean)
[ortak alanlar]
```

### iam_permissions
İzinler.
```
id (UUID, PK)
code (varchar, unique)
name_i18n (JSONB)
description_i18n (JSONB, nullable)
module (varchar)
permission_type (varchar)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### iam_role_permissions
Rol-izin ilişkisi.
```
id (UUID, PK)
role_id (UUID, FK)
permission_id (UUID, FK)
[ortak alanlar]

UNIQUE(role_id, permission_id)
```

### iam_user_roles
Kullanıcı-rol ilişkisi.
```
id (UUID, PK)
user_id (UUID, FK)
role_id (UUID, FK)
firm_id (UUID, FK, nullable)
[ortak alanlar]

UNIQUE(user_id, role_id, firm_id)
```

### iam_user_permissions
Kullanıcıya özel izinler.
```
id (UUID, PK)
user_id (UUID, FK)
permission_id (UUID, FK)
grant_type (varchar) → "grant", "revoke"
firm_id (UUID, FK, nullable)
[ortak alanlar]

UNIQUE(user_id, permission_id, firm_id)
```

### iam_user_sessions
Oturum takibi.
```
id (UUID, PK)
user_id (UUID, FK)
token_hash (varchar)
ip_address (varchar)
user_agent (text, nullable)
device_info (JSONB, nullable)
expires_at (timestamp)
last_activity_at (timestamp)
is_active (boolean)
created_at (timestamp)
```

### iam_audit_logs
Kritik işlem logları.
```
id (UUID, PK)
user_id (UUID, FK, nullable)
entity_type (varchar)
entity_id (UUID)
action (varchar)
old_values (JSONB, nullable)
new_values (JSONB, nullable)
ip_address (varchar, nullable)
user_agent (text, nullable)
context (JSONB, nullable)
created_at (timestamp)
```

---

## 11. CMS Modülü (İçerik Yönetimi)

### cms_site_menus
Site menüleri.
```
id (UUID, PK)
firm_platform_id (UUID, FK)
code (varchar)
name_i18n (JSONB)
menu_type (varchar) → "header", "footer", "sidebar", "mobile"
display_style (varchar) → "simple", "dropdown", "mega", "full_page"
is_active (boolean)
sort_order (integer)
[ortak alanlar]

UNIQUE(firm_platform_id, code)
```

### cms_site_menu_items
Menü öğeleri.
```
id (UUID, PK)
menu_id (UUID, FK)
parent_id (UUID, FK → self, nullable)
name_i18n (JSONB)
item_type (varchar) → "link", "mega_panel", "page_link", "custom_page"
target_type (varchar, nullable) → "category", "product_group", "url", "page"
target_id (UUID, nullable)
custom_url (varchar, nullable)
icon (varchar, nullable)
image_url (varchar, nullable)
open_in_new_tab (boolean, default false)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### cms_menu_mega_panels
Mega menü panelleri.
```
id (UUID, PK)
menu_item_id (UUID, FK)
name (varchar, nullable)
layout_type (varchar) → "columns", "grid", "mixed"
column_count (integer, default 4)
background_color (varchar, nullable)
background_image_url (varchar, nullable)
custom_css (text, nullable)
is_active (boolean)
[ortak alanlar]
```

### cms_menu_panel_groups
Panel içi gruplar/sütunlar.
```
id (UUID, PK)
mega_panel_id (UUID, FK)
name_i18n (JSONB, nullable)
column_index (integer)
column_span (integer, default 1)
show_title (boolean, default true)
title_style (JSONB, nullable)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### cms_menu_panel_items
Grup içi öğeler.
```
id (UUID, PK)
panel_group_id (UUID, FK)
name_i18n (JSONB)
description_i18n (JSONB, nullable)
item_type (varchar) → "link", "category", "product_group", "image_link", "banner", "html"
target_type (varchar, nullable)
target_id (UUID, nullable)
custom_url (varchar, nullable)
image_url (varchar, nullable)
image_position (varchar, nullable) → "left", "right", "top", "background"
badge_text (varchar, nullable)
badge_color (varchar, nullable)
custom_html (text, nullable)
gender_filter (varchar, nullable)
open_in_new_tab (boolean, default false)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### cms_page_templates
Sayfa şablonları.
```
id (UUID, PK)
code (varchar, unique) → "home", "category_landing", "gender_landing", "product_list", "product_detail", "campaign_landing"
name_i18n (JSONB)
description_i18n (JSONB, nullable)
template_type (varchar) → "system", "custom"
default_layout (varchar) → "full_width", "with_sidebar", "magazine"
is_active (boolean)
[ortak alanlar]
```

### cms_pages
Sayfalar.
```
id (UUID, PK)
firm_platform_id (UUID, FK)
template_id (UUID, FK)
code (varchar)
name_i18n (JSONB)
slug_i18n (JSONB) → {"tr": "kadin", "en": "women"}
page_type (varchar) → "home", "landing", "content", "menu_page"
target_gender (varchar, nullable) → "male", "female", "kids", "unisex"
target_category_id (UUID, FK, nullable)
meta_title_i18n (JSONB, nullable)
meta_description_i18n (JSONB, nullable)
is_active (boolean)
publish_at (timestamp, nullable)
unpublish_at (timestamp, nullable)
[ortak alanlar]

UNIQUE(firm_platform_id, code)
```

### cms_section_types
Bölüm tipleri (sistem tanımlı).
```
id (UUID, PK)
code (varchar, unique) → "hero_banner", "slider", "product_carousel", "category_grid", "text_block", "image_gallery", "video", "countdown", "newsletter", "instagram_feed", "custom_html"
name_i18n (JSONB)
description_i18n (JSONB, nullable)
settings_schema (JSONB)
supports_items (boolean)
is_active (boolean)
[ortak alanlar]
```

### cms_page_sections
Sayfa bölümleri.
```
id (UUID, PK)
page_id (UUID, FK)
section_type_id (UUID, FK)
name (varchar, nullable)
title_i18n (JSONB, nullable)
subtitle_i18n (JSONB, nullable)
settings (JSONB)
layout_settings (JSONB, nullable)
background_color (varchar, nullable)
background_image_url (varchar, nullable)
custom_css (text, nullable)
visible_from (timestamp, nullable)
visible_until (timestamp, nullable)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### cms_page_section_items
Bölüm öğeleri.
```
id (UUID, PK)
section_id (UUID, FK)
item_type (varchar) → "image", "product", "category", "text", "button", "video", "html"
title_i18n (JSONB, nullable)
subtitle_i18n (JSONB, nullable)
description_i18n (JSONB, nullable)
image_url (varchar, nullable)
image_alt_i18n (JSONB, nullable)
mobile_image_url (varchar, nullable)
video_url (varchar, nullable)
link_type (varchar, nullable) → "category", "product", "page", "url"
link_target_id (UUID, nullable)
link_url (varchar, nullable)
button_text_i18n (JSONB, nullable)
button_style (varchar, nullable) → "primary", "secondary", "outline"
product_id (UUID, FK, nullable)
category_id (UUID, FK, nullable)
custom_html_i18n (JSONB, nullable)
badge_text_i18n (JSONB, nullable)
badge_color (varchar, nullable)
open_in_new_tab (boolean, default false)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### cms_product_lists
Dinamik ürün listeleri.
```
id (UUID, PK)
firm_platform_id (UUID, FK)
code (varchar)
name_i18n (JSONB)
list_type (varchar) → "manual", "filter", "bestseller", "new_arrivals", "on_sale", "recently_viewed"
filter_rules (JSONB, nullable)
product_limit (integer, nullable)
is_active (boolean)
[ortak alanlar]

UNIQUE(firm_platform_id, code)
```

### cms_product_list_items
Manuel liste öğeleri.
```
id (UUID, PK)
product_list_id (UUID, FK)
product_id (UUID, FK, nullable)
variant_id (UUID, FK, nullable)
sort_order (integer)
[ortak alanlar]
```

---

## 12. Ek Tablolar

### iam_admin_menus
Admin panel menüleri.
```
id (UUID, PK)
parent_id (UUID, FK → self, nullable)
code (varchar, unique)
name_i18n (JSONB)
icon (varchar, nullable)
route (varchar, nullable) → "/orders", "/products"
permission_code (varchar, nullable)
is_active (boolean)
sort_order (integer)
[ortak alanlar]
```

### catalog_variant_price_history
Satış fiyat geçmişi.
```
id (UUID, PK)
variant_id (UUID, FK)
firm_platform_id (UUID, FK, nullable) → NULL ise baz fiyat
price_type (varchar) → "base_price", "base_cost", "platform_price"
old_value (numeric)
new_value (numeric)
changed_at (timestamp)
changed_by (UUID, FK)
change_reason (text, nullable)
```

### fin_supplier_price_history
Alış fiyat geçmişi.
```
id (UUID, PK)
variant_id (UUID, FK)
supplier_id (UUID, FK)
old_price (numeric)
new_price (numeric)
changed_at (timestamp)
changed_by (UUID, FK)
change_reason (text, nullable)
```

### ord_order_gifts
Sipariş hediye ürünleri.
```
id (UUID, PK)
order_id (UUID, FK)
variant_id (UUID, FK)
quantity (integer, default 1)
gift_reason (varchar, nullable) → "campaign", "promotion", "goodwill"
campaign_id (UUID, FK, nullable)
added_at_stage (varchar) → "checkout", "packing"
added_by (UUID, FK, nullable)
show_on_invoice (boolean, default true)
invoice_description (varchar, nullable) → "Hediye Ürün"
unit_value (numeric, default 0)
[ortak alanlar]
```

---

## 13. Tablo Özeti (Güncellenmiş)

### Core (18 tablo)
- core_languages
- core_contents
- core_lookup_types
- core_lookup_values
- core_platform_types
- core_firms
- core_firm_platforms
- core_integration_services
- core_firm_integrations
- core_expense_types
- core_cargo_rules
- core_order_statuses
- core_order_item_statuses
- core_payment_methods
- core_return_reasons
- core_notification_types
- core_notification_templates
- core_firm_notification_settings

### Catalog (16 tablo)
- catalog_attribute_types
- catalog_attribute_values
- catalog_product_groups
- catalog_product_group_attributes
- catalog_categories
- catalog_category_products
- catalog_products
- catalog_product_attributes
- catalog_product_variants
- catalog_product_variant_attributes
- catalog_product_variant_images
- catalog_firm_platform_products
- catalog_firm_platform_variants
- catalog_variant_price_history
- catalog_product_units (B2B)

### Inventory (8 tablo)
- inv_warehouses
- inv_warehouse_locations
- inv_stocks
- inv_stock_movements
- inv_stock_reservations
- inv_transfer_requests
- inv_transfer_request_items
- inv_transfer_tracking

### CRM (21 tablo)
- crm_member_groups
- crm_members
- crm_countries
- crm_cities
- crm_districts
- crm_neighborhoods
- crm_streets
- crm_buildings
- crm_addresses
- crm_carts
- crm_cart_items
- crm_wallets
- crm_wallet_transactions
- crm_loyalty_accounts
- crm_loyalty_transactions
- crm_member_credits (B2B)
- crm_member_prices (B2B)
- crm_member_discounts (B2B)
- crm_order_templates (B2B)
- crm_order_template_items (B2B)

### Order (20 tablo)
- ord_orders
- ord_order_items
- ord_order_discounts
- ord_order_expenses
- ord_order_taxes
- ord_order_payments
- ord_order_gifts
- ord_invoice_series
- ord_invoices
- ord_invoice_items
- ord_shipments
- ord_shipment_items
- ord_shipment_events
- ord_order_notifications
- ord_gift_cards
- ord_gift_card_transactions
- ord_returns
- ord_return_items
- ord_return_refunds
- ord_quotes (B2B)
- ord_quote_items (B2B)

### Fulfillment (4 tablo)
- ful_picking_plans
- ful_sorting_bins
- ful_packing_stations
- ful_packages

### POS (5 tablo)
- pos_registers
- pos_sessions
- pos_session_transactions
- pos_quick_products
- pos_receipts

### Finance (10 tablo)
- fin_suppliers
- fin_supplier_invoices
- fin_supplier_invoice_items
- fin_supplier_deliveries
- fin_supplier_delivery_items
- fin_supplier_transactions
- fin_supplier_payments
- fin_supplier_returns
- fin_supplier_return_items
- fin_supplier_price_history

### Promotion (7 tablo)
- prm_campaign_types
- prm_campaigns
- prm_campaign_products
- prm_campaign_exclusions
- prm_campaign_platforms
- prm_coupons
- prm_coupon_usages

### IAM (9 tablo)
- iam_users
- iam_roles
- iam_permissions
- iam_role_permissions
- iam_user_roles
- iam_user_permissions
- iam_user_sessions
- iam_audit_logs
- iam_admin_menus

### CMS (13 tablo)
- cms_site_menus
- cms_site_menu_items
- cms_menu_mega_panels
- cms_menu_panel_groups
- cms_menu_panel_items
- cms_page_templates
- cms_pages
- cms_section_types
- cms_page_sections
- cms_page_section_items
- cms_product_lists
- cms_product_list_items

**Toplam: ~130 tablo**

---

## Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 1.0 | Ocak 2025 | İlk versiyon - Tüm modüller |
| 1.1 | Ocak 2025 | core_languages düzeltmesi, crm_addresses name alanları, ord_orders para birimi/kur, CMS modülü, admin menü, fiyat geçmişi, hediye ürün tabloları eklendi |
| 1.2 | Ocak 2025 | B2B özellikleri: crm_member_credits, crm_member_prices, crm_member_discounts, crm_order_templates, catalog_product_units, ord_quotes tabloları eklendi |
| 1.3 | Ocak 2025 | POS modülü: pos_registers, pos_sessions, pos_session_transactions, pos_quick_products, pos_receipts tabloları eklendi |
