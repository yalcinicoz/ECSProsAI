# ECSPros — Performans Analizi ve Optimizasyon Rehberi

**Tarih:** 2026-05-26  
**Kapsam:** Backend API, Veritabanı, Önbellekleme, Altyapı

---

## 1. Gerçek Boyutu Anlamak

Performans tartışmalarını somutlaştırmak için önce gerçekçi bir yük profili çıkarmak gerekir.

### Örnek Senaryo: Orta Ölçekli E-Ticaret

| Metrik | Düşük | Orta | Yüksek |
|--------|-------|------|--------|
| Günlük aktif kullanıcı (DAU) | 1.000 | 10.000 | 100.000 |
| Sayfa görüntüleme / kullanıcı | 8 | 12 | 15 |
| Günlük toplam istek | 8.000 | 120.000 | 1.500.000 |
| Tepe saatteki istek/sn (RPS) | 2–3 | 20–30 | 200–300 |
| Sipariş dönüşüm oranı | %2 | %2 | %2 |
| Günlük sipariş | 20 | 200 | 2.000 |

**Sonuç:** Çoğu B2B veya niche e-ticaret sitesi "Orta" kategorisinde kalır. 20–30 RPS, doğru yapılandırılmış tek bir sunucu için son derece yönetilebilir bir yüktür.

---

## 2. Mimari Güçlü Yanlar

| Özellik | Avantaj |
|---------|---------|
| Modular Monolith | Servisler arası network overhead yok; aynı process içi çağrı |
| MediatR CQRS | Read/Write path ayrımı; okuma tarafı bağımsız optimize edilebilir |
| EF Core 8 | Compiled queries, split queries, AsNoTracking desteği |
| PostgreSQL 16 | JSONB indexleme, partial index, materialized view, advisory lock |
| .NET 8 | AOT dışında bile Kestrel ile çok yüksek throughput |
| JWT stateless | Her istekte DB session sorgusu yok |

---

## 3. Tespit Edilen Bottleneck'ler

### 3.1 Veritabanı Bağlantı Havuzu

**Sorun:** EF Core varsayılan bağlantı havuzu küçüktür. Tepe yükte "bağlantı bekliyor" durumu oluşabilir.

**Çözüm: PgBouncer**

```yaml
# docker-compose.yml'e eklenecek
pgbouncer:
  image: pgbouncer/pgbouncer:1.22
  environment:
    - DATABASES_HOST=postgres
    - DATABASES_PORT=5432
    - DATABASES_DBNAME=ecommerce_db
    - DATABASES_USER=ecommerce
    - PGBOUNCER_POOL_MODE=transaction
    - PGBOUNCER_MAX_CLIENT_CONN=200
    - PGBOUNCER_DEFAULT_POOL_SIZE=20
  ports:
    - "6432:5432"
```

`appsettings.json` bağlantı stringini `localhost:6432` olarak güncelle. Transaction mode'da prepared statement kullanılamaz; EF Core'da `UseNodaTime` gibi uzantılar ayarlanmalıdır.

---

### 3.2 Eksik Index'ler

**Sorun:** JSONB sütunlarına ve sık sorgulanan FK kolonlarına index yok.

**Migration ile eklenecek index'ler:**

```sql
-- Kategori hiyerarşisi
CREATE INDEX IF NOT EXISTS ix_categories_parent_id 
  ON catalog.catalog_categories(parent_id) WHERE is_deleted = false;

-- Ürün kodu arama
CREATE INDEX IF NOT EXISTS ix_products_code 
  ON catalog.catalog_products(code) WHERE is_deleted = false;

-- Stok: firma + depo + varyant üçlüsü
CREATE INDEX IF NOT EXISTS ix_stocks_firm_warehouse_variant 
  ON inventory.inventory_stocks(firm_id, warehouse_id, product_variant_id) 
  WHERE is_deleted = false;

-- Sipariş: firma + durum + tarih (liste sayfası)
CREATE INDEX IF NOT EXISTS ix_orders_firm_status_created 
  ON "order".orders(firm_id, status, created_at DESC) 
  WHERE is_deleted = false;

-- JSONB name_i18n üzerinde GIN index (tam metin arama için)
CREATE INDEX IF NOT EXISTS ix_products_name_gin 
  ON catalog.catalog_products USING GIN(name_i18n);

CREATE INDEX IF NOT EXISTS ix_categories_name_gin 
  ON catalog.catalog_categories USING GIN(name_i18n);

-- AttributeValue: type bazlı gruplama
CREATE INDEX IF NOT EXISTS ix_attribute_values_type_id 
  ON catalog.catalog_attribute_values(attribute_type_id) 
  WHERE is_deleted = false;
```

