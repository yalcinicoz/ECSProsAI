# E-Ticaret Altyapı Projesi - Backend Modül Yapısı

**Doküman Versiyonu:** 1.0  
**Son Güncelleme:** Ocak 2025  
**Framework:** .NET 8  
**Mimari:** Modüler Monolith + Clean Architecture

---

## 1. Genel Bakış

Proje, Modüler Monolith mimarisi ile tasarlanmıştır. Her modül kendi içinde bağımsız çalışır ancak tek bir uygulama olarak deploy edilir. Bu yaklaşım:

- Microservices karmaşıklığından kaçınır
- 3 kişilik ekip için yönetilebilir kalır
- İleride gerekirse modüller ayrılabilir
- Tek veritabanı ile başlanır, sonra ayrılabilir

---

## 2. Proje Yapısı

```
src/
├── ECommerce.Api/                    # Ana API projesi (host)
├── ECommerce.Shared/                 # Paylaşılan altyapı
│   ├── ECommerce.Shared.Kernel/      # Domain primitives, base classes
│   ├── ECommerce.Shared.Infrastructure/ # Ortak altyapı servisleri
│   └── ECommerce.Shared.Contracts/   # Modüller arası iletişim kontratları
│
├── Modules/
│   ├── Core/
│   │   ├── ECommerce.Core.Domain/
│   │   ├── ECommerce.Core.Application/
│   │   ├── ECommerce.Core.Infrastructure/
│   │   └── ECommerce.Core.Api/
│   │
│   ├── Catalog/
│   │   ├── ECommerce.Catalog.Domain/
│   │   ├── ECommerce.Catalog.Application/
│   │   ├── ECommerce.Catalog.Infrastructure/
│   │   └── ECommerce.Catalog.Api/
│   │
│   ├── Inventory/
│   │   ├── ECommerce.Inventory.Domain/
│   │   ├── ECommerce.Inventory.Application/
│   │   ├── ECommerce.Inventory.Infrastructure/
│   │   └── ECommerce.Inventory.Api/
│   │
│   ├── Crm/
│   │   ├── ECommerce.Crm.Domain/
│   │   ├── ECommerce.Crm.Application/
│   │   ├── ECommerce.Crm.Infrastructure/
│   │   └── ECommerce.Crm.Api/
│   │
│   ├── Order/
│   │   ├── ECommerce.Order.Domain/
│   │   ├── ECommerce.Order.Application/
│   │   ├── ECommerce.Order.Infrastructure/
│   │   └── ECommerce.Order.Api/
│   │
│   ├── Fulfillment/
│   │   ├── ECommerce.Fulfillment.Domain/
│   │   ├── ECommerce.Fulfillment.Application/
│   │   ├── ECommerce.Fulfillment.Infrastructure/
│   │   └── ECommerce.Fulfillment.Api/
│   │
│   ├── Finance/
│   │   ├── ECommerce.Finance.Domain/
│   │   ├── ECommerce.Finance.Application/
│   │   ├── ECommerce.Finance.Infrastructure/
│   │   └── ECommerce.Finance.Api/
│   │
│   ├── Promotion/
│   │   ├── ECommerce.Promotion.Domain/
│   │   ├── ECommerce.Promotion.Application/
│   │   ├── ECommerce.Promotion.Infrastructure/
│   │   └── ECommerce.Promotion.Api/
│   │
│   ├── Iam/
│   │   ├── ECommerce.Iam.Domain/
│   │   ├── ECommerce.Iam.Application/
│   │   ├── ECommerce.Iam.Infrastructure/
│   │   └── ECommerce.Iam.Api/
│   │
│   ├── Cms/
│   │   ├── ECommerce.Cms.Domain/
│   │   ├── ECommerce.Cms.Application/
│   │   ├── ECommerce.Cms.Infrastructure/
│   │   └── ECommerce.Cms.Api/
│   │
│   └── Integration/
│       ├── ECommerce.Integration.Domain/
│       ├── ECommerce.Integration.Application/
│       ├── ECommerce.Integration.Infrastructure/
│       └── ECommerce.Integration.Api/
│
tests/
├── ECommerce.Shared.Tests/
├── ECommerce.Core.Tests/
├── ECommerce.Catalog.Tests/
├── ECommerce.Inventory.Tests/
├── ECommerce.Crm.Tests/
├── ECommerce.Order.Tests/
├── ECommerce.Fulfillment.Tests/
├── ECommerce.Finance.Tests/
├── ECommerce.Promotion.Tests/
├── ECommerce.Iam.Tests/
├── ECommerce.Cms.Tests/
└── ECommerce.Integration.Tests/
```

