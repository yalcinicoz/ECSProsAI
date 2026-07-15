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
- [x] CRM üye şifre hashing: SHA256 → BCrypt (D5, 2026-07-10 — ilk girişte re-hash; legacy hex + Base64 tanınır)
- [ ] Elasticsearch entegrasyonu (ürün arama için)

---

## Aktif Session Notları

> Bu bölümü her session başında güncelle, session sonunda temizle.

### ⭐ SESSION KAPANIŞ ÖZETİ (2026-07-15, 3. oturum) — SMS GERÇEK KANAL: GES TELEKOM (RESTART BEKLİYOR)
**K3 (SMS sağlayıcı) kapandı:** GES Telekom (TT Mesaj, restapi.ttmesaj.com). SMTP kalıbı birebir:
`GesTelekomSmsService` (token 23s cache + 401'de yenile → SendSingle; "*OK*" değilse fırlatır;
ayar yoksa log yedeği) + `ISmsSettingsProvider`/`DbSmsSettingsProvider` (aktif sms-tipli
FirmPlatformIntegration, firma geneli öncelikli, 2 dk cache). Servis kataloğu `gestelekom`
kaydı: seed + canlı DB'ye SQL ile basıldı (satır DB'DE HAZIR). Kimlik bilgileri admin firma
detayından girilecek (username/password credentials-şifreli; origin=mesaj başlığı zorunlu).
API sözleşmesi gestelekom.com/tr/api-baslangic-metotlari + /tr/sms-gonderim-metotlari'ndan.
Commit 46414ce; publish alındı → restart SONRASI OTP SMS'leri gerçek kanaldan çıkar
(admin kaydı girilene kadar log yedeğinde kalır, davranış değişmez).
**Canlı test (commit 49ea38e):** kimlik doğru ama 2 hata vardı: (1) apiUrl ".../api" girilince
çift /api → 404 (kod sondaki /api'yi atar + DB kaydı düzeltildi, restart'sız etkili);
(2) SendSingle'da ed/recipentType/brandCode API'de ZORUNLU (dokümanda opsiyonel) — eklendi.
KALAN ENGEL GES TARAFINDA: TokenJson 200 dönerken SendSingle/Otp gövde kimliğini reddediyor
("Kullanici adi/parola yanlis") → GES desteği (444 21 66) gönderim kullanıcısını tanımlamalı.
**Ek (commit eb28120):** `PlatformSchemaField.HelpI18n` (camelCase helpI18n) — firma entegrasyon
formunda alan yanı Info ikonu + tıklanır açıklama balonu (FirmDetailPage FieldHelp). GES Telekom
şemasında isNotification (İYS bilgilendirme/kampanya — OTP'de İŞARETLİ olmalı) + origin (onaylı
başlık) açıklamaları seed + canlı DB'de. helpI18n GET yanıtında RESTART SONRASI görünür
(eski binary PlatformSchemaField'de alanı tanımaz, düşürür); admin/dist şimdiden canlıda.

---

### ⭐ SESSION KAPANIŞ ÖZETİ (2026-07-15, 2. oturum) — VARYANTSIZ ÜRÜN: DEFAULT VARYANT MODELİ (RESTART BEKLİYOR)
**Karar (kullanıcı onaylı):** Satılabilir birim her zaman `ProductVariant`; varyantsız ürün =
özniteliksiz tek "default" varyant (Shopify kalıbı). Kurallar: (1) invariant komut katmanında —
eksensiz grupta CreateProduct default varyantı otomatik açar (SKU=ürün kodu, fiyat=BasePrice);
eksenli grupta 0 varyant = taslak. (2) Özniteliksiz ve öznitelikli varyant aynı anda aktif olamaz —
ilk gerçek kombinasyon eklenince default pasife çekilir (AddProductVariants). (3) Fiyatın sahibi
varyant — UpdateProduct tek özniteliksiz varyanta fiyat/maliyeti senkron yazar (VariantPriceHistory'li).
Admin: eksensiz grupta 0 varyantlı eski ürün için boş durumda "Default Varyant Oluştur" butonu.
Vitrin tarafı değişmedi (TekVaryantId yolu zaten hazırdı). Commit 92fbee5; admin/dist canlıda,
API publish alındı → `sudo systemctl restart ecspros` BEKLİYOR.

---

### ⭐ SESSION KAPANIŞ ÖZETİ (2026-07-15) — SATIŞ GÖRÜNÜRLÜĞÜ M2+M3 TAMAM (RESTART BEKLİYOR)
**Bu oturumda tamamlananlar (kod + migration + değer aktarımı canlı DB'de):** Satış görünürlüğü
modelinin M2 (kanal seçimi) + M3 (kanalda durdurma) katmanları.
1. **Domain+migration:** `ChannelProduct.IsActive` = K2 kanal seçimi (opt-out); yeni
   `SaleStoppedFrom/Until` = K3 durdurma penceresi. Migration `AddChannelProductSaleStop` CANLI DB'DE.
2. **Ortak geçit:** `IChannelProductFlagService.GetChannelExcludedProductIdsAsync` opt-out deny-set;
   tüm storefront yüzeyleri (liste/arama/facet/grup/detay→301) + sepet/ödeme uygular. Cache sürümleri
   artırıldı (channelcat products v5, facets v3, store facets v3).
3. **Panel:** yeni "Kanal Ürünleri" toplu sayfası (storefront/channel-products) — kanal seçici +
   arama/durum + satır toggle + toplu Kanala Al/Çıkar + Satışı Durdur(tarih)/Başlat + tümünü seç.
   Backend: NavigationController manage/ids + bulk-select + bulk-stop. **admin/dist canlıda** (nginx).
4. **Değer aktarımı (MigrationTool Faz 17, ÇALIŞTIRILDI):** legacy `plurunler.satisaAcik` YOK →
   gerçek `satista`(K2)+`yayinda`(K3); kullanıcı onayıyla satista→K2, yayinda→K3, 3 site.
   Sonuç: **Mishar(canlı) 344 durduruldu/0 çıkarıldı**; Tozlu 28.129 durd.; Julude 22.507 çıkar.+28.139 durd.
5. **İzole doğrulama (5053 Production):** çıkar→301, durdurma penceresi→301, pencere geçmiş→otomatik 200,
   geri al→200. Deploy DLL'leri doğrulanan staging ile byte-AYNI.

**DEPLOY + DOĞRULANDI + COMMIT:** kullanıcı restart yaptı (10:20); canlıda Redis AKTİF, kanaldaki
ürün 200, durdurulmuş ürün→301, endpoint 401. 5 commit (geçit+panel+Faz17+PROGRESS+M1-contract);
site.js dokunulmadı/commit dışı.

**DEPRECATED TEMİZLİK (kısmi):** `catalog.products.IsActive` KOLONU DÜŞÜRÜLDÜ (migration
DropProductIsActive, canlı DB'de; M1 contract tamam — kolon yetim+NOT NULL/default'suz insert
riskiydi; redeploy gerekmedi). ⚠️ Diğer 3 "deprecated" kalem (Warehouse.IsSellableOnline,
inv_stocks.LocationId, inv_warehouse_locations) ÖLÜ DEĞİL — hâlâ canlı-bağlı (depo/transfer/
rezervasyon CRUD); emekliye ayırmak Inventory refactoru gerektirir, hızlı temizlik değil.
Detay: `project_sale_visibility_model_2026-07-14.md`.

---

### ⭐ SESSION KAPANIŞ ÖZETİ (2026-07-14) — SONRAKİ OTURUM BURADAN DEVAM
**Bu oturumda tamamlananlar (hepsi canlı DB'de + commit'li):**
1. **Stok/depo üçlü yapı + aktarım:** entity temeli (WarehouseSection/Bin, IsCentral/ErpCode) →
   katalog reload (`dotnet run 20`: 28.651 ürün +102, kanal verisi yeniden kuruldu) → stok
   aktarımı (Faz 16 genişletildi: inv_stocks 165.110 satır/277.879 adet + 1.207 rezerv, BinId'li)
   → **handler cutover** (StockOps: 8 handler + okuma kısım-duyarlı; online = satışa-açık kısımlar).
2. **Satış görünürlüğü M1:** `catalog.products.IsActive`→`IsSaleOpen` (satışa kapalı → liste dışı
   + detay 301 kategoriye/ana sayfaya). Kalan M2/M3 (kanal seçim/durdurma) + değer aktarımı.
3. **Stok görünürlüğü (Sıra 1+1.5+2):** kanal ayarlı "stoğu biteni listede göster" (aç/kapa +
   tarih eşiği; admin ChannelsPage 'Stok Görünürlüğü'); liste/arama/facet/grup filtreli;
   detay beden-gating + 'gelince haber ver' popup. Stok HER ZAMAN aktif (stockControlEnabled emekli).

**⚠️ RESTART BEKLİYOR:** Sıra 2 (commit 0e148ae) publish'te — kullanıcı `sudo systemctl restart
ecspros` yapmadı. Yeni publish'te Sıra 1+1.5+2'nin tamamı var; restart sonrası doğrula.

**SIRADAKİ İŞLER (öncelik sırasız — kullanıcı seçecek):**
- (a) Sıra 2 restart + storefront doğrulama (stoğu biten ürün detayı + popup).
- (b) **erp_variant_data Phase11 FIX:** EnsureNebimFirmIntegration bayat (demo firma yok +
  core_integration_services→definition.integration_services, core_firm_integrations→
  core_firm_platform_integrations taşındı). Reload'da çöktü, erp aktarımı ERTELENDİ; H3/entegrasyon
  işiyle düzeltilecek. erp_variant_data şu an eski GUID'li (327.821, ölü entegrasyon id'li).
- (c) **Satış görünürlüğü M2/M3:** kanal seçim (plurunler.satisaAcik) + kanal anlık/zamanlı
  durdurma (plurunler.yayinda + pencere) + değer aktarımı (apurunler.satisaAcik→IsSaleOpen).
  Bkz. `project_sale_visibility_model_2026-07-14.md`.
- (d) **DEPRECATED TEMİZLİK:** Warehouse.IsSellableOnline, inv_stocks.LocationId,
  inv_warehouse_locations tablosu + eski admin komutları, eski catalog.products.IsActive kolonu.
- (e) **FAZ H kalanı:** H3 görsel arama (API key admin'den `visual_search` servisine — KULLANICI
  BEKLENİYOR), H-M5 (H10 devredenler + H7 QA). Bkz. `project_misharix_razor_phase_status.md`.
- (f) FAZ R (ERP placeholder ekranları).

**BEKLEYEN DIŞ GİRDİLER:** H3 görsel arama API key, H8 SMTP kimlik bilgileri, başka oturumun
`site.js` lazy-load değişikliği (deploy edilmiş, commit bekliyor — ona DOKUNMA).
**YEDEK:** `~/yedekler/reload-oncesi-2026-07-14-1441.dump` (reload öncesi tam DB).
**Detay hafıza:** `project_legacy_stock_model_2026-07-13.md` (stok/depo), `project_sale_visibility_model_2026-07-14.md` (satış+stok görünürlük).

- **2026-07-14 — STOK GÖRÜNÜRLÜĞÜ Sıra 1 TAMAM (commit 2f4fe2e) — RESTART BEKLİYOR:**
  Kullanıcı modeli: firma satış kanalı bazında "stoğu biten ürünleri listede göster" +
  ekleme-tarihi eşiği. Stok artık HER ZAMAN aktif (stockControlEnabled emekli). Liste
  (SSR+API kategori) varsayılan olarak stoğu bitenleri GİZLER; kanal ayarı açıksa VE ürün
  CreatedAt >= eşik ise gösterir. InStockProductProvider (raw-SQL, IMemoryCache 2dk) +
  StoreContext.StokBitenGoster/Tarih + GetChannelCategoryProductsQuery filtresi (cache v3) +
  admin ChannelsPage 'Stok Görünürlüğü' bölümü. Doğrulama: kadin-elbise 3098→1505 (kapalı),
  show-on(2025+) 1754. **STOK GÖRÜNÜRLÜĞÜ TAMAM (Sıra 2, commit 0e148ae):** stoğu biten ürün
  detayla/linkle erişilebilir (301 yok) ama alınamaz; tükenen beden 'Tükendi'; tüm bedenler
  tükendiyse açılışta 'gelince haber ver' popup (beden seçimli), tükenen bedene tıklayınca notify
  popup; üye→POST stock-alerts/misafir→giriş modalı; headless doğrulandı. Sıra 1+1.5+2 bitti. Sıra 1.5 TAMAM
  (commit 8bad0e0): arama (GetStoreProducts, ApplyStockFilter bayrağı — vitrin/bildirim hariç) +
  arama/kategori facet'leri + grup listesi de aynı stok filtresini uygular; controller'lar platform
  ayarını geçirir; izole: arama 7.133 stoklu. Detay:
  `project_sale_visibility_model_2026-07-14.md`.

- **2026-07-14 — STOK AKTARIMI için KATALOG RELOAD hazırlığı TAMAM, YIKICI ADIM BEKLİYOR
  (commit 4a7c3a0):** Kullanıcı kararı: önce katalog reload (102 eksik ürün + GUID tazeleme),
  sonra stok; handler cutover ertelenecek (stok kontrolü kapalı). Reload denetlendi ve YIKICI
  DEĞİL: firma/platform Phase14 upsert korunur (CMS/vitrin/entegrasyon güvende), manuel pin/
  vitrin ürün-ref/product_groups = 0, kategori kuralları için Faz 1-4 koşulmayacak (gruplar
  korunur). Sadece channel_variants(279K)+channel_products(85K) yetim → Phase14 yeniden kurar.
  **Hazırlık:** (1) tam yedek `~/yedekler/reload-oncesi-2026-07-14-1441.dump` (122MB); (2)
  MigrationTool Faz 20 orkestratörü (`dotnet run 20` → 5→6→7→11→12→13→14, ClearAll+Faz1-4 YOK)
  + channel delete-first.
  **KATALOG RELOAD KOŞULDU + DB DOĞRULANDI — RESTART BEKLİYOR:** 28.651 ürün (+102),
  329.047 varyant, 205.912 görsel, 228.857 özellik, 3 platform channel_variants 280.749
  (yetim=0); ürün detayı+ana sayfa origin 200. Faz 1-4 korundu. Yol boyu çözülen: (1) Phase5
  IsSaleOpen doldurmuyordu → reload sonrası tüm ürün kapalı → düzeltildi + UPDATE (26.862 açık);
  (2) Phase11 erp bayat → çöktü → **erp ERTELENDİ** (kritik değil). Görsel 205.912 dedup'lu doğru
  (kullanıcı onayladı). Restart yapıldı, storefront sağlıklı.
  **STOK AKTARIMI TAMAM (Faz 16 genişletildi):** inv_stocks 165.110 satır (BinId'li yeni şekil;
  toplam 277.879 adet, rezerv 1.207) + 1.207 rezervasyon (LegacyReferenceId'li); atlanan 447 adet
  (%0.16). Depo: Merkez 240.363/Mağaza 30.467/Ayakkabı 7.049. Şema additive (SectionId/BinId/
  LegacyReferenceId), handler cutover ERTELENDİ (stok kontrolü kapalı, redeploy gerekmez).
  **HANDLER CUTOVER TAMAM (commit 5b4d0ea) — RESTART BEKLİYOR:** StockOps + tüm handler'lar
  (variant,BinId) yapısına geçti; online satılabilir = satışa-açık kısımlar (doğrulama: 275.170
  = 276.672 − İade/Defo/Bağış 1.502); eski unique index düşürüldü. Stok kontrolü kapalı → canlı etkisi yok.
  **KALAN:** (b) erp_variant_data (Phase11) entegrasyon-şeması düzeltmesi; (c) satış
  görünürlüğü M2/M3 (kanal seçimi/durdurma). Yedek: reload-oncesi-2026-07-14-1441.dump.
  Detay: `project_legacy_stock_model_2026-07-13.md`.

- **2026-07-14 — Satış görünürlüğü M1 TAMAM (commit e713ddd) — RESTART BEKLİYOR:**
  "Ürün listede var ama detay 404" hatası → 3 katmanlı satış modelinin Katman 1'i uygulandı.
  `catalog.products.IsActive`→`IsSaleOpen` (yeni ürün default kapalı); migration
  `AddProductIsSaleOpen` EXPAND-CONTRACT (yeni kolon + backfill; iki kolon canlıda, eski
  IsActive sonra düşürülecek) CANLI DB'DE. Liste/detay/facet/checkout artık IsSaleOpen
  filtreliyor (liste filtresi = asıl bug); 404→301 (kapalı ürün → kategorisine, yoksa ana
  sayfa); admin durum etiketleri "Satışta/Satışa Aç/Kapat". İzole 5053 doğrulandı
  (kapalı→301, açık 200, admin 401). Publish alındı → kullanıcı `sudo systemctl restart
  ecspros`. Bilinen sınır: DTO/kontrat alan adları hâlâ isActive (değer IsSaleOpen).
  **Ek düzeltme (commit 0e4593e):** kullanıcı "ürün hâlâ kategoride listeleniyor" bildirdi —
  ilk liste filtresi ölü koda konmuştu; asıl yol ResolveCategoryProductIds'e taşındı +
  Redis cache v2. TEMİZ publish alındı (--no-restore stale assembly tuzağı — bkz. feedback).
  RESTART BEKLİYOR. Kalan: eski IsActive kolonu temizlik migration'ı (restart sonrası); M2 (kanal seçimi) +
  M3 (kanal durdurma) + değer aktarımı. Detay: `project_sale_visibility_model_2026-07-14.md`.

- **2026-07-14 — Stok/depo üçlü yapı: TEMEL DİLİM TAMAM (kullanıcı FAZ P sonrası bu işi
  seçti, additive/güvenli ilk adım):** Yeni entity'ler `WarehouseSection`
  (`inv_warehouse_sections`: WarehouseId, Code, Name, **IsSellableOnline** yönetim noktası,
  PickingOrder/IsActive/SortOrder) + `WarehouseBin` (`inv_warehouse_bins`: SectionId, Code,
  Barcode, Name?; ParentId/LocationType YOK — sadeleşmiş raf) + `Warehouse`'a **IsCentral**
  + **ErpCode** (mevcut alanlar KORUNDU — IsSellableOnline/WarehouseType/Locations hâlâ
  yerinde). Config + DbContext + IInventoryDbContext DbSet'leri eklendi. Migration
  `20260714111509_AddWarehouseSectionsAndBins` **CANLI DB'YE UYGULANDI** (yalnız 2 yeni
  boş tablo + 2 kolon [ErpCode nullable, IsCentral default false]; 2 depo/100 lokasyona
  eklemeli, çalışan binary'yi etkilemez — yeni tablolar yok sayılır). Doğrulama: psql ile
  tablolar + kolonlar mevcut ✓; tam API build 0 hata.
  - **DOKUNULMADI (bilinçli — cutover işi):** Stock/StockReservation/StockMovement şekli,
    event handler'lar, inv_stocks'un VariantId+BinId'ye inişi, satışa-açıklığın
    Warehouse→Section'a taşınması, eski inv_warehouse_locations'ın emekliye ayrılması.
  - **MigrationTool Faz 16 TAMAM (2026-07-14) — DEPO YAPISI aktarımı (yapı; stok MİKTARI
    değil):** `Phase16_WarehouseStructure` — 3 depo (Merkez IsCentral/D012, Mağaza M002,
    Ayakkabı M004) + dfstorages→kısımlar (onaylı kod eşlemesi; yalnız stoklu+eşlenen) +
    stoklu dfstorageunits→raflar. **Canlı sonuç:** 3 depo, **15 kısım** (İade/Defo/Bağış
    IsSellableOnline=false, katlar+reyonlar=true — doğrulandı), **13.541 raf**. Stok
    dağılımı READ-ONLY raporlandı: 276.696 fiziksel adet, **tamamı eşlenen kısımlarda,
    düşen=0** (Tekkeköy TD + Güngören + boş WEBDEPO bölümleri ŞU AN stoksuz — analizdeki
    "Tekkeköy 13 raf" artık boş; canlı drift). Rezerv ~1.573 (drift; analiz 1.853). Yeni
    boş tablolara + 3 yeni koda yazdı (mevcut demo depolar DEPO-01/merkez korundu).
  - **⚠️ Faz 16 stok MİKTARI + rezerv YAZMADI (bilinçli):** inv_stocks'un VariantId+BinId'ye
    yeniden şekillenmesine (cutover — 8 handler + 249 demo satır etkiler) bağlı. Kod stok
    dağılımını yalnız raporluyor. Ayrıca tam sayısal aktarım katalog reload'a bağlı
    (45 eksik ürün → variantMap). Miktar/rezerv yazımı cutover adımında yapılacak.
  - **SIRADAKİ (kullanıcı girdisi/karar bekler):** (1) admin Depo/Kısım/Birim ekranları —
    **K16 gereği başlamadan ekran kurgusu konuşulacak**; (2) handler cutover: inv_stocks
    reshape (BinId/SectionId + yeni unique index) + 8 handler kısım-duyarlı + Faz 16 stok
    miktarı/rezerv yazımı + satışa-açıklığın Warehouse→Section'a taşınması — deploy penceresi;
    (3) tam katalog reload (45 ürün) + nihai stok aktarımı doğrulaması.
  - Detay/kararlar: hafıza `project_legacy_stock_model_2026-07-13.md` +
    `docs/stok-aktarimi-analizi-2026-07-13.md`.

- **2026-07-13 — Stok/depo aktarım analizi — ÖNERİ ONAYLANDI (2026-07-14), kod değişikliği YOK:**
  Eski DB'de gerçek stok `opproductlocations` (1 satır = 1 fiziksel adet; 278.283 adet,
  rezerv: 754 sipariş + 1.099 özel toplama; `stokAdedi` ve `opmagazadepo` aktarım DIŞI —
  kullanıcı kararı). Üçlü depo yapısı önerildi: `inv_warehouses` (fiziki depo, IsCentral +
  ErpCode) → `inv_warehouse_sections` (YENİ — kat/bölme, **IsSellableOnline burada**) →
  `inv_warehouse_bins` (raf; mevcut locations sadeleşir). Rezervler `LegacyReferenceId` ile
  taşınacak; hareketler süreçsiz tek satır (ad-hoc `duzeltme` dahil). Kullanıcı 45 eksik
  ürün kodunu `yeniurunkodlari`'na ekledi → Faz 5/6/7 delete-reload olduğundan TAM katalog
  reload gerekir (Faz 16 stok testiyle birlikte koşulacak). **Kararlar (2026-07-14):**
  depolar Merkez (IsCentral, D012) / Mağaza (M002) / Ayakkabı (M004); Tekkeköy kullanım
  dışı — oluşturulmayacak (13 dolu rafı aktarım dışı, doğrulama toplamından düşülecek);
  İade/Defo/Bağış merkez kısımları, IsSellableOnline kapalı başlar. Uygulama FAZ P
  kapandığı için planlanabilir; sıralama önerisi: şema+Faz 16 kodu önce (eklemeli),
  handler cutover + tam katalog reload sessiz pencerede. Detay: hafıza
  `project_legacy_stock_model_2026-07-13.md` + `docs/stok-aktarimi-analizi-2026-07-13.md`.

- **2026-07-13 — Kategori listesi lazy-load düzeltmesi (ayrı oturum, hata bildirimi):**
  Infinite-scroll'la eklenen kartların görselleri kaydırınca yüklenmiyordu (yalnız hover'da
  beliriyordu). Kök neden: `site.js gorselHazirla`, `data-ms-lazy-src`'si henüz yazılmamış
  iskelet görseli `msLazyHazir="true"` damgalıyor, `kartDoldur` adresi yazınca ikinci
  `msLazyLoadYenile` bayrağa takılıyordu. Bayrak artık görsel gerçekten kurulunca basılıyor.
  Tasarım kaynağı yazma-korumalı olduğundan `wwwroot/js/site.js` gerekçesiyle
  `allowed-diffs.txt`'e eklendi. E2E kanıt (5051, eski↔yeni): eski sürümde 288 JS kartın
  tümü src'siz; yenide görünür kartlar 12/12 yüklü. Düzeltme 18:24 publish + 18:26
  restart'la (paralel oturumun deploy'u) canlıya çıktı, canlıda doğrulandı. Commit bekliyor.

- **2026-07-13 (devam) — P0 TAMAM (geriye dönük panel taraması, K16):**
  - **8.1–8.9'un her bölümüne "Panel (P0)" notu düşüldü** (plan Bölüm 8). Bilinen boşluk
    listesi doğrulandı: sipariş/iade/fatura → P1, CMS içerik → P2, kampanya/kupon → P3,
    üyeler (+üye grupları placeholder) → P4, iletişim+bildirim izleme → P5. Panel karşılığı
    TAM olanlar: menüler, vitrin (8.8 örnek model), yorum+koleksiyon moderasyonu, katalog
    (ürün/görsel/fiyat/featured/video), servis kataloğu + firma entegrasyonları.
  - **3 yeni bulgu:** (1) `return_reason` Lookup değerleri panelden yönetilemiyor → P1c'ye
    eklendi; (2) bülten aboneleri listesi yok → P5'e eklendi; (3) ⚠️ **Kanallar formu şema
    dışı Settings/Credentials anahtarlarını kayıtta sessizce siliyordu** (backend Settings'i
    replace eder; form yalnız şema alanlarını gönderiyordu — stockControlEnabled/tema/domain
    silinirdi) → `ChannelsPage.tsx` düzeltildi (şema dışı anahtarlar korunur), npm build
    alındı (admin/dist canlıda). B12 stok anahtarının panel yolu: PlatformType şemasına
    boolean alan eklemek yeterli (veri işi, kod gerekmez).
  - **K19 (2026-07-13, kullanıcıyla konuşuldu):** Sipariş ekranı kurgusu — (1) liste
    SEKMELİ, açılış "Aktif"; sayaç yalnız aktif durum sekmelerinde; Teslim/İptal/Tümü
    sayaçsız + son-30-gün varsayılanı (sipariş sayısı milyonlara çıkacak — açılış anlık
    olmalı, kullanıcı şartı); (2) detay TEK SAYFA dikey bölümler; (3) İadeler/Faturalar
    AYRI sayfalar + detayda bölüm.
  - **P1a TAMAM (2026-07-13):** `/orders` OrdersPage (sekmeler+sayaçlar+arama+tarih+
    sayfalama; satır→detay) + backend additive `Statuses/CreatedFrom/CreatedTo` +
    `RecipientName` + `GET /api/orders/status-counts` + `AddOrderListIndexes` migration
    CANLI DB'DE ((Status,CreatedAt)+CreatedAt, IsDeleted=false filtreli). Doğrulama:
    izole 5052 publish routing 401/404 ✓; psql indeksler ✓; EXPLAIN Index Scan Backward
    sort'suz ✓. Kimlikli UI duman testi kullanıcıda (credential kuralı). NOT: 5051'i
    başka bir session'ın test instance'ı tutuyordu — dokunulmadı, 5052 kullanıldı.
  - **P1b TAMAM (2026-07-13):** `/orders/{id}` OrderDetailPage (K19 tek-sayfa):
    duruma göre aksiyonlar (Onayla+depo / İşleme Al / Kargoya Ver+anlaşma+takip+paket /
    Teslim / İptal+neden), kalemler+özet+ödemeler+adresler (bölge adları yeni
    `GET /api/crm/geo-names` — CRM GetGeoNamesQuery)+kargo (anlaşma adı+takip linki+
    olaylar)+notlar+sözleşme kabulleri+türetilmiş durum geçmişi. OrderDetailDto'ya
    additive 16 alan. Doğrulama: izole 5052 routing 401/200 ✓; tsc+build temiz.
  - **✅ DEPLOY (2026-07-13 18:40, paralel oturumun restart'ı):** H1..H9 + platform
    entegrasyonları + P0 düzeltmesi + P1a + P1b CANLIDA — canlı 5000'de status-counts
    401 ✓, geo-names 401 ✓, anasayfa 200 ✓, Redis AKTİF ✓. **P1c backend'i restart
    bekliyor** (publish 18:44'te güncellendi).
  - **P1c TAMAM (2026-07-13):** `/orders/returns` (durum sekmeli, açılış "Talep Edilen")
    + `/orders/returns/{id}` detay (kalemler+neden adları+görseller+muayene+geri
    ödemeler) + aksiyonlar (Onayla/Reddet-PATCH-nedenli/Teslim Al-depolu/Geri Ödeme) +
    **İade Nedenleri modalı** (return_reason lookup CRUD; alt nedenler
    ExtraData.subReasons — Create/UpdateLookupValueCommand'a additive ExtraData).
    Doğrulama: izole 5052 routing 401/200 ✓. Migration yok.
  - **P1d TAMAM (2026-07-13) → P1 + P-M1 KAPANDI:** `/orders/invoices` (durum sekmeli;
    satır → modal: PDF adresi girişi + iptal) + "Fatura Serileri" modalı (yeni
    `GET/POST /api/orders/invoice-series` — tablo boştu, API yoktu) + sipariş
    detayından "Fatura Oluştur" (seri/tip/tarih + alıcı öndolumu) + yeni
    `PATCH /api/orders/invoices/{id}/integrator-url` (https zorunlu; H1 "Faturayı
    Görüntüle" kaynağı artık panelden) + OrderDetailPage'e Faturalar/İadeler bölümleri
    (K19). Doğrulama: izole 5052 routing 401/200 ✓. Migration yok.
  - **K20 (2026-07-13, kullanıcıyla konuşuldu):** P2 CMS kurgusu — (1) tür sekmeli düz
    liste + platform seçici; (2) WYSIWYG (Quill, gömülü) + HTML kaynağı sekmesi;
    (3) platform kopyaları ayrı düzenlenir + "diğer platformlara kopyala" butonu.
  - **P2a TAMAM (2026-07-13):** `/cms/pages` liste (tür sekmeleri + platform seçici +
    SON İÇERİK kolonu — sözleşme sürüm kuralıyla aynı max hesabı) + `/cms/pages/{id}`
    detay çatısı (ad/slug/meta/aktiflik/yayın penceresi PUT ile; bölüm listesi
    read-only). Backend additive: GetPagesQuery.PageType + DTO FirmPlatformId/
    LastContentUpdatedAt; GetPageDetail.Sections (settings+faq items — P2b temeli).
    Doğrulama: izole 5052 routing 401 ✓ + storefront kurumsal/yasal/SSS/anasayfa 200 ✓.
  - **P2b TAMAM (2026-07-13):** sayfa detayında bölüm editörleri — rich_text: Quill
    WYSIWYG (gömülü) + HTML kaynağı sekmesi → yeni `PUT /cms/sections/{id}/content`
    (UpdatedAt → sözleşme sürümü otomatik ilerler); faq: soru/cevap ekle/düzenle/sil →
    yeni `POST /cms/sections/{id}/items` + `PUT/DELETE /cms/section-items/{id}`;
    K20 kopyalama → yeni `POST /cms/pages/{id}/copy-content` (aynı Code şartı, tip+sıra
    eşleme, hedef öğeler soft-delete+klon). ICmsDbContext'e PageSectionItems eklendi.
    Doğrulama: izole 5052 — 5 yeni route 401 ✓ + storefront regresyon 200 ✓.
  - **P2c TAMAM (2026-07-13, yalnız frontend) → P2 KAPANDI:** yasal sayfa detayında
    "⚖️ sürüm takibi" kartı (güncel sözleşme sürümü + açıklama) + sipariş detayı kabul
    kayıtlarında "⚠ metin bu kabulden sonra güncellendi" rozeti (kabul sürümü <
    sayfanın güncel LastContentUpdatedAt'i; kod eşlemesi platform legal sayfalarından).
    Bilinen sınır: sürüm-başına kabul sayacı yok (jsonb sorgusu Application'a Npgsql
    getirirdi — kalıp bozulmadı).
  - **P3 TAMAM (2026-07-13) → P-M2 KAPANDI:** Kampanyalar sayfası (tip seçici +
    CampaignEngine'le birebir tip-özel ayar alanları; UpdateCampaign'e additive
    Settings) + **4 kampanya tipi canlı DB'ye seed edildi** (tablo 0 satırdı — sistem
    veri yokluğundan ölüydü; DatabaseSeeder'a da eklendi) + Kuponlar sayfası (sidebar
    'Kuponlar': sayfalı liste + arama + CRUD + kullanım kayıtları modalı; yeni
    GET/POST/PUT coupons + usages endpoint'leri). Doğrulama: izole 5052 — 6 route
    401 ✓, / ve /sepet 200 ✓. Migration yok. Sınır: kampanya ürün seçimi yalnız
    "tüm ürünler" (specific → Faz G kampanya etiketi işiyle).
  - **P4 TAMAM (2026-07-13):** Üyeler listesi (arama+sekme+sayfalı) + üye detayı
    (doğrulama rozetleri, 8'li özet şeridi — yeni engagement endpoint'i Storefront
    GetMemberEngagementSummary'den, üye grubu ataması+aktiflik, duyuru tercihleri
    salt-okunur, adresler, son 10 sipariş → sipariş detayı linki, oturumlar — yeni
    sessions endpoint'i) + Üye Grupları CRUD (placeholder'dan gerçeğe). Doğrulama:
    izole 5052 — 3 route 401 ✓, /hesabim 302 + / 200 ✓. Migration yok.
  - **P5 hata düzeltmesi (2026-07-14, commit 7f76bba):** kullanıcı canlıda "İletişim
    Mesajları" modal butonu ve "Bildirimler → Şimdi Tara"da `insertBefore: node not a
    child` React çökmesi bildirdi. İzole headless-tarayıcı testiyle (gerçek router+
    layout, simüle edilmiş çeviri-uzantısı DOM mutasyonu) doğrulandı: kök neden P5'e
    özgü değil, paylaşılan `Button.tsx`'in `loading` spinner span'ini koşullu mount
    etmesiydi (herhangi bir `loading` prop'lu buton risk altındaydı, uzantı DOM'u
    değiştirdiğinde React'in yeni kardeş düğümü mevcut düğümün önüne eklemesi
    çöküyordu). Düzeltme: spinner + NotificationsMonitorPage'in scanResult span'i
    artık her zaman DOM'da, yalnız `display` değişiyor. admin/dist build alındı
    (restart gerektirmez, nginx statik dosyayı direkt sunuyor). **Kullanıcı canlıda
    doğruladı (2026-07-14): restart sonrası testler sorunsuz.**
  - **P5 TAMAM (2026-07-14) → P-M3 + FAZ P KAPANDI:** İletişim Mesajları gelen kutusu
    (`/storefront/contact-messages` — Yeni/Okundu/Tümü + platform seçici + arama; mesaj
    modalı, yeni mesaj açılınca otomatik okundu; yeni `GET /api/contact-messages` +
    `PATCH .../{id}/status`) + Bildirimler izleme (`/storefront/notifications` — Stok
    Alarmları ve Kayıtlı Aramalar sekmeleri, gönderim durumları, "Şimdi Tara" → mevcut
    H8 scan endpoint'i) + Bülten Aboneleri (`/storefront/newsletter`). Yeni GET'ler:
    `store-notifications/stock-alerts|saved-searches|newsletter-subscriptions`.
    Sidebar: Müşteriler→İletişim Mesajları; Pazarlama→Bildirimler+Bülten Aboneleri.
    Doğrulama: izole 5052 — 5 yeni route 401 ✓, `/`+`/iletisim`+`/sepet` 200 ✓.
    Migration yok. admin/dist build alındı (canlıda).
  - **RESTART BEKLİYOR (publish güncel, 2026-07-14):** P1c+P1d+P2a+P2b+P3+P4+P5
    backend'leri tek restart'la çıkar. Restart öncesi bilinen durum: yeni admin
    sayfaları API'den 404 alır (boş liste/hata görünümü), storefront etkilenmez.

- **2026-07-13 (devam) — İŞ LİSTESİ GÜNCELLENDİ: Site–Panel senkron kuralı (K16) + FAZ P/R:**
  - **Yeni kural (kullanıcı):** sitede canlı her işlev panelde yönetim karşılığı olmadan
    "bitti" sayılmaz; API/DB müdahalesi yönetim yolu DEĞİLDİR. Plan Bölüm 2 madde 11 +
    K16 + `feedback_site_panel_sync.md` hafızası. Geriye dönük tarama YAPILACAK (P0).
  - **FAZ P (Panel Senkronizasyonu) plana eklendi:** P-M1 = P0 geriye dönük envanter
    taraması + P1 sipariş yönetimi (BÜYÜK — **başlamadan ekran kurgusu kullanıcıyla
    konuşulacak**, kullanıcı talimatı); P-M2 = P2 CMS içerik editörü (BÜYÜK — konuşulacak)
    + P3 kampanya/kupon; P-M3 = P4 üyeler + P5 iletişim mesajları + bildirim izleme.
    Bilinen boşluklar: sipariş/iade/fatura, CMS içerik (yasal metinler SQL'le giriliyor —
    kabul edilemez), kampanya/kupon, üyeler, contact_messages, stok alarm/kayıtlı arama
    izleme. (Yorum + koleksiyon moderasyonu ve vitrin yönetimi panelde ZATEN VAR.)
  - **FAZ R (K17):** saf ERP placeholder ekranları (IAM/POS/fulfillment/finans/hediye
    kartı/entegrasyon logları/teklifler) ayrı faz — P sonrası, başlamadan kapsam
    kullanıcıyla.
  - **Uygulama sırası (Claude'a bırakıldı):** P-M1 → (H3 araya girer, key admin'e
    girilince — K13-ek: key artık config değil DB/visual_search servisi) → P-M2 → P-M3 →
    H-M5 (H7 QA'sına panel-senkron denetimi eklendi) → R → İ. Durum panosu güncellendi
    (H: 🟡 M-1..3 tamam).
  - **K18 (2026-07-13):** iş kapanış raporu kısa — sonuç 2-3 cümle + deploy tek satır +
    🧪 numaralı kullanıcı test talimatı (olumsuz senaryolu) + bilinen sınırlar +
    **kalan iş listesi** (raporun sonunda, toplam sayıyla). P1 (a-d) ve P2 (a-c) alt
    dilimlere bölündü — her dilim ayrı E2E + raporla kapanır, BÜYÜK işlerde başlamadan
    kullanıcıyla ekran kurgusu konuşulur.
  - **Kalan iş sayımı (2026-07-13):** 25 iş — P:11 (P0, P1a-d, P2a-c, P3-P5) + H:3
    (H3/H10/H7) + R:5 + İ:6. Tahmin: ~8-10 aktif çalışma günü / takvimde ~2 hafta.
  - **SIRADAKİ: P-M1/P0** geriye dönük panel taraması; ardından P1 öncesi kullanıcıyla
    sipariş ekranı kurgusu konuşması. (Restart hâlâ bekliyor — H1..H9 + bugünkü 3 iş
    tek restart'la canlıya çıkar.)

- **2026-07-13 — Platform servis entegrasyonları yeniden yapılandırıldı (kullanıcı kararı;
  ayarlar DB'de + Data Protection):**
  - **Tablo:** `core_firm_integrations` → **`core_firm_platform_integrations`**
    (`RestructureFirmPlatformIntegrations` migration'ı CANLI DB'YE UYGULANDI — tablo boştu).
    `ContactName/Phone/Email`, `ContractNumber`, `DocumentUrl` kolonları KALDIRILDI
    (gerekirse SettingsSchema'da tanımlanıp Settings jsonb'sine girilir);
    **nullable `FirmPlatformId` eklendi**: null → firma geneli, dolu → platforma özel;
    çözümlemede platforma özel kayıt firma-geneline TERCİH edilir (H2
    GetPlatformActiveCargoCarrier bu kurala güncellendi). Entity/DbSet adları
    FirmPlatformIntegration/FirmPlatformIntegrations; CargoRule FK'sı da yeniden adlandı.
  - **Şifreleme:** `Credentials` at-rest Data Protection ile şifreli (jsonb→text; EF value
    converter `EncryptedCredentials`). Key ring `~/.ecspros/dp-keys`
    (`DataProtection:KeysPath` config'i; ⚠️ key ring silinirse kimlik bilgileri çözülemez —
    yedeğe dahil edilmeli). Admin GET yanıtları maskeli (`•••`); güncellemede maskeli
    bırakılan alanın saklı değeri korunur (`CredentialsMasking.MergeMasked`).
  - **SMTP artık DB'den:** `ISmtpSettingsProvider` (Api: `DbSmtpSettingsProvider`,
    IMemoryCache 2 dk) — aktif email-tipli kayıt (firma geneli öncelikli) → yoksa
    `Email:Smtp:*` config → ikisi de yoksa log (eski LogEmailService biçimi korundu,
    h8 log denetimleri bozulmaz). Seed: `smtp` (email) + `visual_search` IntegrationService
    satırları SettingsSchema'lı — CANLI DB'DE (5051 test instance'ının seeder'ı ekledi).
  - **Admin:** FirmDetailPage entegrasyon formu — sözleşme-no/yetkili/belge alanları
    kalktı, Platform seçici geldi (Tüm platformlar = firma geneli); liste kolonunda
    PLATFORM; maskeli credential ipucu. npm build alındı (admin/dist güncel).
  - **Doğrulama (izole 5051 publish, canlı DB):** create firma-geneli + platforma-özel +
    yanlış-firma-platformu reddi ✓; GET maskeli + platform adı ✓; DB'de ciphertext
    (`CfDJ8...`) ✓; **decrypt aracıyla maskeli-merge birebir doğrulandı** (maskeli host/user
    korundu, yeni password/from yazıldı) ✓; anasayfa 200 + cargo servisleri 8 ✓; drift
    TEMİZ ✓; TEST kayıtları silindi (artık 0), 5051 kapatıldı (PID cwd doğrulamalı).
    NOT: eski h2/e4 E2E suite'leri repoda değil (oturum-içi scriptlerdi) — birebir
    koşulamadı; kargo çözümlemesi kod + duman testiyle doğrulandı, tam h2 akışı canlı
    doğrulamada izlenecek.
  - **⚠️ DEPLOY BEKLİYOR — publish GÜNCELLENDİ (/opt/ECSProsAI/publish):** H1..H9 + bu iş
    tek restart'la canlıya çıkar. Migration canlı DB'de olduğundan ESKİ binary'nin admin
    firma-sözleşme ekranı restart'a kadar çalışmaz (bilinçli kısa pencere) — restart
    geciktirilmemeli. Doğrulama: admin firma detayı → Entegrasyonlar + `journalctl`.
  - **SIRADAKİ:** H-M4/H3 görsel arama — API key artık admin'den `visual_search` servisine
    girilecek (appsettings yerine DB; K13 bu yönde güncellenmeli). SMTP bilgileri de
    admin'den girilebilir (smtp servisi, firma geneli kayıt).
  - **(devam, aynı gün) Servis Kataloğu CRUD + şema-tabanlı form:**
    - `IntegrationService.SettingsSchema` → `SettingsSchemaJson` (PlatformType kalıbı:
      camelCase JSON, `List<PlatformSchemaField>` — key/labelI18n/type/section/required;
      section=credentials → şifreli, settings → jsonb). Kolon adı DB'de değişmedi
      (migration YOK); canlıdaki smtp/visual_search satır içerikleri SQL ile yeni biçime
      çevrildi, seed de güncellendi.
    - **Yeni endpoint'ler:** `POST/PUT /api/core/integration-services` (Create/Update
      IntegrationServiceCommand; kod unique, edit'te code+serviceType KİLİTLİ — çözümleme
      sorguları ServiceType'a bağlı). GET artık `settingsSchema` da döner (eski/bozuk
      şema JSON'ı null'a düşer — admin kilitlenmez, serbest editör devreye girer).
    - **Admin:** yeni sayfa `/settings/integration-services` (Sidebar 'Servis Kataloğu') —
      liste + create/edit modal + `SchemaEditor` (PlatformTypesPage'den export edilip
      yeniden kullanıldı); FirmDetailPage entegrasyon formu artık seçilen servisin
      şemasından alan üretir (tip bazlı input, credentials/settings otomatik ayrışır,
      şema dışı mevcut anahtarlar serbest editörde korunur). npm build alındı.
    - **Doğrulama (5051):** şema DTO'su ✓, katalog create+duplicate reddi+update ✓,
      şemalı serviste entegrasyon (boolean/number tipli settings korunur: `aktif_mi:true`)
      ✓, maskeli GET + decrypt (`api_key: gizli-abc`) ✓. TEST kayıtları silindi.
    - Publish güncellendi — aynı restart'la canlıya çıkar.
  - **(devam) Servis kataloğu → definition şeması (kullanıcı kararı):**
    - Tablo `core.core_integration_services` → **`definition.integration_services`**
      (`MoveIntegrationServicesToDefinitionSchema` — EF gerçek RenameTable üretti, 11
      satır + FK korunarak CANLI DB'YE UYGULANDI). Definition altın kuralı bu tabloya da
      uygulanır: yalnız geliştirici firma doldurur, aktarımlar kayıt ekleyemez.
    - **Yeni Layer-1 permission `definition.manage`** (Permissions.DefinitionManage;
      AllPermissions'a eklendi → super_admin/platform_admin; firm_admin'de YOK; seeder
      permission satırını + rol atamalarını canlı DB'ye ekledi). POST/PUT
      integration-services `[RequirePermission]` ile korundu; **GET bilinçli açık**
      (firma entegrasyon formunun servis dropdown'ı + şema buradan okunur).
    - **Admin:** Sidebar NavItem'a `permission` alanı eklendi (genel mekanizma) —
      'Servis Kataloğu' yalnız yetkili kullanıcıda görünür; sayfa permission'sız
      kullanıcıya "yalnız platform yönetimine açık" mesajıyla kapalı.
    - **Doğrulama (5051):** permission seed + super_admin ataması ✓; super_admin POST
      201 ✓; rolsüz kullanıcı POST **403** / GET **200** ✓ (test kullanıcısı+kayıtlar
      silindi, artık 0). Publish güncellendi — aynı restart'la çıkar.

- **2026-07-12 (devam) — H5 TAMAM → H-M3 KAPANDI (alt bar + değerlendirmeler + videolar):**
  - Keşif: product_videos + TAM dosya yükleme pipeline'ı (FTP+batch+admin sekmesi) zaten
    kuruluymuş (0 kayıt). K15'le URL yolu eklendi: additive VideoUrl/ThumbnailUrl
    (migration CANLIDA), POST videos/by-url + admin "URL ile Video Ekle" kartı (npm build).
  - Efektif URL: VideoUrl ?? "VideoServer.CdnBaseUrl" ayarı + FileName (ayar yoksa dosya
    kayıtları storefront'ta atlanır). Storefront: detay galerisi video thumb+slayt+rozet+
    modal; kart rozeti SSR + liste client kartları (StoreProductDto.VideoUrl additive).
  - Bilinçli sınır: vitrin infinity client kartlarında rozet yok (SSR vitrin rozetli).
  - **E2E h5 9/9 ✓ + b10/b6/h9 regresyon 59 adım ✓; drift TEMİZ; artık 0.**
  - **⚠️ DEPLOY BEKLİYOR — H1..H9 (H-M1+M2+M3) publish ALINDI (/opt/ECSProsAI/publish):**
    4 migration da canlı DB'de; restart tek adım. admin/dist yeni build'i şimdiden sunuluyor
    (by-url endpoint'i restart'a kadar 404 — restart geciktirilmemeli).
  - **SIRADAKİ: H-M4 / H3 görsel arama — KULLANICIDAN API KEY GEREKLİ** (K13: servis
    çalışıyor; key appsettings.Production'a girilecek). SMTP bilgileri de hâlâ bekleniyor.

- **2026-07-12 (devam) — H9 TAMAM (ürün değerlendirmeleri sayfası — K14 çekirdek port):**
  - Route ürün bazlı: `/urun-degerlendirmeleri/{code}`; girişler detay üst linki + alt
    bilgi bölümü (E7'de statik 0 kalmıştı — gerçek puana bağlandı).
  - SSR: ürün özeti + istatistik + puan dağılımı (yeni GetProductReviewSummaryQuery);
    liste canlı API'den infinite (GetProductReviews'a additive Ratings/Sort/Search +
    summary endpoint'i). Verisiz bloklar gizli (AI özeti/fotoğraflı/konu-beden/sekmeler).
  - ⚠️ İki gerçek bulgu: site.js özel-select çifte bağlanması (sayfa C4 deseniyle kendi
    mekaniğini taşıdı) + alt bilgi E7 eksiği.
  - **E2E h9 18/18 ✓ + e7 17/17 + b6 19/19 ✓; drift TEMİZ; h9-degerlendirmeler.png.**
  - **KARARLAR: K14 (H9 çekirdek) + K15 (H5 video URL tabanlı — kullanıcının video
    sunucusu/dış kaynak URL'i; product_videos'a additive VideoUrl/ThumbnailUrl).**
  - **SIRADAKİ: H5 videolar** (K15'e göre) → H-M3 kapanışı → H-M4 görsel arama.

- **2026-07-12 (devam) — H4 TAMAM (mobil alt bar — H-M3 başladı):**
  - Kaynak dosya katalog önizleme paneliydi — canlıya nav bloğu `_MobilAltBarNav.cshtml`
    (İZİNLİ YENİ) olarak taşındı; CSS hazırdı (fixed bottom z-120, <1024px grid).
  - Rota-duyarlı aktif durum SSR; **detay + sepet akışında render edilmez** (sabit aksiyon
    barları z-90 — çakışma kararı); misafirde Favorilerim/Hesabım giriş modalı; layout'ta
    _AnaNavigasyon'dan ÖNCE include (dinleyici parse sırası).
  - **E2E h4 12/12 ✓ + b6/b4/g9c regresyon ✓** (b4 lokatörü :visible'a kapsamlandı);
    drift TEMİZ; `shots/h4-mobil-alt-bar.png` gözle doğrulandı.
  - **SIRADAKİ: H9** ürün değerlendirmeleri sayfası (595 satır tasarım; backend E7 hazır);
    sonra H5 videolar (⚠️ veri modeli + dosya/embed kararı KULLANICIYA SORULACAK —
    `catalog.product_videos` tablosu zaten var, önce şeması incelenecek).

- **2026-07-12 (devam) — H8 TAMAM → H-M2 KAPANDI (bildirimler):**
  - **SmtpEmailService** (Shared) — `Email:Smtp:Host` config'liyse SMTP, yoksa Log stub
    (⚠️ SMTP kimlik bilgileri KULLANICIDAN BEKLENİYOR — appsettings.Production'a girilince
    gerçek gönderim kendiliğinden açılır, kod değişmez). Gönderim hatası akışları düşürmez.
  - **Stok bildirimi (C9 devri):** yeni `StockIncreasedEvent` — AdjustStock(+)/
    ReturnReceived/PosSaleRefunded yayınlar; `StockAlertNotifier` (Api) active alert →
    e-posta → notified (idempotent; gönderilemeyen active kalır; e-postasız cancelled).
  - **Favori arama bildirimi (E11 devri):** `LastNotifiedAt` migration CANLIDA;
    `SavedSearchNotifier` + `SavedSearchNotifyWorker` (6 saatte bir; günde-1 sınırı) +
    admin tetik POST /api/store-notifications/saved-search-scan; GetStoreProducts'a
    additive `CreatedSince`. `IStoreLinkBuilder` (Store:Hosts tersinden mutlak link).
  - **E2E h8 16/16 ✓ + c9/e11/e8 regresyon 58 adım ✓; drift TEMİZ; artık 0.**
  - H5 notu: `catalog.product_videos` tablosu ZATEN VAR (H8 keşfi) — H5 veri modeli
    kararında önce bu şema incelenmeli.
  - **⚠️ DEPLOY BEKLİYOR — H-M1+H-M2 (H1+H2+H8) publish ALINDI (/opt/ECSProsAI/publish):**
    restart sonrası her şey canlıda; iki migration da canlı DB'ye uygulandı. Bildirimler
    SMTP config'i girilene dek log'a yazar (canlıda gerçek e-posta GİTMEZ — bilinçli).
  - **SIRADAKİ: H-M3** (H4 mobil alt bar + H9 ürün değerlendirmeleri sayfası + H5 videolar).

- **2026-07-12 (devam) — H2 TAMAM → H-M1 KAPANDI (fatura + kargo):**
  - **Kargo firması tanımı = IntegrationService (cargo)** — additive LogoUrl +
    TrackingUrlTemplate (migration canlıda); 8 firmalık katalog seed'i (aras/yurtici/mng/
    ptt/surat/hepsijet/kolaygelsin/ups, idempotent) canlı DB'de. Admin, firma-sözleşme
    ekranından cargo FirmIntegration açınca storefront bağlanır.
  - **Çözümleme:** GetCargoCarriersByFirmIntegrations + GetPlatformActiveCargoCarrier —
    müşteriye SERVİS adı basılır, sözleşme etiketi değil (E2E yakaladı, düzeltildi).
  - **UI:** kargo modalı tasarım üçlüsüne döndü (Kargo Firması/Son İşlem/Alıcı; yetim
    gönderide E4 düzeni korunur) + logo (varsa) + şablondan takip linki; detay modal
    kargo-bilgi'ye firma adı; duyuru barı Kargo Takip (üye→Siparişlerim, misafir→giriş
    modalı) + Yardım & Destek→/sik-sorulan-sorular (F artığı); C10 onay Kargo Bilgisi
    açıldı (firma SSR, Hazırlanıyor, adres client-side).
  - Bilinçli sınırlar: B9 teslimat bilgileri/dönen mesajlar tahmini-teslimat konfigü
    gelene dek gizli; C4 hızlı teslimat gizli.
  - **E2E h2 15/15 ✓ + e4/h1/b6/c5c10 regresyon 66 adım ✓; drift TEMİZ; artık 0.**
  - **⚠️ DEPLOY BEKLİYOR — H-M1 (H1+H2) publish ALINDI (/opt/ECSProsAI/publish):**
    kullanıcı `sudo systemctl restart ecspros` çalıştırınca canlıya çıkar. Migration
    CANLIYA UYGULANDI (AddCargoCarrierFieldsToIntegrationService — additive, eski binary
    etkilenmez); kargo kataloğu seed'i de DB'de. Doğrulama: duyuru barı Kargo Takip/
    Yardım linkleri + journalctl'de hata yok.
  - **SIRADAKİ: H-M2 / H8** (SMTP e-posta + stok/favori arama bildirimleri).

- **2026-07-12 (devam) — H1 TAMAM (fatura PDF modalı — H-M1'in ilk yarısı):**
  - **Backend:** `FaturaPdfProxy` (Api/Services/Store — misharix mantığı + config allowlist
    `Store:InvoiceProxy`, https + /earchive/ şartı, hata yutulmaz) +
    `GetMemberInvoicePdfSourceQuery` (Order.Application — sahiplik fatura→sipariş→MemberId;
    URL istemciye HİÇ inmez). İki giriş: MVC `/hesabim/fatura/{orderId}/{invoiceId}/pdf`
    (iframe/indir — cookie kimlik) + API `/api/store/account/orders/{orderId}/invoices(+/pdf)`
    (mobil, bearer). `InvoiceListDto`'ya additive `HasIntegratorPdf`.
  - **Frontend:** Siparişlerim "Faturayı Görüntüle" yalnız PDF'li faturada; detay modalına
    tasarımdaki Fatura Bilgileri sütunu döndü. **E8 "Dekontu Gör" bilinçli gizli** (iade
    dekontu üreten akış yok — yanıltıcı olurdu).
  - **E2E h1 17/17 ✓ + e4 15/15 + e8 35/35 ✓; drift TEMİZ; test artığı 0.**
    Test altyapısı: e8'in log/media yolları TEST_LOG/TEST_MEDIA env'ine bağlandı
    (⚠️ 5051 instance'ı `Store__MediaRootPath` ile başlatılmalı — bugün canlı media'ya
    sızan 1 test görseli temizlendi).
  - ⚠️ Bilinen sınır: dış servis BAŞARI yolu (gerçek key → PDF) canlıda ilk gerçek fatura
    oluşunca duman testi. Canlıda ord_invoices 0 satır — butonlar veri gelene dek gizli.
  - **SIRADAKİ: H2 kargo takip** (kargo firma tanımı ad/logo/URL şablonu + E4 modal
    zenginleştirme + duyuru barı Kargo Takip linki + C10 onay kargo bölümü).
  - NOT: Deploy H-M1 kapanınca (H2 ile birlikte) yapılacak.

- **2026-07-12 — FAZ H PLANLANDI (kullanıcıyla 4 karar netleşti):**
  - **Kapsam (K10):** H1–H7'ye ek: H8 bildirim gönderimi (C9 stok + E11 favori arama devri),
    H9 ürün değerlendirmeleri sayfası (E7 devri, 595 satır tasarım), H10 Faz G devredenleri
    (koleksiyon public, kaynak filtreleri, son gezilenler/favoriler kaynakları, YanMenu
    statü, GeoLite2).
  - **K11:** H6 ödeme entegrasyonu ERTELENDİ (sağlayıcı seçilmedi, mock sürer).
    **K12:** bildirim kanalı yalnız e-posta (SMTP — IEmailService'in ilk gerçek
    implementasyonu); SMS LogSmsService'te kalır. **K13:** görsel arama servisi çalışıyor,
    API key kullanıcıda — indeks doğrulaması H3 başında.
  - **5 milestone:** H-M1 fatura(H1)+kargo(H2) → H-M2 bildirimler(H8) → H-M3 alt bar(H4)+
    değerlendirmeler(H9)+videolar(H5) → H-M4 görsel arama(H3) → H-M5 devredenler(H10)+QA(H7).
    Detay: `docs/misharix-razor-tasima-plani.md` FAZ H bölümü (durum panosu + karar kaydı
    K10-K13 de güncellendi).
  - Temel hazırlık notları: H1 için `Invoice.IntegratorInvoiceUrl` mevcut (proxy misharix'te
    119 satır; ECSPros'ta URL server-side çözülecek — client'a sızmaz); H2 için `Shipment`
    TrackingNumber/Url/Events mevcut (eksik: kargo firma tanımı ad/logo/URL şablonu);
    H5 veri modeli + video kaynak biçimi H5 başında kullanıcıya sorulacak.
  - **SIRADAKİ: H-M1 / H1** (fatura PDF proxy + modal + E4/E8 butonları).

- **2026-07-12 — G9c TAMAM (mobilde manuel şehir seçimi girişi — Faz G'nin tek 🔶'si kapandı):**
  - **Kullanıcı kararı (2026-07-11):** duyuru barı mobilde tasarım gereği gizli olduğundan
    mobil giriş, mobil menü alt nav'ına eklenen **'Konum Seç' satırı** oldu
    (_AnaNavigasyonMobilMenu — konum ikonu + SSR şehir adı + ok; Üye Ol/Giriş Yap
    satırıyla aynı kalıp). Aynı `data-ms-sehir-cip` özniteliği — _SehirSecim script'i
    zaten querySelectorAll ile tüm çipleri bağlıyor, JS değişikliği GEREKMEDİ.
    `data-ms-mobil-menu-kapat` ile menü kapanır + modal açılır (listener sırası doğru:
    nav script layout'ta modaldan önce → önce menü kapanır, overflow çakışmaz).
  - allowed-diffs.txt B1+G9c olarak güncellendi; _SehirSecim'deki "mobilde giriş yok"
    notu düzeltildi. **Drift TEMİZ ✓**
  - **E2E (izole 5051 publish): g9c YENİ suite 6/6 ✓** (menüde satır, tıkla→menü kapanır
    modal açılır, 81 il, seçim cookie 35 + reload sonrası SSR 'İzmir', masaüstü çipi
    regresyonu) **+ g9b 15/15 + g9a 9/9 + b6 19/19 ✓**. g9b lokatörleri duyuru barına
    kapsamlandı (çip artık 2 yerde — strict mode). Görüntüler:
    `tools/misharix-sync/shots/g9c-*` (2, gözle doğrulandı). Test artığı 0.
  - Kalan (Faz G'den devreden, değişmedi): GeoLite2 mmdb, koleksiyon public sayfası,
    /urun-degerlendirmeleri, stok/etiket kaynak filtreleri, son gezilenler/favoriler
    vitrin kaynakları, YanMenu statü bloğu.
  - **⚠️ DEPLOY BEKLİYOR — publish ALINDI (/opt/ECSProsAI/publish):** kullanıcı
    `sudo systemctl restart ecspros` çalıştırınca G9c canlıya çıkar. Migration YOK.
    Doğrulama: mobil görünümde menü → 'Konum Seç'.

- **2026-07-11 (devam) — G14 TAMAM → FAZ G KAPANDI 🎉 (Vitrin & Kişiselleştirme uçtan uca):**
  - Envanter 8.8 tam işaretli. **Tek 🔶 (kullanıcı kararı bekleyen): mobilde manuel
    şehir seçimi girişi yok** — duyuru barı tasarımda mobilde tamamen gizli; istenirse
    mobil menü paneline çip eklenebilir. GeoLite2 IP halkası mmdb edinilince bağlanır.
  - **Faz G toplu regresyon (taze 5051 publish): 10 G-suite = 141 adım ✓ + çekirdek
    b6/b4/e2/e5/f1 = 79 adım ✓; drift TEMİZ; test artığı 0.** Kural matrisi g10'da,
    rollback tatbikatı g4/g7/g13'te.
  - Görüntüler: `tools/misharix-sync/shots/g14-*` (anasayfa vitrin desktop+mobil +
    şehir modalı — gözle doğrulandı).
  - Test altyapısı: b4 artık kendini temizliyor; admin-mutasyonlu suite'lere audit
    temizliği eklendi (G13'ten beri suite'ler audit üretiyor).
  - **✅ DEPLOY YAPILDI (2026-07-11 22:09):** kullanıcı restart etti; canlı duman
    testi yeşil — Redis AKTİF ✓, anasayfa vitrinden + şehir çipi ("Konum Seç"),
    segment API (cookie 06 → Ankara/ic-anadolu), store pages v4/4 blok, yeni admin
    endpoint'leri auth'lu (401), kurumsal 200. G-M2'nin tamamı canlıda.
  - **SIRADAKİ FAZ → H** (plan: H1 fatura/dekont PDF, H2 kargo entegrasyonu, ...;
    planlamayla başlanmalı). Faz G'den devreden işler: koleksiyon public sayfası,
    /urun-degerlendirmeleri, stok/etiket kaynak filtreleri, son gezilenler/favoriler
    vitrin kaynakları, YanMenu statü bloğu.

- **2026-07-11 (devam) — G13 TAMAM (audit/değişiklik geçmişi) → G14 QA kaldı:**
  - **VitrinAuditLogger (Api):** IAM'ın `iam_audit_logs` tablosuna yazar (İLK yazan
    vitrin oldu; şema spec'le birebir). ActionType: Created/Updated/Deleted/Activated/
    Deactivated/Published/Rollback/Previewed; EntityType: BannerBlock..AnnouncementBlock
    + Slide/StoryItem/TabItem/BlockItem + Rule + PublishedSnapshot + PagePlacement.
    Kural değişimi ayrıca Rule kaydı düşürür (eski/yeni). Audit hatası admin işlemini
    düşürmez. PagesController tüm mutasyonlar + publish/rollback/preview'da loglar.
  - **GET /api/pages/audit-logs** (platform süzgeci Context jsonb'sinden bellek tarafında)
    + React "Değişiklik Geçmişi" paneli (rozet + başlık + kullanıcı + zaman); npm build.
  - **E2E g13 11/11 ✓ + G4/G6/G10/G12/G8 regresyon ✓; drift TEMİZ; audit artığı 0**
    (admin-mutasyonlu g-suite'lerinin temizliğine audit silme eklendi — artık kendileri
    audit üretiyor).
  - **SIRADAKİ: G14** QA (envanter 8.8 + faz kapanış regresyonu) → Faz G kapanışı.
    Sonrasında DEPLOY (G9a'dan beri biriken G-M2 + bugünkü G9b-G13).

- **2026-07-11 (devam) — G12 TAMAM (admin önizleme):**
  - **PagePreviewService (Api):** TASLAK bloklar + kurgu segment üzerinde kural motoru —
    composer'ın karar sırasıyla ama gizleneni nedeniyle listeler (pasif / tarih penceresi /
    blok kuralı / öğe kalmadı / ürün-koleksiyon kaynağı boş; görünürde "N öğe eşleşti /
    N ürün"). Cache'e yazmaz, canlıyı etkilemez (spec).
  - **POST /api/pages/preview** ([Authorize]) — kurgu segment `BuildAsync` (plaka→il
    adı+bölge); yanıtta segment yankısı + blok listesi.
  - **React:** PagesManagementPage'e "Önizleme" modalı (81 il + cinsiyet/cihaz/üyelik +
    üye grubu formu → Görünür/Gizli rozetli, nedenli sonuç listesi); npm build alındı.
  - **E2E g12 15/15 ✓ + G4/G6/G10/G9b regresyon ✓; drift TEMİZ; test artığı 0.**
  - **SIRADAKİ: G13** audit/yayın logu ekranları (spec ActionType/EntityType; yayın
    geçmişi ekranı G6'da var — değişiklik geçmişi/audit tarafı kaldı) → G14 QA 8.8.

- **2026-07-11 (devam) — G10+G11 TAMAM (kural motoru + segmentli cache):**
  - **PageRuleEvaluator (Api):** alan içi OR, alanlar arası AND, boş alan atlanır,
    kural yoksa herkese, uymayana blok/öğe basılmaz + default aranmaz (spec birebir);
    bilinmeyen segment boyutu kısıtlı kuralla eşleşmez; bozuk JSON gizler (ama yayına
    giremez). PageComposer'ın iki metodu da `VisitorSegment` alır (zorunlu parametre);
    infinity devamı gizli bloğa 404. Çağıranlar: store pages API (bearer'lı segment,
    `MemberIdFromClaims` ortak yardımcı), HomeController + duyuru şeridi (MsSegment).
  - **Kural şeması doğrulaması** `PageBlockCatalog.ValidateRule` (Domain tek kaynak):
    SavePageBlock/Items 400 + Yayınla reddi (iki katmanlı).
  - **G11 birlikte kapandı** (kural filtresi segmentsiz anahtarla yanlış cache üretirdi):
    anahtarlara `:seg:{CacheHash()}` eklendi (SHA256 ilk 16 hex); ürün devam cache'i
    kural denetiminden sonra yazılır — gizli bloğun ürünleri o segmente cache'lenmez.
  - **E2E g10 17/17 ✓ + G4/G5/G6/G7/G8/G9a/G9b/B6 (117 adım) regresyon ✓; drift TEMİZ;
    test artığı 0.**
  - **SIRADAKİ: G12** admin önizleme (kurgu segmentle kompozisyon — ComposeAsync zaten
    segment alıyor, endpoint + React ekranı kaldı) → G13 audit/yayın logu ekranları →
    G14 QA 8.8.

- **2026-07-11 (devam) — G9b TAMAM → G9 KAPANDI (segment tespiti uçtan uca):**
  - **Şehir çipi:** duyuru barı sağ link alanında (SSR etiket — StorePageController
    artık her sayfada `ViewData["MsSegment"]` çözer; G10 kural motoru aynı segmenti
    kullanacak). Segment API'ye additive `cityName`.
  - **_SehirSecim.cshtml (izinli yeni, ms-ornek-modal kalıbı):** 81 il modalı
    (plaka+ad gömülü, arama filtresi, mevcut seçim işaretli), seçim → `ms_sehir`
    cookie (1 yıl) + reload; Seçimi Temizle; **Konumumu Kullan** — izin YALNIZ
    butonla, 81 il merkezi koordinatı gömülü, en-yakın-nokta LOKAL hesap (dış
    servis yok) → cookie + reload.
  - ⚠️ **Bilinçli sınır:** duyuru barı tasarımda mobilde TAMAMEN gizli → çip yalnız
    masaüstünde. Mobil manuel seçim girişi (ör. mobil menü paneli) ayrı karar —
    G14'te değerlendirilecek; adres/profil halkaları mobilde de çalışır.
  - **E2E 15/15 ✓ + G9a 9/9 + G8 10/10 + G5 20/20 + E2 19/19 + B4 12/12 regresyon ✓;
    drift TEMİZ; test artığı 0** (b4 kalıntı üyeleri de temizlendi).
  - **SIRADAKİ: G10** kural motoru (RuleJson şeması: alan içi OR, alanlar arası AND,
    boş alan değerlendirilmez; PageComposer'a segment parametresi) → G11 segment
    cache → G12 önizleme → G13 audit ekranları → G14 QA 8.8.

- **2026-07-11 (devam) — DEPLOY YAPILDI + G-M2 BAŞLADI (G9a TAMAM):**
  - **DEPLOY:** publish'i Claude aldı, kullanıcı 16:33'te restart etti; canlı duman
    testi yeşil (Redis AKTİF, anasayfa vitrinden 4 kapsül + carousel'ler + duyuru,
    store pages API v1, kurumsal/sepet 200). B11'den beri biriken her şey canlıda.
    Deploy akışı netleşti: publish Claude'un işi, yalnız `sudo systemctl restart
    ecspros` kullanıcıda.
  - **G9a Segment backend:** `VisitorSegmentResolver` + `GET /api/store/segment` —
    konum zinciri (varsayılan adres → profil → ms_sehir cookie → GeoLite2 yuvası →
    unknown), cihaz UA sınıfı, cinsiyet yalnız profilden, üyelik/grup. Kural
    kimlikleri: plaka kodu + kebab bölge. crm_cities.Region zaten doluymuş (C4).
    MemberInfo additive genişledi. **E2E 9/9 ✓ + G5/E2/B4 regresyon ✓; drift TEMİZ.**
  - **SIRADAKİ: G9b** şehir çipi UI (duyuru barına çip + 81 il modalı + Konumumu
    kullan) → **G10** kural motoru (composer'a segment) → G11 segment cache →
    G12 önizleme → G13 audit ekranları → G14 QA.

- **2026-07-11 (devam) — G6+G8 TAMAM → G-M1 KAPANDI 🎉 (vitrin sistemi uçtan uca canlı):**
  - **G6 Admin:** blok/öğe CRUD backend (SavePageBlock upsert — katalog doğrulamalı,
    DeletePageBlock yayına dokunmaz, Reorder, SavePageBlockItems replace [SaveNavNodes
    deseni; banner öğesine kural 400], GetPageBlocks/Detail; /api/pages/catalog +
    blocks CRUD endpoint'leri). **E2E 19/19** — bug: jsonb kolonda LIKE (42883) →
    hasProductSource bellek tarafına. React: PagesManagementPage (platform + 8 yerleşim
    sekmesi + sıralı blok tablosu ↑↓ + Yeni Blok modalı + Yayınla/hata + versiyonlar/
    rollback + yayın geçmişi) + PageBlockDetailPage (form + config JSON editörü [örnek
    iskelet] + öğe editör modalı); sidebar 'Vitrin Yönetimi'; npm build.
  - **G8 Geçici kodlar kalktı:** SeedDefaultVitrinAsync — blok+yayın olmayan platforma
    B6 kompozisyonunun birebir karşılığını kurup v1 yayınlar (duyuru + kapsül +
    3 kategori carousel'i; 3 platformda yayınlandı — canlı görünüm değişmez).
    HomeController/Index B6 yolu + AnaSayfaVm silindi; yayın yoksa yerleşim boş.
    Duyuru şeridi announcement bloklarından (StorePageController + _AnaNavigasyonDuyuru;
    statik yedek F4 deseni). Seed düzeltmesi: kök kategori Status='published'.
    **E2E 10/10 ✓ + g4/g7/g6/g5 (64) + b6/b4/e5/f1 (60) regresyon ✓; drift TEMİZ.**
  - **Test altyapısı G8'e uyarlandı:** suite'ler seed bloklarını koşu boyunca
    pasifleştirip sonda geri açar + platform yayınını taslaklardan geri üretir.
  - **G-M1 TAMAM (G1-G8).** SIRADAKİ: **G-M2** (G9 segment tespiti → G10 kural motoru →
    G11 segment cache → G12 admin önizleme → G13 audit/yayın logu ekranları → G14 QA 8.8).

- **2026-07-11 (devam) — G5+G7 TAMAM (vitrin render + versiyonlu cache):**
  - **G5 Razor render:** `Views/Shared/Store/_VitrinBloklar.cshtml` (izinli yeni) —
    11 blok tipi GorunumTipleri markup'ıyla birebir: slider/story (frame JSON +
    modal iskeleti)/banner grid+reklam kompoziti/carousel 3 şablon (tema config'ten;
    flash geri sayımı config.endsAt'ten canlı script)/infinity (Daha Fazla → G4 devam
    endpoint'i)/tabs/collection (maskeli üye adı + kapak kolajı — YENİ CrmMemberService,
    IMemberService'in İLK implementasyonu)/categories kapsül+vitrin/brands/instagram/
    announcement (span'lar, G8'de duyuru barına). `VitrinVmBuilder` + HomeController:
    yayın varsa vitrin, yoksa B6 geçici kompozisyon (G8'e köprü). **E2E 20/20 ✓ +
    B6/B4/E5/F1 regresyon (60 adım) ✓.**
  - **G7 Versiyonlu cache:** PageComposer ICacheService'le sarıldı — anahtar
    `page:{yerlesim}:{platform}:v{n}:{snapshotId}` + infinity `page-products:...`;
    yeni yayın eski cache'i kendiliğinden geçersizleştirir; TTL 5 dk. **E2E 6/6 ✓
    (cache hit kanıtı: DB snapshot'ı bozuldu, GET eskiyi döndü; v2 anında görünür;
    rollback dönüşü) + G4 19/19 + G5 20/20 + B6 19/19 ✓.** E2E'nin yakaladığı ders:
    versiyon numarası tek başına anahtar olamaz (silinip yeniden üretilirse çakışır)
    → SnapshotId anahtara eklendi.
  - **SIRADAKİ: G6** Admin UI (blok/öğe CRUD + sıralama + Yayınla/rollback ekranı —
    publish/rollback/snapshots/publish-logs endpoint'leri G4'te hazır; blok/öğe CRUD
    endpoint'leri eksik) → **G8** B6 geçici anasayfa + B3 statik duyuru kaldırma
    (admin vitrini yönetebilir olunca). Sonra G-M2 (kural motoru + segment).

- **2026-07-11 (devam) — FAZ G BAŞLADI; G1+G2+G3+G4 TAMAM (G-M1 backend çekirdeği):**
  - **G1 Veri modeli:** `storefront.page_blocks` + `page_block_items` + `published_snapshots`
    + `publish_logs` (AddPageBlocksAndSnapshots canlıda). Taslak/yayın ayrımı baştan:
    canlı site taslak tabloları OKUMAZ, yalnız aktif snapshot okur.
  - **G2 Katalog:** `PageBlockCatalog` (Domain) — 11 blok tipi + kural seviyeleri
    (spec'e birebir) + şablonlar (banner 6, carousel 3 + 16 tema) + 8 yerleşim +
    doğrulama yardımcıları. Palet başka yerde tekrarlanmaz.
  - **G3 Kaynak motoru:** `PageBlockSourceResolver` (Api) — 6 ürün kaynağı
    (new-arrivals/manual/brand/category/campaign/best-sellers) + koleksiyon kaynağı
    (yalnız approved+IsPublic). Yeni sorgular: GetTopSellingVariants (Order),
    GetActiveCampaignProductRefs (Promotion), GetShowcaseCollections (Storefront);
    GetStoreProducts'a additive ProductIds. ConfigJson sözleşmesi: productSource/
    collectionSource düğümleri.
  - **G4 Yayın çekirdeği + store API:** PublishPageSnapshot (katalog validasyonu;
    hata varsa yayın YOK + failed log) / RollbackPageSnapshot / GetActivePageSnapshot;
    `IPageComposer` (store API + G5 Razor ortak; RuleJson müşteriye sızmaz);
    `GET /api/store/pages/{placement}` + blocks/{id}/products devamı; admin
    /api/pages/publish|rollback|snapshots|publish-logs. **E2E 19/19 ✓ + B6/E5/F1
    regresyon ✓; drift TEMİZ.** İlk koşu bug yakaladı: manuel kaynakta page yok
    sayılıyordu → kod listesi üzerinde sayfalama.
  - **SIRADAKİ: G5** Razor render (yerleşim → blok → GorunumTipleri partial'ları
    birebir HTML; boş blok basılmaz; flash geri sayımı config'ten) → sonra G6 admin UI,
    G7 versiyon cache, G8 B6 geçici anasayfanın kaldırılması. M1'de kurallar
    değerlendirilmiyor (herkese görünür — plan kararı).

- **2026-07-11 — F5 TAMAM → FAZ F KAPANDI 🎉:**
  - Envanter 8.7 tam işaretli (tek 🔶: banka logoları/sosyal linkler statik '#' —
    gerçek URL'ler config/Faz G, firma hesapları verilince).
  - 9 görüntü: 7 kurumsal sayfa + footer desktop + footer mobil (akordiyon açık) →
    `tools/misharix-sync/shots/f5-*.png`; Hakkımızda CMS içerik + yan menü aktifliği
    gözle doğrulandı.
  - **Faz F toplu regresyon (taze 5051 publish): F1 16 + F2 6 + F3 8 + F4 6 = 36 adım ✓.**
    f4 orkestrasyonu: footer test menüsü SQL ile instance'tan ÖNCE (5 dk cache) →
    menülü instance 6/6 → menü silindi → menüsüz taze instance'ta statik yedek
    kolonları (Kurumsal|Hesabım) doğrulandı. Drift TEMİZ; test artığı 0
    (newsletter/contact/nav_menus boş).
  - **FAZ F TAMAM (F1–F5). SIRADAKİ FAZ → G (Vitrin & Kişiselleştirme — G-M1 blok
    sistemi; spec: anasayfa-dizayn-yönetimi.txt; büyük mimari, planlamayla başlanmalı).**
    DEPLOY BEKLİYOR (B11'den beri; artık Faz E+F de dahil) — kullanıcı
    `sudo systemctl restart ecspros`.

- **2026-07-10 (devam) — F4 TAMAM: Footer canlı:**
  - **Kolonlar admin footer menüsünden:** StorePageController tabanı "footer" kodlu nav
    menüsünü yükler (5 dk cache); kök label = kolon başlığı, çocuklar linkler; menü
    tanımsızsa tasarımın statik kolonları yedek (bugünkü durum — nav_menus boş; admin
    Menus editöründen tanımlayınca devreye girer; testte menülü görünüm doğrulandı).
  - **Bülten:** storefront.newsletter_subscriptions (AddNewsletterSubscriptions canlıda;
    unique platform+email) + SubscribeNewsletter (idempotent, normalize) + POST
    /api/store/newsletter (anonim); footer formu bağlandı. Gönderim entegrasyonu ileri iş.
  - Mobil uygulama/sosyal linkler statik '#' (config/Faz G — firma hesapları verilince).
  - **E2E 6/6 ✓ + statik yedek taze instance'la ✓ + B6/F1/F3/E1 regresyon ✓; drift
    TEMİZ.** Test dersi: type=email inputta native validation submit'i engeller —
    sunucu doğrulama testi 'a@b' gibi native-geçer değerle yapılır. Sıradaki: F5 QA
    (envanter 8.7) → Faz F kapanışı.

- **2026-07-10 (devam) — F3 TAMAM: İletişim formu (kullanıcı kararı: mesaj kaydı):**
  - `storefront.contact_messages` (AddContactMessages canlıda) + CreateContactMessage
    (doğrulamalar + aynı e-postadan saatte 5 throttle) + POST /api/store/contact
    (anonim; bearer varsa MemberId kaydedilir). Admin mesaj listesi ileri iş.
  - Tasarımın İletişim sayfasında form YOKTU — ms-form kalıplarıyla eklendi (izinli
    fark); harita + 8 bilgi kartı birebir.
  - **E2E 8/8 ✓ + F1 16/16 + F2 6/6 ✓; drift TEMİZ.** Sıradaki: F4 Footer
    (menus footer kodundan + bülten aboneliği newsletter_subscriptions) + F5 QA.

- **2026-07-10 (devam) — F2 TAMAM: SSS akordiyonu CMS'ten:**
  - Yeni `faq` section type (SupportsItems) + `kurumsal-sss` sayfası; soru/cevap
    item'ları PageSectionItem.TitleI18n/DescriptionI18n (9 soru seed, 3 platform,
    canlı DB'de). GetStoreFaqQuery + partial @foreach (ilk soru açık); CMS boşsa demo
    yedek; 5 dk cache. Tek-açık davranışı site.js'te (dokunulmadı).
  - **E2E 6/6 ✓ + F1 16/16 ✓** (f1 seed sayımı 6'ya güncellendi); drift TEMİZ.
    Sıradaki: F3 İletişim (⚠️ kullanıcı kararı: form contact_messages kaydına mı
    e-postaya mı bağlanacak).

- **2026-07-10 (devam) — FAZ F BAŞLADI; F1 TAMAM: Kurumsal çerçeve + CMS içerikleri:**
  - `KurumsalController` — misharix'in 7 kök route'u birebir; Sayfa.cshtml + 7 partial
    bayt-birebir (yan menü aktiflik + mobil menü kaynak script'i). Literal route'lar
    /{slug} kategori route'unu ezmiyor (B6 regresyonla doğrulandı). NOT: lazy panel
    (data-ms-lazy-panel-url) yalnız kaynak demo galerisindeymiş — gerçek sayfalar
    route-bazlı SSR.
  - **İçerik CMS'ten:** GetStoreLegalPages'e PageType parametresi (additive); 5 sayfa
    PageType='corporate', kod 'kurumsal-*' (legal kodlarıyla çakışmaz); partial section
    kökünü koruyup iç HTML'i basar, CMS boşsa tasarım demo yedeği (D3 deseni); 5 dk
    IMemoryCache. Seed idempotent → canlı DB'ye 15 sayfa (3 platform × 5). SSS F2'ye,
    İletişim F3'e kabuk kaldı. Footer kurumsal linkleri çalışır oldu.
  - **E2E 16/16 ✓ + B6 19/19 + E1 13/13 + C8 14/14 ✓; drift TEMİZ.**
    Sıradaki: F2 SSS akordiyonu (CMS soru/cevap yapısıyla), sonra F3 İletişim
    (⚠️ kullanıcı kararı: form mesaj kaydına mı e-postaya mı bağlanacak).

- **2026-07-10 (devam) — E14 TAMAM → FAZ E KAPANDI 🎉:**
  - Envanter 8.6 satır satır işaretlendi (ertelenenler hedef fazlı: fatura/dekont PDF
    H1, kargo firması adı/logosu H2, YanMenu statü bloğu + koleksiyon public sayfası
    Faz G, /urun-degerlendirmeleri sayfası ileri iş).
  - 12 Hesabım sayfası görüntülendi: `tools/misharix-sync/shots/e14-*.png`.
  - **Faz E toplu regresyon (taze publish): E1-E13 = 186 adım ✓; drift TEMİZ.**
    QA'da düzelen test kırılganlıkları: e12 ikinci ürün seçimi GetStoreProducts
    görünürlük şartını (aktif + ürün görseli) hesaba katmıyordu; yetim viewed_products
    assertion'ı koşu sırasına duyarlıydı (hard-delete edilen test üyeleri kayıt
    bırakıyor) → önce/sonra farkına çevrildi, yetimler temizlendi.
  - **FAZ E TAMAM (E1–E14).** SIRADAKİ FAZ → **F (Kurumsal/statik sayfalar: F1-F5)**;
    F3 iletişim formu kararı (mesaj kaydı mı e-posta mı) kullanıcıya sorulacak.
    DEPLOY BEKLİYOR (B11'den beri; artık Faz E de dahil).

- **2026-07-10 (devam) — E13 TAMAM: Hesabım ana sayfası:**
  - **Yeni backend gerekmedi** — 6 özet kartı (tasarımda yoktu, plan şartı; E4 grid
    kalıbı): sipariş/iade/kupon/favori sayıları + Hediye Çeki Bakiyesi (wallet) +
    Kullanılabilir Puan (loyalty); cüzdan/puan kaydı olmayan üyede 0.
  - **Kaynak kusuru onarıldı:** E1'deki bozuk `<img src=\` kısayol grid'i sade ikon
    setiyle değiştirildi (7 kısayol, hepsi /ikons/'ta mevcut).
  - **YanMenu statü bloğu kararı:** statü/harcama eşikleri tanımsız — Faz G
    segmentasyonuyla açılacak (yorum güncellendi, @if(false) kaldı).
  - **E2E 8/8 ✓ + E1 13/13 ✓; drift TEMİZ.** Sıradaki: E14 QA (envanter 8.6 +
    12 sayfa görüntü karşılaştırması) → FAZ E KAPANIŞI.

- **2026-07-10 (devam) — E12 TAMAM: Önceden Gezdiklerim:**
  - **Backend (YENİ):** `storefront.viewed_products` (AddViewedProducts canlıda) —
    ürün başına TEK kayıt (tekrar gezmede ViewedAt güncellenir), üye başına son 50
    (kayıt anında budanır). RecordProductView/ClearViewedProducts +
    GetMemberViewedProducts (Faz G "son gezilenler" bloğu da kullanacak);
    GET/DELETE /api/store/viewed-products.
  - **Kayıt noktası:** ürün detayı render'ında sunucuda (MsUye varsa; hata sayfayı
    düşürmez). **Misafir fallback:** detay config script'i — token yokken
    localStorage.ms_gezilenler {kod,t} (dedupe + 50; üyeyken yazılmaz).
  - **Sayfa SSR:** gezme kayıtları × canlı katalog kartları (silinen/pasif gizli,
    güncel fiyat); zaman "Bugün/Dün HH:mm" TR saatiyle; Listeyi Temizle DELETE.
  - **E2E 10/10 ✓ + B6 19/19 + B5 10/10 + E1 13/13 + C1 12/12 ✓; drift TEMİZ.**
    Sıradaki: E13 Hesabım ana sayfası (_HesabimVarsayilan özet kartları + E1'deki
    bozuk ikon grid'i), sonra E14 QA → Faz E kapanışı.

- **2026-07-10 (devam) — E11 TAMAM: Favori Aramalarım:**
  - **Backend (YENİ):** `storefront.saved_searches` (AddSavedSearches canlıda) —
    Name/Query/Filters jsonb (UI şimdilik yalnız metin; filtre entegrasyonu ileri iş)/
    NotifyEnabled; unique (platform, member, Query). Create (mükerrer engeli + soft
    geri açma) / Update (sahiplik) / Delete (soft, idempotent) / GetMemberSavedSearches;
    GET/POST/PUT/DELETE /api/store/saved-searches (MemberOnly).
  - **Sayfa SSR:** kartlar (Bildirim Açık/Aktif rozeti); kaydet/düzenle modalı eklendi
    (tasarımda yoktu — E7 yorum modalı deseni); Sil eklendi (plan şartı); "Sonuçları
    Gör" canlı arama rotasına (/urunler?search=). Bildirim gönderimi Faz H (rozet tercih).
  - NOT: "popüler aramalar" agregasyonu ertelendi (kayıt hacmi gerekir; B2 kaynağı kaldı).
  - **E2E 12/12 ✓ + E1 13/13 + B6 19/19 ✓; drift TEMİZ.**
    Sıradaki: E12 Önceden Gezdiklerim (YENİ backend: viewed_products + detay kaydı +
    misafir localStorage fallback).

- **2026-07-10 (devam) — E10 TAMAM: Tekrar Satın Al:**
  - **Yeni backend gerekmedi** — teslim edilmiş siparişlerin kalemleri → varyant başına
    bir kart (distinct, en son alışveriş öne, 24 sınır); ad/görsel GetVariantDisplayAsync,
    **fiyat GÜNCEL satış fiyatı** (GetStoreProductDetail → PlatformPrice ?? BasePrice —
    ürün detayıyla aynı kaynak; snapshot fiyat değil); silinen/pasif/fiyatsız varyant
    listelenmez. Sepete Ekle ürün detayının cart/items deseniyle (ecspros_cart + mini
    sepet tazeleme + "Eklendi ✓"); tasarımda olmayan boş durum eklendi.
  - **Siparişlerim köprüsü:** teslim kartlarındaki "Tekrar Satın Al" açıldı →
    /Hesabim/TekrarSatinAl linki.
  - **E2E 10/10 ✓ + E4 15/15 + C1 12/12 + E1 13/13 ✓; drift TEMİZ.**
    Sıradaki: E11 Favori Aramalarım (YENİ backend: saved_searches + kaydet/sil/çalıştır).

- **2026-07-10 (devam) — E9 TAMAM: İndirim Kuponlarım:**
  - **Backend:** `GetMemberCouponsQuery` — yalnız üyeye (Coupon.MemberId) veya üyenin
    grubuna (MemberGroupId) tanımlı kuponlar (genel pazarlama kodları sızdırılmaz);
    aktiflik/tarih/limit koşulları ValidateCoupon'la aynı; grup kimliği API katmanında
    CRM'den. `GET /api/store/account/coupons`. Şema değişikliği YOK (alanlar zaten vardı).
  - **Sayfa SSR:** kod + indirim + koşul metinleri; "Sepette Kullan" C3'ün sessionStorage
    kupon sözleşmesine ({kod}) yazıp sepete gider — sepet açılışta sessizce doğrulayıp
    uygular (C3'te hazırdı). "Kupon Kodu Ekle" demo'su gizli (claim modeli yok).
  - **C3 kupon modalı canlı:** sepetteki "Kuponlarım" butonu açıldı (C3'ten beri
    @if(false) idi); modal üye listesiyle dolar, "Kullan" kodu sepet alanına yazıp
    mevcut Uygula akışını tetikler. Dinleyici delegation'la — modal partial'ı sayfa
    başında include ediliyor, buton parse edilmeden script çalışıyor (ilk koşu dersi).
  - **E2E 13/13 ✓ + C3 10/10 + C5/C10 15/15 + E1 13/13 ✓; drift TEMİZ.**
    Sıradaki: E10 Tekrar Satın Al (geçmiş sipariş kalemlerinden liste + toplu sepete ekle).

- **2026-07-10 (devam) — E8 TAMAM: İadelerim + iade talebi akışı:**
  - **Nedenler Lookup'ta:** `return_reason` tipi + 9 ana neden; alt nedenler değerin
    `ExtraData.subReasons`'ında (LookupValue'da hiyerarşi yok — alt nedenler metin
    snapshot). Seeder idempotent + canlıya SQL uygulandı.
  - **Backend:** `Return.CargoReturnCode`+`ImageUrls` (AddReturnStoreFields canlıda);
    `CreateStoreReturn` — üye kapsamlı, yalnız delivered, kalemler farklı siparişlerden
    (sipariş başına Return; kod modalı tüm kodları listeler), mükerrer engeli, beklenen
    tutar kalem toplamından, kod `IAD-XXXXXX`. Neden = ana LookupId + CustomerNotes
    JSON snapshot (relaxed encoding). Görsel: POST returns/images (5×5MB) →
    `Store:MediaRootPath` altına /media/returns/yyyyMM (nginx sunar). SMS: D4
    otp_codes purpose=phone_verify — kod KAYITLI telefona (send|verify endpoint'leri);
    doğrulanmamışsa POST returns `phone_verification_required` döner, akış SMS
    modalından sonra otomatik devam eder; başarı IsPhoneVerified=true.
    **Sahiplik düzeltmesi:** store GetOrder/GetReturn MemberId denetler (404).
  - **Frontend:** İadelerim SSR kartlar (4 adımlı akış; rejected'da akış gizli +
    inceleme notu; Dekont H1, kargo firması H2 gizli); talep modalı SSR ürünlerle
    (iade edilen kalem kilitli + önceki neden chip'leri); Siparişlerim "İade Et" →
    `/Hesabim/Iadelerim?iade=yeni` köprüsü (modal otomatik).
  - **E2E 35/35 ✓ + E1 13/13 + E4 15/15 + E7 17/17 + D4 18/18 ✓; drift TEMİZ.**
    Test dersi: üye API'leri JWT bearer ister — sayfa script'i localStorage token'ıyla
    çağırmalı (ilk koşuda 401); rozet/durum kutularında `hidden` attribute sınıf
    display'ine yenilebilir → inline style kullanıldı. Sıradaki: E9 İndirim Kuponlarım
    (üyeye tanımlı kupon listesi API'si + sayfa; C3 kupon modalı da bundan beslenir).

- **2026-07-10 (devam) — E7 TAMAM: Ürün Değerlendirme modülü (en büyük kalem):**
  - **Backend:** `storefront.product_reviews` (AddProductReviews canlıda) — puan/metin/
    pending|approved|rejected + red nedeni + maskeli ad snapshot'ı; Create (mükerrer engeli)/
    Moderate/Delete (soft — Silinenler sekmesi); GetMemberReviews/GetProductReviews/
    GetReviewsForModeration. **Satın alma şartı** API katmanında: delivered kalem
    VariantId'leri Catalog'la koda çözülür (modüller birbirini bilmez).
  - **Puanlar gerçek ortalamadan (yalnız approved):** yeni port IProductReviewStatsService;
    GetStoreProducts + GetChannelCategoryProducts DTO'larına additive Rating/ReviewCount
    (kategori handler'ı sarmalandı — cache'ten bağımsız taze); kart puan bölümü açıldı
    (SSR + infinite JS), detay puan + ilk 10 yorum SSR.
  - **UI:** Yorumlarım 5 sekme SSR + yorum yazma modalı (tasarımda yoktu) + Yeniden Düzenle;
    admin ReviewsModerationPage (npm build alındı). /urun-degerlendirmeleri sayfası (595
    satır tasarım) taşınmadı — ileri iş, backend hazır.
  - **E2E 17/17 ✓ + B6/B10/E1/E4/E5 regresyon ✓; drift TEMİZ.** ⚠️ B10'da bilinen sınır
    kayda geçti: sıralama anahtarı (BasePrice) ↔ kart kanal fiyatı ayrışması — Faz G fiyat
    mimarisi (test notu b10-e2e'de). Sıradaki: E8 İadelerim + iade talebi akışı.

- **2026-07-10 (devam) — E6 TAMAM: Koleksiyonlar canlı (yeni backend + admin onay ekranı):**
  - **Backend:** `storefront.collections` + `collection_items` (AddCollections canlıda) —
    ShareCode + Status (pending/approved/rejected) + IsQuickSave; item'lar ProductCode
    anahtarlı. CreateCollection (pending doğar) / ToggleQuickSave / ModerateCollection;
    GET/POST /api/store/collections + POST saved/toggle; admin /api/collections
    (liste+approve/reject).
  - **Admin UI:** React CollectionsModerationPage (sekmeli kuyruk; sidebar girişi;
    npm build alındı) — Faz G koleksiyon bloğu yalnız approved+public gösterir.
  - **Bookmark kararı:** tasarımda seçici yok → kart/detay bookmark'ı otomatik
    "Kaydedilenler" hızlı koleksiyonuyla çalışır (toggle-off yalnız oradan çıkarır);
    misafir giriş modalına (E5 capture deseni).
  - **Koleksiyonlarım SSR:** kartlar + durum rozeti (Onay bekliyor/Onaylanmadı — tasarıma
    eklendi); modal panelleri gerçek favori/koleksiyon ürünleriyle; ms:koleksiyon-olustur
    → POST + reload; Paylaş linki panoya (public sayfa Faz G).
  - **E2E 15/15 ✓ + E5 13/13 + C1 12/12 + E1 13/13 ✓; drift TEMİZ.**
    Sıradaki: E7 Yorumlarım + Ürün Değerlendirme modülü (en büyük kalem — product_reviews
    + satın alma şartı + admin moderasyon + kart/detay puanları gerçek ortalamadan).

- **2026-07-10 (devam) — E5 TAMAM: Favoriler canlı (yeni backend):**
  - **Backend:** `storefront.favorites` (AddFavorites migration canlıda) — anahtar
    **ProductCode** (plan ProductId diyordu; kartların kullandığı stabil kod seçildi,
    C9 StockAlert deseni); Add idempotent (soft kayıt geri açılır) / Remove soft delete /
    GetMemberFavorites kod listesi; GET/POST/DELETE /api/store/favorites (MemberOnly);
    GetStoreProductsQuery'ye additive ProductCodes filtresi.
  - **Frontend:** `_FavoriDavranis` (izinli yeni, _Layout'ta) — site.js kalp toggle/
    animasyonuna dokunmadan **capture-phase** dinleyici (misafir: toggle engellenir +
    giriş modalı; üye: POST/DELETE); GET ile kart/detay/sepet işaretleme + MutationObserver.
    Kart köküne data-ms-urun-kod. **Sepet favori butonu canlı şablona alındı** (C1'de
    ertelenmişti — görünen butonlar gizli demo bloğundaymış, E2E yakaladı).
  - **Favorilerim SSR:** kodlar → Catalog kart verisi → paylaşılan _UrunKarti; boş durum;
    Paylaş gizli (E6 kararına dek).
  - **E2E 13/13 ✓ + C1 12/12 + B6 19/19 + E1 13/13 ✓; drift TEMİZ.**
    Sıradaki: E6 Koleksiyonlarım (collections + collection_items + moderasyon).

- **2026-07-10 (devam) — E4 TAMAM: Siparişlerim canlı:**
  - **Kartlar SSR** (misharix kart/filtre script'i parse anında dinleyici bağlıyor —
    dinamik karta bağlanamaz; `HesabimSiparisVm`, ilk 20 sipariş). Kalemler IProductService
    zenginleştirmesi (silinen varyantta snapshot ad/fiyat, görsel/link yok). Durum→rozet/
    akış/filtre eşlemesi; cancelled'da akış şeridi gizli (tasarımda iptal akışı yok).
  - **Detay modalı** gömülü JSON'dan; **kargo takip modalı sipariş başına SSR** (H2 köprüsü:
    takip no + olay çizelgesi; firma adı/logo H2'de). Gizli faz köprüleri: fatura H1,
    iade E8, tekrar satın al E10, yorum E7.
  - **E2E 15/15 ✓ + E1/E2/E3 regresyon ✓; drift TEMİZ.** Test dersi: ord_shipments.
    ShipmentNumber UNIQUE — sabit test numarası önceki yarım koşuyla çakışıp INSERT'i
    sessizce düşürdü (psql exit 0); testler artık koşu başına tekil numara + ön temizlik.
  - Sıradaki: E5 Favorilerim (YENİ backend: favorites tablosu + API + kalp butonları).

- **2026-07-10 (devam) — E3 TAMAM: Adreslerim canlı:**
  - NOT: tasarımın Adreslerim'i C4 modalını değil sayfa içi form kullanıyor (sol kartlar +
    sağ form) — plan buna göre uygulandı; C4 modalı teslimatta kalmaya devam ediyor.
  - **Backend:** `UpdateMemberAddressCommand` (C4'te ertelenen güncelleme; sahiplik denetimli)
    + PUT addresses/{id}; `SetDefaultMemberAddressCommand` + POST addresses/{id}/default.
  - **Frontend:** kartlar GET addresses'ten (varsayılan kartta Sil yok — tasarım); form
    POST/PUT ikili mod; il→ilçe kademeli geo, mahalle serbest metin (NeighborhoodId'siz —
    checkout istemiyor); Varsayılan/Sil aksiyonları; boş durum satırı.
  - **E2E 16/16 ✓ + E1 13/13 + C4 11/11 + E2 19/19 ✓; drift TEMİZ.**
    Sıradaki: E4 Siparişlerim (account orders + detay modal + durum chip'leri;
    kargo takip H2 köprüsü, fatura PDF H1 köprüsü).

- **2026-07-10 (devam) — E2 TAMAM: Üyelik Bilgilerim canlı:**
  - **Backend:** `Member.CityId` (`AddMemberCity` migration canlıda, crm_cities FK — G9
    segmenti); UpdateMemberProfile genişledi (telefon normalize+benzersizlik+değişince
    IsPhoneVerified düşer; CityId denetimi; e-posta bu komutla değişmez);
    `UpdateMemberMarketingConsents` (Consents jsonb "marketing") + PUT marketing-consents;
    `GetMemberSessions` + GET sessions; login/OTP/refresh oturumlarına IP+UserAgent yazılır.
  - **Frontend:** form profile GET/PUT'a bağlı (e-posta readonly); rozetler + telefon
    doğrulama durumu gerçek; **Şehir alanı eklendi** (tasarımda yoktu — G9); duyuru
    tercihleri; Aktif Cihazlar + Giriş Geçmişi sessions'tan (UA çözümleme + göreli zaman).
    Hesabı Sil tasarım demo'su kaldı (kullanıcı kararı bekler); şifre değiştirme/TCKN
    alanı tasarımda yok (TCKN C7 modalında; şifre forgot-password ile ileri fazda).
  - **E2E 19/19 ✓ + E1 13/13 + B4 12/12 + D1 12/12 + D4 18/18 ✓; drift TEMİZ.**
    Sıradaki: E3 Adreslerim (C4 modalı yeniden kullanılır; adres güncelleme API'si gerekli).

- **2026-07-10 (devam) — FAZ E BAŞLADI; E1 TAMAM: Hesabım çerçevesi:**
  - `HesabimController` (StorePageController tabanı) — misharix çift route şeması birebir:
    12 sayfa × (/Hesabim/... + kebab-case), tek Sayfa.cshtml + partial adı kalıbı.
    **SSR üye guard'ı:** cookie kimliği yoksa köke redirect.
  - 18 Hesabim partial'ı + Sayfa.cshtml bayt-birebir (Faz A kabuk yöntemi — sayfalar
    E2-E13'te teker teker bağlanır, o güne dek tasarım demo içeriği render olur).
    İzinli fark 2: YanMenu statü bloğu @if(false) (E13/G); Varsayilan karşılama adı
    SSR kimlikten (tr-TR upper). Nav hesap paneli linkleri ('#'tı) route'lara bağlandı.
  - ⚠️ Tasarım kaynağı kusuru: kısayol grid'inde `<img src=\` bozuk markup — bayt-birebir
    korundu (etiketler görünür, ikonlar kırık placeholder); E13'te ele alınacak.
  - **E2E 13/13 ✓ + B4 regresyon 12/12 ✓; drift TEMİZ** (2 yeni izinli girdi; D4'te
    bayatlayan 3 gerekçe yorumu da güncellendi). Sıradaki: E2 Üyelik Bilgilerim
    (profile GET/PUT + cinsiyet/şehir alanları — G9 segmenti bunlardan beslenir).

- **2026-07-10 (devam) — D7 TAMAM → FAZ D KAPANDI 🎉:**
  - Envanter 8.1 auth satırları gerçek durumla güncellendi (kayıt modalı 🔶→✅ D3;
    hesap paneli/çıkış D1 logout notu; e-posta sekmesi "varsayılan artık SMS" notu).
  - Oturumlu/oturumsuz nav görüntüleri `tools/misharix-sync/shots/d7-*` (6 adet:
    desktop 1440 + mobil 390 × oturumsuz/oturumlu + SMS'li giriş modalı + açık hesap
    paneli) — "Giriş Yap"→"Hesabım", avatar, panel linkleri görsel doğrulandı.
  - **Faz D toplu regresyon (taze publish): B4 12/12 + D1 12/12 + D3 9/9 + D4 18/18 =
    51 adım ✓**; drift TEMİZ; test verileri silindi (canlı crm_members yine 0 kayıt).
  - **FAZ D TAMAM (D1–D7).** Bekleyen tek dış karar: gerçek SMS sağlayıcısı (şimdilik
    LogSmsService). SIRADAKİ FAZ → **E (Hesabım kümesi — 12 sayfa + yeni backend
    özellikleri)**. DEPLOY BEKLİYOR (B11'den beri; artık Faz D de dahil).

- **2026-07-10 (devam) — D5 TAMAM: üye şifreleri BCrypt:**
  - `IMemberPasswordHasher` (Crm.Application) + `MemberPasswordHasher` (Crm.Infrastructure,
    BCrypt.Net-Next wf12 — IAM'la aynı). Yeni yazımlar hep BCrypt: Register + admin
    CreateMember (oradaki Base64-SHA256 "geçici" yolu da kaldırıldı).
  - **İlk girişte re-hash:** LoginMember üç formatı doğrular ($2*=BCrypt, 64 hex, 44 Base64);
    doğrulama başarılıysa eski hash BCrypt'e yükseltilir (login'in SaveChanges'iyle kalıcı) —
    toplu migration yok. OTP girişi şifreye dokunmaz.
  - Not: canlı `crm_members` bu tarihte BOŞtu (test üyeleri temizlenmişti) — legacy yol yine de
    korundu (dump geri yükleme / eski aktarım ihtimali).
  - **Doğrulama:** yeni kayıt `$2a$` ✓; legacy hex+Base64 üye login ✓ → hash `$2a$`'ya yükseldi
    → tekrar login ✓; yanlış şifre ✗; B4 12/12 + D4 18/18 + D1 12/12 ✓. Test üyeleri silindi.
  - **Faz D'de kalan: yalnız D7 QA.**

- **2026-07-10 — D4 TAMAM: SMS/OTP girişi canlı:**
  - **Backend:** `crm.otp_codes` (`AddOtpCodes` migration canlıda) + `SendLoginOtpCommand`
    (yalnız kayıtlı üye — son 10 hane eşleşmesi; 6 haneli kriptografik kod, 120 sn geçerli,
    60 sn yeniden gönderim + saatte 5 sınırı, yeni kod eskileri yakar) + `VerifyLoginOtpCommand`
    (5 deneme sınırı, tek kullanımlık; başarıda LoginMember'la aynı session+token +
    `IsPhoneVerified=true`). Port: `ISmsSender` (Crm.Application) → `CrmSmsSenderAdapter` (Api)
    → Shared `ISmsService` (dev'de LogSmsService; gerçek sağlayıcı = kullanıcı kararı).
    Endpoint: `POST /api/store/auth/otp/{send,verify}` — verify SSR cookie'sini de yazar.
  - **Frontend:** SMS sekmesi canlı + **tasarımın varsayılan sekmesi (sms) geri döndü**;
    misharix'in adım geçişi/02:00 sayacı/kod kutuları korunarak API'ye bağlandı; hata alanı
    `data-ms-giris-sms-hata`; başarıda `window.msGirisBasarili` köprüsü (token/panel/sepet
    birleştirme e-postayla ortak). Telefon+şifre sekmesi pasif (backend yok).
  - **E2E 18/18 ✓ + B4 12/12 (sms varsayılana göre güncellendi) + D1 12/12 + D3 9/9 ✓.**
    Drift TEMİZ. Test üyeleri + otp kayıtları silindi. D2+D6 plan dosyasında resmen
    işaretlendi (B4+D1'de kapanmışlardı). Kalan: D5 BCrypt geçişi, D7 QA.

- **2026-07-09 (devam) — D3 TAMAM: kayıt belgeleri CMS'ten + Member.Consents onay kaydı:**
  - 2 yeni legal sayfa: `uyelik-sozlesmesi` + `kvkk-aydinlatma` (3 platform × 7 legal;
    seeder kod-bazlı idempotent yapıldı, canlıya SQL). `MsSozlesmeler` yüklemesi
    StorePageController tabanına taşındı (nav belge modalı her sayfada).
  - `_AnaNavigasyon` `belgeIcerikleri` map'i CMS'ten data-binding (demo metin yedek).
  - Register: FirmPlatformId+AcceptedContracts → sunucu CMS'ten sürüm çözer →
    `Member.Consents.acceptedContracts` (AddMemberConsents migration canlıda).
  - **E2E 9/9 ✓ + B4/C8/D1 regresyonları ✓** (c8-e2e 5→7 sayfa güncellendi). Drift TEMİZ.
  - Kalan: D4 SMS/OTP, D5 BCrypt geçişi, D7 QA.

- **2026-07-09 (devam) — FAZ D BAŞLADI; D1 TAMAM: Razor oturum stratejisi:**
  - Login/refresh → HttpOnly `ecspros_member` cookie'si (SameSite=Lax, Secure=IsHttps);
    `IStoreMemberSession` cookie JWT'sini doğrular → tüm store sayfalarında
    `ViewData["MsUye"]` SSR kimliği. JS localStorage akışı değişmedi; nav SSR kimlikle
    /me beklemeden boyanır.
  - **⚠️ Kritik keşif:** IdentityModel 7.1.2'de (JwtBearer 8.0.14'ün getirdiği)
    `JwtSecurityTokenHandler` geçerli exp'li token'a SecurityTokenNoExpirationException
    fırlatıyor — SSR doğrulamada `JsonWebTokenHandler` kullan (pipeline'ınki de o).
  - **Logout:** `POST /api/store/auth/logout` — `RevokeMemberSessionCommand` (session
    IsActive=false) + cookie temizliği; nav çıkışı bağlandı (D6'nın session iptali kapandı).
  - **E2E 12/12 ✓ + B4 regresyon 12/12 ✓.** Drift TEMİZ. Kalan: D3 KVKK belge modalı,
    D4 SMS/OTP, D5 BCrypt geçişi, D7 QA.

- **2026-07-09 (devam) — C11 TAMAM → FAZ C KAPANDI 🎉:**
  - Envanter 8.5 tam işaretli; ertelenenler hedef fazlı (favori E5, kupon listesi E9, adres
    düzenle E4, tahsilat/BIN H6, bildirim gönderimi H).
  - **Toplu regresyon (taze publish): C1 12/12 + C3 10/10 + C4 11/11 + C5/C10 15/15 + C7 11/11
    + guard 4/4 + C8 14/14 + C9 11/11 = 88 adım ✓**, 0 konsol hatası, drift temiz.
  - c3-e2e düzeltildi: C3TEST10 kuponunu DB fixture'ı sanıyordu — artık kendisi oluşturup siler.
  - Sıradaki faz: **D (üye oturumu Razor tarafı + SMS/OTP)**. DEPLOY BEKLİYOR (B11'den beri).

- **2026-07-09 (devam) — C9 TAMAM: stok gelince haber ver:**
  - **Backend:** Storefront'a `StockAlert` entity (`storefront.stock_alerts`: platform/varyant/üye +
    Email/ProductCode/VariantInfo snapshot + Status active|notified|cancelled; `AddStockAlerts`
    migration canlıya uygulandı). `CreateStockAlertCommand` (idempotent) + `GetMemberStockAlertsQuery`;
    `POST/GET /api/store/stock-alerts` (MemberOnly, e-posta claim'den). Bildirim gönderimi Faz H'de.
  - **Frontend:** sepet satır şablonuna tasarımın tükendi butonu eklendi (hidden) — isAvailable=false
    kalemde görünür; misafir→giriş modalı, üye→POST→"Stok gelince haber verilecek ✓"; reload'da GET
    ile işaretlenir. Not: kalem IsAvailable sepete ekleme anı değeridir (B12 bilinen sınırı).
  - **E2E 11/11 ✓ + C1 regresyon 12/12 ✓.** Drift TEMİZ. Test notu: julude ürünü 1K00005.0001
    ürün temizliğinde silinmiş — testler artık P-00020797 kullanıyor; localhost test instance'ı
    artık MISHAR'a çözülüyor (B12 dönemindeki julude varsayılanı geçerli değil).
  - **Faz C kalan:** C11 (QA kapanışı — envanter 8.5).

- **2026-07-09 (devam) — C8 TAMAM: sözleşme modalları CMS'ten + kabul kaydı:**
  - **CMS:** `GetStoreLegalPagesQuery` + `GET /api/store/cms/legal` (PageType='legal',
    rich_text section'ların Settings["html"] birleşimi). Seed: Dev'de `SeedCmsLegalPagesAsync`,
    canlıya SQL — 3 platform × 5 sayfa (mesafeli satış, ön bilgilendirme, gizlilik, kullanım,
    kargo; firmanın gerçek unvan/adres/VKN'siyle; 'rich_text' type + 'icerik-sayfasi' template).
  - **SSR:** SepetController → ViewData["MsSozlesmeler"] (5 dk IMemoryCache); modal panelleri
    CMS'ten (demo ELDİ metni gitti, `data-ms-sozlesme-kodlar`); ödeme "Sözleşmeler ve Onaylar"
    bölümü 3 bilgi grubuyla açıldı.
  - **Kabul kaydı:** checkout `AcceptedContracts` kodları → sunucu CMS'ten başlık+sürüm çözer →
    `Order.CustomerNotes.acceptedContracts` (code/title/acceptedAt/contentUpdatedAt).
  - **Bonus:** CustomerNotes sessizce düşüyordu → düzeldi (`note`); _SepetModallari ÇİFT include
    (C5'ten beri) → tekilleştirildi; VariantInfo zorunluydu (seçeneksiz ürün 400) → nullable.
  - **E2E 14/14 ✓ + C5+C10 regresyon 15/15 ✓.** Drift TEMİZ. Faz C kalan: C9, C11.

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
    şifre (`EcsPros2025RedisPass!`) docker-compose.yml'dekiyle aynı görünüyor ama ÇALIŞAN container
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
