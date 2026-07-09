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

- **2026-07-09 (devam) — C7 TAMAM: TCKN doğrulama (K9) canlı:**
  - **Backend:** `Member.IdentityNumber/IdentityVerifiedAt` + `AddMemberIdentity` migration
    (canlı DB'ye uygulandı); `SetMemberIdentityCommand` + `TcknValidator` (11 hane + kontrol
    basamağı algoritması sunucuda); `POST /api/store/account/identity`;
    `MemberDetailDto.IdentityVerified`. **Checkout guard'ı:** `Store:TcknThreshold`
    (varsayılan 13.000) — eşik üzeri + doğrulanmamış üye → 400 `tcknRequired:true`
    (mesaj tr-TR N0 formatlı).
  - **Frontend:** demo TCKN bloğu `_SepetSayfasi`'ndan söküldü; canlı script `_SepetModallari`
    sonunda (client'ta aynı checksum, POST, `window.msTcknDogrulandi` + `ms:tckn-dogrulandi`,
    `window.msTcknModalAc`, /me'den başlangıç). Sepet banner'ı `data-ms-tckn-banner`
    (eşik SSR'dan `data-ms-tckn-esik`, SepetController ViewData); ödeme siparisOlustur
    ön-kontrol + sunucu tcknRequired 400'ü de modal açar.
  - **E2E 10/10 + guard 4/4 ✓** (banner eşik koşulu, geçersiz/geçerli TCKN client+sunucu,
    reload durumu, guard eşik altı/üstü/doğrulama sonrası). Drift TEMİZ. Test yan etkisi
    (2 sahte 'C7 Guard' siparişi) tespit edilip silindi — sahte variantId'li checkout'un
    200 dönmesi not: sipariş kalemleri snapshot, variantId FK'siz (bilinen tasarım).
  - **Faz C kalan:** C8 (sözleşme CMS), C9 (stok haber ver), C11 (QA kapanışı).
    DEPLOY BEKLİYOR (B11+B12+C1–C7+C10 birikti — `sudo systemctl restart ecspros`).

- **2026-07-09 (devam) — ÜRÜN TEMİZLİĞİ (kullanıcı talimatı): yeniurunkodlari dışı her şey silindi:**
  - Kaynak: eski MySQL `yeniurunkodlari` (28.609 kod). PG analizi: 117.569 üründen 28.549
    kalacak / 89.020 silinecek / keep listesinden 60 kod PG'de yoktu (yeniden aktarımda gelir).
  - **Yedek:** `/home/yalcin/yedekler/urun-temizligi-oncesi-2026-07-09.dump` (pg_dump -Fc,
    463MB — etkilenen 30 tablo; pg_restore ile tablo bazlı geri yüklenebilir).
  - **Silinen:** 89.020 ürün, 882.979 varyant, 2,69M varyant-attribute, 1,15M görsel, 481K
    ürün-attribute, 806K erp_variant_data, 883K channel_variant, 267K channel_product, 3 sepet
    satırı (tek transaction, FK sırasıyla). **Dokunulmayan:** sipariş/satış/finans geçmişi
    (snapshot alanlı; yetim VariantId kabul). ShowcaseProductId silinenler için NULL.
  - **ANALYZE** tüm etkilenen tablolarda ✓. Doğrulama: liste dışı kalan 0, yetim satır 0;
    /kadin taze sorgu 34.588 ürünle 1.2s; kalan ürün detayı 200. Redis (10dk) + ana sayfa
    IMemoryCache (15dk) kısa süre bayat olabilir — TTL/bekleyen restart düzeltir.
  - **MigrationTool:** 7 noktaya `yeniurunkodlari` filtresi (Faz 3 ana SELECT + EnsureProductMap
    guard + cinsiyet + ERP model kodları + beden özellikleri + açıklamalar + varyant tip
    değerleri) — baştan aktarım yalnız keep listesini taşır. Build temiz. Commit: 80fb69c.

- **2026-07-09 (devam) — C5+C6+C10 TAMAM: ödeme + checkout UÇTAN UCA canlı 🎉:**
  - `/odeme`: yöntemler + kart canlı önizleme (bilgi gönderilmez — K2 test modu, tahsilat mock),
    özet cart+kupon durumundan, teslimatsız girişte /teslimat'a döner; taksit kutusu statik (C6,
    K2; gerçek BIN H6); sözleşme metinleri C8'e kadar gizli, onay modalı çalışır.
  - `/siparis-tamamlandi`: msSiparisSonucu'ndan; sipariş no üyenin sipariş listesinden.
  - **C10:** Siparişi Tamamla → POST /api/store/checkout (msSiparisAsamasiGoster sarıldı);
    MemberAddressDto'ya geo ID'leri additive eklendi (checkout guid'leri buradan); request'e
    CouponId/CouponDiscount eklendi → controller sipariş sonrası UseCouponCommand (C3'ün 'use
    checkout'ta' sözü kapandı); başarıda sepet DELETE + storage temizliği + mini sepet yenilenir.
  - **E2E 15/15 ✓ (İLK SEFERDE):** üye+sepet+%10 kupon+adres → teslimat → ödeme (779,98→701,98)
    → kart önizleme → sözleşme → checkout → onay; DB: sipariş pending + kupon kullanım kaydı +
    sepet temiz. Test verileri silindi. Drift TEMİZ (2 yeni izinli girdi).
  - **Faz C kalan:** C7 (TCKN gerçek koşul+algoritma), C8 (sözleşme CMS), C9 (stok haber ver),
    C11 (QA kapanışı). DEPLOY BEKLİYOR (B11'den beri biriken her şeyle).

- **2026-07-09 (devam) — C4-b TAMAM → C4 KAPANDI (teslimat sayfası canlı):**
  - `/teslimat` + `_SepetTeslimatSayfasi` bağlandı; adres kartları account/addresses'ten
    (oturumsuzken giriş çağrısı + Ödemeye Geç giriş modalını açar); seçilen adres
    `msTeslimatDurumu` sessionStorage'ına yazılır (C5/C10 okur); adres seçilmeden Ödemeye
    Geç engelli (capture guard). Yeni partial `_SepetAdresModali` (İZİNLİ YENİ — kaynağı
    _SepetSiparis demo bloğu): il/ilçe/mahalle aramalı select'leri api/store/geo'dan —
    davranış sayfa script'inde (site.js ozel-select dinamik seçenek desteklemiyor; panel
    `ms-ozel-select-acik` sınıfıyla açılıyor — hidden attr İŞE YARAMAZ, CSS invisible).
    "Adresi Düzenle" E4'e (update API'si yok); kargo statik (H).
  - E2E: **11/11 ✓** (İstanbul→Kadıköy→Caferağa aramalı kademeli seçim dahil); drift TEMİZ
    (2 yeni izinli girdi). Sıradaki: C5 ödeme (test modu — msSepetKuponDurumu +
    msTeslimatDurumu okur) → C6 taksit → C7 TCKN → C8 sözleşme → C9 stok haber → C10 checkout.

- **2026-07-09 (devam) — C4-a TAMAM (adres hiyerarşisi + veri + kademeli geo API):**
  - **K6'ya gerekçeli düzeltme:** hiyerarşi Core'da değil CRM'de — crm_countries/cities/
    districts/neighborhoods tabloları ve Address FK'ları zaten mevcuttu (boştu). City.Region
    eklendi (AddCityRegion migration canlıda; G9 il→bölge buradan okuyacak).
  - **Veri:** turkey-neighbourhoods (npm, PTT-türevi, otomatik güncellenen) → TR + 81 il
    (coğrafi bölgeli) + 973 ilçe + 73.305 mahalle (staging+INSERT SELECT, ANALYZE ✓; Code'lar
    varchar(20) için md5 kısaltması). **PostalCode ŞİMDİLİK BOŞ** — set mahalle→PK vermiyor;
    formda PK manuel; resmi PTT eşleme dosyası gelince tek UPDATE (kullanıcı onayına açık).
  - **API:** GET /api/store/geo/{countries,cities,districts,neighborhoods} (anonim, mahalle
    aramalı+limitli, 4.7ms; Türkçe arama/sıralama bellek tarafında — B2 jsonb dersi).
    Duman testi uçtan uca ✓ (TR→34→Kadıköy→'bostan'→Bostancı/Caddebostan).
  - **KALAN C4-b:** _SepetTeslimatSayfasi portu + adres modalı (geo select'ler + account
    addresses) — sonraki oturum buradan devam etmeli.

- **2026-07-09 (devam) — C3 TAMAM (kupon akışı canlı):**
  - Yeni store endpoint'i `POST /api/store/checkout/coupon/validate` (AllowAnonymous; üye
    token'ı varsa MemberId koşulları; `use` kaydı C10 checkout'ta). Sepette uygula/kaldır +
    gerçek indirim hesabı; sepet değişince sessiz yeniden doğrulama — koşul bozulursa otomatik
    kaldırma + neden. Tasarımın msSepetKuponDurumu/sessionStorage/ms:sepet-kupon-degisti
    sözleşmesi korunarak couponId+tutar eklendi (ödeme sayfası C5'te okuyacak). Demo kupon
    mantığı script'ten çıkarıldı. Reload'da kupon yeniden doğrulanıp geri gelir.
  - E2E (geçici C3TEST10 kuponu, sonra DB'den silindi): **10/10 ✓**; drift TEMİZ.
  - Sıradaki: C4 (teslimat + K6 adres hiyerarşisi — Core countries/provinces/districts/
    neighborhoods + PTT seed + kademeli select API) → C5 ödeme (test modu) → C6-C10.

- **2026-07-09 (devam) — FAZ C BAŞLADI: C1+C2 TAMAM (sepet sayfası canlı):**
  - `/sepet` (SepetController) — Index/_SepetSayfasi/_SepetModallari birebir kopya; satırlar
    template + script'le GET /api/store/cart'tan (istemci-durumlu sepet, B5 deseni; ad/görsel/
    seçenek özeti IProductService zenginleştirmesinden). Adet ± PUT ile kalıcı (1–10), silme
    misharix sil onay modalı üzerinden DELETE (modal script'i değişmedi — onay butonuna ikinci
    dinleyici), tümünü seç/checkbox özet toplamını belirler, boş durum + mini sepet rozet
    senkronu. Mini sepet "Sepete Git/Siparişi Tamamla" → /sepet (C4'te Tamamla → /teslimat).
  - Fazına bırakılanlar @if(false): TCKN uyarısı C7, kupon alanları C3, kargo bilgileri H,
    kampanya/koleksiyon G/E6; favoriye taşı (C2) canlı şablona alınmadı — E5'te eklenir.
  - E2E (5051 publish): **12/12 ✓**; 0 konsol hatası; drift TEMİZ (1 yeni izinli girdi).
  - ⚠️ Test instance başlatmada `cd X && nohup ... &` tuzağına B13'te bir kez daha düşülmüştü
    (yetim süreç 5051'i tuttu, yeni instance core-dump) — ss -ltnp + /proc doğrulamasıyla
    çözüldü; tek satırda cd+nohup bileşiği YASAK (bkz. feedback_background_nohup_pid_trap).
  - Sıradaki: C3 (kupon) → C4 (teslimat+adres hiyerarşisi K6) → C5-C10.

- **2026-07-09 (devam) — Faz B13 TAMAMLANDI → FAZ B KAPANDI 🎉:**
  - Envanter 8.1–8.4'teki 25 açık satır gerçek durumla işaretlendi (Faz B işleri ✅; SMS/OTP→D4,
    belge metinleri→D3, favori/koleksiyon/puan→E5-E7, kampanya→G, görsel arama→H3, video→H5);
    işaretsiz satır kalmadı. Kapanış görüntüleri `tools/misharix-sync/shots/b13-*` (3 yüzey ×
    desktop+mobil); drift TEMİZ. Misharix tasarım projesi .NET 9 hedefliyor — bu makinede (SDK 8)
    çalıştırılamıyor; yan-yana yerine faz faz birebir doğrulamalar + drift kontrolü esas alındı.
  - **FAZ B TAMAM: B1–B14 tümü bitti.** Geçici çözümler: B3 duyuru + B6 ana sayfa → G8'de
    vitrin sistemine devrolur. SIRADAKİ FAZ → **C (Sepet + Checkout)**: sepet sayfası /sepet,
    teslimat, ödeme (test modu), adres (Core ülke/il/ilçe/mahalle), kupon, TCKN format kontrolü.
  - Deploy notu: B11+B12 hâlâ restart bekliyor (B13 kod değişikliği içermiyor).

- **2026-07-09 (devam) — Faz B12 TAMAMLANDI: stok kontrolü anahtarı:**
  - Anahtar `FirmPlatform.Settings."stockControlEnabled"` (JSONB, kolon yok; varsayılan KAPALI —
    bugünkü veri durumu, her şey satılabilir; stok dolunca true yapmak yeterli, kod değişmez).
  - Açıkken: detayda beden satılabilirliği IStockService'ten — tükenen beden ana alan + sabit
    panelde `ms-beden-secim-tukendi` + disabled, config haritasına girmez; Stok Durumu gerçek;
    bedensiz stoksuz üründe TekVaryantId verilmez. Sunucu guard'ı: `AddToCartCommand.EnforceStock`
    (API anahtarı 5 dk cache ile çözer) → stoksuz varyant 400.
  - E2E 12/12 ✓ (julude platformu testte açılıp GERİ ALINDI, mishar'a dokunulmadı; test stok/
    sepet satırları temizlendi) + B6 regresyon 19/19 ✓; drift TEMİZ. NOT: inv_stocks'ta artık
    249 pozitif satır var (2026-07-06 "hiç yok" notu güncel değil). **DEPLOY BEKLİYOR.**
  - Faz B'de kalan tek iş: B13 (görsel + davranış QA — faz kapanışı).

- **2026-07-09 (devam) — Faz B11 TAMAMLANDI: "öne çıkar" bayrağı (K8):**
  - `ChannelProduct.FeaturedFrom/Until` (migration canlıda); admin GET/PUT
    `channel-products/{platform}/products/{product}/featured` + ProductDetailPage "Satış
    Kanalları" sekmesinde "Öne Çıkar" paneli (npm build alındı — admin canlıda).
  - Yeni port `IChannelProductFlagService` (Storefront implemente eder); kategori + genel
    liste sorguları YALNIZ varsayılan sırada öne alır (açık sıralama tercihi bozulmaz),
    DTO'lara additive `IsFeatured`; kartta "Sponsorlu" rozeti bağlandı (SSR + JSON devam).
  - NOT: kategori varsayılan sayfaları 10 dk Redis cache'inde — işaretleme en geç 10 dk
    içinde görünür; filtreli/aramalı istekler anında.
  - E2E: **14/14 ✓** + B6 regresyon 19/19 ✓; drift TEMİZ. **DEPLOY BEKLİYOR** (restart).
  - Kalan: B12 (stok anahtarı), B13 (görsel QA — faz kapanışı).

- **2026-07-09 (devam) — Faz B4 TAMAMLANDI: giriş/kayıt modalları canlı (e-posta), SMS Faz D'ye kadar pasif:**
  - E-posta girişi varsayılan sekme (`_AnaNavigasyon`'da tek satır `tabAc("eposta")`), SMS/
    Telefon "(Yakında)" disabled. Login/register/me/refresh `api/store/auth`'a bağlı; token'lar
    `ecspros_member_token`/`ecspros_member_refresh`; oturum reload'da `me` ile kalıcı (401'de
    bir kez refresh); girişte `cart/merge` + mini sepet tazelenir; çıkışta token temizliği.
    Kayıt formuna Şifre alanı eklendi (API zorunlu; Faz D OTP'de gözden geçirilir); hesap
    panelinde statü/harcama bloğu @if(false) (veri yok — Faz E/G).
  - **CANLI DB'DE İKİ EKSİK BULUNDU (üyelik hiç çalışamazdı):** (1) `crm_member_groups` BOŞ —
    register "Varsayılan üye grubu bulunamadı" veriyordu → 'standart' grubu SQL ile eklendi +
    `SeedCrmDefaultsAsync` seeder'a eklendi (idempotent). (2) `crm.member_sessions` tablosu
    YOKTU — login 500 veriyordu → bekleyen `20260310131553_AddMemberSession` migration'ı
    canlıya uygulandı (`dotnet ef database update --context CrmDbContext`).
  - E2E (5051 publish): **12/12 ✓** + B6 regresyon 19/19 ✓; 0 konsol hatası; test üyeleri
    (%@e2e.local) DB'den silindi. `check.sh` TEMİZ (4 yeni izinli girdi: GirisModal/
    KayitModal/GirisMenu/_AnaNavigasyon tek satır).
  - **DEPLOY TAMAM (2026-07-09 07:51):** B10+B5+B4 birlikte canlıya çıktı (Redis AKTİF ✓;
    tüm izler canlı HTML'de doğrulandı). Önceki üç başarısız 'restart' beyanının kökü:
    kullanıcı `restart` yerine `start` yazıyormuş — aktif serviste no-op. Deploy doğrulaması
    her zaman `systemctl show ecspros -p ExecMainStartTimestamp` ile.
  - Kalan Faz B işleri: B11 (öne çıkar bayrağı), B12 (stok anahtarı), B13 (görsel QA kapanışı).

- **2026-07-09 (devam) — Faz B5 TAMAMLANDI: mini sepet canlı:**
  - `_AnaNavigasyonUst` sepet hover paneli GET /api/store/cart'a bağlandı (SPA anahtarları
    ecspros_sid/ecspros_cart); rozet adet (0'da gizli), silme DELETE ile, kalem ürüne linkli,
    ürün detay eklemesi `window.msMiniSepetYenile()` ile reload'suz rozet günceller.
  - **Yeni port:** `IProductService.GetVariantDisplayAsync` (Shared.Contracts +
    `CatalogProductService` ilk implementasyon, Catalog DI'da) — CRM `CartItemDto`'ya additive
    ProductCode/NameI18n/ImageUrl/OptionsText alanları eklendi ("Beden: ST, Renk: Pembe").
    Faz C sepet sayfası aynı zenginleştirmeyi hazır bulacak.
  - E2E (5051 publish): 10/10 ✓ + B6 regresyon 19/19 ✓; check.sh TEMİZ; test sepeti DB'den
    temizlendi. **DEPLOY BEKLİYOR** (B10 ile birlikte publish'te).
  - ⚠️ Kullanıcının önceki "restart yaptım" işlemi GERÇEKLEŞMEMİŞ (PID 08.07 20:38'den beri
    aynı — systemctl show ile doğrulandı); B10+B5 canlıya çıkmadı, restart tekrar gerekli.

- **2026-07-09 — Faz B10 TAMAMLANDI: sunucu tarafı filtre/sıralama + kategoride ara:**
  - **Sorgular additive genişledi:** `GetStoreProductsQuery` + `GetChannelCategoryProductsQuery`
    → `AttributeValueIds`/`PriceMin`/`PriceMax`/`Sort` (+kategoriye `Search`); api/store
    endpoint'leri `attrs` (virgüllü valueId), `priceMin/priceMax`, `sort`, `search` alır —
    parametresiz eski çağrılar aynı (mobil etkilenmez). Filtre semantiği: grup içi OR,
    gruplar arası AND; kategori kartında (ürün×renk) eşleşme kartın kendi varyantlarında
    ve AYNI varyantta; genel listede ürün seviyesinde. Fiyat filtresi varyant BasePrice
    (kartların fiyat kaynağı). Sıralama: price_asc/desc + newest (CreatedAt).
  - **Sayfa tarafı:** B7'nin client-side filtre motoru KALDIRILDI — her filtre/sıralama
    değişikliği URL query parametreleriyle (api ile aynı adlar) SSR yeniden yükler; SSR
    seçili durumu geri işaretler, infinite scroll devam sayfalarını aynı parametrelerle
    çeker. Sol filtre anında, panel içi (üst/mobil) "Filtrele" ile uygular. **Kopya
    checkbox senkronu şart** — aynı valueId sol+üst+mobil panellerde tekrarlanıyor;
    senkronsuz bırakılınca kaldırma işlemi URL'den düşmüyordu (E2E yakaladı).
  - **Kategoride ara kapandı** (B2/B7'den beri açık boşluk): kategori sayfası
    `ViewData["MsAktifKategori"]` doldurur, nav arama panelindeki gizli buton
    "{Kategori} içinde ara" olur; öneriler `channel-categories/{id}/products?search=`,
    Tümünü Gör/Enter → `/{slug}?search=`.
  - **Cache:** filtreli istekler kategori Redis cache'ini atlar (yalnız parametresiz
    varsayılan sayfalar cache'lenir — anahtar kombinasyonu patlaması önlenir).
  - NOT: model modu kategorilerde filtre/sıralama uygulanmaz (grup vitrini, Faz G);
    fallback (eksensiz) modda yalnız arama. "Çok satan/favori" sıralamaları veri
    kaynağı gelene dek gizli (E7/B11).
  - **E2E (5051 Production publish): B10 22/22 ✓** + mobil sıralama modalı ✓ + B6/B8
    regresyon suite'leri yeniden 19/19 + 6/6 ✓; 0 konsol hatası; `check.sh` TEMİZ ✓
    (yeni izinli girdi yok — tüm dosyalar zaten listedeydi). Kadın kategorisi gerçek
    toplamı 162.697 kart; "elbise" kategori araması 17.051.
  - **DEPLOY BEKLİYOR (kullanıcı):** publish/ dizinine yayınlandı — `sudo systemctl
    restart ecspros` sonrası canlıda filtre/sıralama/kategoride-ara aktif olur.
  - **SIRADAKİ ADIM → B4-B5 (giriş/kayıt modalları + mini sepet) veya B11 (öne çıkar
    bayrağı) / B12 (stok anahtarı); B13 görsel QA faz kapanışında.**

- **2026-07-08 (üçüncü oturum) — Faz B3 + B6 TAMAMLANDI: duyuru şeridi (geçici statik) + ana sayfa (geçici kompozisyon):**
  - **B6 ana sayfa:** sayfa seçici kaldırıldı; `Home/Index.cshtml` = Kapsül Kategori Şeridi
    (kök kanal kategorileri; görsel: kategori görseli yoksa ilk ürün görseli; görselsiz kapsül
    basılmaz) + kök kategori başına Standart Carousel (ilk 3 kök × 10 ürün, "Tümünü Gör" →
    /{slug}). Banner bloğu bilinçli atlandı (banner görseli yok — Faz G vitrini). Veri:
    `HomeController` → `GetChannelCategoryProductsQuery` kök başına PARALEL (görev başına ayrı
    DI scope — scoped DbContext paylaşılamaz), `AnaSayfaVm` 15 dk IMemoryCache (soğuk ilk
    istek sıralıyken 11.3s idi → paralel + PG cache ile ~ms'ler).
  - **Kart tek kaynağa alındı:** B7'nin `UrunKarti` local function'ı paylaşılan
    `ProjeElementleri/Urun/_UrunKarti.cshtml` partial'ına taşındı (liste SSR + `<template>` +
    ana sayfa carousel aynı markup; `UrunKartMap` dönüştürücüsü `Models/Store`'da, listeleme
    controller'ı delegate ediyor). Infinite scroll template'i partial'ın null-model yoluyla
    iskelet kart üretiyor — /kadin 24→72 E2E ile doğrulandı.
  - **KRİTİK DERS 1 (test ortamı):** `bin/Release/net8.0`'dan çalıştırılan instance'ta wwwroot
    YOK — tüm statikler (site.js dahil) 404, hiçbir JS modülü yüklenmez, testler yanlış sonuç
    verir. 5051 testi HER ZAMAN publish çıktısından yapılmalı (bu oturumda scratchpad'e izole
    `dotnet publish` yapıldı, canlı publish/ dizinine dokunmadan test edildi).
  - **KRİTİK DERS 2 (arka plan süreç):** `cd X && ENV nohup dotnet ... & echo $!` kalıbında `&`
    tüm `&&` zincirini subshell'e alır — $! subshell PID'idir, kill dotnet'i ÖLDÜRMEZ (yetim
    kalır, portu tutmaya devam eder; ikinci instance sessizce ölür ve testler eski süreçe
    çarpar). Doğru kalıp: önce `cd`, sonra tek satır `nohup env ... dotnet ... &` (`$!` = dotnet).
    Öldürmeden önce `/proc/$PID/cwd` ile doğrula.
  - **Kart davranışları ana sayfada:** site.js bootstrap'i yalnız `data-ms-infinite-liste`
    konteynerlerini tarar — carousel kartları için sayfa sonuna `msUrunKartDavranislariYenile`
    çağıran config script'i eklendi (DOMContentLoaded'a ertelenir; site.js body sonunda).
    Tooltip E2E doğrulaması B8'deki gibi `ms-urun-renk-tooltip-acik` class'ıyla yapılır —
    tooltip anchor'ı tasarım gereği 0 yükseklikte, `isVisible()` yanlış negatif verir.
  - **B3 duyuru:** `_AnaNavigasyonDuyuru` zaten B1'den beri her sayfada statik render;
    "Misharitalia" demo metni mishar'a uyarlandı; linkler Faz F/H'ye kadar `#`; kalıcısı G8.
  - **check.sh geliştirmesi:** kaynakta karşılığı olmayan bilinçli dosyalar için "İZİNLİ YENİ"
    durumu (allowed-diffs listesindeyse) — `_UrunKarti.cshtml` bu yolla izinli.
  - **E2E (headless Chromium, 5051 Production publish): desktop 19/19 ✓ + mobil 6/6 ✓**;
    0 konsol hatası; ekran görüntüleri scratchpad `pw-b6/shots/b6-*.png`. Görünen veri
    gerçekleri (kod değil): kart görselleri CDN "RESİM HAZIRLANIYOR" placeholder'ları, Kadın
    carousel'i tek ürünün renk kartlarıyla dolu (kategori listesiyle aynı davranış).
    `check.sh` TEMİZ ✓ (3 yeni izinli girdi). Plan (B3/B6 [x] + Durum Panosu + 8.1 duyuru
    satırı) ve eşleme tablosu güncellendi.
  - **DEPLOY TAMAM + SYSTEMD'YE DÖNÜLDÜ (2026-07-08 akşam):** B8+B3+B6 canlıda, servis yeniden
    systemd altında (Redis: AKTİF ✓). Dönüş sırasında ikinci tuzak yaşandı: kullanıcıya
    verdiğim `kill $(pgrep -f "publish/ECSPros.Api.dll" | head -1)` komutu YANLIŞTI —
    `pgrep -f` deseni, süreci başlatan bash wrapper'ının cmdline'ında da geçtiğinden head -1
    wrapper'ı seçti, dotnet yetim kaldı ve 5000 portunu tutmaya devam etti; systemd süreci
    "address in use" ile core-dump döngüsüne girdi (`systemctl is-active` yine de "active"
    diyordu — RestartSec=5 döngüsü). Çözüm: port sahibini `ss -ltnp | grep :5000` ile bulup
    `/proc/PID/cwd` doğrulamasından sonra o PID'i öldürmek; systemd 5 sn'de portu aldı.
    Ders `feedback_background_nohup_pid_trap.md`'ye eklendi (pgrep -f wrapper tuzağı).
  - **SIRADAKİ ADIM → B4-B5 (giriş/kayıt modalları + mini sepet) veya B10 (sunucu tarafı
    filtre/sıralama; "kategoride ara" da burada bağlanacak); B13 görsel QA faz kapanışında.**

- **2026-07-08 (ikinci oturum) — Faz B8 TAMAMLANDI: ürün kartı derinleştirme (önceki oturumda başlanmış commit'siz değişiklikler devralındı, tamamlandı, doğrulandı):**
  - **Hover görsel galerisi + nokta göstergeleri:** kartın (seçili) rengine ait görsel havuzunun
    ilk 4'ü `data-ms-urun-galeri-resimler`'e | ayraçlı; misharix site.js modülü mousemove/touch
    ile gezdirir. Renk çözülemezse galeri verilmez (karışık havuz "tekrarlı galeri" üretir).
    Kategori: `GetChannelCategoryProductsQuery` (ProductId,ColorValueId)→görsel havuzu; arama:
    `GetStoreProductsQuery` ana görselin VariantId'sinden renk çözer. DTO'lar additive:
    `ProductListingColorDto.ImageUrl`, `StoreProductDto.GalleryUrls`,
    `ChannelCategoryProductItemDto.GalleryUrls/AxisColors`.
  - **Renk tooltip'i:** eksen (renk) kartları kendi görselleri + `/urun/{code}?color={eksenDeğerId}`
    linkleriyle (ilk 4 desktop; mobilde rozet tıklaması bottom-sheet panel). **Görselsiz renkler
    listelenmez** (B9 kuralı; E2E bunu gerçek bug olarak yakaladı — görselsiz İndigo linklenince
    detay ilk görünür renge düşüp yanıltıyordu, filtre eklendi). Eksen yoksa filtre_rengi'ne düşer.
  - **Kart → detay linki renk taşır:** kategori kartları `?color=`; `UrunDetayController` eksen-dışı
    değeri (filtre_rengi bucket) o değeri taşıyan varyantın eksen rengine çözer.
  - JSON kartlarda aynı markup enjekte edilir — tooltip rozetten ÖNCE (site.js rozeti bağlarken
    tooltip alanı yoksa bir daha bağlamaz), galeri "hazır" bayrağı silinir ki
    `msUrunKartDavranislariYenile` yeniden bağlasın.
  - **E2E (headless Chromium, 5051 Production): desktop 20/20 ✓ + mobil 5/5 ✓**; 0 konsol hatası;
    regresyon yok. Not: rozet hover'ı tooltip'i açınca tooltip rozeti örtüyor — Playwright
    `hover()` hit-target retry'a takılır, ham `mouse.move` kullan. `check.sh` TEMİZ ✓.
  - **⚠️ CANLI KAZA + TELAFİ:** test instance'ını kapatırken `pkill -f "ECSPros.Api.dll"` canlı
    servisi de öldürdü (systemd `User=yalcin` — aynı kullanıcı). Site ~2-3 dk kapalı kaldı;
    şifresiz sudo olmadığından servis dosyasındaki env birebir kopyalanarak **manuel** geri
    başlatıldı (nohup, publish binary). **Canlı şu an systemd DIŞINDA çalışıyor** — kullanıcı
    systemd'ye dönmeli: `pgrep -f publish/ECSPros.Api.dll` ile PID bul + kill + `sudo systemctl
    start ecspros`. Ders kaydedildi: `feedback_no_broad_pkill_prod_same_user.md` (bir daha asla
    geniş pkill; süreçler PID ile).
  - **DEPLOY:** B8 publish edildi (publish/ dizininde hazır) — yukarıdaki systemd'ye dönüş
    adımı aynı zamanda B8'i canlıya alır (manuel süreç eski binary'de).
  - **SIRADAKİ ADIM → B3 (duyuru şeridi geçici statik) veya B6 (ana sayfa geçici kompozisyon);
    sonrası B10 (sunucu tarafı filtre/sıralama) / B4-B5 (oturum+sepet).**

- **2026-07-08 — Faz B9 TAMAMLANDI: Ürün Detay canlıda hazır (kullanıcı seçimi: B8 yerine B9 öncelikli) — kart linkleri `/urun/{code}` artık 404 DEĞİL:**
  - **Mimari (tamamen SSR):** `Controllers/Store/UrunDetayController` (`/urun/{code}?color={valueId}`) +
    `Models/Store/UrunDetayVm`. Renk değişimi ?color= navigasyonudur (yeni SSR isteği — misharix'in
    renk script'i aktif sınıfı bizden önce değiştirdiğinden karşılaştırma URL/config üzerinden);
    beden seçimi client-side (misharix script'i değişmedi — sticky bar + beden modalı gerçek
    bedenlerle otomatik çalışıyor); sepete ekleme partial sonundaki config script'iyle
    `api/store/cart/items` (SPA ile aynı localStorage anahtarları; Şimdi Al da şimdilik sepete
    ekler, gerçek akış Faz C).
  - **Breadcrumb:** yeni `GetProductChannelCategoryChainQuery` (Storefront.Application) — kategoriler
    filtre tanımlı (FillType=filter/mixed) olduğundan ürün→kategori TERS eşleme kural
    değerlendirmesiyle: ProductGroupIds + AttributeFilters (listelemedeki ProductFilterHelper ile
    aynı semantik — product-level attributes), manuel atama/IsExcluded dahil, en derin aday kazanır.
  - **DTO additive genişledi:** `StoreProductDetailDto`'ya DescriptionI18n + Attributes (ürün
    seviyesi özellikler) + ProductGroupNameI18n eklendi — endpoint aynı, mobil/SPA etkilenmez.
  - **Kurallar:** beden sıralaması konfeksiyon sırası (S<M<L<XL…, numerik bedenler sayısal);
    sıfır fiyatlı varyantlar gösterim fiyatına girmez (SPA paritesi); "renk"/"filtre_rengi"
    eksenleri asla beden sanılmaz; filtre_rengi hiç yoksa "renk" ekseni renk kabul edilir;
    görselsiz renkler listelenmez; stok kontrolü B12 anahtarına kadar kapalı (hepsi satılabilir).
  - **CANLI BUG DÜZELTİLDİ (B9 dışı, CRM):** `AddToCartCommand` mevcut sepete İKİNCİ FARKLI ürünü
    hiç ekleyemiyordu — tracked cart'ın koleksiyonuna Id'si baştan atanmış (BaseEntity Guid.NewGuid)
    yeni CartItem eklenince EF DetectChanges bunu Added değil Modified sayıyor → var olmayan satıra
    UPDATE → DbUpdateConcurrencyException/500. Çözüm: `db.CartItems.Add(...)`. (Aynı kalıp başka
    handler'larda da olabilir — çocuk satırı tracked parent koleksiyonuna ekleyen yerlere dikkat.)
  - Gizlenen demo blokları (@if): puan tooltip'i, dönen teslimat mesajları (B8/B11), çoklu fiyat
    senaryoları (Faz G), model ölçüleri (manken verisi ürünlerde 0 satır), teslimat bilgileri
    (Faz H kargo), beden tablosu (veri yok), video+görsel etiketleri (B11/G). **Benzer ürünler
    bölümü misharix detay tasarımında YOK** — envanter satırı Faz G vitrinlerine devredildi.
  - **E2E (headless Chromium, 5051 Production): 12/12 ✓** — galeri (thumb=slide, tıklama), beden
    seçimi etiketi, sepete ekle API 200 (bedenli akış + bedensiz→modal→seçim→ekleme), renk SSR
    değişimi (Mavi→Lacivert, galeri değişti), tam ekran görsel modalı, paylaş modalı gerçek adla,
    mobil sabit aksiyon barı + gerçek fiyat + sticky beden paneli; **0 konsol hatası**; regresyon
    yok (favicon/kategori/arama/api 200, olmayan ürün 404). Test sepetleri DB'den silindi.
    Ekran görüntüleri: scratchpad `pw/shots/b9-*.png` (gerçek görselli ürün: P-034482).
  - `check.sh` TEMİZ ✓ (6 yeni izinli B9 girdisi). Plan (B9 [x] + Durum Panosu + envanter 8.4) ve
    `misharix-partial-vm-eslemesi.md` (6 satır) güncellendi.
  - **DEPLOY BEKLİYOR (kullanıcı):** publish edildi (B2+B7+B9 birlikte) — `sudo systemctl restart
    ecspros` sonrası https://new.ecspros.com 'da arama + kategori + ürün detay uçtan uca çalışır.
  - **SIRADAKİ ADIM → B8 (kart derinleştirme: varyant görsel galerisi + renk tooltip + kart→detay
    linki alanları) veya B3 (duyuru şeridi) / B6 (ana sayfa geçici kompozisyon).**

- **2026-07-07 — Faz B7 TAMAMLANDI (üçüncü oturum): Ürün Listesi canlıya hazır (kullanıcı seçimi: B3/B6 yerine B7 öncelikli):**
  - **Üç yüzey tek controller'da** (`Controllers/Store/UrunListesiController` + `Models/Store/UrunListesiVm`):
    kategori `/{slug}` (nav ağacından çözülür, bulunamazsa 404), arama `/urunler?search=`
    (B2 "Tümünü Gör" hedefi artık çalışıyor), tümü `/urun-listesi`. **Nav'daki kategori
    linkleri artık 404 DEĞİL** — B14 erken geçişinin bilinen boşluğu kapandı.
  - **KRİTİK ROUTE DERSİ:** kısıtsız `[HttpGet("/{slug}")]` `/favicon.ico` gibi kök statik
    dosyaları da endpoint olarak eşleştiriyor ve StaticFileMiddleware devre dışı kalıyor
    (WebApplication örtük UseRouting'i pipeline'ın BAŞINA koyar; endpoint eşleşince statik
    dosya sunulmaz). Çözüm: `{slug:regex(^[[a-z0-9-]]+$)}` (47 slug'ın tamamı kebab-case).
  - **Mimari:** ilk sayfa SSR (plan 3.3) — kart markup'ı `UrunKarti` Razor local function
    (SSR + `<template>` tek kaynak); devam sayfaları misharix infinite-scroll modülünün
    `kartHazirla`/`sonra` hook'larıyla (ilk:0, gerçek toplam) api/store JSON'dan; iskelet
    kartlar veri gelince dolar. Facet'ler controller'da süreç içi MediatR'dan SSR (kategori
    facets Redis'ten 0.01-0.03s; tüm-katalog facets ilk çağrı ~6s → 15dk IMemoryCache,
    yalnız /urun-listesi ve aramasız /urunler'i etkiler). Filtre/sıralama **SPA paritesiyle
    client-side** (valueId OR eşleşmesi + fiyat aralığı + fiyat artan/azalan; sayaç güncellenir) —
    sunucu tarafı filtre/sıralama B10'un işi. Mobil filtre panelleri gerçek facet gruplarından
    üretilir (`anaFiltreAdlari` bağlandı, 650 satırlık misharix script'i değişmedi).
  - Veri karşılığı olmayan demo blokları `@if` ile gizli: kart puan/teslimat/kargo/video/
    sponsor/kampanya/renk-tooltip (B8/B11), hızlı filtre chip'leri + Kampanya filtre bloğu
    (Faz G), eksik sıralama seçenekleri (B10). "Kategoride ara" YİNE ertelendi — backend'de
    kategori+arama birleşik sorgu yok (B10'da sorgu genişleyince bağlanmalı; plana not düşüldü).
  - **E2E (headless Chromium, 5051 Production):** Kadın 24 SSR kart → scroll 48 (24 JSON'la
    doldu), renk filtresi 33 görünür/15 gizli + sayaç "33 Ürün", fiyat sıralaması artan
    (4.99→9.99), arama sayfası 8.045 sonuç, mobil filtre panelleri (Filtre Rengi/Beden/Fiyat),
    favicon/statik/swagger/api regresyon yok, **0 konsol hatası**; `check.sh` TEMİZ ✓
    (5 yeni izinli B7 girdisi). Ekran görüntüleri scratchpad `pw/shots/b7-*.png`.
  - Plan (B7 [x] + envanter 8.3 ✅ satırları + Durum Panosu) ve `misharix-partial-vm-eslemesi.md`
    (6 yeni satır) güncellendi.
  - **Bilinen veri gerçeği (B7 dışı):** Kadın kategorisinin ilk sayfası tek ürünün renk
    kartlarıyla dolu ve görselleri CDN placeholder ("RESİM HAZIRLANIYOR" — B2'de not edilen
    kısa-slug .jpg sorunu). Kod doğru; katalog/CDN verisi düzelince kartlar gerçek görsellerle gelir.
  - **DEPLOY BEKLİYOR (kullanıcı):** publish edildi (B2 arama backend'i + B7 birlikte) —
    `sudo systemctl restart ecspros` sonrası https://new.ecspros.com 'da kategori sayfaları,
    arama sonuç sayfası ve nav araması uçtan uca çalışır olacak.
  - **SIRADAKİ ADIM → B8 (kart derinleştirme: varyant görsel galerisi + renk tooltip) veya
    B9 (ürün detay — kart linkleri `/urun/{code}` şu an 404); B3/B6 küçük işler araya alınabilir.**

- **2026-07-07 — Misharix Razor taşıma planı hazırlandı (kod yazılmadı):**
  - Karar (kullanıcı onaylı): storefront SPA'dan (`store/index.html`+`app.js`) çıkarılıp **Razor/MVC**
    render'a geçilecek; 6 Temmuz portu tasarımı "yorumlayarak" bozduğu için partial'lar bu kez
    **birebir dosya kopyası** ile taşınacak (HTML elden yazılmayacak).
  - İki yeni doküman: `docs/misharix-tasarim-projesi-inceleme.md` (tasarım envanteri) ve
    `docs/misharix-razor-tasima-plani.md` (**A→İ fazlı, checkbox'lı iş planı + Bölüm 8 işlev
    envanteri**). Sonraki session'lar plandaki Durum Panosu'ndan devam etmeli, biten işleri
    `[x] (tarih)` işaretlemeli.
  - **Tüm açık kararlar kullanıcıyla tek tek kapatıldı** (plan Bölüm 6 Karar Kaydı): host=Api içinde
    MVC (mobil app api/store/*'ı kullanmaya devam eder — API-first kuralı plan 3.4), tema=platform
    başına + token override, ödeme=test modu, SMS=soyutlama+log, vitrin=**docs/anasayfa-dizayn-yönetimi.txt**
    spec'ine göre blok+kural+snapshot sistemi (her sayfada yerleşim: anasayfa/duyuru/liste/detay/
    sepet-teslimat-ödeme; iki milestone; üye grubu segmenti dahil), adres=Core'da ülke/il/ilçe/mahalle
    tabloları, konum=pasif zincir+kullanıcı tetiklemeli izin+GeoLite2, sponsorlu=öne çıkar bayrağı,
    TCKN=format kontrolü, stok=gerçek stok+platform anahtarı.
  - **Faz A uygulandı (A2–A12 tamam, aynı gün):** ECSPros.Api'ye MVC view desteği eklendi
    (AddControllersWithViews — API JSON ayarları korunarak), misharix kabuğu bayt-bayt kopyalandı
    (Views: _Layout + 10 nav partial + footer + görsel arama modalları + Home/Index; wwwroot:
    ikons/images/video/fontawesome + tailwind.css + site.js 4388 satır + derlenmiş site.css md5-aynı),
    tema iskeleti kuruldu (StoreThemeViewLocationExpander — varsayılan tema kök Views ağacında;
    tema kodu FirmPlatform.Settings'te `theme`, token override `themeTokens` → _MsTemaTokenlari
    partial'ı; IStoreContext host→platform çözümü Store:Hosts/Store:DefaultFirmPlatformCode
    config'inden). Drift aracı `tools/misharix-sync/check.sh` (TEMİZ ✓ — tek izinli fark _Layout
    tema satırı) + `screenshot.mjs` hazır. Eşleme tablosu `docs/misharix-partial-vm-eslemesi.md`.
  - **Doğrulama:** 5051'de Production duman testi — `/` 200 (nav tam render), site.css/js/ikons 200,
    api/store/* 200, 0 hata. Canlı servise DOKUNULMADI.
  - **FAZ A KAPANDI (2026-07-07):** kullanıcı publish+restart+`up -d nginx` çalıştırdı;
    http://51.178.208.59:8080 canlı. Headless Chromium ile desktop+mobil ekran görüntüleri
    alındı, tasarımla birebir doğrulandı (0 beklenmeyen konsol hatası; favicon 404'ü kaynaktan
    kopyalanarak giderildi — sonraki publish'te canlıya gider). Certs `:ro` volume mount'u
    artık çalışıyor; manuel cert kopyalama tarifi GEÇERSİZ (memory güncellendi).
  - **Faz B1 TAMAMLANDI (2026-07-07, ikinci oturum):** platform **mishar**. Üç nav partial'ı
    kanal kategorilerine bağlandı (markup birebir korunarak): `_AnaNavigasyonDesktopMenu`
    (mega menü + üst şerit; ilk grup `-varsayilan`; kampanya şeridi statik — Faz G),
    `_AnaNavigasyonMobilMenu` (ana sekme=kök, 2 seviyede tek yan sekme+tek panel; kampanya+alt
    nav statik), `_AnaNavigasyonUst` (mobil kategori kaydırma şeridi; sepet/oturum B5/D6'ya).
    Üçü `allowed-diffs.txt`'e gerekçeyle eklendi; eşleme tablosu güncellendi.
    Kritik bulgular: nav_menus BOŞ → nav kanal kategorilerinden gelir (mishar: 4 kök + 43 çocuk,
    2 seviye, görselsiz → grid'de <img> koşullu); linkler `/{slug}` (B7'ye kadar 404 normal);
    SPA'nın app.js mega menü markup'ı örnek alınmaz; menü davranış JS'i `_AnaNavigasyon.cshtml`
    içinde inline (site.js'te değil), sol kolonu runtime'da kendisi kurar.
  - **B1 doğrulaması:** build 0 hata; 5051 Production duman testi (4 kök grup + 43 grid linki
    desktop ve mobilde, gerçek slug href'leri); `check.sh` TEMİZ ✓ (4 izinli fark); headless
    Chromium ile mega menü hover + mobil menü sekme geçişi ekran görüntüleri doğrulandı,
    0 konsol hatası; api/store/* regresyon yok. Chromium tarifi yeniden kuruldu
    (playwright-core scratchpad'e, libler apt-get download ile — binary ~/.cache'te duruyordu).
  - **DEPLOY BEKLİYOR (kullanıcı):** `dotnet publish` + `sudo systemctl restart ecspros` —
    sonrasında http://51.178.208.59:8080 'de gerçek kategorili nav görünür.
  - **B14 ERKEN GEÇİŞ (2026-07-07, kullanıcı kararı):** https://new.ecspros.com artık Razor
    storefront'u sunuyor — `locations.inc` `/` bloğu host:5000'e proxy'ye çevrildi (Cloudflare
    Flexible: origin'e 80/HTTP gelir, https'e yönlendirme YASAK — döngü yapar). Eski SPA
    yedeği 8080 portuna taşındı (rol değişimi; api+media blokları eklendi). appsettings
    `Store:Hosts["new.ecspros.com"]="mishar"` eklendi (sonraki publish'te binary'ye girer;
    default zaten mishar olduğundan acil değil). Bilinçli kabul: B7'ye kadar kategori
    linkleri 404, ana sayfa geçici sayfa seçici. Geri dönüş: iki nginx dosyasını git'ten
    geri al + `nginx -s reload`.
  - **Faz B2 TAMAMLANDI (2026-07-07):** arama paneli canlı veriye bağlandı.
    - `_AnaNavigasyonSearch.cshtml`: demo içerik boşaltıldı, dosya sonuna canlı arama
      script'i eklendi (DOMContentLoaded'da bağlanır — misharix'in parse-anı IIFE'sinden
      sonra çalışır, o görünürlüğü yönetir/biz içerik doldururuz; debounce 300ms, min 2
      karakter, istek sırası koruması). Ürün önerileri `products?search` (10 kart + toplam
      + "Tümünü Gör"), kategori önerileri nav ağacından client-side ("Kök › Çocuk" etiketi).
      Popüler aramalar gerçek terimli statik chip'ler; popüler ürünler ilk ürünlerden
      (15 dk sessionStorage); son aramalar localStorage (kalıcıları E11).
    - **Backend:** `products?search` ve `products/facets?search` artık kod VEYA Türkçe ad
      eşleşmesi yapıyor. Dictionary indexer'ı dinamik JSON'da çevrilmediği için
      `PgJsonFunctions.JsonText` → PG `jsonb_extract_path_text` DbFunction eşlemesi
      eklendi (CatalogDbContext.OnModelCreating). ~0.7-0.85s/sorgu; gerekirse pg_trgm.
    - "Kategoride ara" kapsam daraltması B7'ye bağlı (kategori sayfası yok) — plana not düşüldü.
    - E2E: headless Chromium — "elbise" 8045 ürün + 2 kategori chip, sonuçsuz mesajı,
      0 konsol hatası; `check.sh` TEMİZ ✓.
    - **BULGU (B2 dışı):** bazı ürünlerin görselleri CDN'de yok — kısa slug'lı `.jpg`'ler
      (ör. simli-abiye-elbise-2f15.jpg) hep aynı 12134b "RESİM HAZIRLANIYOR" placeholder'ını
      dönüyor; eski uzun adlı `.webp`'ler gerçek. ImageSet/yeni adlandırma verisiyle ilgili
      görünüyor, araştırılmalı.
    - **DEPLOY BEKLİYOR (kullanıcı):** `dotnet publish` + `sudo systemctl restart ecspros`.
  - **SIRADAKİ ADIM → B3 (duyuru şeridi geçici statik) veya B6 (ana sayfa geçici
    kompozisyon) — ya da kullanıcı önceliğine göre B7 ürün listesi.**

- **2026-07-07 — filtre_rengi insert'i sonrası sayfa yavaşlaması çözüldü (3 katman):**
  - **Kök neden 1 (ASIL suçlu): bayat Postgres istatistikleri.** 1.27M satırlık filtre_rengi bulk
    insert'inden sonra `ANALYZE` çalıştırılmamıştı; planner `product_variant_attributes` join'lerinde
    kötü plan seçiyordu. Tek satır `ANALYZE catalog.product_variant_attributes` ile Kadın kategori
    listesi **5.8s → 0.009s**, kategori facets 0.8s → 0.009s oldu. **DERS: her toplu insert'ten
    sonra ANALYZE çalıştır** (bkz. `feedback_analyze_after_bulk_insert.md`).
  - **Kök neden 2: `/products/facets` zaten yapısal olarak ağırdı** (tüm varyant-attribute
    satırlarını belleğe çekip C#'ta topluyordu — insert sonrası 3.7M satır, ~10 sn). Yeni sayfa
    tasarımı grid'i facet'lerle birlikte beklettiği için "Tüm Ürünler" 10 sn'ye kilitleniyordu.
    **Fix (3 parça):** (a) `GetStoreFacetsQuery.BuildFacets` DB tarafında toplanacak şekilde
    yeniden yazıldı (Distinct→GroupBy→Count; IQueryable productIds overload'ı eklendi, 90K id
    materialize edilmiyor; fiyat Min/Max da SQL'de) — 10s→~4s; (b) tüm-katalog facet'i
    **IMemoryCache** ile 15 dk cache'leniyor (`AddMemoryCache()` Program.cs'e eklendi; Redis bu
    ortamda kullanılamadığı için süreç-içi cache — arama filtreli istekler cache'lenmez);
    (c) frontend `_renderListing` artık facet'leri BEKLEMİYOR — grid ürünler gelir gelmez render
    ediliyor, filtre paneli facet cevabı gelince ayrıca doluyor (token guard ile sayfa değişimi
    yarışı korumalı).
  - İzole test (`scratchpad/facettest` console app — canlıda deneme-yanılma yapılmadı):
    yeni BuildFacets ~3.5-6s; cache'le ilk istek sonrası anlık.
  - Ölçümler (ANALYZE sonrası, eski binary): kategori products 0.009s, kategori facets 0.009s,
    ürün detay 0.56s, ürün listesi 1.1s. `products/facets` restart sonrası ilk çağrıda ~4-6s
    (arka planda, sayfayı bloklamaz), sonrasında cache'ten anlık.
  - `index.html` asset sürümü `?v=20260707a`'ya yükseltildi (immutable cache bust).
  - Publish edildi; **`sudo systemctl restart ecspros` kullanıcıda bekliyor** (frontend değişikliği
    bind-mount ile zaten canlı; restart yalnızca facets DB-aggregation + IMemoryCache için gerekli).

- **2026-07-06/07 (gece) — "renk" → "filtre_rengi" eşlemesi yapıldı, ürün listesi renk filtresi artık gerçek çalışıyor:**
  - **Kullanıcı talebi:** "Ürün listesinde renk filtresinde ürünlerin 'Filtre Rengi' özellik değerleri kullanılacak."
  - **Durum tespiti:** `filtre_rengi` attribute type'ının `definition.attribute_values`'ta 25 kürasyonlu
    değeri (Siyah #000000 → Gümüş #B0BEC5, gerçek hex kodlarıyla) zaten TANIMLIYDI ama hiçbir
    varyanta atanmamıştı (0 satır). Gerçek renk verisi tamamen "renk" attribute'unda (1.210.800
    varyant-satırı, **2648 farklı serbest-metin değer** — "Koyugri", "Siyahbeyaz", "Kiremitvizon"
    gibi birleşik/tutarsız isimler) duruyordu. Backend'in facet sorgusu (`GetStoreFacetsQuery.
    BuildFacets`) zaten `filtre_rengi`'yi `IsColorType=true` olarak işaretliyordu — sadece veri
    eksikti.
  - **Yapılan:** `/tmp/.../scratchpad/map_colors.py` — Türkçe renk kökü + eş anlamlı kelime
    sözlüğüyle (Bordo/Vişne→Kırmızı, Vizon/Taba/Camel→Bej/Kahve, Hardal/Safran→Sarı, Mint→Turkuaz,
    Antrasit/Füme→Gri ailesi, İndigo→Lacivert, vb. — ~90 kural) 2648 değeri 25 kürasyonlu bucket'a
    eşleyen bir sınıflandırıcı yazıldı. **Kapsama: %99,1** (1.199.641 / 1.210.800 varyant satırı
    eşlendi). Eşlenemeyen ~11K satır gerçekten renk olmayan değerlerdi (Standart, Renkli, Rengarenk,
    Çokrenkli, Şeffaf, Tint, Metalik, salt sayısal kodlar) — bilinçli olarak atlandı.
    Birleşik renk isimleri (örn. "Siyahbeyaz") **birden fazla filtre_rengi değerine** eşlendi
    (multi-value şema desteği zaten vardı, bkz. `project_multi_value_attributes_and_phase12_2026-07-02`).
    `definition.*`'a **hiç yeni satır eklenmedi** (Altın Kural korundu) — sadece mevcut 25 bucket'a
    eşleme yapıldı. Sonuç: `catalog.product_variant_attributes`'a 1.277.035 yeni filtre_rengi satırı
    bulk-insert edildi (transaction içinde, NOT EXISTS ile idempotent).
  - **Backend değişikliği:** `GetStoreFacetsQuery.BuildFacets`'ta "renk" artık facet listesinden
    çıkarılıyor (`byType.Remove("renk")`) — 2082 farklı değeri olan kullanılamaz bir filtre yerine
    artık sadece 25 değerli, gerçek hex'li `filtre_rengi` gösteriliyor. "renk" verisi kendisi
    silinmedi/değişmedi, sadece listeleme filtresi olarak sunulmuyor (ürün kartı/detayında hâlâ var).
    Publish edildi, **`sudo systemctl restart ecspros` kullanıcıda bekliyor**.
  - **Beklenen yan etki (olumlu):** Ürün detay sayfasındaki görsel renk swatch'ları da artık
    çalışmalı — `GetStoreProductDetailHandler`'daki `IsColor: AttributeTypeCode=="filtre_rengi"`
    mantığı zaten oradaydı, sadece veri eksikti (önceki oturumlarda bilinen bir boşluktu).
  - **Doğrulama (restart sonrası yapılmalı):** `/api/store/catalog/products/facets` çağrısında
    `filtre_rengi` (25 değer, hex'li) görünmeli, `renk` artık listede olmamalı; ürün listesi
    sayfasında sol filtrede gerçek renk swatch'ları görünmeli.

- **2026-07-06 (gece) — Misharix tasarım sistemi Faz 0+1 portu (Navigasyon+Ana Sayfa+Liste+Detay):**
  - **Kapsam kararı (kullanıcı onaylı):** `/opt/misharixWebSites` (ayrı ASP.NET+Tailwind prototipi)
    tam kapsamlı planla ECSProsAI storefront'a portlanacak — Faz 0 (Tailwind build altyapısı) + Faz 1
    (Nav/Home/Liste/Detay) bu oturumda uygulandı; Faz 2 (Sepet-Checkout), Faz 3 (Hesabım), Faz 4
    (Kurumsal/CMS), Faz 5 (Değerlendirmeler) plan dosyasında tanımlı, sonraki oturumlara bırakıldı.
    Plan: `/home/yalcin/.claude/plans/clever-tumbling-hellman.md` (referans için okunabilir, ama
    plan dosyaları kalıcı değildir — asıl kaynak bu PROGRESS.md notu ve kod).
  - **Faz 0 — Tailwind build:** `store/package.json` (yeni, `@tailwindcss/cli`), `store/css/tailwind.css`
    misharix'ten **birebir kopyalandı** (11.547 satır, `@source` sadece `../index.html` + `../js`
    olarak değiştirildi — geri kalan `@theme`/`@layer base`/`@layer components` aynen korundu, elle
    çeviri yapılmadı). `npm run css:build` → `store/css/site.css` (828KB, minified). `main.css` artık
    kullanılmıyor (silinmedi, referans için duruyor). `.gitignore`'a `node_modules/` eklendi.
  - **`store/js/site.js` (yeni, ~700 satır):** Misharix'in `wwwroot/js/site.js`'inden (4388 satır)
    gerçekten paylaşılan/global parçalar portlandı: sayfa-modülü registry (`msRegisterPageModule`/
    `msRunPageModules`), lazy-load (IntersectionObserver, `.lazy-infinite-on` opt-in), genel modal
    (`ms-ornek-modal`), filtre akordiyonu, sıralama select'i, özel select, mobil menü aç/kapa, arama
    paneli, sepet dropdown, mega menü hover/click, footer akordiyonu, ürün kartı davranışları
    (mini galeri hover/touch, favori kalp animasyonu — **localStorage bazlı, backend'i yok**, renk
    tooltip), ürün detay galerisi (thumb rail + sürükle-geçiş + lightbox modal).
    **Bilinçli sadeleştirmeler:** infinite-scroll motoru Misharix'te statik `<template>` klonlayan bir
    demo motoruydu — burada **gerçek API sayfalamasıyla** çalışan bir motora dönüştürüldü
    (`window.msInfiniteLoaders[ad]` — sayfa kendi yükleyicisini kaydeder). Ürün detay galerisinde
    Misharix'in hover-zoom lens'i ve modal pinch-zoom'u **yok** (basit lightbox var) — fast-follow.
    Kampanya şeridi, görsel arama, giriş/kayıt modalleri bu Faz'da **yok** (veri/kapsam yok).
  - **Mimari:** `data-ms-page-module="..."` listesi `<body>` etiketine eklendi (ornek-modal, filtre-
    bloklari, siralama-select, ozel-select, gorunum-carousel, urun-karti, urun-detay-resim, magaza-
    menu, footer-akordiyon, tab-grubu) — `window.msRunPageModules(document)` her route değişiminde
    çağrılır (idempotent, WeakMap ile). `app.js`'teki API client/Cart/LS veri mantığı **korundu**,
    sadece render fonksiyonları (`prodCardHtml`, `listingHtml`, `pageHome`, `pageProduct`, `initNav`,
    `renderCartPanel`) `ms-` class'larına geçirildi. Ölü kod temizlendi (`buildPagination`,
    `prodSwatchClick`, `quickAdd` — infinite-scroll ve gerçek ürün detay sayfası bunların yerini aldı).
  - **Bulunan ve düzeltilen 4 gerçek bug** (headless Chromium ile doğrulama sırasında bulundu):
    (1) `#homeFeat` grid'inde `lazy-infinite-on` class'ı eksikti → hiç görsel yüklenmiyordu.
    (2) Mega menü üst linkleri `.ms-magaza-menu-kaydirma-grubu` sarmalayıcısı olmadan doğrudan
    2-kolonlu CSS grid'e ekleniyordu → satır kaydı bozuk sarıyordu; sarmalayıcı eklendi.
    (3) Ürün detay lightbox modalına temel `ms-ornek-modal` class'ı eksikti → modal sayfa
    yüklenir yüklenmez açık görünüyordu (varsayılan gizli state'i CSS'te bu class'a bağlı).
    (4) `LS.renderFacets()` artık var olmayan `#filterPanel` id'sini arayıp erken çıkıyordu →
    filtre facet'leri hiç render olmuyordu; kontrol kaldırıldı.
  - **Doğrulama:** root'suz headless Chromium (`reference_headless_chromium_no_root` tarifi) ile
    canlı sitede ana sayfa/liste/ürün detay/mobil menü uçtan uca test edildi — **0 konsol hatası**.
    Sepet API entegrasyonu (`dpAddToCart`→API→badge→dropdown) doğrulandı, hatasız.
  - **Bilinmesi gereken önemli veri durumu (kod hatası DEĞİL):** `inventory.inv_stocks` tablosunda
    sadece 249 satır var ve **hiçbiri pozitif kullanılabilir stok içermiyor** (`Quantity -
    ReservedQuantity <= 0` her yerde) — yani canlı katalogda şu an HİÇBİR ürün gerçek stokla
    "Sepete Ekle" gösteremiyor, her yerde "Seçim Yapın" (devre dışı) görünüyor. Bu Faz 1'in bir
    regresyonu değil, önceki app.js'te de aynı veriye dayanıyordu — sadece bu oturumda fark edildi.
    Stok verisi ayrı bir konu (muhtemelen ERP senkronizasyonu hiç çalışmamış).
  - **`ms-urun-detay-renk-listesi` (görsel renk swatch) boş kalıyor:** çünkü katalogda "filtre_rengi"
    attribute'u hâlâ hiç populate edilmemiş (önceden bilinen boşluk); "renk" attribute'u generic
    `ms-beden-secim` listesi olarak (Beden gibi) düz metin pilleri şeklinde render ediliyor — bu
    davranış Faz 1 öncesi app.js'te de aynıydı, regresyon değil.
  - **Sıradaki adım:** Faz 2 (Sepet→Teslimat→Ödeme→Sipariş Tamamlandı + giriş/kayıt modalı + misafir
    sepeti `POST /cart/merge` ile üyeye taşıma) — plan dosyasında detaylı.

- **2026-07-06 (akşam) — Ürün detay "resimler tekrarlı" sorunu kökten çözüldü:**
  - **Kök neden 1 — DB'de 651.165 birebir çift resim satırı** (toplam aktif 1.465.086'nın %44'ü):
    eski MySQL `apurunresimleri` aynı (ürün, varyant, dosya) kaydını birden çok satırla tutuyor,
    MigrationTool Faz 7 bunları aynen kopyalıyordu. Handler'daki bellek-içi dedupe bunu store
    detayında maskeliyordu ama her migration yeniden çalıştırıldığında çiftler geri geliyordu.
    **Temizlik:** çiftler soft-delete edildi (kapak tercihli: `IsVariantCover DESC` sıralamasıyla
    teki tutuldu) → 813.921 aktif satır kaldı, 0 çift grup, hiçbir varyant kapak kaybetmedi.
  - **Kök neden 2 — MigrationTool Faz 7 tekilleştirmiyordu:** `seenTargetKeys`
    (productGuid, variantGuid, fileName) HashSet'i eklendi; yeniden çalıştırma artık çift üretmez.
  - **Kök neden 3 — karışık renk galerisi:** kendi görseli olmayan varyantlar TÜM renklerin
    ürün-düzeyi (VariantId=null) havuzuna düşüyordu (örn. P-00010460: 18 resim = aynı 3 poz × 6 renk;
    bu ürün-düzeyi satırlar, `variantMap`'te bulunamayan eski varyantların görsellerinden oluşuyor).
    **Fix:** `GetStoreProductDetailHandler` renk grubunu artık `filtre_rengi` YOKSA `renk` ekseniyle
    kuruyor; birleştirilen listeler dosya adına göre teke iniyor (SQL DISTINCT değil, bellek içi).
  - **İkinci katman (P-00022181 raporu üzerine bulundu):** Eski MySQL'de İKİ resim seti var
    (`dfresimsetleri`: 1=Varsayılan 775K satır, 2=Julude 691K satır) ve **197.078 varyantın
    resimleri her iki sette de kayıtlı** — aynı fotoğraf set başına AYRI dosya adıyla
    (`…_5639.webp` / `…_5650.webp`, md5 birebir aynı; son ek set kopya id'si). İlk temizlik
    (651K) dosya adı AYNI olan set kopyalarını zaten götürmüştü; dosya adı FARKLI olan 20.958
    kalıntı ikinci geçişte soft-delete edildi (anahtar: ProductId+VariantId+SortOrder+
    `REGEXP_REPLACE(FileName,'_[0-9]+(\.ext)$','\1')`). Aktif satır: 792.963; kapak kaybı 0.
  - **MigrationTool Faz 7'ye set seçimi eklendi:** varyant başına tek resim seti alınır
    (en çok resmi olan, eşitlikte küçük id) — `chosenSet` ön-taraması. Not: `imageSetMap`
    isimle eşleşemeyince (legacy "Varsayılan" ≠ bizim "Varsayılan Resim Seti") tüm satırlar
    zaten Julude setine yığılıyormuş; set bilgisi PG'de ayrıştırıcı olarak kullanılamadı,
    o yüzden dosya adı son eki kullanıldı.
  - **Handler'a son kural:** ürün-düzeyi (VariantId=null) havuz, üründe HİÇ varyant-bağlı
    görsel yoksa kullanılır; varsa görselsiz renk boş döner (UI görseli olan ilk varyanta
    düşüyor) — P-00010460'ta resmi hiç olmayan Kırmızı'nın 18'lik karışık galerisi bununla kapandı.
  - Yeni binary publish edildi; **`sudo systemctl restart ecspros` kullanıcıda** (şifresiz sudo yok).
  - Doğrulama: P-00022181 restart'sız düzeldi (veri temizliği yetti — 5 tekil poz × tüm bedenler,
    200 ürünlük API taramasında 0 exact + 0 set-kopyası dup). Restart sonrası P-00010460'ta
    18'lik varyant kalmamalı ([3] olmalı).

- **2026-07-06 — Mishar storefront canlıya alındı (menü + kategori listeleme + ürün detay):**
  - **Kritik bulgu — canlı API stale binary'ydi:** `ecspros` servisi 2026-07-04'teki Catalog→Storefront
    refactor'ünden (firm_platform_variants/products tablolarının kaldırılması) beri hiç yeniden
    publish edilmemişti; `GET /api/store/catalog/products/{code}` TÜM platformlarda 500 veriyordu
    (`catalog.firm_platform_variants` yok hatası). Kaynak kod zaten doğruydu — sadece
    `dotnet publish` + `systemctl restart ecspros` gerekiyordu (kullanıcı restart'ı yaptı).
  - **İkinci bug (bu oturumda bulundu ve düzeltildi):** `GetStoreProductDetailHandler.cs`, varyant
    görsellerini sadece `filtre_rengi` attribute'una göre gruplu (`imgsByColor`) veya
    VariantId=null (ürün düzeyi) görsellerle dolduruyordu. Gerçek katalogda (1.21M varyant)
    `filtre_rengi` HİÇ atanmamış — VariantId'ye doğrudan bağlı görseller sessizce kayboluyordu.
    Fix: `imgsByVariantId`'den doğrudan fallback eklendi (renk grubu → varyantın kendi görseli →
    ürün düzeyi görsel). Tekrar publish + restart ile canlıya alındı.
  - **`tools/MigrationTool/Program.cs`'e Faz 15 eklendi** (`Phase15_SeedChannelCategories`,
    `dotnet run -- 15 <firmPlatformId>`) — cinsiyet×ürün grubu kesişimine göre (DB'den gerçek
    sayılar çekilerek, ≥10 ürünlü kombinasyonlar) ChannelCategory ağacı kurar, slug bazlı upsert
    (tekrar çalıştırılabilir). Mishar (`c900c659-8d0f-4754-9658-aa157ea3072e`) için çalıştırıldı:
    **4 kök (Kadın/Erkek/Çocuk & Bebek/Ev & Yaşam) + 43 alt kategori**, hepsi `FillType=filter`
    (117K ürünlük katalogda dinamik filtre, manuel ürün senkronu gerekmiyor).
  - `store/js/app.js`'teki `CFG.FPID` demo_web'den mishar'a çevrildi — nginx artık `/store`'u
    kök `/`'e yönlendiriyor (önceki oturumda değişmiş), yani canlı site artık `https://<ip>/`.
  - Root'suz headless Chromium ile uçtan uca doğrulandı: ana sayfa, kategori listeleme (gerçek
    görsel/fiyat/facet), ürün detay (galeri/fiyat/varyant seçimi) — hepsi çalışıyor, konsol hatası yok.
  - **Bilinen, düzeltilmeyen iki boşluk (kapsam dışı bırakıldı, kullanıcıya bildirildi):**
    (1) `filtre_rengi` hiç populate edilmediği için ürün detayında renk swatch/görsel-bazlı seçim
    yerine düz metin buton listesi görünüyor (fonksiyonel ama görsel değil).
    (2) Bazı ürün görselleri `cdn.misharitalia.com`'da (müşterinin kendi harici CDN'i) gerçekten
    yok — CDN "RESİM HAZIRLANIYOR" placeholder görseli dönüyor (200 OK ama yer tutucu). Kaç
    ürünü etkilediği ölçülmedi; bu ECSPros tarafında düzeltilecek bir şey değil.
  - **Sıradaki adım:** tozlu/julude için aynı Faz 15'i çalıştırmak (istenirse), veya
    `filtre_rengi`/CDN boşluklarının araştırılması.

- **2026-07-06 (devam) — Kategori ürün listesi performans sorunu:**
  - Kullanıcı "kategori ürün listesi çok yavaş" bildirdi. Kök neden: `GetChannelCategoryProductsQuery.HandleColorMode`
    (renk-kartı listeleme), büyük `FillType=filter` kategorilerde (10K+ ürün) TÜM varyant+attribute
    satırlarını uygulamaya çekip bellekte (LINQ-to-Objects) grupluyordu — kategori boyutuyla orantılı,
    sayfa boyutuyla değil. Ölçüm: Kadın›Pantolon (22.7K ürün) 2.4sn, Kadın kökü (~84K ürün) 6.3sn.
  - **Uygulanan düzeltme (canlıda, kalıcı):** `allVariantAttrs` sorgusu artık sadece primary-axis
    attribute type'ı çekiyor (ör. "beden" atlanıyor) ve `ColorNameI18n`/`HexCode` bu toplu sorgudan
    kaldırıldı — renk adı artık sadece SAYFALANMIŞ ~24 öğe için ayrı, küçük bir sorguyla çekiliyor
    (`HexCode` zaten hiç kullanılmıyormuş, tamamen kaldırıldı). Sonuç: Kadın›Pantolon ~2.1sn,
    Kadın kökü ~4.8sn (~%10-25 iyileşme, doğrulandı — aynı toplam sayı 40.799 korunuyor).
    Küçük/orta kategoriler (43 kategorinin çoğu) zaten 0.16sn-1.5sn arasında, sorun değil.
  - **Redis cache denemesi BAŞARISIZ — geri alındı, kod şu an cache'siz durumda:** `ICacheService`
    (`Shared.Infrastructure/Caching`) hiç kullanılmıyordu, `Shared.Contracts`'a taşınıp
    `GetChannelCategoryProductsQueryHandler`/`GetChannelCategoryFacetsQueryHandler`'a wire edildi.
    Ama production Redis **NOAUTH/AuthenticationFailure** veriyor — appsettings.Production.json'daki
    şifre (`***KALDIRILDI***`) docker-compose.yml'dekiyle aynı görünüyor ama ÇALIŞAN container
    farklı bir şifreyle ayağa kalkmış olmalı (muhtemelen `docker compose restart` `command:`'ı
    yeniden uygulamıyor — gerçek çözüm `sudo docker compose up -d redis` ile container'ı yeniden
    OLUŞTURMAK, sadece restart etmek değil). Bu denendi (kullanıcı restart etti) ama hata devam etti —
    muhtemelen kullanıcı da sadece `restart` kullandı (`up -d` değil), ya da başka bir uyuşmazlık var.
    **Cache kodu güvenlik için tamamen geri alındı** (2 kez düzeltip 2 kez geri almak zorunda kalındı,
    ilk denemede Redis timeout'u istekleri 6sn'ye kadar YAVAŞLATMIŞTI). `ICacheService` arayüzü
    `Shared.Contracts`'ta duruyor (zararsız, kullanılmıyor) ama implementasyon/wiring geri alındı.
    **Redis şifresi konusunda ben kimlik doğrulama denemesi YAPMADIM** (auto-mode credential-guessing
    olarak engellendi, doğru bir engelleme) — bu tamamen kullanıcının kontrol etmesi gereken bir konu.
  - **Redis, container yeniden oluşturulduktan SONRA da denendi — daha da başarısız oldu:**
    Kullanıcı `sudo docker exec ecommerce-redis redis-cli -a '<şifre>' ping` ile Redis'in şifreyi
    doğru kabul ettiğini kanıtladı (**PONG** döndü). Cache kodu 3. kez eklendi, bu sefer try/catch
    (Redis erişilemezse sessizce DB'ye düş) + `AbortOnConnectFail=false` + `ConnectTimeout=2000`
    ile daha dayanıklı hale getirildi. Sonuç: **DAHA DA KÖTÜ** — Kadın kökü 4.8sn'den 15-22sn'ye
    çıktı (hem cold hem "warm" istekler). Redis şifre olarak çalışıyor ama .NET/StackExchange.Redis
    client'ının bu ortamda (network/timeout/retry davranışı belirsiz) ciddi bir uyumsuzluğu var —
    kök neden bulunamadı. **Cache kodu 3. ve SON kez tamamen geri alındı, bu oturumda Redis
    tamamen bırakıldı.** `ICacheService` arayüzü `Shared.Contracts`'ta duruyor (zararsız,
    kullanılmıyor), implementasyon/wiring/`DependencyInjection.cs` ayarları hepsi geri alındı.
    Canlıda şu an sadece sorgu optimizasyonu aktif: Kadın›Pantolon ~1.8-2.9sn, Kadın kökü ~4.6-5.0sn
    (restart sonrası doğrulandı, 0 Redis log satırı).
  - **Kesin teşhis (4. ve son deneme — izole test + canlı zamanlama logları):** Kullanıcı "cache
    nerede, sorun çözülmedi" diye haklı olarak itiraz etti. İzole bir standalone .NET konsol testi
    yazıldı (production ile AYNI StackExchange.Redis 2.7.27 + Microsoft.Extensions.Caching.
    StackExchangeRedis 8.0.14 sürümleri, aynı şifre) — **tamamen sorunsuz çalıştı** (bağlantı 136-187ms,
    SET/GET 0-25ms). Bu, Redis'in, şifrenin ve kütüphanenin kesinlikle sorunsuz olduğunu kanıtladı.
    Sonra cache kodu 4. kez eklendi ama bu sefer GEÇİCİ zamanlama enstrümantasyonuyla (Stopwatch +
    ILogger.LogWarning, her adımda: GetAsync/SetAsync/DB compute/TOTAL). Canlıda gerçek loglar:
    `GetAsync FAILED elapsedMs=5935`, `SetAsync FAILED elapsedMs=5715` — ikisi de TAM 5000ms
    StackExchange.Redis timeout'una çarpıyor. Exception detayları: **her seferinde birebir aynı**
    `state: ConnectedEstablishing`, `last-heartbeat: never`, `last-recv: 309` — bu, her istekte
    yeni bir bağlantı denemesi YAPILMADIĞINI, tek bir paylaşılan bağlantı nesnesinin bir kere (muhtemelen
    ilk kullanımda) handshake ortasında tıkanıp kaldığını ve process ömrü boyunca hep o bozuk
    bağlantıya karşı timeout yediğini kanıtlıyor. **Kök neden: production sürecinin içinde paylaşılan
    Redis multiplexer'ın İLK bağlantı kurulumu sırasında bir yerde takılıp kalması — kimlik bilgisi,
    kütüphane veya Redis'in kendisiyle ilgisi yok.** İzole tek seferlik testlerle üretilemiyor (süreç
    ortamına özgü, muhtemelen bir başlangıç zamanlaması/eşzamanlılık sorunu).
  - **✅ ÇÖZÜLDÜ (5. deneme) — kök neden bir config anahtarı uyuşmazlığıymış:** `NOAUTH` hatasındaki
    kritik ipucu ("Attempted command: ECHO" — yani AUTH hiç gönderilmemiş) sonunda doğru okundu:
    uygulama Redis'e hep **ŞİFRESİZ** bağlanıyormuş. Base `appsettings.json`'da
    `ConnectionStrings:Redis = "localhost:6379"` (şifresiz) vardı; production şifresi ise
    `appsettings.Production.json`'da **standart olmayan** `Redis:ConnectionString` anahtarı
    altındaydı. Kod `GetConnectionString("Redis")` çağırınca base'deki şifresiz değeri buluyordu
    (null olmadığı için `?? configuration["Redis:ConnectionString"]` fallback'i de hiç devreye
    girmemişti). İzole test çalışmıştı çünkü şifre elle yazılmıştı. **Fix:** hem
    `src/ECSPros.Api/appsettings.Production.json` hem `publish/appsettings.Production.json`'da
    (publish bu dosyayı ÜZERİNE YAZMIYOR — ayrı ayrı düzeltildi) Redis bağlantısı standart
    `ConnectionStrings:Redis` anahtarına taşındı, eski anahtar kaldırıldı. Cache kodu geri eklendi
    (try/catch'li dayanıklı desen), publish + restart sonrası doğrulandı:
    **cache hit 11-19ms** (önceden 2-5.5sn), gerçek cold (hiç açılmamış sayfa) ~5.5sn → warm 11ms,
    restart sonrası 0 Redis hatası. Facets endpoint'i de cache'li (17-19ms).
  - **Kalan doğal davranış:** her (kategori, sayfa) kombinasyonunun İLK ziyaretçisi hâlâ DB maliyetini
    öder (büyük kategorilerde 2-5.5sn), sonraki 10dk (TTL) içindeki herkes ~15ms alır. İstenirse
    popüler kategorilerin 1. sayfalarını periyodik ısıtan bir background warmer eklenebilir.
  - **Not:** `publish/appsettings.Production.json` publish ile kopyalanmıyor (elle yönetiliyor) —
    gelecekte config değişikliklerinde İKİ dosyanın da güncellenmesi gerektiği unutulmamalı.

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

- **2026-06-15 — Demo web sitesi kanal kategorileri (ChannelCategory) seed:**
  - `DemoDataSeeder` içine `SeedChannelCategoriesAsync` eklendi — `demo_web` platformu için 10 kanal kategorisi oluşturuldu
  - Kök: Giyim, Ayakkabı, Çanta (filter), Spor & Aktif Giyim (filter), Kampanyalar
  - Alt: Erkek Giyim, Kadın Giyim, Çocuk Giyim (Giyim altında); Erkek Ayakkabı, Kadın Ayakkabı (Ayakkabı altında)
  - Filter kategoriler: ProductGroupIds + target_audience (Erkek/Kadın → +Unisex) AttributeFilters birleşimi
  - `EnsureProductTargetAudienceAsync` eklendi — demo ürünlerine eksikse `target_audience` (cinsiyet) ProductAttribute atanır (TSHIRT-001 zaten Kadın idi, diğer 6 ürüne atandı)
  - FilterDef'e göre eşleşen ürünler `ChannelCategoryProducts`'a yazıldı (toplam 10 atama), production'da `dotnet publish` + `systemctl restart ecspros` ile çalıştırıldı
  - **Düzeltme (aynı gün):** FilterDef alan adları PascalCase yazılmıştı, admin `FilterBuilder` camelCase (`productGroupIds`, `attributeFilters[].attributeTypeId/valueIds`) bekliyor — düzeltildi
  - `ChannelCategoryGroups` (kapsam) ve `ChannelProductGroups` (kanalda aktif 7 ürün grubu) seed'e eklendi — coverage artık 7/7 gösteriyor
  - Seed artık slug bazlı upsert: tekrar çalıştırıldığında kategorileri güncelleyip ürün/kapsam atamalarını yeniden hesaplar (idempotent, "zaten mevcut" ile tamamen atlamıyor)

- **2026-05-31 — Filtre Şablonu (FilterPreset):**
  - `FilterPreset` entity: Code, NameI18n, Description (insan dili), FilterDef (JSONB); migration: `AddFilterPreset`
  - `Category.FilterPresetId` nullable FK; query-time merge: preset + override birleşir
  - CQRS: Create/Update/Delete/GetList(usageCount)/GetDetail + UpdateCategory güncellendi
  - `SyncCategoryProducts` ve `GetStoreCategoryProductsQuery` preset-aware hale getirildi
  - Admin Panel: `/catalog/filter-presets` — liste + create/edit/delete modal, JSON editör, kullanım sayacı
  - CategoryDetailPage: Filtre Şablonu selector, preset özeti (collapsible JSON), override kurallar

- **2026-07-02 — ERP entegrasyon anahtarları (erp_variant_data) + MigrationTool düzeltmesi:**
  - `integration.erp_variant_data` tablosu eklendi (VariantId+FirmIntegrationId unique, JSONB Payload) — ERP anahtarları (Nebim modelCode/colorCode/sizeValue/barcode) normalize edilmeden, tamamen varyant bazlı saklanıyor (kullanıcı kararı)
  - **`tools/MigrationTool/Program.cs` tamamen bozuktu** — definition/catalog şema ayrımından önce yazılmış eski `catalog_*` tablo adlarını hedefliyordu, `catalog.products`/`catalog.product_variants` canlıda 0 satırdı. Tüm dosya güncel şemaya göre düzeltildi (DEF/CAT şema sabitleri), 500'lük batch INSERT'e çevrildi (öncesi tek tek PgExec — saatler sürerdi)
  - Canlıya yüklendi: **117.569 ürün, 1.210.800 varyant, 2.421.591 varyant-attribute, 1.465.086 resim, 1.210.800 erp_variant_data** satırı
  - `core_integration_services`'a 'nebim' (ServiceType=erp), `core_firm_integrations`'a demo firma↔nebim bağlantısı eklendi (idempotent, Phase 11 içinde)
  - Detaylar: `project_erp_variant_data_2026-07-02.md` (auto-memory)

- **2026-07-02 — Çoklu değerli özellik desteği + Faz 12 (beden özellikleri/açıklama aktarımı):**
  - **Çoklu filtre rengi tereddüdü çözüldü:** `catalog.product_attributes` ve `catalog.product_variant_attributes` unique index'i `(EntityId, AttributeTypeId)` → `(EntityId, AttributeTypeId, AttributeValueId)` olarak gevşetildi (migration: `MultiValueProductAttributes`). Artık bir ürün/varyant aynı attribute type için birden fazla değer taşıyabilir (örn. "Kırmızı-Mavi Çizgili" varyantı hem kırmızı hem mavi filtre rengi altında görünebilir). `SetProductAttributesCommand` buna göre güncellendi (eşleştirme artık AttributeTypeId+AttributeValueId çiftine göre, tek AttributeTypeId'ye göre değil).
  - **Kök neden bulundu (P-00022000 örneği):** `apurunbedenozellikleri` (beden bazlı ölçüler: Göğüs/Bel/Üst-Alt Boy) ve `apurunaciklamalari` (serbest metin: Kalıp/Astar/Fermuar/Esneklik/Kumaş Özelliği/...) kaynak tabloları migration'a HİÇ dahil edilmemişti.
  - **`tools/MigrationTool/Program.cs`'e Faz 12 eklendi:** `apurunbedenozellikleri` → `catalog.product_axis_sub_attribute_values` (Üst Boy + Alt Boy gibi aynı tipe düşen çakışmalar tek satırda etiketli metin olarak birleştirildi, örn. "Üst Boy (Cm): 42 / Alt Boy (Cm): 33" — yeni attribute_type eklemeden); `apurunaciklamalari` → `catalog.product_attributes` (var olan tiplere eşlenen temiz anahtarlar: Kalıp/Astar/Fermuar/Esneklik/Taban Özelliği/Taban Yükseklik/Dış Materyal/Çanta Ağzı/Askı Tipi-Boyu/İç Cep/Balen/Dolgu/İç Yüzey/Topuk Boyu — Zipper/Underwire/Platform Boy İngilizce eş anlamlıları da aynı tiplere yönlendirildi).
  - **Kumaş Özelliği (kompozisyon metni, örn. "%97 Polyester %3 Likra") bilinçli olarak picklist'e değer olarak eklenmedi** (318+ dağınık varyant, kumas_turu'nün temiz halini bozar) — bunun yerine `kumas_turu` tipi üzerinde `ProductAttribute.CustomValue` JSON alanına serbest metin olarak yazıldı.
  - **Definition şemasına hiç yeni attribute_type eklenmedi** (kullanıcı talimatı) — sadece var olan tiplere yeni attribute_values eklendi (6094 → 7913). `primer` tipi definition şemasında artık yok (önceki oturumun notu güncel değilmiş), Faz 12 bunu tespit edip uyarı verdi ve sessizce atladı.
  - Arapça i18n eş anahtarları ve "Ekstra Askı"/"Açıklama ve Uyarı" bu geçişte kasıtlı olarak atlandı (ayrı takip konusu, gerekirse ileride eklenebilir).
  - Önce P-00022000 üzerinde test edildi (kullanıcı onayı ile), doğrulandı, sonra tüm 117.569 ürüne uygulandı: **21.321 axis-sub-attribute satırı + 352.453 ürün özelliği + 72.039 kumaş kompozisyonu**. Faz idempotent (yeniden çalıştırılabilir, kendi yazdığı satırları temizleyip yeniden yazıyor).
  - **Sıradaki adım:** Admin UI'da çoklu değerli attribute girişi (örn. filtre rengi için çoklu seçim) henüz eklenmedi — backend/şema hazır, arayüz ayrı bir iş.

- **2026-07-02 — Ürün Özellikleri sekmesi şema tamamlama (Faz 13 sonrası bulunan takip işi):**
  - Kullanıcı P-00021204'te Faz 13 ile aktarılan verilerin panelde görünmediğini bildirdi. Kök neden: "Özellikler" sekmesi `product.attributes` değil, ÜRÜN GRUBUNUN `product_group_attributes` şemasını render ediyor — şemada olmayan bir attribute type'ın hiçbir yerde görünecek alanı yok (veri doğru olsa bile).
  - Gerçek bug DEĞİL: Cinsiyet/Esneklik/Kalıp zaten şemada vardı ve doğru gösteriliyordu (kullanıcının ekran görüntüsüyle doğrulandı). Görünmeyen 6 tip (Boy, Kumaş Türü, Yaş Grubu, Malzeme zaten vardı, Astar Durumu, Fermuar) hiçbir grup şemasında (Marka) veya bu ürünün grubunda (diğerleri) tanımlı değildi.
  - Canlı DB'de hangi grupların bu 5 tip (boy/kumas_turu/yas_grubu/astar_durumu/fermuar) için gerçek ürün verisi olduğu sorgulandı (151 grup×tip kombinasyonu, `project_group_schema_completion_2026-07-02.md`), veri kanıtına göre `Cloth()`/`Shoe()`/`Acc()` base attribute'lerine eklendi (Marka hariç — o panelde attribute olarak gösterilmiyor, ayrı konu).
  - Canlıya direkt SQL ile 225 yeni `product_group_attributes` satırı eklendi (ON CONFLICT DO NOTHING, mevcut MAX(SortOrder)+n ile). `DatabaseSeeder.cs`'deki `Cloth()`/`Shoe()`/`Acc()` helper'ları da güncellendi (gelecek fresh install'lar için, sort 100+).
  - Doğrulama: gerçek Chromium (playwright-core, sudo olmadan .deb paketlerinden çıkarılan shared library'lerle) ile canlı panelde login olup sayfa render edildi, Cinsiyet/Esneklik/Kalıp'ın doğru göründüğü teyit edildi.
  - **Takip (performans):** Kullanıcı şema tamamlama sonrası "etiketler anında, değerler 10 saniye sonra doluyor" bildirdi. Kök neden: `GET /catalog/attribute-types` 15+ saniye sürüyordu (`GetAttributeTypesQueryHandler`'daki "kaç üründe kullanılıyor" sayımı `GroupBy(...).Select(g => g.Select(x=>x.ProductId).Distinct().Count())` şeklinde yazılmıştı — EF Core bunu grup başına korelasyonlu alt sorguya çeviriyor, artık 708K+2.4M satırlık tablo üzerinde çok yavaş). Önce sorgu `Select(...).Distinct().GroupBy(...).Count()` şeklinde yeniden yazıldı (15s→4s), sonra bu sayımın SADECE `AttributeTypeDetailPage`'de kullanıldığı görülüp yeni `includeCounts` parametresi eklendi; `ProductDetailPage`/`ProductGroupDetailPage`/`FilterBuilder` artık `includeCounts=false` ile çağırıyor (4s→~1s). `dotnet publish` + `systemctl restart ecspros` (kullanıcı kendi terminalinde çalıştırdı, bu oturumda sudo şifresi yok) + `npm run build` ile canlıya alındı. Detaylar: `project_group_schema_completion_2026-07-02.md`.

- **2026-07-02 — Faz 13: apurunvaryanttipdegerleri (ürün bazlı gerçek özellik değerleri) migrasyonu:**
  - **Kök neden (P-00021204 örneği, kullanıcı bildirdi):** `apurunvaryanttipleri` sadece "bu ürüne bu tip atanmış" bilgisini taşıyordu, DEĞERİ taşımıyordu. Gerçek değer (örn. Cinsiyet=Kadın) `apurunvaryanttipdegerleri` tablosunda (1.17M satır) duruyordu ve bu tablo migration'a HİÇ dahil edilmemişti. Sadece marka (Faz 5), varyant eksenleri (Faz 6, apurunvaryantlari), ve apurunaciklamalari serbest metninden parse edilen birkaç tip (Faz 12: kalıp/astar/fermuar/esneklik) aktarılıyordu. Faz 10 (cinsiyet) ise hiç çalıştırılmamıştı ve zaten ürün bazlı değil sınıf bazlı varsayım kullanıyordu.
  - `tools/MigrationTool/Program.cs`'e Faz 13 eklendi: `apurunvaryanttipdegerleri` → `catalog.product_attributes`, ON CONFLICT DO NOTHING (Faz 10/12 ile çakışmaları güvenle atlıyor). Renk/Beden (varyant ekseni) ve ~15 ops/takip tipi (Tedarikçi, Kampanya Kodu, Ürün Grubu, vb.) atlanıyor. 3 yeniden adlandırılmış tip yönlendirildi: Kumaş Tipi(27)→`kumas_turu`, Yaka Stili(33)→`yaka_tipi`, Meteryal(42)→`malzeme`. Cep(30)/Tipi(35)/Stil(31) belirsiz/kasıtlı kaldırılmış, atlanıyor.
  - Önce P-00021204 üzerinde test edildi (kullanıcı onayı ile), doğrulandı, sonra tüm 117.569 ürüne uygulandı: **166.786 yeni product_attributes satırı**, cinsiyet 0→115.701 ürün, kumaş türü 84.200, yaş grubu 15.566, boy 11.237 ürün.
  - Detaylar: `project_phase13_product_attribute_values_2026-07-02.md` (auto-memory)

- **2026-07-04 — Kanal-özel ürün verisi Catalog'dan Storefront'a taşındı (mimari düzeltme):**
  - Kullanıcı kararı: platform bazlı ürün bilgisi (fiyat override, ileride URL/SEO) `Catalog` şemasında değil `Storefront` şemasında olmalı — tamamen satış kanalına özel veri, bir firmanın çok sayıda kanalı olabilir. Analiz + plan: `docs/urun-url-kanal-mimarisi.md`.
  - `Catalog.FirmPlatformProduct`/`FirmPlatformVariant` silindi, `catalog.firm_platform_products`/`firm_platform_variants` tabloları migration ile drop edildi (0 satırdı, veri kaybı yok).
  - `Storefront.ChannelProduct`'a `NameI18n`/`ShortDescriptionI18n` eklendi; yeni `Storefront.ChannelVariant` (`storefront.channel_variants`) eski fiyat alanlarını taşıyor. Migration'lar canlıya uygulandı.
  - Fiyatlandırma command/query'leri Storefront.Application'a taşındı (`SetChannelVariantPriceCommand`, `GetChannelVariantPricingQuery`), API rotaları `CatalogController`'dan `NavigationController`'a (`/api/navigation/channel-variants/...`), admin `ProductDetailPage.tsx` güncellendi.
  - **Yeni port: `IChannelPricingService`** (`Shared.Contracts`, `IStockService` ile aynı desen) — Catalog.Application'daki mağaza sorguları (`GetStoreProductsQuery`, `GetStoreProductGroupProductsQuery`, `ProductFilterHelper`) ve Api'deki `GetStoreProductDetailHandler` artık kanal fiyatını bu servis üzerinden okuyor (Catalog, Storefront'a doğrudan referans veremez — döngüsel olur). `StorefrontChannelPricingService` implementasyonu Storefront.Infrastructure'da, DI'a kayıtlı.
  - `VariantPriceHistory` bilinçli olarak Catalog'da bırakıldı (taşınmadı).
  - Tüm çözüm + `tools/MigrationTool` temiz derleniyor.
  - **Sıradaki adım:** `docs/urun-url-kanal-mimarisi.md` §5'teki kalan açık sorular netleşmeden Slug/SEO/robots alanları ve `FirmPlatform.Domain` gerçek kolonu eklenmeyecek.

- **2026-07-04 (devam) — Gerçek Firma/Site oluşturma + plurunler aktarımı (Faz 14):**
  - Kullanıcı talimatı: seed/demo Firma+FirmPlatform silinsin, sadece platform 1/2/41 (tipi `site`) + Mişaroğlu/Eldi Tekstil (2 firma) aktarılsın.
  - `tools/MigrationTool/Program.cs`'e `Phase14_FirmsAndChannelData` eklendi, production'da çalıştırıldı (`dotnet run -- 14`).
  - Seed firmalar (Code≠`misaroglu`/`eldi`) + bağlı `core_firm_platforms`/`core_firm_integrations`/`core_cargo_rules`/`core_firm_notification_settings` silindi; storefront demo verisi (`nav_menus`, `channel_categories`, `channel_product_groups`, eski FirmPlatformId'lere göre) de temizlendi (cross-schema FK olmadığı için elle).
  - 2 gerçek firma (`misaroglu`, `eldi` — vergi bilgisi legacy `dfinvoiceinfo`'dan) + 3 gerçek site (`tozlu`→misaroglu, `julude`→eldi, `mishar`→misaroglu, hepsi `PlatformType=site`) upsert edildi. Domain bilgisi `FirmPlatform.Settings` JSONB'sinde (`{"domain":"tozlu.com"}` vb.) — ayrı bir `Domain` kolonu henüz yok.
  - `plurunler` (platformId 1/2/41) → platform başına **361.907 ChannelVariant + 117.495 ChannelProduct** aktarıldı (Price/CompareAtPrice/IsActive). `yayinda` bilinçli olarak kullanılmadı (veri incelemesinde tutarsız/anlamsız çıktı — platform 1/2'de neredeyse hep 0, sadece 41'de tutarlı; ayrıca int 0/1/2 değerleri var, temiz bir bayrak değil); `satista` tek başına `ChannelVariant.IsActive` oldu.
  - Faz idempotent (Code'a göre firma/platform upsert, ürün verisi unique index üzerinden ON CONFLICT DO UPDATE).
  - Detaylar: `docs/urun-url-kanal-mimarisi.md` (2026-07-04 revizyon notları) + `project_product_url_channel_analysis_2026-07-04.md` (auto-memory).

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
