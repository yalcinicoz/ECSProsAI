# ECSPros — Geliştirme İlerleme Takibi

> **Kural:** Her session bu dosyadan başla, bu dosyayla bitir.
> Bir faz tamamlanmadan bir sonrakine geçme.
> Son güncelleme: 2026-03-10

---

## Faz 0 — Temel Altyapı ✅ TAMAMLANDI

- [x] Modüler monolith mimarisi (.NET 8)
- [x] CQRS / MediatR
- [x] EF Core + PostgreSQL (schema-per-module)
- [x] JWT auth + Refresh Token rotation
- [x] Soft delete (global query filter)
- [x] Result<T> pattern
- [x] PagedResult<T>
- [x] BaseEntity, AggregateRoot, IDomainEvent
- [x] GlobalExceptionMiddleware
- [x] DatabaseSeeder (admin user, seed data)
- [x] Docker Compose (postgres, redis, nginx)
- [x] Systemd servisi + production deploy

---

## Faz 1 — Temel Modüller (CRUD + Auth) ✅ TAMAMLANDI

### IAM
- [x] Login, Refresh Token, Me, Change Password
- [x] Kullanıcı listesi, oluşturma, güncelleme, şifre sıfırlama
- [x] Rol listesi, rol atama

### Core
- [x] Dil listesi
- [x] LookupType CRUD
- [x] LookupValue CRUD

### Catalog
- [x] Kategori listesi, oluşturma, güncelleme
- [x] Ürün listesi (sayfalı), oluşturma, detay, güncelleme

### Inventory
- [x] Depo listesi, oluşturma, güncelleme
- [x] Stok listesi, stok hareketi (adjust)

### CRM
- [x] Üye listesi, detay, oluşturma, güncelleme

### Finance
- [x] Tedarikçi listesi, detay, oluşturma, güncelleme

### Promotion
- [x] Kampanya listesi, oluşturma, güncelleme
- [x] İndirim hesaplama, kupon doğrulama, kupon kullanımı

### CMS
- [x] Sayfa listesi, detay, oluşturma, güncelleme

---

## Faz 2 — Sipariş + POS + Fulfillment Akışları ✅ TAMAMLANDI

### Order
- [x] Sipariş oluşturma, listeleme, detay
- [x] Durum makinesi: confirm → processing → shipped → delivered
- [x] İptal (cancel)
- [x] İade akışı: create → approve → receive → complete-refund
- [x] Fatura: oluşturma, iptal
- [x] Teklif akışı: create → send → respond → convert-to-order
- [x] Hediye kartı: oluşturma, kullanma, bakiye sorgulama
- [x] Domain events: OrderConfirmed, OrderCancelled, OrderShipped, ReturnReceived

### POS
- [x] PosReceipt → PosSale/PosSaleItem/PosSalePayment refactor
- [x] Oturum aç/kapat
- [x] Satış tamamlama (stok otomatik düşer)
- [x] Satış iade (stok geri gelir)
- [x] Satış listesi, detay
- [x] Gün sonu kasa raporu
- [x] Domain events: PosSaleCompleted, PosSaleRefunded

### Fulfillment
- [x] Application katmanı oluşturuldu
- [x] PickingPlan: oluşturma, başlatma, tamamlama, listeleme, detay
- [x] PackingStation: oluşturma, güncelleme, listeleme
- [x] Package: oluşturma, etiket yazdırma, listeleme
- [x] SortingBin durum güncelleme
- [x] Domain events: PickingPlanCreated, PickingPlanCompleted

### Inventory Event Handlers
- [x] OrderConfirmed → stok rezervasyonu
- [x] OrderCancelled → rezervasyon serbest
- [x] OrderShipped → rezervasyonu tüket
- [x] PosSaleCompleted → stok düş
- [x] PosSaleRefunded → stok geri
- [x] ReturnReceived → stok arttır

---

## Faz 3 — Çok Firmalı Yapı + Core Genişletme ✅ TAMAMLANDI

> Domain entity'leri + konfigürasyonlar zaten mevcuttu. Application + Controller tamamlandı.

