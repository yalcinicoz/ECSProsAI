# ECSPros — Proje Kılavuzu

E-ticaret odaklı **.NET 8 Modular Monolith** API projesi. Her modül kendi Domain/Application/Infrastructure katmanlarına sahiptir; tek bir API host üzerinden servis edilir.

---

## Teknoloji Yığını

| Katman | Teknoloji |
|--------|-----------|
| Framework | .NET 8, ASP.NET Core |
| ORM | Entity Framework Core 8 |
| Veritabanı | PostgreSQL 16 (`ecommerce_db`) |
| CQRS / Mesajlaşma | MediatR 14 |
| Auth | JWT Bearer (HS256), Refresh Token rotation |
| JSON | System.Text.Json — camelCase, null ignore, enum-as-string |
| Dokümentasyon | Swagger / OpenAPI |

---

## Proje Yapısı

```
/opt/ecsPros/src/
├── ECSPros.Api/                    # Host — tek giriş noktası
│   ├── Controllers/                # HTTP katmanı
│   ├── Extensions/DatabaseSeeder.cs
│   └── Middleware/GlobalExceptionMiddleware.cs
├── Modules/
│   ├── Iam/                        # Identity & Access Management
│   ├── Core/                       # Dil, LookupType/Value, referans verisi
│   ├── Catalog/                    # Ürün, kategori, varyant
│   ├── Inventory/                  # Depo, stok, stok hareketi
│   ├── Crm/                        # Üye, üye grubu
│   ├── Order/                      # Sipariş, sipariş kalemi
│   ├── Fulfillment/                # Kargo, teslimat (Application katmanı YOK — eksik)
│   ├── Finance/                    # Tedarikçi, fatura
│   ├── Promotion/                  # Kampanya, kupon
│   ├── Cms/                        # Sayfa, şablon, menü
│   └── Pos/                        # Kasa, oturum, fiş
└── Shared/
    └── ECSPros.Shared.Kernel/      # BaseEntity, Result<T>, PagedResult
```

Her modül şu katman yapısına sahiptir:
```
ECSPros.<Modül>.Domain/         → Entity sınıfları (BaseEntity türevleri)
ECSPros.<Modül>.Application/    → CQRS: Commands/, Queries/, Services/I<Modül>DbContext.cs
ECSPros.<Modül>.Infrastructure/ → DbContext, Migrations, DependencyInjection.cs
```

---

## Temel Kurallar

### BaseEntity
Her entity şu alanları miras alır (`Shared.Kernel.Domain.BaseEntity`):
```csharp
Guid Id          // Guid.NewGuid() — DB'ye gitmeden üretilir
DateTime CreatedAt
Guid? CreatedBy
DateTime? UpdatedAt
Guid? UpdatedBy
bool IsDeleted   // Soft delete
DateTime? DeletedAt
Guid? DeletedBy
```

### Soft Delete
Tüm `BaseEntity` türevlerinde global query filter uygulanır:
```csharp
modelBuilder.Entity<X>().HasQueryFilter(x => !x.IsDeleted);
```

### Result<T> Pattern
Tüm command/query handler'lar `Result<T>` döner. Controller'larda:
```csharp
if (result.IsFailure)
    return BadRequest(new { success = false, error = result.Error });
return Ok(new { success = true, data = result.Value });
```