---

### 3.3 N+1 Sorguları ve Kartezyen Çarpım

**Sorun:** Birden fazla collection Include'u olan sorgular `AsSplitQuery` olmadan kartezyen çarpım üretir.

**Örnek — Hatalı:**
```csharp
// Ürün + varyantlar + özellikler + resimler → satır patlaması
var products = await _db.Products
    .Include(p => p.Variants)
        .ThenInclude(v => v.Attributes)
    .Include(p => p.Images)  // 2. collection: kartezyen çarpım!
    .ToListAsync(ct);
```

**Düzeltme:**
```csharp
var products = await _db.Products
    .Include(p => p.Variants)
        .ThenInclude(v => v.Attributes)
    .Include(p => p.Images)
    .AsSplitQuery()  // Her Include ayrı SQL sorgusu — satır patlaması yok
    .ToListAsync(ct);
```

---

### 3.4 AsNoTracking Eksikliği

**Sorun:** Salt okunur sorgularda EF Core change tracker aktif kalır; bellek ve CPU harcar.

**Kural:** Veri değiştirmeyen tüm query handler'larda `AsNoTracking()` kullan.

```csharp
// Query handler — veriyi sadece okuyoruz
var result = await _db.Products
    .AsNoTracking()  // ← mutlaka ekle
    .Include(p => p.Category)
    .Where(p => p.FirmId == request.FirmId)
    .OrderByDescending(p => p.CreatedAt)
    .ToListAsync(ct);
```

**Etki:** %15–30 bellek tasarrufu; yüksek hacimli liste sorgularında ölçülebilir fark.

---

### 3.5 Sayfalama — Offset Problemi

**Sorun:** `Skip(n).Take(20)` büyük tablolarda yavaşlar; PostgreSQL `OFFSET 10000` için 10.000 satır okur.

**Mevcut (sorunlu büyük sayfalarda):**
```csharp
query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
```

**Keyset Pagination (cursor-based) — daha ölçeklenebilir:**
```csharp
// İlk istek: cursor yok
// Sonraki istek: son satırın (createdAt, id) değerleri gönderilir
query = query.Where(p => 
    p.CreatedAt < request.CursorCreatedAt ||
    (p.CreatedAt == request.CursorCreatedAt && p.Id < request.CursorId));
```

**Not:** Keyset pagination sayfa atlamayı imkansız kılar; "önceki/sonraki" şeklinde navigasyon gerektiriyorsa uygundur. Admin paneller için offset kabul edilebilir; Store API için keyset tercih edilmeli.

---

## 4. Redis Önbellekleme Stratejisi

Redis zaten `docker-compose.yml`'de mevcut; kullanılmıyor.

### 4.1 Önbelleklenecek Veriler ve TTL

| Veri | Cache Key Örneği | TTL | Invalidasyon |
|------|------------------|-----|--------------|
| Dil listesi | `languages:all` | 24 saat | Dil güncellenince |
| Lookup değerleri | `lookup:{firmId}:{typeCode}` | 1 saat | Değer güncellenince |
| Kategori ağacı | `categories:{firmId}` | 30 dk | Kategori değişince |
| Özellik tipleri | `attr-types:{firmId}` | 30 dk | Özellik güncellenince |
| Filtre renkleri | `filter-colors:all` | 6 saat | Renk güncellenince |
| Ürün listesi | `products:{firmId}:{hash}` | 5 dk | Ürün değişince |
| Ürün detayı | `product:{code}` | 10 dk | Ürün güncellenince |
| Store kategori ağacı | `store:cats:{firmId}` | 15 dk | Kategori değişince |

### 4.2 IDistributedCache Kullanımı

```csharp
// IDistributedCache extension — SerializeAsync/DeserializeAsync yardımcısı
public static class CacheExtensions
{
    public static async Task<T?> GetAsync<T>(
        this IDistributedCache cache, string key, CancellationToken ct = default)
    {
        var bytes = await cache.GetAsync(key, ct);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes);
    }

    public static async Task SetAsync<T>(
        this IDistributedCache cache, string key, T value,
        TimeSpan expiry, CancellationToken ct = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        await cache.SetAsync(key, bytes,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }, ct);
    }
}

// Handler örneği
public async Task<Result<List<FilterColorDto>>> Handle(GetFilterColorsQuery _, CancellationToken ct)
{
    const string cacheKey = "filter-colors:all";
    var cached = await _cache.GetAsync<List<FilterColorDto>>(cacheKey, ct);
    if (cached is not null) return Result.Success(cached);

    var colors = await _db.FilterColors
        .AsNoTracking()
        .Where(c => c.IsActive)
        .OrderBy(c => c.SortOrder)
        .Select(c => new FilterColorDto(c.Id, c.Code, c.NameI18n, c.HexCode, c.SortOrder, c.IsActive))
        .ToListAsync(ct);

    await _cache.SetAsync(cacheKey, colors, TimeSpan.FromHours(6), ct);
    return Result.Success(colors);
}
```

