using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECSPros.Api.EventHandlers;
using ECSPros.Api.Extensions;
using ECSPros.Api.Hubs;
using ECSPros.Api.Middleware;
using ECSPros.Api.Services;
using ECSPros.Shared.Infrastructure;
using ECSPros.Shared.Infrastructure.Behaviors;
using ECSPros.Shared.Infrastructure.Messaging;
using FluentValidation;
using MediatR;
using Serilog;
using ECSPros.Catalog.Application.Queries.GetProducts;
using ECSPros.Catalog.Infrastructure;
using ECSPros.Crm.Application.Queries.GetMembers;
using ECSPros.Inventory.Application.Queries.GetWarehouses;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Cms.Infrastructure;
using ECSPros.Requests.Infrastructure;
using ECSPros.Core.Application.Queries.GetLanguages;
using ECSPros.Core.Infrastructure;
using ECSPros.Crm.Infrastructure;
using ECSPros.Cms.Application.Queries.GetPages;
using ECSPros.Finance.Application.Queries.GetSupplierInvoices;
using ECSPros.Finance.Infrastructure;
using ECSPros.Pos.Application.Queries.GetPosRegisters;
using ECSPros.Promotion.Application.Queries.GetCampaigns;
using ECSPros.Fulfillment.Application.Queries.GetPickingPlans;
using ECSPros.Fulfillment.Infrastructure;
using ECSPros.Integration.Application.Queries.GetIntegrationLogs;
using ECSPros.Integration.Infrastructure;
using ECSPros.Accounts.Application.Queries.GetCurrentAccounts;
using ECSPros.Accounts.Infrastructure;
using ECSPros.Storefront.Application.Queries.GetNavigationMenus;
using ECSPros.Storefront.Infrastructure;
using ECSPros.Iam.Application.Commands.Login;
using ECSPros.Iam.Infrastructure;
using ECSPros.Inventory.Infrastructure;
using ECSPros.Order.Infrastructure;
using ECSPros.Pos.Infrastructure;
using ECSPros.Promotion.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

// ─── Serilog Bootstrap Logger ───────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<HostOptions>(o =>
    o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

// ─── Serilog Full Configuration ─────────────────────────────────────
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ECSPros")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/ecspros-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"));

// ─── NpgsqlDataSource (EnableDynamicJson — Dictionary<string,X> için gerekli) ──
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

var npgsqlDataSource = new NpgsqlDataSourceBuilder(connectionString)
    .EnableDynamicJson()
    .Build();
builder.Services.AddSingleton(npgsqlDataSource);

// ─── Controllers + Storefront Razor Views ──────────────────────────
// Storefront sayfaları da bu host'tan render edilir (Razor taşıma planı 3.1);
// API controller'ları etkilenmez, JSON ayarları aynen geçerli kalır.
// Faz 8 (misharix tasarım sözleşmesi): Production'da dinamik Brotli/Gzip sıkıştırma.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/javascript",
        "application/json",
        "image/svg+xml"
    });
});
// SmallestSize (brotli q11) 700KB'lik SSR HTML'de istek başına ~1.2 sn CPU yakıyordu —
// her menü tıklamasında ziyaretçinin gördüğü ilk tepki gecikmesi buydu (2026-07-30 ölçümü:
// br TTFB 1.24s → Fastest 0.07s; boyut 57KB → 75KB, fark önemsiz). SmallestSize'a geri dönme.
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

var mvcBuilder = builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Dev'de cshtml değişikliği rebuild istemesin
if (builder.Environment.IsDevelopment())
    mvcBuilder.AddRazorRuntimeCompilation();

// Storefront tema çözümü (varsayılan tema kök ~/Views ağacında — bkz. StoreThemeViewLocationExpander)
builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
    options.ViewLocationExpanders.Add(new StoreThemeViewLocationExpander()));

// ─── Süreç-içi cache (facet gibi ağır, nadiren değişen sorgu sonuçları için) ───
builder.Services.AddMemoryCache();

