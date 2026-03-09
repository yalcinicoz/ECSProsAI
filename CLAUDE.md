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
| POST | `/api/crm/members` | Üye oluştur |

### Order
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/orders` | Sipariş listesi (sayfalı) |
| POST | `/api/orders` | Sipariş oluştur |

### Finance
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/finance/suppliers` | Tedarikçi listesi |
| POST | `/api/finance/suppliers` | Tedarikçi oluştur |

### Promotion
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/promotion/campaigns` | Kampanya listesi |
| POST | `/api/promotion/campaigns` | Kampanya oluştur |

### CMS
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/cms/pages` | Sayfa listesi |
| POST | `/api/cms/pages` | Sayfa oluştur |

### POS
| Method | Endpoint | Açıklama |
|--------|----------|---------|
| GET | `/api/pos/registers` | Kasa listesi |
| POST | `/api/pos/sessions/open` | Oturum aç |
| POST | `/api/pos/sessions/{id}/close` | Oturum kapat |

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

- **Fulfillment modülü eksik**: Domain + Infrastructure var, Application katmanı (queries/commands) ve controller henüz oluşturulmadı.
- **Update endpoint'leri yok**: Hiçbir modülde PUT/PATCH handler'ı yazılmadı.
- **Password hashing**: IAM'da `IPasswordHasher` (BCrypt). CRM'deki SHA256 geçici; production'da değiştirilmeli.
- **GitHub push**: SSH 22 portu engellidir. `ssh.github.com:443` veya HTTPS kullanılmalı.
- **API portu**: Development'ta `http://localhost:5050`
- **Swagger**: `http://localhost:5050/swagger` (sadece Development)
