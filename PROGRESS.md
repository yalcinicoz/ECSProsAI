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

## Faz 4 — Eksik Kritik Endpoint'ler ✅ TAMAMLANDI

> Faz 3 bitmeden başlanmaz.

### 4a. IAM Genişletme ✅
- [ ] `POST /api/auth/forgot-password` — şifremi unuttum (⚠️ Faz 5'e ertelendi: email servisi gerekiyor)
- [ ] `POST /api/auth/reset-password` — token ile şifre sıfırlama (⚠️ Faz 5'e ertelendi)
- [x] `GET /api/iam/users/{id}` — kullanıcı detayı (roller + izinler + son giriş)
- [x] `GET/DELETE /api/iam/users/{id}/sessions` — oturum listeleme/tümünü sonlandırma
- [x] `DELETE /api/iam/sessions/{id}` — tek oturum sonlandırma
- [x] `GET /api/iam/audit-logs` — audit log listeleme (filtreli + sayfalı)
- [x] `GET/POST/PUT /api/iam/admin-menus` — admin menü yönetimi (ağaç yapısı)

### 4b. Catalog Genişletme ✅
- [x] `GET/POST /api/catalog/attribute-types` — özellik tipleri
- [x] `POST /api/catalog/attribute-types/{id}/values` — özellik değeri ekle
- [x] `GET/POST /api/catalog/product-groups` — ürün grupları
- [x] `PUT /api/catalog/product-groups/{id}` — ürün grubu güncelleme
- [x] `POST /api/catalog/product-groups/{id}/attributes` — gruba özellik ekle
- [x] `POST /api/catalog/variants/{id}/images` — varyant görseli ekle
- [x] `PATCH /api/catalog/products/{id}/activate|deactivate`
- [x] `GET/PUT /api/catalog/firm-platforms/{platformId}/variants/{variantId}/price` — platform bazlı fiyatlandırma (upsert)
- [x] `GET /api/catalog/firm-platforms/{platformId}/products/{productId}/pricing` — pricing sorgulama

### 4c. Inventory Genişletme ✅
- [x] `GET/POST /api/inventory/warehouses/{id}/locations` — depo lokasyonları
- [x] `PUT /api/inventory/locations/{id}` — lokasyon güncelleme
- [x] `GET /api/inventory/reservations` — rezervasyon listeleme
- [x] `GET/POST /api/inventory/transfers` — transfer listesi + oluşturma
- [x] `GET /api/inventory/transfers/{id}` — transfer detayı
- [x] `PATCH /api/inventory/transfers/{id}/status` — durum makinesi

### 4d. CRM Genişletme ✅
- [x] `GET /api/crm/countries`, `/countries/{id}/cities`, `/cities/{id}/districts`
- [x] `GET/POST /api/crm/members/{id}/addresses`
- [x] `DELETE /api/crm/members/{memberId}/addresses/{addressId}`
- [x] `GET /api/crm/members/{id}/wallet`
- [x] `GET /api/crm/members/{id}/loyalty`
- [x] `GET/POST/PUT /api/crm/member-groups`

### 4e. Finance Genişletme ✅
- [x] `GET/POST /api/finance/supplier-invoices`
- [x] `POST /api/finance/supplier-deliveries`
- [x] `POST /api/finance/supplier-payments` (SupplierTransaction otomatik)
- [x] `POST /api/finance/supplier-returns`
- [x] `GET /api/finance/suppliers/{id}/transactions`

### 4f. Order Genişletme ✅
- [x] `GET /api/orders/{id}/shipments`
- [x] `GET/POST /api/orders/{id}/payments`
- [x] `PATCH /api/orders/returns/{id}/reject`

### 4g. Fulfillment Genişletme ✅
- [x] `POST /api/fulfillment/picking/{planId}/scan-item`
- [x] `POST /api/fulfillment/sorting/bins/{binId}/scan`
- [x] `GET /api/fulfillment/dashboard/summary`

---

## Faz 5 — Shared Infrastructure ✅ TAMAMLANDI

- [x] Redis cache servisi (`ICacheService`, `RedisCacheService`)
- [x] Email servisi (`IEmailService` + `LogEmailService` stub)
- [x] SMS servisi (`ISmsService` + `LogSmsService` stub)
- [x] Shared.Contracts — modüller arası interface'ler (IProductService, IStockService, IMemberService)
- [x] FluentValidation pipeline behavior
- [x] Serilog structured logging (konsol + rolling file)
- [x] Polly retry policies (AddResilientHttpClient — 3x retry + circuit breaker)

---

## Faz 6 — Integration Modülü ✅ TAMAMLANDI

- [x] Pazaryeri adapter interface'i + Trendyol implementasyonu (stub → production-ready)
- [x] Kargo adapter interface'i + Yurtiçi Kargo implementasyonu (stub)
- [x] e-Fatura entegratör interface'i + eLogo implementasyonu (stub)
- [x] AdapterResolver — serviceCode ile adapter çözümleme
- [x] MarketplaceOrderFetchWorker — 15dk periyodik background worker
- [x] IntegrationLog, MarketplaceProduct entity'leri + migration (integration schema)
- [x] API endpoint'leri: GET/POST /api/integrations/...

---

## Faz 7 — Store API (Müşteriye Dönük) ✅ TAMAMLANDI

- [x] Üye auth: kayıt, giriş, refresh token rotation, me — MemberSession entity + MemberTokenService (type=member JWT)
- [x] Store Katalog: ağaç kategoriler, platform fiyatlı ürün listesi, ürün detayı (varyant+görsel+özellik)
- [x] Sepet: GET/POST/PUT/DELETE + anonim (sessionId) + üye + MergeCarts (giriş sonrası birleştirme)
- [x] Checkout: sepet → sipariş (pending, tüm adres alanları)
- [x] Hesabım: profil, adresler, siparişlerim, iadelerim, cüzdan, sadakat
- [x] CMS: public sayfa listesi + detay
- [x] MemberOnly auth policy (type=member JWT claim)
- [x] Migration: crm.member_sessions

---

## Faz 8 — SignalR + Real-Time ✅ TAMAMLANDI

- [x] `/hubs/fulfillment` — PickingPlan grup tabanlı, JoinPlan/LeavePlan/JoinWarehouse
- [x] `/hubs/notifications` — sipariş, stok, POS bildirimleri; topic Subscribe/Unsubscribe; kullanıcıya özel grup
- [x] `/hubs/dashboard` — canlı metrikler (MetricsUpdated, MetricChanged)
- [x] `IRealtimeNotificationService` + `SignalRNotificationService` implementasyonu
- [x] `DashboardMetricsWorker` — 30sn periyodik background worker (sipariş/POS/fulfillment metrikleri)
- [x] SignalR event handler'ları: OrderConfirmed/Shipped/Cancelled, PickingPlanCreated/Completed, PosSaleCompleted
- [x] JWT WebSocket desteği (access_token query param)
- [x] CORS `.AllowCredentials()` (SignalR için zorunlu)

---

## Faz 9 — Frontend 🟡 DEVAM EDİYOR

### Admin Panel (React 18 + TypeScript + Tailwind) — Temel İskelet ✅
- [x] Proje iskelet: Vite 7, TanStack Query 5, Zustand, React Hook Form, Zod, Axios, React Router v7
- [x] Tailwind CSS v4 (@tailwindcss/vite plugin)
- [x] Path alias: `@/` → `src/`
- [x] Auth: login sayfası, JWT token yönetimi (auto-refresh interceptor), Zustand persist, AuthGuard
- [x] Layout: Sidebar (tüm modüller, grup açma/kapama), Header (kullanıcı + çıkış), MainLayout
- [x] UI bileşenleri: Button, Input, Card, Badge, Table, Pagination, Modal, Spinner
- [x] Sayfa listesi: Dashboard (stat cards), Users, Products, Orders, Members, Stocks, POS Sales
- [x] PlaceholderPage — henüz tamamlanmamış modüller için
- [x] Build: `admin/dist/` — production ready
- [x] Nginx: `/admin` path'inden statik dosya sunumu (docker-compose volume mount)
- [x] Base path: `/admin` (vite base + router basename)

### Modül Detay Sayfaları ✅
- [x] UI bileşenleri: Select, Textarea, Alert
- [x] IAM Users — CreateUserModal + EditUserModal
- [x] Catalog Categories — liste + CreateCategoryModal + EditCategoryModal
- [x] Catalog Products — aktif/pasif toggle + satır tıkla → ProductDetailPage (/catalog/products/:code)
- [x] CRM Members — CreateMemberModal + EditMemberModal
- [x] Inventory Warehouses — liste + CreateWarehouseModal + EditWarehouseModal
- [x] Orders — satır tıkla → OrderDetailPage (/orders/:id) + aksiyon butonları (onayla/iptal/kargoya ver/teslim)
- [x] Finance Suppliers — liste + CreateSupplierModal + EditSupplierModal
- [x] Promotion Campaigns — liste + CreateCampaignModal
- [x] CMS Pages — liste + CreatePageModal
- [x] Code splitting (vendor chunk'ları): react, query, form, ui

### Tüm Modül Sayfaları ✅
- [x] IAM: Roller (liste), Audit Logları (filtrelenebilir + sayfalı)
- [x] CRM: Üye Grupları (CRUD)
- [x] Inventory: Transferler (liste + oluştur + durum geçişleri)
- [x] Entegrasyon: Log Kayıtları (filtrelenebilir + sayfalı)
- [x] Orders: İadeler (aksiyon butonları), Teklifler (gönder/dönüştür), Faturalar (iptal)
- [x] Fulfillment: Picking Planları (oluştur/başlat/tamamla), Paketleme İstasyonları (CRUD)
- [x] Finance: Tedarikçi Faturaları (oluştur)
- [x] Core: Firmalar (CRUD), Diller (liste), Lookup Tipleri (CRUD + değer ekleme)
- [x] Dashboard: gerçek API verisi (toplam sipariş, bekleyen, POS satış sayısı)
- [x] Sıfır PlaceholderPage kaldı (Store hariç — bilgi sayfası)

### Sıradaki (Faz 9 devamı)
- [ ] Ürün Oluşturma / Düzenleme sayfası (prototip — Adım 3: dinamik sekmeler, varyant girişi)
- [ ] POS Terminal arayüzü
- [ ] Storefront (Store API üzerine)

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

- **2026-05-31 — Menü-Kategori mimarisi yeniden yapılandırması (`docs/menu-kategori.md` kararları uygulandı):**
  - **Category → Global (site-bağımsız):** `FirmPlatformId` kaldırıldı; migration: `RemoveFirmPlatformIdFromCategory`
  - **Yeni Storefront modülü** oluşturuldu: `NavigationMenu` + `NavNode` + `ChannelProduct` entity'leri; schema: `storefront`; migration: `InitialStorefront`
  - **CMS'ten Menu temizlendi:** `SiteMenu`, `SiteMenuItem`, `MenuMegaPanel`, `MenuPanelGroup`, `MenuPanelItem` entity + command/query + configuration kaldırıldı; migration: `RemoveSiteMenuTables`
  - **Yeni `/api/navigation/menus` controller:** CreateMenu, UpdateMenu, DeleteMenu, GetMenus, GetMenuDetail, SaveNavNodes
  - **Store API güncellendi:** `GET /api/store/cms/menus/{code}` → artık `StorefrontDbContext` + `GetStoreNavigationMenuQuery` kullanıyor
  - **NodeType:** `category | link | label`; categoryId (nullable FK), SEO alanları, badgeLabel
  - **Admin Panel güncellendi:** `MenusPage` + `MenuDetailPage` → `/navigation/menus` path'i, `itemType`→`nodeType`, `nameI18n`→`nameOverrideI18n`, `targetType/targetId`→`categoryId`
  - **Seeder'lar güncellendi:** `TestDataSeeder`, `DemoDataSeeder` → `NavigationMenu/NavNode` kullanıyor
  - **Bir sonraki adım:** Inventory sayfaları (Depolar, Stok, Transferler) veya POS Terminal

- **2026-05-31 — Kategori seed verileri:**
  - `DatabaseSeeder.SeedCategoriesAsync` eklendi — **111 kategori**, 3 seviye derinlik, 12 kök
  - `SeedPermissionsAndRolesAsync` scope bug düzeltildi (root provider → kendi scope'u oluşturur)
  - `DemoDataSeeder.SeedCategoriesAsync` güncellendi — artık kendi kategorisini oluşturmuyor, DatabaseSeeder'ınkileri kullanıyor
  - Seed idempotent: `erkek` kodu yoksa eski kategorileri hard-delete edip yenilerini ekler

- **2026-05-31 — Filtre Şablonu (FilterPreset):**
  - `FilterPreset` entity: Code, NameI18n, Description (insan dili), FilterDef (JSONB); migration: `AddFilterPreset`
  - `Category.FilterPresetId` nullable FK; query-time merge: preset + override birleşir
  - CQRS: Create/Update/Delete/GetList(usageCount)/GetDetail + UpdateCategory güncellendi
  - `SyncCategoryProducts` ve `GetStoreCategoryProductsQuery` preset-aware hale getirildi
  - Admin Panel: `/catalog/filter-presets` — liste + create/edit/delete modal, JSON editör, kullanım sayacı
  - CategoryDetailPage: Filtre Şablonu selector, preset özeti (collapsible JSON), override kurallar

---

## Yeniden Yapılanma Kararları (2026-03-11)

### Genel Yaklaşım
- Frontend-driven: Önce admin panel sayfası yapılır, API o sayfayı takip eder.
- Sayfa sayfa ilerlenir, bitirmeden sonrakine geçilmez.
- Mevcut `/admin` klasörü ve API temizlenip sıfırdan başlanacak.

### Görsel Tasarım
- Referans: partner.trendyol.com ve merchant.hepsiburada.com ama özgün olacak.
- Tema altyapısı: Çok şablonlu, firma/kullanıcı tercihine göre değişebilen.
- Stil: Kurumsal/Modern.
- Sidebar: Sol, collapse edilebilir (başlangıçta kapalı).
- Arama: Sidebar içi menü araması (sadece nav filtreler) + global hızlı arama (Ctrl+K).
- Sık Kullanılanlar: Sağdan açılan slide-over panel; her sayfada ekle/çıkar butonu; mobilde alt nav'da yıldız ikonu.
- **ŞABLON KARAR VERILDI: option-h.html** — Yeşil accent (#059669), koyu sidebar, mobile-responsive.
- Örnek sayfalar: `/admin/examples/option-a.html` … `option-h.html` (production: `http://51.178.208.59/admin/examples/`)
- option-h üzerine Özellik Tipleri sayfaları eklendi (liste + detay + yeni oluşturma modal).

### Çok Dillilik Kararları
- Panel arayüz dili: Sınırsız dil desteği, personel kendi dilini seçer.
- Zorunlu veri dili: Site kurulum ayarlarından alınır.
- Veri girişi: Ana formda sadece zorunlu dil ile çalışılır.
- Çeviri girişi: "Çeviri" butonu / kısayol → popup açılır.
  Popupta: Dil seçimi (pill butonlar, tab görüntüsü YOK) + grid:
  Alan etiketi | Zorunlu dildeki değer (read-only) | Hedef dil değeri (editable)
- Hangi alanlar çok dilli: Sadece müşteriye görünen alanlar
  (ürün adı, açıklama, kategori adı, SEO başlık vb.)
  Fiyat, stok, SKU, tarih alanları çok dilli DEĞİL.
- Çeviri popup örneği: option-a ve option-c'de görülebilir.