### I18n Alanları
Çok dilli metin alanları `Dictionary<string, string>` olarak tutulur, JSONB sütununa yazılır:
```csharp
public Dictionary<string, string> NameI18n { get; set; } = new();
// Örnek: { "tr": "Erkek Giyim", "en": "Men's Clothing" }
```
JSONB desteği için `NpgsqlDataSource.EnableDynamicJson()` zorunludur (Program.cs'de singleton).

---

## Authentication

- **Access Token**: HS256 JWT, 60 dakika ömür
- **Refresh Token**: Opaque string, SHA256 hash ile `UserSessions` tablosunda saklanır, 30 gün
- **Rotation**: Refresh işleminde eski session `IsActive = false`, yeni session açılır
- **Claims**: `sub` (userId), `email`, `full_name`, `permission` (çoklu), `must_change_password`
- **Admin sıfırlama**: `ChangePasswordCommand(IsAdminReset: true)` ile mevcut şifre doğrulama atlanır

---

## Veritabanı

- **Bağlantı**: `Host=localhost;Port=5432;Database=ecommerce_db;Username=ecommerce`
- **Migration history tabloları**: Her modül kendi schema'sında (`iam`, `core`, `catalog`, `inventory`, `crm`, `order`, `finance`, `promotion`, `cms`, `pos`)
- **Yeni migration eklemek**:
  ```bash
  dotnet ef migrations add <MigrationName> \
    --project src/Modules/<Modül>/ECSPros.<Modül>.Infrastructure \
    --startup-project src/ECSPros.Api
  ```
- **Migration uygulamak**: Uygulama başlangıcında `DatabaseSeeder.SeedAsync()` çağrısı migrate de yapar (Development modda)

---

## Program.cs Kayıt Kalıbı

Yeni modül eklendiğinde iki şey yapılmalı:

**1. MediatR handler'larını kaydet** (Application assembly'sindeki herhangi bir tip üzerinden):
```csharp
cfg.RegisterServicesFromAssembly(typeof(GetXxxQuery).Assembly);
```

**2. Infrastructure DI'ını kaydet**:
```csharp
builder.Services.AddXxxInfrastructure(npgsqlDataSource);
```

Her `AddXxxInfrastructure` şu kalıbı izler:
```csharp
public static IServiceCollection AddXxxInfrastructure(
    this IServiceCollection services, NpgsqlDataSource dataSource)
{
    services.AddDbContext<XxxDbContext>(options =>
        options.UseNpgsql(dataSource,
            o => o.MigrationsHistoryTable("__ef_migrations_xxx", "xxx")));
    services.AddScoped<IXxxDbContext>(sp => sp.GetRequiredService<XxxDbContext>());
    return services;
}
```

---

## API Endpoint'leri

### Auth
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| POST | `/api/auth/login` | Kullanıcı girişi — JWT + refresh token |
| POST | `/api/auth/refresh` | Access token yenileme |
| GET | `/api/auth/me` | Mevcut kullanıcı bilgisi |
| POST | `/api/auth/change-password` | Şifre değiştirme |

### IAM
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/iam/users` | Kullanıcı listesi |
| POST | `/api/iam/users` | Kullanıcı oluştur |
| PUT | `/api/iam/users/{id}` | Kullanıcı güncelle |
| POST | `/api/iam/users/{id}/reset-password` | Şifre sıfırla (admin) |
| POST | `/api/iam/users/{id}/roles` | Rol ata |
| GET | `/api/iam/roles` | Rol listesi |

### Core
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/core/languages` | Dil listesi |
| GET | `/api/lookup/types` | Lookup tip listesi |
| POST | `/api/lookup/types` | Lookup tip oluştur |
| GET | `/api/lookup/types/{code}/values` | Lookup değerleri |
| POST | `/api/lookup/types/{code}/values` | Lookup değeri ekle |

### Catalog
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/catalog/categories` | Kategori listesi |
| POST | `/api/catalog/categories` | Kategori oluştur |
| GET | `/api/catalog/products` | Ürün listesi (sayfalı) |
| POST | `/api/catalog/products` | Ürün oluştur (varyantlarla birlikte) |
| GET | `/api/catalog/products/{code}` | Ürün detayı |

### Inventory
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/inventory/warehouses` | Depo listesi |
| POST | `/api/inventory/warehouses` | Depo oluştur |
| GET | `/api/inventory/stocks` | Stok listesi |
| POST | `/api/inventory/stocks/adjust` | Stok hareketi (giriş/çıkış/düzeltme) |

### CRM
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/crm/members` | Üye listesi |
| GET | `/api/crm/members/{id}` | Üye detayı |
| POST | `/api/crm/members` | Üye oluştur |

### Order
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/orders` | Sipariş listesi (sayfalı) |
| POST | `/api/orders` | Sipariş oluştur |
| GET | `/api/orders/{id}` | Sipariş detayı |
| POST | `/api/orders/{id}/confirm` | Onayla — stok rezervasyonu oluşturur |
| POST | `/api/orders/{id}/cancel` | İptal et — rezervasyonları serbest bırakır |
| POST | `/api/orders/{id}/start-processing` | İşleme al (picking plan atanabilir) |
| POST | `/api/orders/{id}/ship` | Kargoya ver — Shipment oluşturur, stok tüketir |
| POST | `/api/orders/{id}/deliver` | Teslim edildi |