---

## 3. Modüller

| Modül | Sorumluluk |
|-------|------------|
| Core | Temel tanımlar, firmalar, platformlar, entegrasyonlar, dil, lookup |
| Catalog | Ürünler, kategoriler, özellikler, fiyatlandırma |
| Inventory | Stok, depolar, lokasyonlar, transferler, rezervasyonlar |
| Crm | Müşteriler, adresler, sepetler, cüzdan, sadakat |
| Order | Siparişler, ödemeler, faturalar, kargo, iadeler |
| Fulfillment | Toplama, ayrıştırma, paketleme, operasyon |
| Finance | Tedarikçiler, alış faturaları, cari hesap |
| Promotion | Kampanyalar, kuponlar, indirimler |
| Iam | Kullanıcılar, roller, izinler, oturumlar, audit |
| Cms | Site menüleri, sayfalar, içerik yönetimi |
| Integration | Pazaryeri, kargo, fatura entegratörü adaptörleri |

---

## 4. Katman Yapısı

Her modül 4 katmandan oluşur:

### 4.1 Domain Katmanı
İş kurallarının ve domain mantığının bulunduğu katman.

```
ECommerce.{Module}.Domain/
├── Entities/
│   ├── Product.cs
│   └── ProductVariant.cs
├── ValueObjects/
│   ├── Money.cs
│   └── Sku.cs
├── Events/
│   ├── ProductCreatedEvent.cs
│   └── PriceChangedEvent.cs
├── Repositories/
│   └── IProductRepository.cs
├── Services/
│   └── PricingDomainService.cs
└── Exceptions/
    └── ProductNotFoundException.cs
```

**İçerik:**
- Entity'ler (iş nesneleri)
- Value Object'ler (değer nesneleri)
- Domain Event'ler
- Repository interface'leri
- Domain servisleri
- Business rule'lar ve validasyonlar
- Domain exception'lar

**Bağımlılık:** Sadece Shared.Kernel'e bağımlı, başka hiçbir projeye bağımlı değil.

### 4.2 Application Katmanı
Use case'lerin ve uygulama mantığının bulunduğu katman.

```
ECommerce.{Module}.Application/
├── Features/
│   ├── Products/
│   │   ├── Commands/
│   │   │   ├── CreateProduct/
│   │   │   │   ├── CreateProductCommand.cs
│   │   │   │   ├── CreateProductCommandHandler.cs
│   │   │   │   └── CreateProductCommandValidator.cs
│   │   │   └── UpdateProduct/
│   │   │       └── ...
│   │   ├── Queries/
│   │   │   ├── GetProduct/
│   │   │   │   ├── GetProductQuery.cs
│   │   │   │   ├── GetProductQueryHandler.cs
│   │   │   │   └── ProductDto.cs
│   │   │   └── GetProducts/
│   │   │       └── ...
│   │   └── EventHandlers/
│   │       └── ProductCreatedEventHandler.cs
│   └── Categories/
│       └── ...
├── Common/
│   ├── Interfaces/
│   │   ├── IProductService.cs
│   │   └── IFileStorageService.cs
│   ├── Mappings/
│   │   └── ProductMappingProfile.cs
│   └── Behaviors/
│       ├── ValidationBehavior.cs
│       └── LoggingBehavior.cs
└── DependencyInjection.cs
```