// ─── Data Protection (FirmPlatformIntegration.Credentials at-rest şifreleme) ──
// Key ring publish dizini DIŞINDA kalıcı dizinde tutulur — publish/deploy'da silinirse
// DB'deki şifreli kimlik bilgileri ÇÖZÜLEMEZ (yeniden girilmeleri gerekir). Yol
// DataProtection:KeysPath ile değiştirilebilir; varsayılan ~/.ecspros/dp-keys.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ecspros", "dp-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
    .SetApplicationName("ECSPros");

// ─── MediatR ───────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetLanguagesQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetProductsQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetMembersQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetOrdersQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetWarehousesQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetSupplierInvoicesQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetCampaignsQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetPagesQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetPosRegistersQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetPickingPlansQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetIntegrationLogsQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetCurrentAccountsQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetNavigationMenusQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(ECSPros.Requests.Application.Queries.GetRequests.GetRequestsQuery).Assembly);
    // API katmanındaki SignalR event handler'ları
    cfg.RegisterServicesFromAssembly(typeof(OrderConfirmedSignalRHandler).Assembly);

    // FluentValidation pipeline behavior
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// ─── FluentValidation — register all validators from all assemblies ─
builder.Services.AddValidatorsFromAssembly(typeof(LoginCommand).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(GetOrdersQuery).Assembly);

// ─── Shared Infrastructure (Redis, Email, SMS stubs) ───────────────
builder.Services.AddSharedInfrastructure(builder.Configuration);
// SMTP ayarları DB'deki platform servis tanımından (yoksa Email:Smtp config yedeği)
builder.Services.AddScoped<ECSPros.Shared.Infrastructure.Messaging.ISmtpSettingsProvider,
    ECSPros.Api.Services.DbSmtpSettingsProvider>();
// SMS ayarları DB'deki platform servis tanımından (yoksa log yedeği — GES Telekom)
builder.Services.AddScoped<ECSPros.Shared.Infrastructure.Messaging.ISmsSettingsProvider,
    ECSPros.Api.Services.DbSmsSettingsProvider>();
// H3: Görsel arama ayarları DB'deki visual_search entegrasyonundan (yoksa VisualSearch:* config)
builder.Services.AddScoped<ECSPros.Api.Services.IVisualSearchSettingsProvider,
    ECSPros.Api.Services.DbVisualSearchSettingsProvider>();

// ─── Infrastructure Modules ────────────────────────────────────────
builder.Services.AddIamInfrastructure(npgsqlDataSource, builder.Configuration);
builder.Services.AddCoreInfrastructure(npgsqlDataSource);
builder.Services.AddCatalogInfrastructure(npgsqlDataSource);
builder.Services.AddInventoryInfrastructure(npgsqlDataSource);
builder.Services.AddCrmInfrastructure(npgsqlDataSource);
builder.Services.AddOrderInfrastructure(npgsqlDataSource);
builder.Services.AddFulfillmentInfrastructure(npgsqlDataSource);
builder.Services.AddFinanceInfrastructure(npgsqlDataSource);
builder.Services.AddPromotionInfrastructure(npgsqlDataSource);
builder.Services.AddCmsInfrastructure(npgsqlDataSource);
builder.Services.AddRequestsInfrastructure(npgsqlDataSource);
builder.Services.AddPosInfrastructure(npgsqlDataSource);
builder.Services.AddIntegrationInfrastructure(npgsqlDataSource);
builder.Services.AddAccountsInfrastructure(npgsqlDataSource);
builder.Services.AddStorefrontInfrastructure(npgsqlDataSource);

// ─── SignalR ────────────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddScoped<IRealtimeNotificationService, SignalRNotificationService>();
builder.Services.AddSingleton<ECSPros.Api.Hubs.DashboardPresenceTracker>(); // dashboard'a bağlı istemci sayacı — worker kimse yokken sorgu atmaz