**İadeler:**
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/orders/returns` | İade listesi (sayfalı, OrderId/MemberId/Status filtreli) |
| POST | `/api/orders/{id}/returns` | İade talebi oluştur |
| POST | `/api/orders/returns/{id}/approve` | İade onayla (requested → approved) |
| POST | `/api/orders/returns/{id}/receive` | İadeyi teslim al (approved → received, stok artar) |
| POST | `/api/orders/returns/{id}/complete-refund` | Geri ödeme tamamla (received → refunded) |

**Faturalar:**
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/orders/invoices` | Fatura listesi (OrderId/Status filtreli) |
| POST | `/api/orders/{id}/invoices` | Sipariş için fatura oluştur |
| POST | `/api/orders/invoices/{id}/cancel` | Fatura iptal et |

**Teklifler (Quote):**
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/orders/quotes` | Teklif listesi |
| POST | `/api/orders/quotes` | Teklif oluştur |
| POST | `/api/orders/quotes/{id}/send` | Teklifi gönder (draft → sent) |
| POST | `/api/orders/quotes/{id}/respond` | Teklifi yanıtla (accepted/rejected) |
| POST | `/api/orders/quotes/{id}/convert` | Siparişe dönüştür (accepted → converted) |

**Hediye Kartları:**
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/orders/gift-cards/{code}` | Hediye kartı bakiye sorgula |
| POST | `/api/orders/gift-cards` | Hediye kartı oluştur |
| POST | `/api/orders/gift-cards/use` | Hediye kartı kullan |

### Finance
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/finance/suppliers` | Tedarikçi listesi |
| GET | `/api/finance/suppliers/{id}` | Tedarikçi detayı |
| POST | `/api/finance/suppliers` | Tedarikçi oluştur |

### Promotion
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/promotion/campaigns` | Kampanya listesi |
| POST | `/api/promotion/campaigns` | Kampanya oluştur |
| PUT | `/api/promotion/campaigns/{id}` | Kampanya güncelle |
| POST | `/api/promotion/calculate` | Sepet için kampanya indirimlerini hesapla |
| POST | `/api/promotion/coupon/validate` | Kupon kodu doğrula + indirim hesapla |
| POST | `/api/promotion/coupon/use` | Kupon kullanımını kaydet |

### CMS
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/cms/pages` | Sayfa listesi |
| GET | `/api/cms/pages/{id}` | Sayfa detayı |
| POST | `/api/cms/pages` | Sayfa oluştur |

### POS
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/pos/registers` | Kasa listesi |
| POST | `/api/pos/sessions/open` | Oturum aç |
| POST | `/api/pos/sessions/{id}/close` | Oturum kapat |
| GET | `/api/pos/sessions/{id}/summary` | Gün sonu kasa raporu |
| GET | `/api/pos/sales` | Satış listesi (SessionId/RegisterId/Tarih/Status filtreli) |
| GET | `/api/pos/sales/{id}` | Satış detayı (kalemler + ödemeler) |
| POST | `/api/pos/sales` | Satış tamamla (stok otomatik düşülür) |
| POST | `/api/pos/sales/{id}/refund` | Satış iade et (stok geri gelir) |

---

## Seed Verisi

`DatabaseSeeder.SeedAsync()` — sadece Development modda çalışır:

**IAM:**
- `admin` kullanıcısı (şifre: `Admin123!`)
- `super_admin` rolü — tüm permission'lar

**Core:**
- Diller: `tr` (Türkçe), `en` (English)
- Sipariş durumları: pending, confirmed, processing, shipped, delivered, cancelled, returned
- Ödeme yöntemleri: cash, credit_card, bank_transfer, online_payment, pos
- Cinsiyet lookup tipi + değerleri: male, female, unisex

---

## Önemli Notlar

