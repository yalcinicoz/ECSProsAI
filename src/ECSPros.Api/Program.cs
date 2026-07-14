using Microsoft.AspNetCore.DataProtection;
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

// ─── Storefront (Razor) servisleri ─────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IStoreContext, StoreContext>();
builder.Services.AddScoped<ECSPros.Shared.Contracts.IInStockProductProvider, ECSPros.Api.Services.InStockProductProvider>();
builder.Services.AddSingleton<ECSPros.Api.Services.IStoreMemberSession, ECSPros.Api.Services.StoreMemberSession>(); // D1: SSR üye kimliği (HttpOnly cookie)
builder.Services.AddTransient<ECSPros.Crm.Application.Services.ISmsSender, ECSPros.Api.Services.CrmSmsSenderAdapter>(); // D4: OTP SMS köprüsü
builder.Services.AddScoped<ECSPros.Api.Services.Store.IPageBlockSourceResolver, ECSPros.Api.Services.Store.PageBlockSourceResolver>(); // G3: vitrin ürün/koleksiyon kaynağı motoru
builder.Services.AddScoped<ECSPros.Api.Services.Store.IPageComposer, ECSPros.Api.Services.Store.PageComposer>(); // G4: yerleşim kompozisyonu (store API + Razor ortak)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IVitrinVmBuilder, ECSPros.Api.Services.Store.VitrinVmBuilder>(); // G5: blok → Razor VM (koleksiyon kartı zenginleştirme dahil)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IVisitorSegmentResolver, ECSPros.Api.Services.Store.VisitorSegmentResolver>(); // G9: ziyaretçi segmenti (kural motoru + segment cache girdisi)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IPagePreviewService, ECSPros.Api.Services.Store.PagePreviewService>(); // G12: admin önizleme (taslak + kurgu segment)
builder.Services.AddScoped<ECSPros.Api.Services.Store.IVitrinAuditLogger, ECSPros.Api.Services.Store.VitrinAuditLogger>(); // G13: vitrin değişiklik geçmişi (iam.audit_logs)
builder.Services.AddSingleton<ECSPros.Api.Services.Store.IFaturaPdfProxy, ECSPros.Api.Services.Store.FaturaPdfProxy>(); // H1: entegratör fatura PDF proxy'si (allowlist config'ten)
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

// ─── Swagger ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ECSPros API", Version = "v1" });
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

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECSPros API v1"));

// Storefront statik varlıkları (wwwroot: css/js/ikons/images/video/fontawesome —
// misharix ile aynı kök yollar, partial'lardaki /ikons/... referansları değişmeden çalışır)
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
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

app.Run();