### 3a. Domain + Migration
- [ ] `Firm` entity (core şeması)
- [ ] `PlatformType` entity
- [ ] `FirmPlatform` entity
- [ ] `IntegrationService` entity (sistem tanımlı, seed ile gelir)
- [ ] `FirmIntegration` entity
- [ ] `CargoRule` entity
- [ ] `ExpenseType` entity
- [ ] Migration: AddFirmAndPlatformStructure

### 3b. API Endpoint'leri ✅
- [x] `GET/POST /api/core/firms` — firma listesi + oluşturma
- [x] `GET/PUT /api/core/firms/{id}` — detay + güncelleme
- [x] `GET/POST /api/core/firms/{firmId}/platforms` — firma platformları
- [x] `GET/PUT /api/core/firm-platforms/{id}` — platform detay + güncelleme
- [x] `GET /api/core/integration-services` — mevcut entegrasyon servisleri
- [x] `GET/POST /api/core/firms/{firmId}/integrations` — firma entegrasyonları
- [x] `GET/POST /api/core/firms/{firmId}/cargo-rules` — kargo kuralları
- [x] `GET/POST /api/core/expense-types` — masraf tipleri
- [x] `GET /api/core/platform-types` — platform tipleri

---

## Faz 4 — Eksik Kritik Endpoint'ler 🔴 SIRADA

> Faz 3 bitmeden başlanmaz.

### 4a. IAM Genişletme
- [ ] `POST /api/auth/forgot-password` — şifremi unuttum (email gönderme)
- [ ] `POST /api/auth/reset-password` — token ile şifre sıfırlama
- [ ] `GET/DELETE /api/iam/sessions` — aktif oturum listeleme/sonlandırma
- [ ] `GET /api/iam/audit-logs` — audit log listeleme
- [ ] `GET/POST/PUT /api/iam/admin-menus` — admin menü yönetimi

### 4b. Catalog Genişletme
- [ ] `GET/POST/PUT/DELETE /api/catalog/attribute-types` — özellik tipleri
- [ ] `GET/POST/PUT /api/catalog/attribute-types/{id}/values` — özellik değerleri
- [ ] `GET/POST/PUT /api/catalog/product-groups` — ürün grupları/şablonları
- [ ] `POST/DELETE /api/catalog/variants/{id}/images` — varyant görselleri
- [ ] `PATCH /api/catalog/products/{id}/activate|deactivate`
- [ ] `GET/PUT /api/catalog/firm-platforms/{platformId}/products` — platform bazlı fiyatlandırma

### 4c. Inventory Genişletme
- [ ] `GET/POST/PUT /api/inventory/warehouses/{id}/locations` — depo lokasyonları
- [ ] `GET /api/inventory/reservations` — rezervasyon listeleme
- [ ] `GET/POST/PATCH /api/inventory/transfers` — transfer yönetimi (tam akış)

### 4d. CRM Genişletme
- [ ] `GET/POST /api/crm/countries`, `cities`, `districts`, `neighborhoods` — adres tanımları
- [ ] `GET/POST/PUT/DELETE /api/crm/members/{id}/addresses` — üye adresleri
- [ ] `GET /api/crm/members/{id}/wallet` + işlemler — cüzdan
- [ ] `GET /api/crm/members/{id}/loyalty` + işlemler — sadakat puanı
- [ ] `GET/POST /api/crm/member-groups` — üye grupları CRUD

### 4e. Finance Genişletme
- [ ] `GET/POST/PUT /api/finance/supplier-invoices` — alış faturaları
- [ ] `GET/POST/PATCH /api/finance/supplier-deliveries` — teslimatlar + kabul akışı
- [ ] `GET/POST /api/finance/supplier-payments` — ödemeler
- [ ] `GET/POST /api/finance/supplier-returns` — tedarikçiye iade
- [ ] `GET /api/finance/suppliers/{id}/transactions` — cari hesap

### 4f. Order Genişletme
- [ ] `GET/POST /api/orders/{id}/shipments` — kargo yönetimi
- [ ] `GET/POST /api/orders/{id}/payments` — sipariş ödemeleri
- [ ] `GET/POST /api/orders/{id}/notifications` — bildirimler
- [ ] `PATCH /api/orders/returns/{id}/reject` — iade reddetme