- **Fulfillment modülü tamamlandı**: picking-plans (start/complete), packing-stations, packages, bins CRUD + durum endpoint'leri mevcut.
- **Update endpoint'leri tamamlandı**: IAM users, CRM members, Catalog categories/products, Inventory warehouses, Finance suppliers, Promotion campaigns, CMS pages, Lookup values, Fulfillment packing stations.
- **Order modülü genişletildi**: İade akışı (return flow), fatura yönetimi (invoice), teklif akışı (quote → order), hediye kartları (gift card balance/use/create) tamamlandı.
- **Fulfillment-Order entegrasyonu**: `PickingPlanCreatedEvent` → Order.StartProcessing; `PickingPlanCompletedEvent` → Order note eklenir.
- **POS derinleştirme**: Satış listesi, satış detayı, iade (refund), gün sonu kasa raporu endpoint'leri eklendi.
- **POS satışları Order modülünden ayrıldı**: `pos.pos_sales`, `pos.pos_sale_items`, `pos.pos_sale_payments` tabloları. `Order` entity'sinde artık POS alanları yok.
- **Domain Event akışı (in-process, MediatR IPublisher)**:
  - `PosSaleCompletedEvent` → `PosSaleCompletedEventHandler` (Inventory) stok düşer
  - `PosSaleRefundedEvent` → `PosSaleRefundedEventHandler` (Inventory) stok geri gelir
  - `OrderConfirmedEvent` → `OrderConfirmedEventHandler` (Inventory) stok rezervasyonu oluşturur
  - `OrderCancelledEvent` → `OrderCancelledEventHandler` (Inventory) rezervasyonları serbest bırakır
  - `ReturnReceivedEvent` → `ReturnReceivedEventHandler` (Inventory) stok artar
  - `PickingPlanCreatedEvent` → `PickingPlanCreatedEventHandler` (Order) sipariş processing'e alınır
  - `PickingPlanCompletedEvent` → `PickingPlanCompletedEventHandler` (Order) sipariş notu eklenir
  - Event bus gelince publisher/subscriber altyapısı değişecek, domain tarafı sabit
- **Order durum makinesi tamamlandı**:
  - `pending` → `confirmed` → `processing` → `shipped` → `delivered`
  - `pending` veya `confirmed` → `cancelled`
  - `OrderShippedEvent` → `OrderShippedEventHandler` (Inventory): rezervasyonları "picked" yapar, Quantity gerçekten düşer
- **Password hashing**: IAM'da `IPasswordHasher` (BCrypt). CRM'deki SHA256 geçici; production'da değiştirilmeli.
- **GitHub push**: SSH 22 portu engellidir. `ssh.github.com:443` veya HTTPS kullanılmalı.
- **API portu**: Production'da `http://0.0.0.0:5000`, Development'ta `http://localhost:5050`
- **Swagger**: `http://localhost:5050/swagger` (Development) / `http://51.178.208.59/swagger` (Production)

---

## Deployment (Production)

**Sunucu**: `51.178.208.59`

**Altyapı** (Docker Compose — `/opt/ECSProsAI`):
```bash
sudo docker compose up -d          # postgres, redis, nginx başlat
sudo docker compose ps             # durum kontrol
sudo docker compose restart nginx  # nginx yeniden başlat
```

**API Servisi** (systemd — published binary):
```bash
sudo systemctl start ecspros       # başlat
sudo systemctl stop ecspros        # durdur
sudo systemctl restart ecspros     # yeniden başlat
sudo systemctl status ecspros      # durum
journalctl -u ecspros -f           # canlı log
```

**Kod değişikliği sonrası deploy:**
```bash
cd /opt/ECSProsAI
dotnet publish src/ECSPros.Api/ECSPros.Api.csproj -c Release -o /opt/ECSProsAI/publish --no-restore
sudo systemctl restart ecspros
```

Servis dosyası: `/etc/systemd/system/ecspros.service`
Binary: `/opt/ECSProsAI/publish/ECSPros.Api.dll`

**Tüm migration'ları uygulamak**:
```bash
cd /opt/ECSProsAI
for ctx in IamDbContext CoreDbContext CatalogDbContext InventoryDbContext OrderDbContext CrmDbContext CmsDbContext PosDbContext PromotionDbContext FinanceDbContext FulfillmentDbContext; do
  dotnet ef database update --project src/ECSPros.Api/ECSPros.Api.csproj --context $ctx
done
```

**Önemli**: `docker-compose.yml`'de nginx servisine `extra_hosts: - "host.docker.internal:host-gateway"` eklenmiştir. Bu Linux'ta `host.docker.internal` çözümlemesi için zorunludur.