// ─── Storefront (Razor) servisleri ─────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IStoreContext, StoreContext>();
builder.Services.AddScoped<ECSPros.Api.Services.StoreUrunDetayBuilder>(); // ürün detay VM (hem /urun/{code} hem gerçek slug URL'i kullanır)
builder.Services.AddSingleton<ECSPros.Api.Services.Store.VitrinSrcsetSaglayici>(); // vitrin görsel srcset üretimi (A fazı — varyant varsa basılır)
builder.Services.AddScoped<ECSPros.Shared.Contracts.IInStockProductProvider, ECSPros.Api.Services.InStockProductProvider>();
// H10: vitrin "indirimli ürünler" kaynak bayrağı — kanalda CompareAtPrice > Price olan ürün kümesi
builder.Services.AddScoped<ECSPros.Shared.Contracts.IDiscountedProductProvider, ECSPros.Api.Services.DiscountedProductProvider>();
// B-005/006: genel liste fiyat sıralaması gösterilen (efektif) fiyattan — kanal override → BasePrice
builder.Services.AddScoped<ECSPros.Shared.Contracts.IEffectivePriceProvider, ECSPros.Api.Services.EffectivePriceProvider>();
builder.Services.AddScoped<ECSPros.Api.Services.Marketplace.MarketplaceAdminService>(); // Pazaryeri yönetim ekranları — cross-schema okuma katmanı

// Pazaryeri referans verisi (marketplace_ref ayrı DB): kategori/özellik/değer senkronu.
// Hata-güvenli: DB yapılandırılmamışsa/erişilemiyorsa yalnız referans uçları anlaşılır hata döner.
builder.Services.AddSingleton<ECSPros.Api.Services.Marketplace.Reference.MarketplaceRefDb>();
builder.Services.AddSingleton<ECSPros.Api.Services.Marketplace.Reference.IMarketplaceReferenceDownloader,
    ECSPros.Api.Services.Marketplace.Reference.TrendyolReferenceDownloader>();
builder.Services.AddSingleton<ECSPros.Api.Services.Marketplace.Reference.MarketplaceReferenceSyncService>();

// Pazaryeri eşleme katmanı (F2): grup→kategori, özellik, değer eşlemeleri + sağlık job'ı.
builder.Services.AddScoped<ECSPros.Api.Services.Marketplace.Mapping.MarketplaceMappingService>();
builder.Services.AddSingleton<ECSPros.Api.Services.Marketplace.Mapping.MappingHealthService>();

// Pazaryeri hazırlık denetimi + tamamlama (F3): readiness motoru + eksik bilgi tamamlama.
builder.Services.AddSingleton<ECSPros.Api.Services.Marketplace.Mapping.MarketplaceReadinessService>();
builder.Services.AddScoped<ECSPros.Api.Services.Marketplace.Mapping.MarketplaceCompletionService>();

// Gerçek pazaryeri gönderimi + batch takibi (F4): Trendyol istemcisi, gönderim servisi,
// hata sınıflandırıcı ve sonuç sorgulama worker'ı (worker hem hosted hem controller'dan
// elle tetiklenebilir — tek örnek).
builder.Services.AddScoped<ECSPros.Api.Services.Marketplace.Send.TrendyolSellerClient>();
builder.Services.AddSingleton<ECSPros.Api.Services.Marketplace.Send.MarketplaceErrorClassifier>();
builder.Services.AddScoped<ECSPros.Api.Services.Marketplace.Send.MarketplaceSendService>();
builder.Services.AddSingleton<ECSPros.Api.Services.Marketplace.Send.MarketplaceBatchWorker>();
builder.Services.AddScoped<ECSPros.Api.Services.Marketplace.Send.MarketplaceIssueService>();
builder.Services.AddScoped<ECSPros.Api.Services.Marketplace.Send.MarketplaceReconciliationService>();
builder.Services.AddHostedService(sp =>
    sp.GetRequiredService<ECSPros.Api.Services.Marketplace.Send.MarketplaceBatchWorker>());