### 4.3 Cache Invalidasyon

Yazma command'larında ilgili cache key'i sil:

```csharp
// UpdateFilterColor handler sonunda
await _cache.RemoveAsync("filter-colors:all", ct);

// UpdateCategory handler sonunda
await _cache.RemoveAsync($"categories:{request.FirmId}", ct);
await _cache.RemoveAsync($"store:cats:{request.FirmId}", ct);
```

---

## 5. Store API Optimizasyonları

Store API (müşteriye dönük) admin API'ye kıyasla çok daha yüksek trafik alır.

### 5.1 Materialized View — Ürün Liste Sayfası

```sql
-- Ürün liste sayfası için önceden hesaplanmış görünüm
CREATE MATERIALIZED VIEW store.product_list_mv AS
SELECT 
    p.id,
    p.code,
    p.name_i18n,
    p.base_price,
    p.tax_rate,
    c.name_i18n AS category_name_i18n,
    c.code AS category_code,
    COUNT(DISTINCT pv.id) AS variant_count,
    MIN(pv.price) AS min_price,
    MAX(pv.price) AS max_price,
    BOOL_OR(s.quantity > 0) AS has_stock
FROM catalog.catalog_products p
JOIN catalog.catalog_categories c ON p.category_id = c.id
LEFT JOIN catalog.catalog_product_variants pv ON pv.product_id = p.id AND NOT pv.is_deleted
LEFT JOIN inventory.inventory_stocks s ON s.product_variant_id = pv.id AND NOT s.is_deleted
WHERE NOT p.is_deleted
GROUP BY p.id, p.code, p.name_i18n, p.base_price, p.tax_rate, c.name_i18n, c.code;

CREATE UNIQUE INDEX ON store.product_list_mv (id);
CREATE INDEX ON store.product_list_mv (category_code);
```

```sql
-- Periyodik yenile (cron veya trigger)
REFRESH MATERIALIZED VIEW CONCURRENTLY store.product_list_mv;
```

### 5.2 Response Sıkıştırma

```csharp
// Program.cs
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});

app.UseResponseCompression();
```

Büyük JSON yanıtlarında (ürün listesi, kategori ağacı) %60–80 bant genişliği tasarrufu.

### 5.3 Output Caching (.NET 8)

```csharp
// Program.cs
builder.Services.AddOutputCache(opts =>
{
    opts.AddPolicy("StoreProductList", b => b
        .Expire(TimeSpan.FromMinutes(5))
        .Tag("products")
        .VaryByRouteValue("firmCode")
        .VaryByQuery("page", "category", "sort", "filter"));
});

// Store controller
[HttpGet("products")]
[OutputCache(PolicyName = "StoreProductList")]
public async Task<IActionResult> GetProducts(...)
```

---

## 6. Uygulama Katmanı Optimizasyonları

### 6.1 DTO Projeksiyon — Entity Yükleme Yerine Direkt Select

```csharp
// Kötü: tüm entity yükle, sonra map et
var products = await _db.Products.Include(p => p.Category).ToListAsync(ct);
return products.Select(p => new ProductListDto(...)).ToList();

// İyi: DB'de projeksiyon yap, sadece gerekli alanları çek
var products = await _db.Products
    .AsNoTracking()
    .Select(p => new ProductListDto(
        p.Id, p.Code, p.NameI18n,
        p.Category.NameI18n,
        p.BasePrice))
    .ToListAsync(ct);
```

### 6.2 Paralel Bağımsız Sorgular

```csharp
// Ürün detay sayfası: seri yerine paralel
var productTask = _db.Products.FirstOrDefaultAsync(p => p.Code == code, ct);
var stockTask = _db.Stocks.Where(s => s.ProductId == productId).ToListAsync(ct);
var reviewTask = _db.Reviews.Where(r => r.ProductId == productId).ToListAsync(ct);

await Task.WhenAll(productTask, stockTask, reviewTask);

var product = await productTask;
var stocks = await stockTask;
var reviews = await reviewTask;
```

### 6.3 Compiled Queries (Yüksek Frekanslı Sorgular)

```csharp
private static readonly Func<CatalogDbContext, string, Task<Product?>> GetProductByCode =
    EF.CompileAsyncQuery((CatalogDbContext db, string code) =>
        db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefault(p => p.Code == code));

// Kullanım
var product = await GetProductByCode(_db, code);
```