### 4g. Fulfillment Genişletme
- [ ] `POST /api/fulfillment/picking/scan-item` — ürün tarama (depo personeli)
- [ ] `POST /api/fulfillment/sorting/scan-to-bin` — koliye tarama
- [ ] `POST /api/fulfillment/final-sorting/scan-to-slot` — son ayrıştırma
- [ ] `GET /api/fulfillment/dashboard/summary` — operasyon dashboard

---

## Faz 5 — Shared Infrastructure 🔴 SIRADA

> Faz 4 bitmeden başlanmaz.

- [ ] Redis cache servisi (`ICacheService`, `RedisCacheService`)
- [ ] Email servisi (`IEmailService` + provider implementasyonu)
- [ ] SMS servisi (`ISmsService` + provider implementasyonu)
- [ ] Shared.Contracts — modüller arası interface'ler (IProductService, IStockService, IMemberService)
- [ ] FluentValidation pipeline behavior (tüm command'lara)
- [ ] Serilog structured logging
- [ ] Polly retry policies (HTTP client'lar için)

---

## Faz 6 — Integration Modülü 🔴 SIRADA

> Faz 3 (FirmIntegration yapısı) ve Faz 5 (Shared.Contracts) bitmeden başlanmaz.

- [ ] Pazaryeri adapter interface'i + Trendyol implementasyonu
- [ ] Kargo adapter interface'i + ilk kargo firması implementasyonu
- [ ] e-Fatura entegratör interface'i + ilk entegratör implementasyonu
- [ ] Stok senkron kuyruğu (Redis + background worker)
- [ ] Entegrasyon log altyapısı
- [ ] API endpoint'leri: `/api/integrations/...`

---

## Faz 7 — Store API (Müşteriye Dönük) 🔴 SIRADA

> Faz 4 ve Faz 5 bitmeden başlanmaz.

- [ ] Katalog: kategori ağacı, ürün listesi/detay, arama
- [ ] Sepet yönetimi
- [ ] Checkout + sipariş oluşturma
- [ ] Üyelik: kayıt, giriş, profil, adreslerim
- [ ] Hesabım: siparişlerim, iadelerim, cüzdanım, sadakatim
- [ ] CMS: menüler, sayfalar, ana sayfa
- [ ] B2B: teklif, sipariş şablonu, hızlı sipariş

---

## Faz 8 — SignalR + Real-Time 🔴 SIRADA

> Faz 4 (Fulfillment) bitmeden öneri: paralel başlanabilir.

- [ ] `/hubs/fulfillment` — depo operasyonları (tarama, görev atama)
- [ ] `/hubs/notifications` — yeni sipariş, stok uyarısı
- [ ] `/hubs/dashboard` — canlı metrikler

---

## Faz 9 — Frontend 🔴 SIRADA

> Backend Faz 4 tamamlanmadan başlanmaz. Paralel ekip varsa Faz 3 sonrası başlanabilir.

### Admin Panel (React 18 + TypeScript + Tailwind)
- [ ] Proje iskelet: Vite, TanStack Query, Zustand, React Hook Form, Zod, Axios, i18next
- [ ] Auth: login sayfası, token yönetimi, permission guard
- [ ] Modül sayfalası: IAM, Core, Catalog, Inventory, CRM, Order, Fulfillment, Finance, Promotion, CMS

### POS Terminal
- [ ] Kasa arayüzü (barkod tarama, ürün arama, ödeme)
- [ ] Oturum açma/kapama
- [ ] Gün sonu raporu

### Storefront
- [ ] Müşteri sitesi (Store API tamamlandıktan sonra)

---

## Teknik Borçlar (Her Faz Öncesi Değerlendir)

- [ ] API URL versiyonlaması: `/api/...` → `/api/v1/...` (breaking change, dikkatli planlanmalı)
- [ ] AutoMapper entegrasyonu (DTO mapping şu an manuel)
- [ ] Tüm command'lara FluentValidation eklenmesi
- [ ] CRM üye şifre hashing: SHA256 → BCrypt (güvenlik borcu)
- [ ] Elasticsearch entegrasyonu (ürün arama için)

---

## Aktif Session Notları

> Bu bölümü her session başında güncelle, session sonunda temizle.

- **2026-03-10:** PROGRESS.md oluşturuldu. Faz 0-3 tamamlandı. Sıradaki: **Faz 4 — Eksik Kritik Endpoint'ler**. IAM genişletmeden başlanacak.