**İçerik:**
- Command'lar ve Handler'lar (yazma işlemleri)
- Query'ler ve Handler'lar (okuma işlemleri)
- DTO'lar (Data Transfer Objects)
- Validation kuralları (FluentValidation)
- Application servisleri
- Interface tanımları (port'lar)
- AutoMapper profilleri
- MediatR behavior'ları

**Bağımlılık:** Domain katmanına ve Shared.Kernel'e bağımlı.

### 4.3 Infrastructure Katmanı
Dış dünya ile iletişimin sağlandığı katman.

```
ECommerce.{Module}.Infrastructure/
├── Persistence/
│   ├── CatalogDbContext.cs
│   ├── Configurations/
│   │   ├── ProductConfiguration.cs
│   │   └── CategoryConfiguration.cs
│   ├── Repositories/
│   │   ├── ProductRepository.cs
│   │   └── CategoryRepository.cs
│   └── Migrations/
│       └── ...
├── Services/
│   ├── FileStorageService.cs
│   └── ElasticsearchService.cs
├── ExternalServices/
│   └── ...
└── DependencyInjection.cs
```

**İçerik:**
- EF Core DbContext ve konfigürasyonları
- Repository implementasyonları
- External servis adaptörleri
- Message queue implementasyonları
- Cache implementasyonları
- File storage implementasyonları

**Bağımlılık:** Domain ve Application katmanlarına bağımlı.

### 4.4 Api Katmanı
HTTP endpoint'lerinin tanımlandığı katman.

```
ECommerce.{Module}.Api/
├── Controllers/
│   ├── ProductsController.cs
│   └── CategoriesController.cs
├── Models/
│   ├── Requests/
│   │   ├── CreateProductRequest.cs
│   │   └── UpdateProductRequest.cs
│   └── Responses/
│       └── ProductResponse.cs
├── Filters/
│   └── ModuleExceptionFilter.cs
└── ModuleRegistration.cs
```

**İçerik:**
- Controller'lar
- Request/Response modelleri
- Modül registrasyonu
- Endpoint tanımları
- Modüle özel filter'lar

**Bağımlılık:** Application ve Infrastructure katmanlarına bağımlı.

---

## 5. Shared Projeler

### 5.1 Shared.Kernel
Tüm modüllerin kullandığı temel yapılar.

```
ECommerce.Shared.Kernel/
├── Domain/
│   ├── BaseEntity.cs
│   ├── AggregateRoot.cs
│   ├── ValueObject.cs
│   ├── IDomainEvent.cs
│   └── AuditableEntity.cs
├── Primitives/
│   ├── Result.cs
│   ├── Error.cs
│   └── PagedList.cs
├── Extensions/
│   └── StringExtensions.cs
└── Interfaces/
    ├── IUnitOfWork.cs
    └── IRepository.cs
```

### 5.2 Shared.Infrastructure
Ortak altyapı servisleri.

```
ECommerce.Shared.Infrastructure/
├── Persistence/
│   ├── BaseDbContext.cs
│   └── UnitOfWork.cs
├── Caching/
│   ├── ICacheService.cs
│   └── RedisCacheService.cs
├── Messaging/
│   ├── IEventBus.cs
│   └── RedisEventBus.cs
├── FileStorage/
│   ├── IFileStorage.cs
│   └── S3FileStorage.cs
├── Email/
│   └── IEmailService.cs
├── Sms/
│   └── ISmsService.cs
└── Logging/
    └── ...
```

### 5.3 Shared.Contracts
Modüller arası iletişim kontratları.

```
ECommerce.Shared.Contracts/
├── Core/
│   ├── IFirmService.cs
│   └── Dtos/
│       └── FirmDto.cs
├── Catalog/
│   ├── IProductService.cs
│   └── Dtos/
│       ├── ProductDto.cs
│       └── VariantDto.cs
├── Inventory/
│   ├── IStockService.cs
│   └── Dtos/
│       └── StockDto.cs
├── Crm/
│   ├── IMemberService.cs
│   └── Dtos/
│       └── MemberDto.cs
├── Order/
│   ├── IOrderService.cs
│   └── Dtos/
│       └── OrderDto.cs
├── Promotion/
│   ├── ICampaignService.cs
│   └── Dtos/
│       └── DiscountDto.cs
└── Events/
    ├── OrderCreatedIntegrationEvent.cs
    ├── StockReservedIntegrationEvent.cs
    └── PaymentCompletedIntegrationEvent.cs
```

---

## 6. Modüller Arası İletişim

### 6.1 Senkron İletişim
Modüller birbirinin verisine Contracts üzerinden erişir.

```csharp
// Shared.Contracts/Catalog/IProductService.cs
public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<ProductDto>> GetByIdsAsync(IEnumerable<Guid> ids);
    Task<bool> ExistsAsync(Guid id);
}

// Order modülünde kullanım
public class CreateOrderCommandHandler
{
    private readonly IProductService _productService;
    
    public async Task Handle(CreateOrderCommand command)
    {
        var product = await _productService.GetByIdAsync(command.ProductId);
        // ...
    }
}
```

### 6.2 Asenkron İletişim
Domain event'ler ile modüller arası iletişim.

```csharp
// Order modülü - event yayınlama
public class Order : AggregateRoot
{
    public void Complete()
    {
        Status = OrderStatus.Completed;
        AddDomainEvent(new OrderCompletedEvent(Id));
    }
}

// Inventory modülü - event dinleme
public class OrderCompletedEventHandler : INotificationHandler<OrderCompletedEvent>
{
    public async Task Handle(OrderCompletedEvent notification)
    {
        // Stok düşme işlemi
    }
}
```

---

## 7. Ana API Projesi

```
ECommerce.Api/
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   └── TenantMiddleware.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── ApplicationBuilderExtensions.cs
└── Configuration/
    ├── SwaggerConfiguration.cs
    └── CorsConfiguration.cs
```

Modüllerin kaydı:

```csharp
// Program.cs
builder.Services
    .AddCoreModule(configuration)
    .AddCatalogModule(configuration)
    .AddInventoryModule(configuration)
    .AddCrmModule(configuration)
    .AddOrderModule(configuration)
    .AddFulfillmentModule(configuration)
    .AddFinanceModule(configuration)
    .AddPromotionModule(configuration)
    .AddIamModule(configuration)
    .AddCmsModule(configuration)
    .AddIntegrationModule(configuration);
```

---

## 8. Bağımlılık Kuralları

```
┌─────────────────────────────────────────────────────────────┐
│                        ECommerce.Api                         │
│                      (Ana Host Projesi)                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Module.Api Katmanları                     │
│         (Controller'lar, Request/Response Modelleri)         │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│               Module.Infrastructure Katmanları               │
│        (DbContext, Repository Impl, External Services)       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                Module.Application Katmanları                 │
│           (Commands, Queries, Handlers, DTOs)                │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                  Module.Domain Katmanları                    │
│         (Entities, Value Objects, Domain Events)             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Shared.Kernel + Contracts                  │
│            (Base Classes, Interfaces, Contracts)             │
└─────────────────────────────────────────────────────────────┘
```

**Kurallar:**
1. Domain katmanı sadece Shared.Kernel'e bağımlı
2. Application katmanı kendi Domain'ine ve Shared.Kernel'e bağımlı
3. Infrastructure katmanı kendi Domain ve Application'a bağımlı
4. Api katmanı kendi Application ve Infrastructure'a bağımlı
5. Modüller birbirine ASLA direkt referans vermez
6. Modüller arası iletişim sadece Shared.Contracts üzerinden

---

## 9. Kullanılan Kütüphaneler

| Kütüphane | Kullanım Alanı |
|-----------|----------------|
| MediatR | CQRS pattern, command/query handling |
| FluentValidation | Request validation |
| AutoMapper | Object mapping |
| Serilog | Logging |
| Npgsql.EntityFrameworkCore | PostgreSQL provider |
| StackExchange.Redis | Redis cache ve messaging |
| Microsoft.AspNetCore.SignalR | Real-time iletişim |
| NEST | Elasticsearch client |
| Polly | Resilience, retry policies |
| Swashbuckle | Swagger/OpenAPI |

---

## 10. Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 1.0 | Ocak 2025 | İlk versiyon |