---

## 7. Altyapı Ölçekleme Yolları

### Seviye 1 — Mevcut Sunucu Optimizasyonu (0 maliyet)

- PgBouncer bağlantı havuzu
- Redis önbellekleme (mevcut container kullanılıyor)
- AsNoTracking + AsSplitQuery
- Response compression
- Eksik index'lerin eklenmesi

**Beklenen kazanım:** Aynı donanımda 3–5x daha fazla eş zamanlı kullanıcı.

### Seviye 2 — Dikey Ölçekleme

- Sunucu RAM: 4 GB → 8 GB (PostgreSQL shared_buffers artışı)
- PostgreSQL `work_mem` ve `effective_cache_size` tuning
- Kestrel thread pool boyutu ayarı

**Beklenen kazanım:** %50–100 throughput artışı.

### Seviye 3 — Yatay Ölçekleme

- Read replica: ağır okuma sorgularını replica'ya yönlendir
- Birden fazla API instance (Nginx upstream load balancing)
- Redis Sentinel veya Cluster
- CDN: statik varlıklar + Store API yanıtları için edge cache

**Gereklilik:** ~500 RPS üzerinde veya yüksek erişilebilirlik SLA gerektiğinde.

---

## 8. Monitoring ve Observability

### 8.1 Minimum Gerekli Metrikler

```csharp
// Program.cs — built-in .NET metrics
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddRedis(redisConnectionString, name: "redis");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### 8.2 Yavaş Sorgu Loglama

```csharp
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

PostgreSQL tarafında:
```sql
-- postgresql.conf
log_min_duration_statement = 500  -- 500ms üzeri sorguları logla
log_line_prefix = '%t [%p]: [%l-1] '
```

### 8.3 Önerilen Stack (Düşük Maliyet)

| Araç | Amaç |
|------|------|
| Prometheus + Grafana | Metrik toplama ve görselleştirme |
| Seq veya Loki | Yapılandırılmış log aggregation |
| PgHero | PostgreSQL sorgu analizi |
| Uptime Kuma | Endpoint uptime monitoring |

---

## 9. Öncelik Tablosu

| Öncelik | Aksiyon | Efor | Etki |
|---------|---------|------|------|
| 🔴 Kritik | Eksik DB index'leri ekle | Düşük | Çok Yüksek |
| 🔴 Kritik | Tüm query handler'lara `AsNoTracking()` ekle | Düşük | Yüksek |
| 🟠 Yüksek | Multi-collection Include → `AsSplitQuery()` | Düşük | Yüksek |
| 🟠 Yüksek | Redis ile referans veri önbellekleme | Orta | Yüksek |
| 🟡 Orta | PgBouncer kurulumu | Orta | Orta |
| 🟡 Orta | Response compression aktifleştir | Düşük | Orta |
| 🟢 Düşük | Store API output caching | Orta | Orta |
| 🟢 Düşük | Materialized view (ürün listesi) | Yüksek | Orta |
| ⚪ İsteğe Bağlı | Compiled queries (kritik path) | Orta | Düşük |
| ⚪ İsteğe Bağlı | Keyset pagination (Store API) | Yüksek | Düşük |

---

## 10. Gerçekçi Beklenti

**Mevcut mimariyle, hiç optimizasyon yapmadan:**
- 10–20 RPS'i rahatça kaldırır
- 50–100 eş zamanlı kullanıcıya hizmet verir

**Seviye 1 optimizasyonlarla (index + cache + AsNoTracking):**
- 100–200 RPS kapasitesi
- Yanıt süresi: liste sorguları p95 < 100ms

**Türkiye'deki tipik e-ticaret siteniz için bu yeterli mi?**

| Segment | Günlük Ziyaret | Gerekli Kapasite | Mevcut Mimari |
|---------|---------------|-----------------|---------------|
| Küçük işletme | < 1.000 | < 5 RPS | ✅ Fazlasıyla |
| Orta işletme | 1.000–20.000 | 5–50 RPS | ✅ Seviye 1 ile |
| Büyük işletme | 20.000–200.000 | 50–500 RPS | ✅ Seviye 2–3 ile |
| Mega kampanya | 200.000+ | 500+ RPS | Mimari revizyon |

**Sonuç:** ECSPros'un mevcut mimarisi doğru optimize edildiğinde 100.000 günlük ziyaretçiye kadar **tek sunucu** üzerinde hizmet verebilir. Şu an için Seviye 1 aksiyonlar yeterlidir; diğerleri büyüme ihtiyacı gerçekleşince uygulanabilir.