builder.Services.AddSingleton<ECSPros.Api.Services.IStoreMemberSession, ECSPros.Api.Services.StoreMemberSession>(); // D1: SSR üye kimliği (HttpOnly cookie)
builder.Services.AddTransient<ECSPros.Crm.Application.Services.ISmsSender, ECSPros.Api.Services.CrmSmsSenderAdapter>(); // D4: OTP SMS köprüsü
builder.Services.AddScoped<ECSPros.Api.Services.Store.IPageBlockSourceResolver, ECSPros.Api.Services.Store.PageBlockSourceResolver>(); // G3: vitrin ürün/koleksiyon kaynağı motoru
builder.Services.AddScoped<ECSPros.Api.Services.Store.IPageComposer, ECSPros.Api.Services.Store.PageComposer>(); // G4: yerleşim kompozisyonu (store API + Razor ortak)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IVitrinVmBuilder, ECSPros.Api.Services.Store.VitrinVmBuilder>(); // G5: blok → Razor VM (koleksiyon kartı zenginleştirme dahil)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IVisitorSegmentResolver, ECSPros.Api.Services.Store.VisitorSegmentResolver>(); // G9: ziyaretçi segmenti (kural motoru + segment cache girdisi)
builder.Services.AddSingleton<ECSPros.Api.Services.Store.IGeoIpCityResolver, ECSPros.Api.Services.Store.GeoIpCityResolver>(); // H10: GeoLite2 IP→il halkası (mmdb yoksa devre dışı, hata-güvenli)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IPagePreviewService, ECSPros.Api.Services.Store.PagePreviewService>(); // G12: admin önizleme (taslak + kurgu segment)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IVitrinAuditLogger, ECSPros.Api.Services.Store.VitrinAuditLogger>(); // G13: vitrin değişiklik geçmişi (iam.audit_logs)
builder.Services.AddSingleton<ECSPros.Api.Services.Store.IFaturaPdfProxy, ECSPros.Api.Services.Store.FaturaPdfProxy>(); // H1: entegratör fatura PDF proxy'si (allowlist config'ten)
// Mobil cihaz doğrulama (2026-07-23): Play Integrity / App Attest → kısa ömürlü device token
builder.Services.AddSingleton<ECSPros.Api.Services.Store.IDeviceTokenService, ECSPros.Api.Services.Store.DeviceTokenService>();
// Part B: eski sistem (juludedb) köprüsü — hata-güvenli, connection string boşsa no-op
builder.Services.AddSingleton<ECSPros.Api.Services.Legacy.ILegacyGateway, ECSPros.Api.Services.Legacy.LegacyGateway>();
builder.Services.AddSingleton<ECSPros.Api.Services.Store.IDeviceAttestationVerifier, ECSPros.Api.Services.Store.PlayIntegrityVerifier>();
builder.Services.AddSingleton<ECSPros.Api.Services.Store.IDeviceAttestationVerifier, ECSPros.Api.Services.Store.AppAttestVerifier>();
builder.Services.AddSingleton<ECSPros.Api.Services.Store.IDeviceAttestationVerifier, ECSPros.Api.Services.Store.DevBypassVerifier>();
builder.Services.AddScoped<ECSPros.Api.Services.Store.IStoreLinkBuilder, ECSPros.Api.Services.Store.StoreLinkBuilder>(); // H8: e-postalardaki mutlak site linki (Store:Hosts tersinden)
builder.Services.AddScoped<ECSPros.Api.Services.Store.ISavedSearchNotifier, ECSPros.Api.Services.Store.SavedSearchNotifier>(); // H8: favori arama bildirimi taraması
builder.Services.AddHostedService<ECSPros.Api.Services.Store.SavedSearchNotifyWorker>(); // H8: periyodik tarama (günde-1 sınırı LastNotifiedAt'ta)
builder.Services.AddHostedService<DashboardMetricsWorker>();
builder.Services.AddSingleton<ECSPros.Api.Services.MigrationService>();

