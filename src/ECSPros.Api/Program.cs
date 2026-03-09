using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECSPros.Api.Extensions;
using ECSPros.Api.Middleware;
using ECSPros.Catalog.Application.Queries.GetCategories;
using ECSPros.Catalog.Infrastructure;
using ECSPros.Crm.Application.Queries.GetMembers;
using ECSPros.Inventory.Application.Queries.GetWarehouses;
using ECSPros.Order.Application.Queries.GetOrders;
using ECSPros.Cms.Infrastructure;
using ECSPros.Core.Application.Queries.GetLanguages;
using ECSPros.Core.Infrastructure;
using ECSPros.Crm.Infrastructure;
using ECSPros.Finance.Infrastructure;
using ECSPros.Fulfillment.Infrastructure;
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

var builder = WebApplication.CreateBuilder(args);

// ─── NpgsqlDataSource (EnableDynamicJson — Dictionary<string,X> için gerekli) ──
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

var npgsqlDataSource = new NpgsqlDataSourceBuilder(connectionString)
    .EnableDynamicJson()
    .Build();
builder.Services.AddSingleton(npgsqlDataSource);

// ─── Controllers ───────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ─── MediatR ───────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetLanguagesQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetCategoriesQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetMembersQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetOrdersQuery).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(GetWarehousesQuery).Assembly);
});

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

// ─── JWT Authentication ────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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
    });

builder.Services.AddAuthorization();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ECSPros API v1"));
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
    await DatabaseSeeder.SeedAsync(app.Services);

app.Run();