// ─── JWT Authentication ────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // "sub" → User.FindFirst("sub") olarak kalır
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
        // SignalR WebSocket handshake'te Authorization header gönderilemez;
        // token ?access_token=<jwt> query parametresiyle iletilir.
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(token) &&
                    (path.StartsWithSegments("/hubs/fulfillment") ||
                     path.StartsWithSegments("/hubs/notifications") ||
                     path.StartsWithSegments("/hubs/dashboard")))
                {
                    ctx.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MemberOnly", policy =>
        policy.RequireClaim("type", "member"));

    // Pazaryeri satıcı paneli (satici/) — yalnız SupplierUser (type=supplier_user) geçer.
    options.AddPolicy("SupplierOnly", policy =>
        policy.RequireClaim("type", "supplier_user"));

    // F0 — Kimlik sınırı: makine/üye kimlikleri düz [Authorize] yönetim uçlarını GEÇEMEZ.
    // Üye token'ı (type=member) ve API hesabı token'ı (type=api_client), admin token'ıyla aynı
    // Jwt:Secret ile imzalandığından yalnız "type" claim'i ayrımı güvenlik sınırıdır.
    // Personel/admin token'ında "type" claim'i YOKTUR → politikayı geçer.
    // API hesapları yalnız kendi RequireScope uçlarına erişir (varsayılanı da geçemez).
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx => !ctx.User.HasClaim(c =>
                  c.Type == "type" && (c.Value == "member" || c.Value == "api_client" || c.Value == "supplier_user"))));

    // Partner API "whoami" gibi scope gerektirmeyen ama YALNIZ API hesabına açık uçlar için.
    options.AddPolicy("ApiClientOnly", policy =>
        policy.RequireClaim("type", "api_client"));

    // Politika belirtilmeyen tüm [Authorize] uçları için varsayılan = AdminOnly.
    // [AllowAnonymous] ve attribute'suz (anonim gezinme) uçlar etkilenmez;
    // [Authorize(Policy="MemberOnly")] ve RequirePermission kendi kurallarını korur.
    options.DefaultPolicy = options.GetPolicy("AdminOnly")!;
});

// ─── CORS ──────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// ─── Rate Limiting (2026-07-23) ────────────────────────────────────
// Anonim uçlara brute-force/istismar freni. Yalnız [EnableRateLimiting] konan uçlarda
// çalışır (global limit YOK — SSR sayfaları ve diğer uçlar etkilenmez). IP bazlı hacim
// freninin asıl katmanı nginx'tedir (00-ratelimit.conf); buradaki politika, nginx'i
// atlayıp 5000 portuna doğrudan gelen istekleri de yakalayan ikinci savunma hattıdır.
static string IstemciIpAnahtari(HttpContext ctx)
{
    // Cloudflare → nginx → app zincirinde gerçek istemci IP'si CF-Connecting-IP'dedir;
    // yoksa nginx'in yazdığı X-Forwarded-For'un ilk halkası; o da yoksa soket adresi.
    var cf = ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(cf)) return cf.Trim();
    var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(xff)) return xff.Split(',')[0].Trim();
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen";
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync(
            """{"success":false,"error":"Çok fazla istek gönderildi. Lütfen biraz bekleyip tekrar deneyin."}""", ct);
    };
    // Kimlik uçları (login/register/otp/refresh): IP başına dakikada 60 —
    // CGNAT arkasındaki meşru kalabalığa pay bırakır, brute-force'u anlamsızlaştırır.
    options.AddPolicy("store-auth", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            IstemciIpAnahtari(ctx),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    // Hassas anonim uçlar (kupon doğrulama = kod taraması, görsel arama = ücretli dış servis):
    options.AddPolicy("store-sensitive", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            IstemciIpAnahtari(ctx),
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ─── Swagger ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Partner API dokümanı — yalnız /api/partner/* uçları; prod'da da açık (dış entegratörler için).
    c.SwaggerDoc("partner", new OpenApiInfo { Title = "ECSPros Partner API", Version = "v1" });
    // Mobil API dokümanı — kendi satış kanalımız: web sitesinin kullandığı /api/store/* yüzeyinin
    // tamamı + görsel arama. Partner'dan ayrı doküman, prod'da da açık (mobil ekip için).
    // Bu uçlar zaten sitenin JS'inden herkese açık çağrılıyor — doküman gizli yüzey sızdırmaz.
    c.SwaggerDoc("mobile", new OpenApiInfo
    {
        Title = "ECSPros Mobil API (Store)",
        Version = "v1",
        Description = "Mobil uygulamanın kullandığı vitrin servisleri — web sitesiyle birebir aynı yüzey. Rehber: docs/mobil-api-referansi.md",
    });
    // İç API dokümanı — yalnız Development'ta ÜRETİLİR (prod'da /swagger/v1 = 404, iç yüzey gizli).
    if (builder.Environment.IsDevelopment())
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "ECSPros API (iç)", Version = "v1" });

    // Uçları doğru dokümana ayır: partner yalnız api/partner/*, mobile yalnız store yüzeyi
    // (api/store/* + gorsel-arama; api/store-notifications iç uçtur, bilerek dışarıda),
    // iç doküman partner dışındaki her şey.
    c.DocInclusionPredicate((docName, api) =>
    {
        var isPartner = api.RelativePath?.StartsWith("api/partner", StringComparison.OrdinalIgnoreCase) == true;
        var isStore = api.RelativePath?.StartsWith("api/store/", StringComparison.OrdinalIgnoreCase) == true
                      || api.RelativePath?.StartsWith("gorsel-arama", StringComparison.OrdinalIgnoreCase) == true;
        return docName switch
        {
            "partner" => isPartner,
            "mobile" => isStore,
            _ => !isPartner,
        };
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token giriniz. Örnek: Bearer {token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ─── Middleware Pipeline ────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

// Üç swagger dokümanı, her biri BAĞIMSIZ adreste (arayüzde doküman seçici yok):
//   /swagger-partner → "partner" (dış entegratörler, yalnız /api/partner/*; prod'da açık)
//   /swagger-mobile  → "mobile" (kendi mobil kanalımız, yalnız /api/store/* + gorsel-arama; prod'da açık)
//   /swagger         → "v1" (iç) — yalnız Development'ta ÜRETİLİR ve servis edilir;
//                      prod'da /swagger ve /swagger/v1/swagger.json = 404 (iç yüzey gizli kalır).
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger-partner";
    c.SwaggerEndpoint("/swagger/partner/swagger.json", "ECSPros Partner API");
    c.DocumentTitle = "ECSPros Partner API";
});
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "swagger-mobile";
    c.SwaggerEndpoint("/swagger/mobile/swagger.json", "ECSPros Mobil API (Store)");
    c.DocumentTitle = "ECSPros Mobil API (Store)";
});
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECSPros API (iç)");
        c.DocumentTitle = "ECSPros API (iç)";
    });
}

// Faz 8 (misharix tasarım sözleşmesi): sürüm sorgulu (?v=) CSS/JS, performans görselleri
// ve Font Awesome dosyalarına 1 yıl immutable cache; HTML'e no-cache (tarayıcı bayat
// vitrin HTML'i tutmaz — sunucu tarafı versiyonlu vitrin cache'i ayrıdır).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var response = context.Response;
        var requestPath = context.Request.Path.Value ?? string.Empty;
        var contentType = response.ContentType ?? string.Empty;
        var surumluStatikDosya = context.Request.Query.ContainsKey("v")
            || requestPath.StartsWith("/images/performance/", StringComparison.OrdinalIgnoreCase)
            || requestPath.StartsWith("/fontawesome-free-7.2.0-web/", StringComparison.OrdinalIgnoreCase);

        if (response.StatusCode == StatusCodes.Status200OK && surumluStatikDosya)
        {
            response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else if (response.StatusCode == StatusCodes.Status200OK
            && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            response.Headers.CacheControl = "no-cache,no-store,must-revalidate";
            response.Headers.Pragma = "no-cache";
            response.Headers.Expires = "0";
        }

        return Task.CompletedTask;
    });

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}

// Storefront statik varlıkları (wwwroot: css/js/ikons/images/video/fontawesome —
// misharix ile aynı kök yollar, partial'lardaki /ikons/... referansları değişmeden çalışır)
app.UseStaticFiles(new StaticFileOptions
{
    // Layout'taki css/js referansları asp-append-version'lı (?v=hash) — içerik değişince
    // URL değişir, uzun önbellek güvenlidir. Önceden hiç Cache-Control gönderilmiyordu;
    // her sayfa geçişinde tarayıcı tüm asset'leri yeniden doğruluyordu (yavaş ilk tepki).
    OnPrepareResponse = ctx =>
    {
        var uzanti = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        var versiyonlu = ctx.Context.Request.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = uzanti switch
        {
            ".css" or ".js" when versiyonlu => "public, max-age=31536000, immutable",
            ".woff2" or ".woff" or ".ttf" => "public, max-age=2592000",           // fontlar — 30 gün
            ".css" or ".js" => "public, max-age=86400",                            // sürümsüz referanslar — 1 gün
            _ => "public, max-age=604800",                                         // görsel/ikon vb. — 7 gün
        };
    }
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
// Device token imza/replay denetimi + (config ile) vitrin kapısı — kimlik çözüldükten sonra
app.UseMiddleware<ECSPros.Api.Middleware.DeviceRequestGuardMiddleware>();
app.UseAuthorization();
app.MapControllers();

// ─── SignalR Hubs ───────────────────────────────────────────────────
app.MapHub<FulfillmentHub>("/hubs/fulfillment");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<DashboardHub>("/hubs/dashboard");

// Permission/rol ve dil seed'i her ortamda çalışır (idempotent — eksik kayıtları ekler)
await DatabaseSeeder.SeedPermissionsAndRolesAsync(app.Services);
await DatabaseSeeder.SeedLanguagesAsync(app.Services);

// Temel sistem seed'i her ortamda çalışır
await DatabaseSeeder.SeedAsync(app.Services);

// Demo veri seed'i — sadece Development ortamında çalışır (production'da crash'e neden olur)
if (app.Environment.IsDevelopment())
    await DemoDataSeeder.SeedAsync(app.Services);

// GeoIP durum satırı — singleton'ı açılışta bir kez kurup AKTİF/KAPALI log'unu bastırır
// (Redis drift detektörü kalıbı; dosya yoksa yalnız uyarı loglanır, açılış etkilenmez).
app.Services.GetRequiredService<ECSPros.Api.Services.Store.IGeoIpCityResolver>();

// ─── Redis cache durum kontrolü (drift detektörü) ──────────────────
// Her açılışta Redis'e bir yaz-oku denemesi yapıp sonucu loglar. Böylece şifre/ayar
// drift'i (compose ↔ appsettings uyumsuzluğu vb.) gizemli yavaşlık olarak değil,
// deploy anında journalctl'de tek satır olarak görünür. Arka planda çalışır — Redis
// kapalıysa bile açılışı geciktirmez/bozamaz (ICacheService hata-güvenli).
_ = Task.Run(async () =>
{
    try
    {
        var cache = app.Services.GetRequiredService<ECSPros.Shared.Contracts.ICacheService>();
        if (cache is ECSPros.Shared.Infrastructure.Caching.NoOpCacheService)
        {
            app.Logger.LogWarning("Redis cache: YAPILANDIRILMAMIŞ (ConnectionStrings:Redis yok) — site cache'siz çalışıyor.");
            return;
        }

        var sentinel = Guid.NewGuid().ToString("N");
        await cache.SetAsync("startup:redis-check", sentinel, TimeSpan.FromMinutes(1));
        var okunan = await cache.GetAsync<string>("startup:redis-check");

        if (okunan == sentinel)
            app.Logger.LogInformation("Redis cache: AKTİF ✓ (yaz-oku doğrulandı)");
        else
            app.Logger.LogWarning("Redis cache: ERİŞİLEMİYOR — site cache'siz çalışıyor. Muhtemel neden: şifre/ayar drift'i (docker-compose ↔ appsettings.Production.json) ya da container yeniden oluşturulmadı ('docker compose restart' requirepass değişikliğini UYGULAMAZ, 'docker compose up -d redis' gerekir).");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Redis cache durum kontrolü tamamlanamadı.");
    }
});

// ─── Pazaryeri referans DB durum kontrolü (Redis kalıbı) ───────────
// marketplace_ref ayrı DB'dir; erişim durumu deploy anında tek satır log olarak görünsün.
_ = Task.Run(async () =>
{
    try
    {
        var refDb = app.Services.GetRequiredService<ECSPros.Api.Services.Marketplace.Reference.MarketplaceRefDb>();
        if (!refDb.IsConfigured)
        {
            app.Logger.LogWarning("Pazaryeri referans DB: YAPILANDIRILMAMIŞ (ConnectionStrings:MarketplaceRef yok) — referans senkron uçları kapalı.");
            return;
        }
        var ds = await refDb.GetAsync();
        if (ds is not null)
            app.Logger.LogInformation("Pazaryeri referans DB: AKTİF ✓ (marketplace_ref şema hazır)");
        else
            app.Logger.LogWarning("Pazaryeri referans DB: ERİŞİLEMİYOR — referans senkron uçları hata dönecek.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Pazaryeri referans DB durum kontrolü tamamlanamadı.");
    }
});

app.Run();
