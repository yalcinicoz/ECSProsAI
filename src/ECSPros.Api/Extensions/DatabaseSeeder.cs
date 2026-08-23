using ECSPros.Catalog.Domain.Entities;
using ECSPros.Catalog.Infrastructure.Persistence;
using ECSPros.Core.Domain.Entities;
using ECSPros.Core.Infrastructure.Persistence;
using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Iam.Infrastructure.Persistence;
using ECSPros.Shared.Kernel.Authorization;
using ECSPros.Storefront.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Api.Extensions;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        await SeedIamAsync(scope.ServiceProvider);
        await SeedCoreAsync(scope.ServiceProvider);
        await SeedCatalogAsync(scope.ServiceProvider);
        await SeedPlatformTypesAsync(services);
        await SeedStorefrontDefaultsAsync(scope.ServiceProvider);
        await SeedCrmDefaultsAsync(scope.ServiceProvider);
        await SeedGeoAsync(scope.ServiceProvider);
        await SeedCmsLegalPagesAsync(scope.ServiceProvider);
        await SeedReturnReasonsAsync(scope.ServiceProvider);
        await SeedCorporatePagesAsync(scope.ServiceProvider);
        await SeedFaqPageAsync(scope.ServiceProvider);
        await SeedDefaultVitrinAsync(scope.ServiceProvider);
        await SeedTelemaniaVitrinAsync(scope.ServiceProvider);
        await SeedCargoCarriersAsync(scope.ServiceProvider);
        await SeedPlatformServiceCatalogAsync(scope.ServiceProvider);
        await SeedCampaignTypesAsync(scope.ServiceProvider);
    }

    /// <summary>
    /// P3: Kampanya tipleri — CampaignEngine'deki işleyicilerle birebir eşleşir
    /// (kod eklemek yeni tip yaratmaz; engine'de karşılığı olmalı). Kod bazlı idempotent.
    /// </summary>
    // Kampanya tipi ŞABLONU alan yardımcıları (docs/kampanya-tip-sablonlari-taslak.md).
    private static ECSPros.Promotion.Domain.Entities.CampaignSchemaFieldOption Opt(string value, string tr) =>
        new() { Value = value, LabelI18n = new() { ["tr"] = tr } };

    private static ECSPros.Promotion.Domain.Entities.CampaignSchemaField SFld(
        string key, string trLabel, string type, bool required = false, string? unit = null,
        List<ECSPros.Promotion.Domain.Entities.CampaignSchemaFieldOption>? options = null,
        ECSPros.Promotion.Domain.Entities.CampaignSchemaFieldCondition? visibleWhen = null,
        decimal? min = null, decimal? max = null, object? def = null, string? help = null) =>
        new()
        {
            Key = key, LabelI18n = new() { ["tr"] = trLabel }, Type = type, Required = required,
            Unit = unit, Options = options, VisibleWhen = visibleWhen, Min = min, Max = max, Default = def,
            HelpI18n = help is null ? null : new() { ["tr"] = help }
        };

    private static ECSPros.Promotion.Domain.Entities.CampaignSchemaFieldCondition WhenNot(string field, string val) =>
        new() { Field = field, NotEqualsValue = val };
    private static ECSPros.Promotion.Domain.Entities.CampaignSchemaFieldCondition WhenEq(string field, string val) =>
        new() { Field = field, EqualsValue = val };

    /// <summary>Kampanya tipleri = definition katmanı (platformdan bağımsız). 6 birleştirilmiş
    /// parametrik tip, SettingsSchema şablonlarıyla (admin kampanya formu bundan üretilir).
    /// Idempotent: mevcut tip güncellenir, eksik eklenir, eski (birleştirilen) tipler pasifleştirilir.</summary>
    private static async Task SeedCampaignTypesAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<ECSPros.Promotion.Infrastructure.Persistence.PromotionDbContext>();
        var O = new Func<string, string, ECSPros.Promotion.Domain.Entities.CampaignSchemaFieldOption>(Opt);

        var tipler = new (string Kod, string Ad, string Aciklama, string Scope, bool UrunIster,
            bool FiyatGoster, bool Birlesir, int Sira, List<ECSPros.Promotion.Domain.Entities.CampaignSchemaField> Sema)[]
        {
            ("discount", "İndirim (Kapsam+Koşul+Fayda)",
             "Sepet ya da kapsamdaki ürünlere yüzde/tutar indirim; opsiyonel eşik koşuluyla.",
             "product", true, true, false, 1, new()
             {
                 SFld("applyTo", "İndirim nereye", "select", true, def: "selected", options: new() {
                     O("cart", "Sepet toplamına"), O("selected", "Kapsamdaki ürünlere") },
                     help: "Kapsam = kampanyaya ilişkili ürünler (tümü/filtre/manuel)."),
                 SFld("conditionType", "Koşul (eşik)", "select", true, def: "none", options: new() {
                     O("none", "Koşulsuz"), O("cartAmount", "Sepet tutarı ≥"), O("cartQty", "Sepet adedi ≥"),
                     O("scopeAmount", "Kapsam tutarı ≥"), O("scopeQty", "Kapsam adedi ≥") }),
                 SFld("conditionValue", "Eşik değeri", "number", true, min: 0, visibleWhen: WhenNot("conditionType", "none")),
                 SFld("benefitType", "İndirim şekli", "select", true, def: "percent", options: new() {
                     O("percent", "Yüzde (%)"), O("amount", "Tutar (₺)") }),
                 SFld("benefitValue", "İndirim değeri", "number", true, min: 0),
                 SFld("maxDiscountAmount", "En çok indirim (₺)", "money", false, min: 0,
                     visibleWhen: WhenEq("benefitType", "percent"), help: "Yüzde indirimde tavan tutar (opsiyonel)."),
             }),

            ("buy_x_get_y", "Al X, Y Bedava/İndirimli",
             "Her X+Y adetlik grupta Y adet bedava/indirimli (1 alana 1 bedava, 3 al 2 öde, ikincisi %50).",
             "product", true, false, false, 2, new()
             {
                 SFld("buyQuantity", "Tam fiyat ödenecek adet (X)", "integer", true, "adet", min: 1,
                     help: "Örn. 3 al 2 öde → X=2, Y=1. 1 alana 1 bedava → X=1, Y=1."),
                 SFld("getQuantity", "İndirimli/bedava adet (Y)", "integer", true, "adet", min: 1),
                 SFld("getBenefitType", "Y ürünlerine uygulanan", "select", true, def: "free", options: new() {
                     O("free", "Bedava (%100)"), O("percent", "Yüzde indirim"), O("amount", "Sabit fiyat/tutar") }),
                 SFld("getBenefitValue", "Y indirim değeri", "number", true, min: 0, visibleWhen: WhenNot("getBenefitType", "free")),
                 SFld("sameProduct", "Aynı üründen olmalı", "boolean", def: true),
                 SFld("cheapestGetsBenefit", "En ucuz olan indirimli", "boolean", def: true),
             }),

            ("cross_group_gift", "Grup Al → Grup Hediye/İndirimli",
             "A grubundan alım koşulu sağlanınca B (hediye) grubundan ürün bedava/indirimli.",
             "product", true, false, false, 3, new()
             {
                 SFld("buyThresholdType", "Alım koşulu", "select", true, def: "qty", options: new() {
                     O("qty", "Adet ≥"), O("amount", "Tutar ≥") }),
                 SFld("buyThresholdValue", "Alım eşiği", "number", true, min: 1),
                 SFld("giftQuantity", "Hediye/indirimli adet", "integer", true, "adet", min: 1),
                 SFld("giftBenefitType", "Hediye grubuna uygulanan", "select", true, def: "free", options: new() {
                     O("free", "Bedava"), O("percent", "Yüzde"), O("amount", "Tutar") }),
                 SFld("giftBenefitValue", "Hediye indirim değeri", "number", false, min: 0, visibleWhen: WhenNot("giftBenefitType", "free")),
             }),

            ("bundle", "Kombin İndirimi",
             "Belirli ürünler birlikte (kombin/takım) alınınca özel paket fiyatı.",
             "product", true, false, false, 4, new()
             {
                 SFld("minBundleItems", "Kombin minimum ürün", "integer", true, "adet", min: 2),
                 SFld("bundleBenefitType", "Kombin fiyatı", "select", true, def: "percent", options: new() {
                     O("fixedPrice", "Sabit paket fiyatı"), O("percent", "Yüzde indirim"), O("amount", "Tutar indirim") }),
                 SFld("bundleBenefitValue", "Kombin değeri", "number", true, min: 0),
             }),

            ("free_shipping", "Kargo Kampanyası",
             "Sepet eşiği/ödeme yöntemine göre ücretsiz veya indirimli kargo.",
             "shipping", false, false, true, 5, new()
             {
                 SFld("thresholdType", "Koşul", "select", true, def: "none", options: new() {
                     O("none", "Koşulsuz"), O("cartAmount", "Sepet tutarı ≥") }),
                 SFld("thresholdValue", "Sepet eşiği (₺)", "money", true, min: 0, visibleWhen: WhenEq("thresholdType", "cartAmount")),
                 SFld("paymentMethods", "Ödeme yöntemi kısıtı", "select", false, def: "all", options: new() {
                     O("all", "Tümü"), O("credit_card", "Kredi kartı") }),
                 SFld("coverage", "Kargo indirimi", "select", true, def: "full", options: new() {
                     O("full", "Ücretsiz"), O("percent", "Yüzde"), O("amount", "Tutar") }),
                 SFld("coverageValue", "İndirim değeri", "number", false, min: 0, visibleWhen: WhenNot("coverage", "full")),
             }),

            ("review_reward", "Resimli Yorum Kampanyası",
             "Fotoğraflı yorum yapan üyeye ödül (kupon/indirim). Tetiği satın alma değildir.",
             "member", false, false, true, 6, new()
             {
                 SFld("benefitType", "Ödül", "select", true, def: "coupon", options: new() {
                     O("coupon", "Kupon kodu"), O("percent", "Sonraki alışverişe %"), O("amount", "Sonraki alışverişe ₺") }),
                 SFld("benefitValue", "Ödül değeri", "number", true, min: 0),
             }),
        };

        var mevcut = await context.CampaignTypes.IgnoreQueryFilters().ToListAsync();
        int eklenen = 0, guncellenen = 0;
        foreach (var t in tipler)
        {
            var e = mevcut.FirstOrDefault(x => x.Code == t.Kod);
            if (e is null)
            {
                context.CampaignTypes.Add(new ECSPros.Promotion.Domain.Entities.CampaignType
                {
                    Code = t.Kod, NameI18n = new() { ["tr"] = t.Ad }, DescriptionI18n = new() { ["tr"] = t.Aciklama },
                    HandlerClass = $"CampaignEngine.{t.Kod}", Scope = t.Scope, RequiresProducts = t.UrunIster,
                    ProductPriceDisplay = t.FiyatGoster, IsStackable = t.Birlesir, IsActive = true,
                    SortOrder = t.Sira, SettingsSchema = t.Sema
                });
                eklenen++;
            }
            else
            {
                e.NameI18n = new() { ["tr"] = t.Ad }; e.DescriptionI18n = new() { ["tr"] = t.Aciklama };
                e.HandlerClass = $"CampaignEngine.{t.Kod}"; e.Scope = t.Scope; e.RequiresProducts = t.UrunIster;
                e.ProductPriceDisplay = t.FiyatGoster; e.IsStackable = t.Birlesir; e.IsActive = true;
                e.SortOrder = t.Sira; e.SettingsSchema = t.Sema; e.IsDeleted = false;
                guncellenen++;
            }
        }

        // Birleştirilen eski tipler (discount altında toplandı) — pasifleştir (0 kampanya, veri kaybı yok).
        var eskiKodlar = new[] { "percentage_discount", "fixed_discount", "min_cart_discount" };
        int pasif = 0;
        foreach (var e in mevcut.Where(x => eskiKodlar.Contains(x.Code) && x.IsActive))
        {
            e.IsActive = false; pasif++;
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: kampanya tipleri — {eklenen} eklendi, {guncellenen} güncellendi, {pasif} eski tip pasifleştirildi.");
    }

    /// <summary>
    /// Platform servisleri kataloğu — SMTP (email) + görsel arama (visual_search)
    /// IntegrationService satırları. Kimlik bilgileri buraya DEĞİL, admin firma detayından
    /// açılan FirmPlatformIntegration kaydına girilir (Credentials şifreli); SettingsSchema
    /// admin formunun alanlarını tanımlar (PlatformSchemaField listesi, camelCase JSON:
    /// section=credentials → şifreli Credentials'a, settings → Settings jsonb'sine).
    /// Kod bazlı idempotent — var olana dokunmaz (admin'in şema düzenlemesi ezilmez).
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions SemaJsonAyar = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static PlatformSchemaField Alan(string key, string etiket, string tip, string bolum, bool zorunlu = false, string? yardim = null) =>
        new()
        {
            Key = key, LabelI18n = new() { ["tr"] = etiket }, Type = tip, Section = bolum, Required = zorunlu,
            HelpI18n = yardim is null ? null : new() { ["tr"] = yardim }
        };

    /// <summary>Reklam/analytics takip servisi tipleri (İE-1) — TrackingSettingsProvider
    /// ile birebir aynı küme; backfill bu tiplerde eksik şema alanı ekler.</summary>
    private static readonly string[] TakipServisTipleri =
    {
        "analytics", "tag_manager", "ads", "merchant", "search_console",
        "meta", "tiktok", "pinterest", "microsoft_ads", "clarity"
    };

    /// <summary>Takip servisi şeması: verilen alanlar + ortak `ownership` alanı
    /// (customer | platform — hesap sahipliği, karar §7-10; davranışı değiştirmez).</summary>
    private static List<PlatformSchemaField> TakipSemasi(params PlatformSchemaField[] alanlar)
    {
        var sema = new List<PlatformSchemaField>(alanlar)
        {
            Alan("ownership", "Hesap sahipliği (customer | platform)", "text", "settings",
                yardim: "customer: hesap müşteri firmanın kendisine ait (varsayılan). platform: hesap ECSPros " +
                        "tarafından açılmış alt mülk/pixel/Merchant hesabı. Yalnız bilgi/raporlama amaçlıdır.")
        };
        return sema;
    }

    private static async Task SeedPlatformServiceCatalogAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        var servisler = new (string Kod, string Ad, string Tip, List<PlatformSchemaField> Sema)[]
        {
            ("smtp", "SMTP E-Posta", "email", new List<PlatformSchemaField>
            {
                Alan("host",     "Sunucu",       "text",     "settings", zorunlu: true),
                Alan("port",     "Port",         "number",   "settings"),
                Alan("user",     "Kullanıcı",    "text",     "credentials"),
                Alan("password", "Şifre",        "password", "credentials"),
                Alan("from",     "Gönderen",     "text",     "settings"),
                Alan("fromName", "Gönderen Adı", "text",     "settings"),
                Alan("useSsl",   "SSL",          "boolean",  "settings")
            }),
            ("visual_search", "Görsel Arama", "visual_search", new List<PlatformSchemaField>
            {
                Alan("apiUrl", "API Adresi",   "text",     "settings",    zorunlu: true),
                Alan("apiKey", "API Anahtarı", "password", "credentials", zorunlu: true)
            }),
            // GES Telekom (TT Mesaj) — restapi.ttmesaj.com; alan adları GesTelekomSmsService/
            // DbSmsSettingsProvider'ın okuduğu anahtarlarla birebir aynı olmalı.
            ("gestelekom", "GES Telekom SMS", "sms", new List<PlatformSchemaField>
            {
                Alan("apiUrl",       "API Adresi (boşsa restapi.ttmesaj.com)", "text",     "settings"),
                Alan("username",     "API Kullanıcı Adı",                      "text",     "credentials", zorunlu: true),
                Alan("password",     "API Şifresi",                            "password", "credentials", zorunlu: true,
                    yardim: "GES API girişinin (TokenJson) şifresi — panel/API şifreniz."),
                Alan("sendPassword", "Gönderim Şifresi",                       "password", "credentials",
                    yardim: "SMS gönderim gövdesinin istediği AYRI şifre. GES bazı hesaplarda API şifresinden " +
                            "farklı bir gönderim şifresi tanımlar; boş bırakılırsa API şifresi kullanılır. " +
                            "Gönderimde 'Kullanici adi/parola yanlis' hatası alınıyorsa bu alan yanlış/eksik demektir."),
                Alan("origin",       "Mesaj Başlığı (Originator)",             "text",     "settings",    zorunlu: true,
                    yardim: "GES Telekom tarafında ONAYLI mesaj başlığınız. Onaysız başlıkla gönderim API tarafından reddedilir.")
            }),
            // Pazaryeri servisleri — Code, adapter ServiceCode'uyla birebir aynı olmalı
            // (AdapterResolver eşleşmesi). Mağaza senkronu bu servise bağlı aktif
            // FirmPlatformIntegration (sözleşme) ister; kimlikler orada şifreli tutulur.
            ("trendyol", "Trendyol", "marketplace", new List<PlatformSchemaField>
            {
                Alan("supplierId",     "Satıcı ID (Supplier ID)",   "text",     "settings",    zorunlu: true),
                Alan("apiKey",         "API Key",                   "password", "credentials", zorunlu: true),
                Alan("apiSecret",      "API Secret",                "password", "credentials", zorunlu: true),
                // F4 ürün gönderiminde Trendyol'un zorunlu payload alanları
                Alan("brandId",        "Marka ID (Trendyol marka kataloğundan)", "text", "settings", zorunlu: true),
                Alan("cargoCompanyId", "Kargo Firma ID (Trendyol kargo listesi)", "text", "settings", zorunlu: true)
            }),
            ("hepsiburada", "Hepsiburada", "marketplace", new List<PlatformSchemaField>
            {
                Alan("merchantId", "Merchant ID",         "text",     "settings",    zorunlu: true),
                Alan("username",   "API Kullanıcı Adı",   "text",     "credentials", zorunlu: true),
                Alan("password",   "API Şifresi",         "password", "credentials", zorunlu: true)
            }),
            ("n11", "n11", "marketplace", new List<PlatformSchemaField>
            {
                Alan("appKey",    "App Key",    "password", "credentials", zorunlu: true),
                Alan("appSecret", "App Secret", "password", "credentials", zorunlu: true)
            }),
            ("amazon", "Amazon", "marketplace", new List<PlatformSchemaField>
            {
                Alan("sellerId",      "Seller ID",      "text",     "settings",    zorunlu: true),
                Alan("marketplaceId", "Marketplace ID", "text",     "settings",    zorunlu: true,
                    yardim: "Amazon pazar yeri kimliği (amazon.com.tr = A33AVAJ2PDY3EV)."),
                Alan("accessKey",     "Access Key",     "password", "credentials", zorunlu: true),
                Alan("secretKey",     "Secret Key",     "password", "credentials", zorunlu: true),
                Alan("refreshToken",  "Refresh Token",  "password", "credentials")
            }),
            ("ciceksepeti", "Çiçeksepeti", "marketplace", new List<PlatformSchemaField>
            {
                Alan("apiKey", "API Anahtarı", "password", "credentials", zorunlu: true)
            }),
            ("pazarama", "Pazarama", "marketplace", new List<PlatformSchemaField>
            {
                Alan("apiKey",    "API Key",    "password", "credentials", zorunlu: true),
                Alan("apiSecret", "API Secret", "password", "credentials", zorunlu: true)
            }),
            // PayTR Direct API (2026-07-30) — ödeme aracısı. Alan adları DbPaymentSettingsProvider'ın
            // okuduğu anahtarlarla birebir aynı olmalı. testMode ŞU AN ZORUNLU AÇIK (PCI-DSS uyumu +
            // PayTR Direct API onayı tamamlanana dek canlıya alınmaz). merchant* bilgileri
            // PayTR Mağaza Paneli > Entegrasyon Bilgileri'nden.
            ("paytr", "PayTR (Direct API)", "payment", new List<PlatformSchemaField>
            {
                Alan("merchantId",   "Mağaza No (merchant_id)", "text",     "credentials", zorunlu: true),
                Alan("merchantKey",  "Mağaza Key",              "password", "credentials", zorunlu: true),
                Alan("merchantSalt", "Mağaza Salt",             "password", "credentials", zorunlu: true),
                Alan("testMode",     "Test Modu (zorunlu açık — canlı için PCI-DSS onayı gerekir)", "boolean", "settings",
                    yardim: "Şu an yalnız test modu desteklenir. Canlı ödeme için işletmenizin PCI-DSS SAQ D " +
                            "uyumu ve PayTR Direct API onayı gereklidir; o tamamlanana dek bu alan açık kalmalıdır.")
            }),
            // Sosyal giriş (OAuth) — firma/platform bazlı Google/Facebook kimlikleri. ClientSecret
            // şifreli Credentials'ta; StoreAuthController bu kayıtları okur (firma/settings deseni).
            ("google_oauth", "Google ile Giriş", "social_login", new List<PlatformSchemaField>
            {
                Alan("clientId",     "Client ID",       "text",     "settings",    zorunlu: true),
                Alan("clientSecret", "Client Secret",   "password", "credentials", zorunlu: true),
                Alan("redirectUri",  "Yönlendirme Adresi (boşsa otomatik)", "text", "settings",
                    yardim: "OAuth callback adresi. Boş bırakılırsa platform host'undan üretilir."),
                Alan("scopes",       "Kapsamlar (scopes)", "text", "settings",
                    yardim: "Boşlukla ayrılmış OAuth kapsamları. Boşsa: openid email profile")
            }),
            ("facebook_oauth", "Facebook ile Giriş", "social_login", new List<PlatformSchemaField>
            {
                Alan("clientId",     "App ID",           "text",     "settings",    zorunlu: true),
                Alan("clientSecret", "App Secret",       "password", "credentials", zorunlu: true),
                Alan("redirectUri",  "Yönlendirme Adresi (boşsa otomatik)", "text", "settings",
                    yardim: "OAuth callback adresi. Boş bırakılırsa platform host'undan üretilir."),
                Alan("scopes",       "Kapsamlar (scopes)", "text", "settings",
                    yardim: "Virgül/boşlukla ayrılmış OAuth kapsamları. Boşsa: email public_profile"),
                Alan("graphApiVersion", "Graph API Sürümü", "text", "settings",
                    yardim: "Facebook Graph API sürümü. Boşsa: v26.0")
            }),
            // Reklam / analytics / dönüşüm takibi servisleri (İE-1 Faz A, 2026-08-22 —
            // plan: docs/reklam-analytics-entegrasyon-is-akisi.md). Alan anahtarları
            // TrackingSettingsProvider + (Faz C/D) adapter'ların okuduğu anahtarlarla birebir.
            // Secret'lar (accessToken/apiSecret/conversionApiToken) credentials → şifreli.
            // `ownership` her serviste ortak: hesap müşterinin mi (customer) ECSPros'un mu
            // (platform) — davranışı değiştirmez, panel rozeti + raporlama ayrımı (karar §7-10).
            ("ga4", "Google Analytics 4", "analytics", TakipSemasi(
                Alan("measurementId", "Ölçüm Kimliği (Measurement ID, G-XXXXXXX)", "text", "settings", zorunlu: true,
                    yardim: "GA4 > Yönetici > Veri Akışları > Web akışı > Ölçüm Kimliği."),
                Alan("measurementProtocolApiSecret", "Measurement Protocol API Secret", "password", "credentials",
                    yardim: "Sunucu taraflı GA4 gönderimi için. GA4 > Veri Akışı > Measurement Protocol API secrets. " +
                            "Boşsa yalnız tarayıcı tarafı (gtag) çalışır."),
                Alan("sendServerSide", "Sunucu taraflı gönderim (Measurement Protocol)", "boolean", "settings",
                    yardim: "Açıksa satın alma event'i sunucudan da gönderilir (API secret gerekir)."))),
            ("gtm", "Google Tag Manager", "tag_manager", TakipSemasi(
                Alan("containerId", "Container ID (GTM-XXXXXXX)", "text", "settings", zorunlu: true,
                    yardim: "Tag Manager > Yönetici > Container ID."),
                Alan("manageGa4", "GA4 GTM içinden yönetiliyor", "boolean", "settings",
                    yardim: "Açıksa siteye ayrıca GA4 gtag basılmaz (çift sayım önlenir); GA4 etiketini GTM'de kurun."),
                Alan("manageAds", "Google Ads GTM içinden yönetiliyor", "boolean", "settings",
                    yardim: "Açıksa Google Ads dönüşüm etiketi siteye basılmaz; GTM'de kurun."),
                Alan("managePixels", "Pixel'ler (Meta/TikTok/UET/Pinterest) GTM içinden yönetiliyor", "boolean", "settings",
                    yardim: "Açıksa pixel script'leri siteye basılmaz; dataLayer event'leri GTM'de pixel etiketlerine bağlanır."))),
            ("google_ads", "Google Ads", "ads", TakipSemasi(
                Alan("conversionId", "Dönüşüm Kimliği (AW-XXXXXXXXX)", "text", "settings", zorunlu: true,
                    yardim: "Google Ads > Araçlar > Dönüşümler > etiket kurulumu > AW- ile başlayan kimlik."),
                Alan("purchaseLabel", "Satın Alma dönüşüm etiketi (label)", "text", "settings", zorunlu: true),
                Alan("addToCartLabel", "Sepete Ekleme dönüşüm etiketi (label)", "text", "settings"),
                Alan("beginCheckoutLabel", "Ödemeye Başlama dönüşüm etiketi (label)", "text", "settings"),
                Alan("enhancedConversions", "Gelişmiş dönüşümler (hash'li e-posta gönder)", "boolean", "settings",
                    yardim: "Açıksa satın almada müşteri e-postası SHA256 ile gtag'e iletilir (Google Ads'te de açık olmalı)."))),
            ("google_merchant", "Google Merchant Center", "merchant", TakipSemasi(
                Alan("merchantId", "Merchant ID", "text", "settings", zorunlu: true),
                Alan("feedCountry", "Hedef ülke (ISO, örn. TR)", "text", "settings", zorunlu: true),
                Alan("feedLanguage", "Feed dili (örn. tr)", "text", "settings", zorunlu: true),
                Alan("currency", "Para birimi (örn. TRY)", "text", "settings", zorunlu: true),
                Alan("includeOutOfStock", "Stoksuz varyantları feed'e dahil et (out_of_stock)", "boolean", "settings"),
                // İE-5: kargo (karar §7-7 feed'e yazılır — sabit temel bedel) + feed anahtarı (sistem üretir)
                Alan("shippingPrice", "Kargo bedeli (feed g:shipping, örn. 49.90; boş = yazılmaz)", "text", "settings",
                    yardim: "Merchant Center sabit bedel ister; ücretsiz kargo eşiğini Merchant Center > Kargo ayarında tanımlayın."),
                Alan("shippingService", "Kargo servis adı (örn. Standart Kargo)", "text", "settings"),
                Alan("feedKey", "Feed erişim anahtarı (sistem üretir — değiştirmeyin)", "text", "settings",
                    yardim: "Feed URL'si bu anahtarla korunur; boşsa ilk üretimde otomatik doldurulur."))),
            ("google_search_console", "Google Search Console", "search_console", TakipSemasi(
                Alan("verificationCode", "Site doğrulama kodu (meta content)", "text", "settings", zorunlu: true,
                    yardim: "Search Console > Mülk ekle > HTML etiketi yöntemindeki content=\"...\" değeri."))),
            ("meta", "Meta (Facebook/Instagram) Pixel + Conversions API", "meta", TakipSemasi(
                Alan("pixelId", "Pixel ID (Dataset ID)", "text", "settings", zorunlu: true,
                    yardim: "Meta Events Manager > Veri kaynakları > Pixel kimliği."),
                Alan("accessToken", "Conversions API Access Token", "password", "credentials",
                    yardim: "Events Manager > Ayarlar > Conversions API > Erişim jetonu oluştur. Sunucu taraflı gönderim için."),
                Alan("conversionApiEnabled", "Conversions API (sunucu taraflı) açık", "boolean", "settings"),
                Alan("testEventCode", "Test Event Code (yalnız test için)", "text", "settings",
                    yardim: "Events Manager > Test Events kodu (TESTxxxxx). Doluyken sunucu event'leri test sekmesine düşer; canlıda BOŞ bırakın."))),
            ("tiktok", "TikTok Pixel + Events API", "tiktok", TakipSemasi(
                Alan("pixelId", "Pixel ID", "text", "settings", zorunlu: true),
                Alan("accessToken", "Events API Access Token", "password", "credentials"),
                Alan("eventsApiEnabled", "Events API (sunucu taraflı) açık", "boolean", "settings"))),
            ("pinterest", "Pinterest Tag + Conversions API", "pinterest", TakipSemasi(
                Alan("tagId", "Tag ID", "text", "settings", zorunlu: true),
                Alan("conversionApiToken", "Conversions API Token", "password", "credentials"),
                Alan("conversionApiEnabled", "Conversions API (sunucu taraflı) açık", "boolean", "settings"))),
            ("microsoft_ads", "Microsoft Ads (UET)", "microsoft_ads", TakipSemasi(
                Alan("uetTagId", "UET Tag ID", "text", "settings", zorunlu: true))),
            ("microsoft_clarity", "Microsoft Clarity", "clarity", TakipSemasi(
                Alan("projectId", "Project ID", "text", "settings", zorunlu: true)))
        };

        var katalogKodlari = servisler.Select(s => s.Kod).ToList();
        var mevcutKodlar = await context.IntegrationServices
            .Where(s => katalogKodlari.Contains(s.Code))
            .Select(s => s.Code)
            .ToListAsync();

        var yeniler = servisler
            .Where(s => !mevcutKodlar.Contains(s.Kod))
            .Select(s => new IntegrationService
            {
                Code = s.Kod,
                NameI18n = new() { { "tr", s.Ad }, { "en", s.Ad } },
                ServiceType = s.Tip,
                IsAvailable = true,
                SettingsSchemaJson = System.Text.Json.JsonSerializer.Serialize(s.Sema, SemaJsonAyar)
            })
            .ToList();

        // Backfill: sosyal giriş (OAuth) ve takip (tracking) katalogları sonradan
        // genişleyebilir — mevcut kayıtlara eksik şema alanlarını ekle; admin'in
        // doldurduğu anahtarlar ve düzenlemeleri korunur.
        var degisti = yeniler.Count > 0;
        var backfillEdilenler = new List<string>();
        var backfillTipleri = new HashSet<string>(TakipServisTipleri) { "social_login" };
        var sosyalTanilar = servisler.Where(s => backfillTipleri.Contains(s.Tip)).ToList();
        if (sosyalTanilar.Count > 0)
        {
            var sosyalKodlar = sosyalTanilar.Select(x => x.Kod).ToList();
            var sosyalMevcutlar = await context.IntegrationServices
                .Where(s => backfillTipleri.Contains(s.ServiceType)
                    && sosyalKodlar.Contains(s.Code))
                .ToListAsync();

            var semaOku = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var tanim in sosyalTanilar)
            {
                var mevcut = sosyalMevcutlar.FirstOrDefault(m => m.Code == tanim.Kod);
                if (mevcut is null) continue;

                List<PlatformSchemaField> sema = new();
                if (!string.IsNullOrEmpty(mevcut.SettingsSchemaJson))
                {
                    try
                    {
                        sema = System.Text.Json.JsonSerializer.Deserialize<List<PlatformSchemaField>>(
                            mevcut.SettingsSchemaJson, semaOku) ?? new();
                    }
                    catch
                    {
                        sema = new();
                    }
                }

                var eksikler = tanim.Sema.Where(y => sema.All(m => m.Key != y.Key)).ToList();
                if (eksikler.Count == 0) continue;

                sema.AddRange(eksikler);
                mevcut.SettingsSchemaJson = System.Text.Json.JsonSerializer.Serialize(sema, SemaJsonAyar);
                backfillEdilenler.Add($"{tanim.Kod}+{eksikler.Count}");
                degisti = true;
            }
        }

        if (yeniler.Count > 0)
            context.IntegrationServices.AddRange(yeniler);

        if (!degisti) return;

        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: platform servisi güncellendi (yeni: {string.Join("/", yeniler.Select(y => y.Code))}; " +
                          $"şema backfill: {string.Join("/", backfillEdilenler)}).");
    }

    /// <summary>
    /// H2: Kargo firması kataloğu — IntegrationService (ServiceType=cargo) satırları
    /// takip URL şablonlarıyla. Firma hangi kargolarla çalışacaksa admin firma-sözleşme
    /// ekranından FirmPlatformIntegration açar; storefront kargo modalı adı/logoyu/takip linkini
    /// buradan çözer. Kod bazlı idempotent (var olana dokunmaz — admin düzenlemesi ezilmez).
    /// </summary>
    private static async Task SeedCargoCarriersAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        // Şemalar taşıyıcıların bilinen API gereksinimlerine göredir; adapter'lar gerçek
        // API'ye bağlandıkça alan adları buradaki anahtarlarla birebir okunmalı.
        // apiUrl her taşıyıcıya ortak eklenir (test/üretim adresi ayrımı için).
        var firmalar = new (string Kod, string Ad, string SablonUrl, List<PlatformSchemaField> Sema)[]
        {
            ("aras", "Aras Kargo", "https://kargotakip.araskargo.com.tr/mainpage.aspx?code={trackingNumber}", new()
            {
                Alan("username",     "API Kullanıcı Adı", "text",     "credentials", zorunlu: true),
                Alan("password",     "API Şifresi",       "password", "credentials", zorunlu: true),
                Alan("customerCode", "Müşteri Kodu",      "text",     "settings",    zorunlu: true,
                    yardim: "Aras Kargo tarafından verilen müşteri numaranız.")
            }),
            ("yurtici", "Yurtiçi Kargo", "https://www.yurticikargo.com/tr/online-servisler/gonderi-sorgula?code={trackingNumber}", new()
            {
                Alan("username", "API Kullanıcı Adı (wsUserName)", "text",     "credentials", zorunlu: true),
                Alan("password", "API Şifresi (wsPassword)",       "password", "credentials", zorunlu: true)
            }),
            // Kullanıcı kararı 2026-07-29: MNG artık DHL — marka adı DHL, API MNG APIZone,
            // servis kodu `mng` sabit (mevcut sözleşme/kural kayıtları kırılmasın).
            ("mng", "DHL Kargo (MNG)", "https://kargotakip.mngkargo.com.tr/?takipNo={trackingNumber}", new()
            {
                Alan("clientId",       "Client ID",             "password", "credentials", zorunlu: true,
                    yardim: "MNG API portalından alınan X-IBM-Client-Id."),
                Alan("clientSecret",   "Client Secret",         "password", "credentials", zorunlu: true),
                Alan("username",       "Müşteri Kullanıcı Adı", "text",     "credentials", zorunlu: true),
                Alan("password",       "Müşteri Şifresi",       "password", "credentials", zorunlu: true),
                Alan("customerNumber", "Müşteri Numarası",      "text",     "settings",    zorunlu: true)
            }),
            ("ptt", "PTT Kargo", "https://gonderitakip.ptt.gov.tr/Track/Verify?q={trackingNumber}", new()
            {
                Alan("customerNumber", "Müşteri Numarası", "text",     "settings",    zorunlu: true,
                    yardim: "PTT müşteri numaranız. Barkod aralığı tahsisi ayrıca kargo kod ayarlarından (range) yönetilir."),
                Alan("username",       "Kullanıcı Adı",    "text",     "credentials", zorunlu: true),
                Alan("password",       "Şifre",            "password", "credentials", zorunlu: true)
            }),
            // Alanlar resmi WSDL'den (docs/APIDocs/SuratWSDL.xml, 2026-07-29):
            // GonderiyiKargoyaGonderYeni/GonderiGeriCek → KullaniciAdi+Sifre;
            // GonderiSil/KargoBarkoduSiparisGuncelle/KargoTakipHareketDetayi → CariKodu+WebPassword.
            ("surat", "Sürat Kargo", "https://www.suratkargo.com.tr/KargoTakip/?kargotakipno={trackingNumber}", new()
            {
                Alan("username",    "API Kullanıcı Adı (KullaniciAdi)", "text",     "credentials", zorunlu: true),
                Alan("password",    "API Şifresi (Sifre)",              "password", "credentials", zorunlu: true),
                Alan("cariKodu",    "Cari Kodu",                        "text",     "settings",    zorunlu: true,
                    yardim: "Sürat Kargo cari hesap kodunuz — silme/güncelleme/takip uçları bu kodla çalışır."),
                Alan("webPassword", "Web Şifresi (WebPassword)",        "password", "credentials",
                    yardim: "Silme/güncelleme uçlarının istediği ayrı web şifresi; boş bırakılırsa API şifresi kullanılır.")
            }),
            ("hepsijet", "HepsiJet", "https://www.hepsijet.com/gonderi-takibi/{trackingNumber}", new()
            {
                Alan("username", "API Kullanıcı Adı", "text",     "credentials", zorunlu: true),
                Alan("password", "API Şifresi",       "password", "credentials", zorunlu: true)
            }),
            ("kolaygelsin", "Kolay Gelsin", "https://esube.kolaygelsin.com/shipments?trackingId={trackingNumber}", new()
            {
                Alan("apiKey",    "API Key",    "password", "credentials", zorunlu: true),
                Alan("apiSecret", "API Secret", "password", "credentials", zorunlu: true)
            }),
            ("ups", "UPS", "https://www.ups.com/track?loc=tr_TR&tracknum={trackingNumber}", new()
            {
                Alan("accountNumber", "Hesap/Müşteri Numarası",      "text",     "settings",    zorunlu: true),
                Alan("username",      "API Kullanıcı Adı",           "text",     "credentials", zorunlu: true),
                Alan("password",      "API Şifresi",                 "password", "credentials", zorunlu: true),
                Alan("apiKey",        "Erişim Anahtarı (Access Key)", "password", "credentials")
            })
        };

        static string SemaYaz(List<PlatformSchemaField> sema) =>
            System.Text.Json.JsonSerializer.Serialize(
                sema.Append(Alan("apiUrl", "API Adresi (opsiyonel)", "text", "settings",
                    yardim: "Boş bırakılırsa taşıyıcının üretim adresi kullanılır; test ortamı adresi girilebilir.")).ToList(),
                SemaJsonAyar);

        var mevcutlar = await context.IntegrationServices
            .Where(s => s.ServiceType == "cargo")
            .ToListAsync();

        var yeniler = firmalar
            .Where(f => mevcutlar.All(m => m.Code != f.Kod))
            .Select(f => new IntegrationService
            {
                Code = f.Kod,
                NameI18n = new() { { "tr", f.Ad }, { "en", f.Ad } },
                ServiceType = "cargo",
                IsAvailable = true,
                TrackingUrlTemplate = f.SablonUrl,
                SettingsSchemaJson = SemaYaz(f.Sema)
                // LogoUrl bilinçli null — logo görselleri edinildikçe admin/SQL ile dolar,
                // storefront logo yoksa yalnız adı basar.
            })
            .ToList();

        // Backfill: şeması hiç doldurulmamış mevcut kargo servisine şablonu yaz —
        // admin'in elle girdiği/düzenlediği şemalar (dolu SettingsSchema) ezilmez.
        var doldurulan = 0;
        foreach (var f in firmalar)
        {
            var mevcut = mevcutlar.FirstOrDefault(m => m.Code == f.Kod);
            if (mevcut is null || !string.IsNullOrEmpty(mevcut.SettingsSchemaJson)) continue;
            mevcut.SettingsSchemaJson = SemaYaz(f.Sema);
            doldurulan++;
        }

        if (yeniler.Count == 0 && doldurulan == 0) return;

        context.IntegrationServices.AddRange(yeniler);
        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: kargo firması — {yeniler.Count} yeni, {doldurulan} şema backfill (cargo IntegrationService).");
    }

    /// <summary>
    /// F2: SSS sayfası — "kurumsal-sss" (PageType corporate) + "faq" section'ı +
    /// soru/cevap item'ları (TitleI18n=soru, DescriptionI18n=cevap; tasarımın 9 sorusu).
    /// Admin CMS'ten soru ekler/düzenler; Kurumsal SSS akordiyonu buradan render olur.
    /// Kod bazlı idempotent.
    /// </summary>
    private static async Task SeedFaqPageAsync(IServiceProvider sp)
    {
        var cms = sp.GetRequiredService<ECSPros.Cms.Infrastructure.Persistence.CmsDbContext>();
        var core = sp.GetRequiredService<CoreDbContext>();

        var sectionType = await cms.SectionTypes.FirstOrDefaultAsync(t => t.Code == "faq");
        if (sectionType is null)
        {
            sectionType = new ECSPros.Cms.Domain.Entities.SectionType
            {
                Code = "faq",
                NameI18n = new() { ["tr"] = "Soru / Cevap Listesi" },
                SettingsSchema = new(),
                SupportsItems = true
            };
            cms.SectionTypes.Add(sectionType);
        }

        var template = await cms.PageTemplates.FirstAsync(t => t.Code == "icerik-sayfasi");
        var platformIdler = await core.FirmPlatforms
            .Where(fp => fp.IsActive).Select(fp => fp.Id).ToListAsync();

        var sorular = new (string Soru, string Cevap)[]
        {
            ("Üyelik", "Üyelik ücretsizdir. Ana sayfadaki \"ÜYE OL\" seçeneğine tıklayarak kayıt olabilirsiniz. Üyeliğinizi iptal etmek veya şifrenizi değiştirmek için Müşteri Hizmetleri ile iletişime geçebilirsiniz."),
            ("İptal ve Değişim", "Ürün iadeleri için İadelerim sayfasından iade talebi oluşturabilirsiniz. Değişim yapılmamaktadır."),
            ("Kargo ve Teslimat", "Siparişiniz anlaşmalı kargo firmalarımız aracılığıyla teslim edilir. Siparişinizin durumunu \"Hesabım\" altından takip edebilirsiniz."),
            ("Sipariş", "Sipariş durumunuzu, değişiklik veya iptal işlemlerinizi \"Hesabım\" üzerinden yönetebilirsiniz."),
            ("Ödeme", "Banka veya kredi kartı ile ödeme seçeneklerimiz mevcuttur. Taksit seçenekleri için kredi kartınızı kullanabilirsiniz."),
            ("İndirim Kuponları ve Kodları", "Sepet özetinin altında yer alan alana indirim kodunuzu girebilirsiniz. Bazı butiklerimiz indirimlere kapalıdır."),
            ("Fatura", "Faturalar sipariş sırasında belirttiğiniz adrese gönderilir. Şirket adına fatura düzenlenmemektedir."),
            ("Hesabım", "Şifre ve e-posta adresi güncellemelerinizi \"Hesabım\" üzerinden yapabilirsiniz."),
            ("Şikayet ve Öneriler", "Şikayetlerinizi mesai saatlerinde çağrı merkezimize iletebilirsiniz.")
        };

        var eklenen = 0;
        foreach (var platformId in platformIdler)
        {
            if (await cms.Pages.AnyAsync(p => p.FirmPlatformId == platformId
                                              && p.PageType == "corporate" && p.Code == "kurumsal-sss"))
                continue;

            var sayfa = new ECSPros.Cms.Domain.Entities.Page
            {
                FirmPlatformId = platformId,
                TemplateId = template.Id,
                Code = "kurumsal-sss",
                NameI18n = new() { ["tr"] = "Sık Sorulan Sorular" },
                SlugI18n = new() { ["tr"] = "kurumsal-sss" },
                PageType = "corporate"
            };
            cms.Pages.Add(sayfa);

            var section = new ECSPros.Cms.Domain.Entities.PageSection
            {
                PageId = sayfa.Id,
                SectionTypeId = sectionType.Id,
                Name = "SSS",
                Settings = new(),
                SortOrder = 0
            };
            cms.PageSections.Add(section);

            var sira = 0;
            foreach (var (soru, cevap) in sorular)
                cms.PageSectionItems.Add(new ECSPros.Cms.Domain.Entities.PageSectionItem
                {
                    SectionId = section.Id,
                    ItemType = "faq",
                    TitleI18n = new() { ["tr"] = soru },
                    DescriptionI18n = new() { ["tr"] = cevap },
                    SortOrder = sira++
                });
            eklenen++;
        }

        if (eklenen > 0)
        {
            await cms.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {eklenen} platforma SSS sayfası oluşturuldu.");
        }
    }

    /// <summary>
    /// F1: kurumsal içerik sayfaları — PageType "corporate", kod "kurumsal-*" önekli
    /// (legal sayfalarla kod çakışmasın). İçerik tasarım partial'larının panel HTML'i;
    /// admin'den düzenlenebilir, Kurumsal sayfaları CMS'ten basar (boşsa tasarım demo
    /// yedeği — D3 deseni). SSS (F2) ve İletişim (F3) ayrı yapıda. Kod bazlı idempotent;
    /// canlıya SQL ile eklendi.
    /// </summary>
    private static async Task SeedCorporatePagesAsync(IServiceProvider sp)
    {
        var cms = sp.GetRequiredService<ECSPros.Cms.Infrastructure.Persistence.CmsDbContext>();
        var core = sp.GetRequiredService<CoreDbContext>();

        var sectionType = await cms.SectionTypes.FirstAsync(t => t.Code == "rich_text");
        var template = await cms.PageTemplates.FirstAsync(t => t.Code == "icerik-sayfasi");

        var platformIdler = await core.FirmPlatforms
            .Where(fp => fp.IsActive).Select(fp => fp.Id).ToListAsync();

        var eklenen = 0;
        foreach (var platformId in platformIdler)
        {
            var mevcutKodlar = await cms.Pages
                .Where(s => s.FirmPlatformId == platformId && s.PageType == "corporate")
                .Select(s => s.Code).ToListAsync();

            foreach (var (kod, baslik, html) in KurumsalSayfaIcerikleri())
            {
                if (mevcutKodlar.Contains(kod)) continue;
                var sayfa = new ECSPros.Cms.Domain.Entities.Page
                {
                    FirmPlatformId = platformId,
                    TemplateId = template.Id,
                    Code = kod,
                    NameI18n = new() { ["tr"] = baslik },
                    SlugI18n = new() { ["tr"] = kod },
                    PageType = "corporate"
                };
                cms.Pages.Add(sayfa);
                cms.PageSections.Add(new ECSPros.Cms.Domain.Entities.PageSection
                {
                    PageId = sayfa.Id,
                    SectionTypeId = sectionType.Id,
                    Name = baslik,
                    Settings = new() { ["html"] = html },
                    SortOrder = 0
                });
                eklenen++;
            }
        }

        if (eklenen > 0)
        {
            await cms.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {eklenen} kurumsal CMS sayfası oluşturuldu.");
        }
    }

    /// <summary>F1: kurumsal sayfa içerikleri — tasarım partial'larının panel iç HTML'i
    /// (section kökü partial'da kalır; buradaki içerik admin'den düzenlenir).</summary>
    private static IEnumerable<(string Kod, string Baslik, string Html)> KurumsalSayfaIcerikleri()
    {
        yield return ("kurumsal-hakkimizda", "Hakkımızda", """
<header class="ms-kurumsal-hero">
    <span>Premium Moda</span>
    <h2>Mishar Italia: Premium Modanın Yeni Tanımı</h2>
    <p>Mishar Italia, kadın, erkek ve çocuk giyiminde şıklığı ve üst segment kalite standartlarını bir araya getiren premium bir moda markasıdır.</p>
</header>
<div class="ms-kurumsal-metin">
    <p>Her yaşa ve her tarza hitap eden seçkin koleksiyonlarımızla, modern gardıropların vazgeçilmez markası olma vizyonuyla hareket ediyoruz.</p>
    <p>Markamızın şık duruşu ve sunduğu premium deneyim, arkasındaki güçlü üretim altyapısıyla desteklenmektedir. Mishar Italia bünyesindeki tüm koleksiyonların üretimi, sektörün öncü ve köklü kuruluşu GÜLSELİ TEKSTİL tarafından gerçekleştirilmektedir.</p>
    <p>Tasarımlarımızı, GÜLSELİ TEKSTİL'in yüksek kalite standartları ve kurumsal güvencesiyle sizlere sunmaktan gurur duyuyoruz.</p>
</div>
<div class="ms-kurumsal-bilgi-kartlari">
    <article><strong>Segment</strong><span>Premium kadın, erkek ve çocuk giyim koleksiyonları.</span></article>
    <article><strong>Üretim güvencesi</strong><span>GÜLSELİ TEKSTİL kalite standartlarıyla desteklenen güçlü altyapı.</span></article>
</div>
""");

        yield return ("kurumsal-kargo-teslimat", "Kargo ve Teslimat", """
<header class="ms-kurumsal-panel-baslik">
    <span>Teslimat</span>
    <h2>Kargomu Nasıl Takip Ederim?</h2>
    <p>Kargo bilgileriniz e-posta ve sipariş takip bilgileriniz üzerinden kontrol edilebilir.</p>
</header>
<div class="ms-kurumsal-adimlar">
    <article><strong>1</strong><span>Ürünleriniz kargo firmasına teslim edildiğinde e-posta adresinize "Teslimat Bilginiz" konulu otomatik bilgilendirme gönderilir.</span></article>
    <article><strong>2</strong><span>Bu e-postada kargoya verilen ürünleriniz, teslimat adresiniz, teslimat bilginiz ve kargo firması bilgileri yer alır.</span></article>
    <article><strong>3</strong><span>E-postanın alt kısmındaki izleme numarası veya sipariş numaranız ile kargonuzun durumunu takip edebilirsiniz.</span></article>
    <article><strong>4</strong><span>E-posta bilgisi bulunmayan müşterilerimiz sipariş numarasını müşteri temsilcilerimizden öğrenerek kargo durumunu sorgulayabilir.</span></article>
</div>
""");

        yield return ("kurumsal-iade-degisim", "İade ve Değişim", """
<header class="ms-kurumsal-panel-baslik">
    <span>İade</span>
    <h2>İade ve Değişim Şartları</h2>
    <p>İade süresi, ücretsiz gönderim ve kabul edilmeyen ürün koşulları.</p>
</header>
<div class="ms-kurumsal-vurgu-grid">
    <article><strong>14 Gün</strong><span>Sebep göstermeden cayma hakkı, ürünü teslim aldığınız günü takip eden ilk 14 gün içinde geçerlidir.</span></article>
    <article><strong>30 Gün</strong><span>Kusurlu ürünlerde iade süresi 30 güne kadar uzamaktadır.</span></article>
    <article><strong>Ücretsiz İade</strong><span>Anlaşmalı kargo firmalarımızla ücretsiz gönderim yapılabilir; iade kodunuzu İadelerim sayfasından alabilirsiniz.</span></article>
</div>
<div class="ms-kurumsal-metin">
    <h3>Hangi ürünler iade edilemez?</h3>
    <ul>
        <li>Tasarım ve abiye ürünler 48 saat içinde iade edilebilir; süre aşımında iade kabul edilmez.</li>
        <li>Kozmetik, kişisel bakım, iç giyim, mayo ve bikini ürünleri iade edilemez.</li>
        <li>Kitap, kopyalanabilir yazılım, DVD, VCD, CD ve kasetlerde ambalaj açılmamış olmalıdır.</li>
        <li>Ev tekstili, kişisel ürünler ve küçük ev aletleri orijinal ambalajında, eksiksiz ve kullanılmamış olmalıdır.</li>
    </ul>
    <h3>İade süreci</h3>
    <p>İade talebi teslimat tarihinden itibaren en geç 14 gün içinde oluşturulmalı ve anlaşmalı kargolarla gönderilmelidir. Faturasız, etiketsiz, yıkanmış, kullanılmış veya hasar görmüş ürünler iade edilmez.</p>
    <p>İade onaylandığında ürün tutarı en geç 3 gün içinde iade edilir. Taksitli ödemelerde iade bankanız tarafından taksitli olarak yansıtılır.</p>
</div>
""");

        yield return ("kurumsal-kullanim-kosullari", "Kullanım Koşulları", """
<header class="ms-kurumsal-panel-baslik">
    <span>Koşullar</span>
    <h2>Kullanım Koşulları</h2>
    <p>Bu internet sitesine girmeniz veya sitedeki bilgileri kullanmanız aşağıdaki koşulları kabul ettiğiniz anlamına gelir.</p>
</header>
<div class="ms-kurumsal-madde-listesi">
    <article><strong>1. Sorumluluk Reddi</strong><p>Bu internet sitesine girilmesi veya sitedeki bilgilerin kullanılması sebebiyle doğabilecek doğrudan ya da dolaylı hiçbir zarardan firmamız sorumlu değildir.</p></article>
    <article><strong>2. Değişiklik ve Güncellemeler</strong><p>Hizmetlerimizi, ürünlerimizi, kullanım koşullarını ve sitede sunulan bilgileri önceden haber vermeksizin değiştirme hakkımız saklıdır.</p></article>
    <article><strong>3. Bağlantılı Siteler</strong><p>Bu internet sitesi, kontrolümüz altında olmayan başka sitelere bağlantı içerebilir. Bu sitelerin içeriklerinden sorumluluk kabul edilmez.</p></article>
    <article><strong>4. Fikri Mülkiyet Hakları</strong><p>Sitedeki marka, logo, tasarım, yazılım, görsel ve tüm materyaller yasal koruma altındadır; izinsiz kullanılamaz, kopyalanamaz veya dağıtılamaz.</p></article>
    <article><strong>5. Yurtdışı Siparişleri</strong><p>Yurtdışı siparişlerde gümrük bedelleri ülkeye göre değişebilir. Gönderilerin gümrüğe takılması durumunda sorumluluk müşteriye aittir.</p></article>
    <article><strong>6. Yasal Uyarı Güncellemeleri</strong><p>Yasal uyarı sayfası içeriğini dilediğimiz zaman güncelleme hakkımız saklıdır.</p></article>
</div>
""");

        yield return ("kurumsal-gizlilik-guvenlik", "Gizlilik ve Güvenlik", """
<header class="ms-kurumsal-panel-baslik">
    <span>Güvenlik</span>
    <h2>Gizlilik ve Güvenlik</h2>
    <p>Gizliliğinize önem veriyoruz. Siteyi kullanarak aşağıdaki şartları kabul etmiş sayılırsınız.</p>
</header>
<div class="ms-kurumsal-madde-listesi">
    <article><strong>Kişisel Bilgiler</strong><p>Üyelik aşamasında ve sonrasında talep edilen kişisel bilgiler, Üyelik Sözleşmesi'nde belirtilen amaçlar dışında kullanılmaz ve üçüncü şahıslarla paylaşılmaz.</p></article>
    <article><strong>IP Adresleri</strong><p>Sistem sorunlarının tespiti ve çözümü için IP adresleri kullanılabilir; ayrıca demografik bilgi toplamak için anonim biçimde değerlendirilebilir.</p></article>
    <article><strong>Verilerin Kullanım Amaçları</strong><p>Siparişlerin alınması, ödeme ve teslimat süreçleri, üyelik yönetimi, pazarlama iletişimi ve size özel önerilerin sunulması için bilgileriniz işlenebilir.</p></article>
    <article><strong>Bilgilendirme E-postaları</strong><p>Üye olduğunuz andan itibaren, aksi talep edilmedikçe bilgilendirme e-postaları gönderilebilir. Dilediğiniz zaman vazgeçebilirsiniz.</p></article>
    <article><strong>Mali Bilgiler</strong><p>Satın alma işlemlerinde mali bilgiler, işleminizi gerçekleştirmek için gerekli banka ve kredi kartı kuruluşlarıyla paylaşılabilir.</p></article>
    <article><strong>Güvenlik</strong><p>Tüm kredi kartı ve kişisel bilgileriniz SSL Secure sistemi ile 256 bit şifrelenerek korunur.</p></article>
    <article><strong>KVKK</strong><p>Kişisel verileriniz izin tercihleriniz doğrultusunda analiz edilebilir ve gerekli güvenlik önlemleri alınarak işlenebilir. Sorularınız için iletişim bölümünden bize ulaşabilirsiniz.</p></article>
</div>
""");
    }

    /// <summary>
    /// E8: iade neden listesi — tasarımın _HesabimIadelerim script'indeki ana/alt neden
    /// haritası Lookup'a taşındı (plan şartı). Ana nedenler `return_reason` tipinin
    /// değerleri; alt nedenler ilgili değerin ExtraData["subReasons"] listesinde
    /// (LookupValue'da hiyerarşi yok; alt nedenler seçim anında metin snapshot olarak
    /// ReturnItem'a yazılır). İdempotent: tip varsa dokunmaz. Canlıya SQL ile eklendi.
    /// </summary>
    private static async Task SeedReturnReasonsAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        if (await context.LookupTypes.AnyAsync(t => t.Code == "return_reason"))
            return;

        var type = new LookupType
        {
            Code = "return_reason",
            NameI18n = new() { { "tr", "İade Nedeni" }, { "en", "Return Reason" } },
            IsSystem = true
        };
        context.LookupTypes.Add(type);
        await context.SaveChangesAsync();

        var nedenler = new (string Ad, string[] AltNedenler)[]
        {
            ("Beden / Kalıp Problemi", new[] { "Küçük geldi", "Büyük geldi", "Dar geldi", "Bol geldi", "Kalıbı dar", "Kalıbı geniş", "Belden olmadı", "Basenden olmadı", "Kalçadan olmadı", "Göğüs kısmı olmadı", "Omuzdan olmadı", "Kol kısmı dar geldi", "Kol kısmı bol geldi", "Bacak kısmı dar geldi", "Normal bedenime uymadı", "Beden tablosu ile uyumsuz", "Üzerimde istediğim gibi durmadı" }),
            ("Ürün Beklediğim Gibi Değil", new[] { "Fotoğraftaki gibi değil", "Açıklamadaki gibi değil", "Ürün görselden farklı duruyor", "Beklediğim kalitede değil", "Beklediğim tarzda değil", "Ürün üzerimde güzel durmadı", "Kesimi beklediğim gibi değil", "Kumaşı beklediğim gibi değil", "Ürün beklentimi karşılamadı", "Ürün anlatıldığı gibi değil", "Ürün ölçüleri beklentime uymadı" }),
            ("Defolu / Hasarlı Ürün", new[] { "Yırtık geldi", "Sökük geldi", "Leke var", "Delik var", "İp çekilmesi var", "Dikiş hatası var", "Fermuar bozuk", "Düğme eksik", "Düğme kopuk", "Baskı hatalı", "Aksesuar eksik", "Ürün deforme olmuş", "Ürün ezilmiş geldi", "Ambalaj hasarlı geldi", "Kargo sırasında zarar görmüş" }),
            ("Yanlış veya Eksik Ürün Geldi", new[] { "Yanlış ürün gönderildi", "Yanlış beden gönderildi", "Yanlış renk gönderildi", "Yanlış model gönderildi", "Siparişimde olmayan ürün geldi", "Eksik ürün geldi", "Takımın parçası eksik geldi", "Ürünün aksesuarı eksik geldi", "Kemer / kuşak eksik geldi", "Hediye ürün eksik geldi", "Ürün adedi eksik geldi" }),
            ("Renk / Model / Kumaş Problemi", new[] { "Rengi görselden farklı", "Renk tonu beklediğim gibi değil", "Yanlış renk gönderildi", "Modelini beğenmedim", "Model üzerimde iyi durmadı", "Kumaşı ince", "Kumaşı kalın", "Kumaşı sert", "Kumaşı esnemiyor", "Kumaşı rahatsız etti", "Kumaşı iç gösteriyor", "Kumaşı terletiyor", "Dokusu hoşuma gitmedi", "Deseni görselden farklı", "Kumaş kalitesi beklentimi karşılamadı" }),
            ("Sipariş Hatası Yaptım", new[] { "Yanlış beden sipariş verdim", "Yanlış renk sipariş verdim", "Yanlış ürün sipariş verdim", "Yanlış adet sipariş verdim", "Yanlışlıkla sipariş verdim", "Aynı üründen fazla sipariş verdim", "Sepete yanlış ürün ekledim", "Farklı ürün almak istiyorum", "Farklı beden almak istiyorum", "Farklı renk almak istiyorum" }),
            ("Vazgeçtim / Beğenmedim", new[] { "Almaktan vazgeçtim", "Ürünü beğenmedim", "Ürüne ihtiyacım kalmadı", "Fikrimi değiştirdim", "Hediye olarak uygun olmadı", "Hediye edilen kişi beğenmedi", "Kombinime uymadı", "Tarzıma uygun değil", "Başka ürün almaya karar verdim", "Fiyatına değmediğini düşündüm" }),
            ("Teslimat Problemi", new[] { "Geç teslim edildi", "İhtiyacım olan tarihe yetişmedi", "Teslimat süreci uzadı", "Yanlış adrese teslim edildi", "Kargo paketi hasarlı geldi", "Kargo firması kaynaklı sorun yaşadım", "Ürün teslim edilmeden iade etmek istiyorum", "Siparişi iptal edemedim, iade etmek istiyorum", "Kargo sürecinden memnun kalmadım" }),
            ("Diğer", Array.Empty<string>())
        };

        var sira = 1;
        foreach (var (ad, altNedenler) in nedenler)
        {
            context.LookupValues.Add(new LookupValue
            {
                LookupTypeId = type.Id,
                NameI18n = new() { { "tr", ad } },
                ExtraData = new() { ["subReasons"] = altNedenler.ToList() },
                SortOrder = sira++,
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
        Console.WriteLine("✓ Seed: İade nedenleri (return_reason) oluşturuldu.");
    }

    /// <summary>
    /// C8: checkout sözleşme modalı + ödeme bilgi grupları için legal CMS sayfaları.
    /// Her aktif platforma 5 sayfa (mesafeli satış, ön bilgilendirme, gizlilik, kullanım,
    /// kargo) — içerik firmanın unvan/adres/vergi bilgileriyle üretilir, CMS admin'den
    /// düzenlenebilir. İdempotent: platformda herhangi bir legal sayfa varsa dokunmaz.
    /// Canlıya 2026-07-09'da aynı içerik SQL ile eklendi.
    /// </summary>
    private static async Task SeedCmsLegalPagesAsync(IServiceProvider sp)
    {
        var cms = sp.GetRequiredService<ECSPros.Cms.Infrastructure.Persistence.CmsDbContext>();
        await cms.Database.MigrateAsync();
        var core = sp.GetRequiredService<CoreDbContext>();

        var sectionType = await cms.SectionTypes.FirstOrDefaultAsync(t => t.Code == "rich_text");
        if (sectionType is null)
        {
            sectionType = new ECSPros.Cms.Domain.Entities.SectionType
            {
                Code = "rich_text",
                NameI18n = new() { ["tr"] = "Zengin Metin" },
                SettingsSchema = new() { ["html"] = "string" },
                SupportsItems = false
            };
            cms.SectionTypes.Add(sectionType);
        }

        var template = await cms.PageTemplates.FirstOrDefaultAsync(t => t.Code == "icerik-sayfasi");
        if (template is null)
        {
            template = new ECSPros.Cms.Domain.Entities.PageTemplate
            {
                Code = "icerik-sayfasi",
                NameI18n = new() { ["tr"] = "İçerik Sayfası" },
                TemplateType = "content",
                DefaultLayout = "full"
            };
            cms.PageTemplates.Add(template);
        }

        var platformlar = await core.FirmPlatforms
            .Where(fp => fp.IsActive)
            .Select(fp => new { fp.Id, FirmaAd = fp.Firm.NameI18n, fp.Firm.Address, fp.Firm.TaxOffice, fp.Firm.TaxNumber })
            .ToListAsync();

        var eklenen = 0;
        foreach (var p in platformlar)
        {
            // D3: kod bazlı idempotenlik — yeni belge türleri (üyelik/KVKK) mevcut
            // platformlara da eklenebilsin (C8'deki "hiç legal yoksa" guard'ı yetmiyordu).
            var mevcutKodlar = await cms.Pages
                .Where(s => s.FirmPlatformId == p.Id && s.PageType == "legal")
                .Select(s => s.Code).ToListAsync();

            var firma = p.FirmaAd.TryGetValue("tr", out var ad) ? ad : p.FirmaAd.Values.FirstOrDefault() ?? "Satıcı";
            var satici = $"{firma} — {p.Address}. Vergi Dairesi/No: {p.TaxOffice} / {p.TaxNumber}.";

            foreach (var (kod, baslik, html) in LegalSayfaIcerikleri(satici))
            {
                if (mevcutKodlar.Contains(kod)) continue;
                var sayfa = new ECSPros.Cms.Domain.Entities.Page
                {
                    FirmPlatformId = p.Id,
                    TemplateId = template.Id,
                    Code = kod,
                    NameI18n = new() { ["tr"] = baslik },
                    SlugI18n = new() { ["tr"] = kod },
                    PageType = "legal"
                };
                cms.Pages.Add(sayfa);
                cms.PageSections.Add(new ECSPros.Cms.Domain.Entities.PageSection
                {
                    PageId = sayfa.Id,
                    SectionTypeId = sectionType.Id,
                    Name = baslik,
                    Settings = new() { ["html"] = html },
                    SortOrder = 0
                });
                eklenen++;
            }
        }

        if (eklenen > 0 || cms.ChangeTracker.HasChanges())
        {
            await cms.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {eklenen} legal CMS sayfası oluşturuldu (C8 sözleşmeler).");
        }
    }

    private static IEnumerable<(string Kod, string Baslik, string Html)> LegalSayfaIcerikleri(string satici)
    {
        yield return ("mesafeli-satis-sozlesmesi", "Mesafeli Satış Sözleşmesi",
            $"<p><strong>Satıcı:</strong> {satici}</p>" +
            "<p><strong>Konu:</strong> Alıcının internet sitesi üzerinden elektronik ortamda sipariş verdiği ürünlerin satışı ve teslimi ile tarafların 6502 sayılı Tüketicinin Korunması Hakkında Kanun ve Mesafeli Sözleşmeler Yönetmeliği kapsamındaki hak ve yükümlülüklerinin belirlenmesidir.</p>" +
            "<p><strong>Ürün ve teslimat:</strong> Ürün cinsi, miktarı, satış bedeli, teslimat adresi, fatura adresi, teslim edilecek kişi ve teslim şekli sipariş bilgileri içinde gösterilir. Ürünler yasal süreyi aşmamak kaydıyla alıcının belirttiği adrese teslim edilir.</p>" +
            "<p><strong>Genel hükümler:</strong> Alıcı, ürünün temel nitelikleri, vergiler dahil satış fiyatı, ödeme şekli, teslimat bilgileri, satıcı bilgileri ve cayma hakkı konusunda bilgilendirildiğini kabul eder. Teslimat sırasında ürün kontrol edilmeli, kargo kaynaklı sorunlarda tutanak tutulmalıdır.</p>" +
            "<p><strong>Cayma hakkı:</strong> Alıcı, ürünü teslim aldığı tarihten itibaren 14 gün içinde herhangi bir gerekçe göstermeksizin cayma hakkını kullanabilir. İade edilecek ürünlerin faturası, ambalajı, varsa aksesuarları eksiksiz ve hasarsız olmalıdır.</p>");

        yield return ("on-bilgilendirme-formu", "Ön Bilgilendirme Formu",
            "<p><strong>Konu:</strong> Bu form, alıcı ile satıcı arasında kurulacak mesafeli satış sözleşmesine ilişkin tüketicinin önceden bilgilendirilmesi amacıyla hazırlanmıştır.</p>" +
            $"<p><strong>Satıcı bilgileri:</strong> {satici}</p>" +
            "<p><strong>Alıcı bilgileri:</strong> Alıcının adı, soyadı, adresi, telefon ve e-posta bilgileri sipariş sırasında beyan edilen bilgiler esas alınarak kullanılır.</p>" +
            "<p><strong>Teslimat ve kargo:</strong> Teslimat masrafları kampanya koşullarına göre alıcıya yansıtılabilir veya satıcı tarafından karşılanabilir. Ürünler yasal süreyi aşmamak kaydıyla alıcının belirttiği adrese gönderilir.</p>" +
            "<p><strong>Ödeme ve iade:</strong> Sipariş iptali veya cayma hakkı kullanımında ödeme yapılan yönteme göre iade süreci işletilir. Banka kaynaklı iade yansıma süreleri ilgili finans kuruluşunun işlem süreçlerine bağlıdır.</p>");

        yield return ("gizlilik-guvenlik", "Gizlilik ve Güvenlik",
            "<p>Kişisel verileriniz 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamında, siparişinizin oluşturulması, teslimatı ve satış sonrası süreçlerin yürütülmesi amacıyla işlenir; yasal zorunluluklar dışında üçüncü kişilerle paylaşılmaz.</p>" +
            "<p>Ödeme sayfasında girilen kart bilgileri sitemizde saklanmaz; ödeme işlemleri güvenli ödeme altyapısı üzerinden gerçekleştirilir.</p>");

        yield return ("kullanim-kosullari", "Kullanım Koşulları",
            "<p>Bu siteyi kullanarak site kullanım koşullarını kabul etmiş sayılırsınız. Sitede yer alan ürün görselleri ve içerikler bilgilendirme amaçlıdır; izinsiz kopyalanamaz ve çoğaltılamaz.</p>" +
            "<p>Sipariş verilmesi, mesafeli satış sözleşmesi ve ön bilgilendirme formu hükümlerinin elektronik ortamda kabulü anlamına gelir.</p>");

        yield return ("kargo-teslimat", "Kargo ve Teslimat",
            "<p>Siparişleriniz ödeme onayının ardından kargoya teslim edilir. Teslimat süresi, yasal azami süre olan 30 günü aşmamak üzere adresinize ve kargo yoğunluğuna göre değişebilir.</p>" +
            "<p>Kargo ücreti ve varsa ücretsiz kargo koşulları ödeme adımındaki sipariş özetinde gösterilir. Teslimat sırasında paketi kontrol ediniz; hasarlı paketlerde kargo yetkilisine tutanak tutturunuz.</p>");

        // D3: kayıt modalındaki belge onayları
        yield return ("uyelik-sozlesmesi", "Üyelik Sözleşmesi",
            $"<p><strong>Taraflar:</strong> {satici} ile siteye üye olan kullanıcı arasında, üyeliğin oluşturulmasıyla birlikte aşağıdaki koşullar yürürlüğe girer.</p>" +
            "<p><strong>Üyelik:</strong> Üye, kayıt sırasında verdiği bilgilerin doğru ve güncel olduğunu kabul eder; hesap bilgilerini üçüncü kişilerle paylaşmamakla yükümlüdür. Hesap üzerinden yapılan işlemler üyenin sorumluluğundadır.</p>" +
            "<p><strong>Kullanım koşulları:</strong> Üye, siteyi hukuka ve dürüstlük kurallarına uygun kullanacağını; site işleyişini bozacak müdahalelerde bulunmayacağını kabul eder. Satıcı, üyelik hizmetini ve site içeriğini değiştirme, askıya alma veya sonlandırma hakkını saklı tutar.</p>" +
            "<p><strong>Fesih:</strong> Üye dilediği zaman üyeliğini sonlandırabilir. Sözleşmeye aykırılık hâlinde satıcı üyeliği askıya alabilir veya sonlandırabilir.</p>");

        yield return ("kvkk-aydinlatma", "Aydınlatma Metni",
            $"<p><strong>Veri sorumlusu:</strong> {satici}</p>" +
            "<p><strong>İşleme amaçları:</strong> Kişisel verileriniz (kimlik, iletişim, adres ve alışveriş bilgileri) 6698 sayılı Kişisel Verilerin Korunması Kanunu uyarınca; üyeliğin oluşturulması, siparişlerin alınması ve teslimi, satış sonrası hizmetler, yasal yükümlülüklerin yerine getirilmesi ve açık rızanız bulunması hâlinde ticari elektronik ileti gönderimi amaçlarıyla işlenir.</p>" +
            "<p><strong>Aktarım:</strong> Verileriniz yalnızca hizmetin gerektirdiği ölçüde kargo, ödeme ve bilişim hizmeti sağlayıcılarıyla ve yasal zorunluluk hâlinde yetkili kurumlarla paylaşılır.</p>" +
            "<p><strong>Haklarınız:</strong> KVKK'nın 11. maddesi kapsamında verilerinize erişme, düzeltme, silme, işlemeye itiraz etme ve diğer haklarınızı veri sorumlusuna başvurarak kullanabilirsiniz.</p>");
    }

    /// <summary>
    /// B4: Üyelik kaydının çalışması için varsayılan üye grubu (RegisterMemberCommand
    /// IsDefault grup arar). İdempotent — grup varsa dokunmaz. Canlıya 2026-07-09'da
    /// aynı kayıt SQL ile eklendi (Code='standart').
    /// </summary>
    private static async Task SeedCrmDefaultsAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ECSPros.Crm.Infrastructure.Persistence.CrmDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.MemberGroups.AnyAsync(g => g.IsDefault))
        {
            db.MemberGroups.Add(new ECSPros.Crm.Domain.Entities.MemberGroup
            {
                Code = "standart",
                NameI18n = new Dictionary<string, string> { ["tr"] = "Standart Üye" },
                IsDefault = true,
                IsActive = true
            });
            await db.SaveChangesAsync();
            Console.WriteLine("✓ Seed: varsayılan üye grubu (standart) oluşturuldu.");
        }
    }

    /// <summary>
    /// Türkiye adres hiyerarşisi referans verisini taze DB'ye yükler (ülke/il/ilçe/mahalle).
    /// Üretimde bu veri bir kerelik dış import ile gelmişti; bundan sonra yeni DB kurulumu
    /// Data/Geo/*.csv dosyalarından idempotent olarak dolar (il/ilçe/mahalle dropdown'ları).
    /// Yalnız tablolar boşken çalışır — mevcut veriye dokunmaz.
    /// </summary>
    private static async Task SeedGeoAsync(IServiceProvider sp)
    {
        var crm = sp.GetRequiredService<ECSPros.Crm.Infrastructure.Persistence.CrmDbContext>();
        await crm.Database.MigrateAsync();
        if (await crm.Cities.AnyAsync()) return; // zaten dolu

        var dataSource = sp.GetRequiredService<Npgsql.NpgsqlDataSource>();
        var geoDir = Path.Combine(AppContext.BaseDirectory, "Data", "Geo");
        if (!Directory.Exists(geoDir)) return;

        var tablolar = new (string Table, string File)[]
        {
            ("crm.crm_countries",      "countries.csv"),
            ("crm.crm_cities",         "cities.csv"),
            ("crm.crm_districts",      "districts.csv"),
            ("crm.crm_neighborhoods",  "neighborhoods.csv"),
        };

        await using var conn = await dataSource.OpenConnectionAsync();
        foreach (var (table, file) in tablolar)
        {
            var yol = Path.Combine(geoDir, file);
            if (!File.Exists(yol)) continue;

            await using var writer = await conn.BeginTextImportAsync(
                $"COPY {table} FROM STDIN (FORMAT csv, HEADER true)");
            await writer.WriteAsync(await File.ReadAllTextAsync(yol));
        }
        Console.WriteLine("✓ Seed: Türkiye adres hiyerarşisi (il/ilçe/mahalle) yüklendi.");
    }

    /// <summary>
    /// Storefront migration'larını uygular ve tüm kategorilere varsayılan listing_mode atar.
    /// İdempotent — zaten doğru değeri olan kayıtlara dokunmaz.
    /// </summary>
    private static async Task SeedStorefrontDefaultsAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<StorefrontDbContext>();

        // Bekleyen migration'ları uygula (listing_mode sütununu ekler)
        await db.Database.MigrateAsync();

        // "model" dışındaki tüm kategorilere listing_mode = "color" ata
        var updated = await db.ChannelCategories
            .Where(c => c.ListingMode != "model" && c.ListingMode != "color")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ListingMode, "color"));

        if (updated > 0)
            Console.WriteLine($"✓ Seed: {updated} kanal kategorisine listing_mode='color' atandı.");
    }

    private static async Task SeedIamAsync(IServiceProvider sp)
    {
        await SeedPermissionsAndRolesAsync(sp);
        await SeedAdminUserAsync(sp);
        await SeedApiClientTypesAsync(sp);
    }

    /// <summary>
    /// definition.api_client_types — 4 API kullanıcı tipi kataloğu (§3). Platformca tanımlı,
    /// kilitli taban scope paketleri. İdempotent: yalnız eksik kodları ekler.
    /// </summary>
    private static async Task SeedApiClientTypesAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<IamDbContext>();

        var tipler = new List<ApiClientType>
        {
            new()
            {
                Code = "supplier_managed",
                NameI18n = new() { ["tr"] = "Yönetilen tedarikçi", ["en"] = "Managed supplier" },
                DefaultClientType = "partner",
                RequiredOwnerType = "current_account",
                // P1 (2026-08-11, K3 kararı): order.read TABAN pakete alındı — üç kargo modunun
                // hepsinde satıcı kendi satışını görür; fulfillment.write bayrağa bağlı kalır.
                BaseScopes = new()
                {
                    ApiScopes.CatalogRead, ApiScopes.CatalogWrite, ApiScopes.StockRead,
                    ApiScopes.StockWrite, ApiScopes.OrderRead, ApiScopes.InvoiceRead, ApiScopes.AccountRead
                }
            },
            new()
            {
                Code = "supplier_merchant",
                NameI18n = new() { ["tr"] = "Pazaryeri tedarikçisi", ["en"] = "Marketplace supplier" },
                DefaultClientType = "partner",
                RequiredOwnerType = "current_account",
                BaseScopes = new()
                {
                    ApiScopes.CatalogRead, ApiScopes.CatalogWrite, ApiScopes.PricingWrite,
                    ApiScopes.StockRead, ApiScopes.StockWrite, ApiScopes.OrderRead,
                    ApiScopes.InvoiceRead, ApiScopes.AccountRead
                }
            },
            new()
            {
                Code = "first_party",
                NameI18n = new() { ["tr"] = "Mobil / birinci taraf", ["en"] = "Mobile / first party" },
                DefaultClientType = "first_party",
                RequiredOwnerType = null,
                BaseScopes = new() { ApiScopes.CatalogRead, ApiScopes.StockRead, ApiScopes.OrderRead }
            },
            new()
            {
                Code = "internal",
                NameI18n = new() { ["tr"] = "İç servis", ["en"] = "Internal service" },
                DefaultClientType = "internal",
                RequiredOwnerType = null,
                BaseScopes = ApiScopes.All.ToList()
            }
        };

        var mevcutKodlar = await context.ApiClientTypes.Select(t => t.Code).ToListAsync();
        var yeniler = tipler.Where(t => !mevcutKodlar.Contains(t.Code)).ToList();
        if (yeniler.Count == 0) return;

        context.ApiClientTypes.AddRange(yeniler);
        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: {yeniler.Count} API kullanıcı tipi eklendi ({string.Join("/", yeniler.Select(y => y.Code))}).");
    }

    /// <summary>
    /// İdempotent: eksik dilleri ekler. Her ortamda (Production dahil) çalışır.
    /// </summary>
    public static async Task SeedLanguagesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existingCodes = (await context.Languages
            .Select(l => l.Code).ToListAsync()).ToHashSet();

        var allLanguages = new[]
        {
            new Language { Code = "tr", NativeName = "Türkçe",  Direction = "ltr", IsDefault = true,  IsActive = true, SortOrder = 1 },
            new Language { Code = "en", NativeName = "English", Direction = "ltr", IsDefault = false, IsActive = true, SortOrder = 2 },
            new Language { Code = "ar", NativeName = "العربية", Direction = "rtl", IsDefault = false, IsActive = true, SortOrder = 3 },
        };

        var toAdd = allLanguages.Where(l => !existingCodes.Contains(l.Code)).ToList();
        if (toAdd.Count > 0)
        {
            context.Languages.AddRange(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {toAdd.Count} dil eklendi ({string.Join(", ", toAdd.Select(l => l.Code))}).");
        }
    }

    /// <summary>
    /// İdempotent: permission ve rol kayıtlarını oluşturur/günceller.
    /// Mevcut kayıtlara dokunmaz, eksik olanları ekler.
    /// </summary>
    public static async Task SeedPermissionsAndRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<IamDbContext>();

        // ── Permissions ────────────────────────────────────────────────────────
        var permDefs = new[]
        {
            (Code: Permissions.CatalogPlatformManage,   Name: "Katalog Platform Yönetimi",  Module: "catalog"),
            (Code: Permissions.DefinitionManage,        Name: "Tanım Verisi Yönetimi",      Module: "definition"),
            (Code: Permissions.CatalogProductsManage,   Name: "Ürün Yönetimi",              Module: "catalog"),
            (Code: Permissions.CatalogCategoriesManage, Name: "Kategori Yönetimi",           Module: "catalog"),
            (Code: Permissions.CatalogImagesManage,     Name: "Görsel Yönetimi",             Module: "catalog"),
            (Code: Permissions.CatalogSettingsManage,   Name: "Katalog Ayarları",            Module: "catalog"),
            (Code: Permissions.InventoryManage,         Name: "Envanter Yönetimi",           Module: "inventory"),
            (Code: Permissions.OrderPackagesMerge,      Name: "Paket Birleştirme (İstisna)", Module: "order"),
        };

        var existingCodes = await context.Permissions.Select(p => p.Code).ToListAsync();

        foreach (var def in permDefs.Where(d => !existingCodes.Contains(d.Code)))
        {
            context.Permissions.Add(new Permission
            {
                Code           = def.Code,
                NameI18n       = new Dictionary<string, string> { { "tr", def.Name } },
                Module         = def.Module,
                PermissionType = "manage",
                IsActive       = true,
            });
        }
        await context.SaveChangesAsync();

        // ── Roles ──────────────────────────────────────────────────────────────
        var roleDefs = new[]
        {
            (Code: "super_admin",    Name: "Süper Admin",     IsSystem: true),
            (Code: "platform_admin", Name: "Platform Admin",  IsSystem: true),
            (Code: "firm_admin",     Name: "Firma Admin",     IsSystem: false),
        };

        var existingRoleCodes = await context.Roles.Select(r => r.Code).ToListAsync();

        foreach (var rd in roleDefs.Where(r => !existingRoleCodes.Contains(r.Code)))
        {
            context.Roles.Add(new Role
            {
                Code     = rd.Code,
                NameI18n = new Dictionary<string, string> { { "tr", rd.Name }, { "en", rd.Name } },
                IsSystem = rd.IsSystem,
                IsActive = true,
            });
        }
        await context.SaveChangesAsync();

        // ── Role → Permission assignments ──────────────────────────────────────
        var allPermissions    = await context.Permissions.ToListAsync();
        var allRoles          = await context.Roles.ToListAsync();
        var existingRolePerms = await context.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync();

        void AssignPerms(string roleCode, IEnumerable<string> permCodes)
        {
            var role = allRoles.FirstOrDefault(r => r.Code == roleCode);
            if (role is null) return;

            foreach (var permCode in permCodes)
            {
                var perm = allPermissions.FirstOrDefault(p => p.Code == permCode);
                if (perm is null) continue;
                if (existingRolePerms.Any(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id)) continue;

                context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }

        AssignPerms("super_admin",    Permissions.AllPermissions);
        AssignPerms("platform_admin", Permissions.AllPermissions);
        AssignPerms("firm_admin",     Permissions.FirmAdminPermissions);

        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Permission ve roller oluşturuldu/güncellendi.");
    }

    private static async Task SeedAdminUserAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<IamDbContext>();
        var hasher  = sp.GetRequiredService<IPasswordHasher>();

        if (await context.Users.AnyAsync(u => u.Username == "admin"))
            return;

        var superAdminRole = await context.Roles.FirstAsync(r => r.Code == "super_admin");

        var adminUser = new User
        {
            Username          = "admin",
            Email             = "admin@ecspros.com",
            PasswordHash      = hasher.Hash("Admin123!"),
            FirstName         = "Sistem",
            LastName          = "Admin",
            Department        = "IT",
            IsActive          = true,
            MustChangePassword = true,
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        context.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = superAdminRole.Id });
        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Admin kullanıcısı oluşturuldu. (admin / Admin123!)");
    }

    /// <summary>
    /// İdempotent: eksik platform tiplerini ekler.
    /// </summary>
    public static async Task SeedPlatformTypesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        var existingCodes = (await context.PlatformTypes
            .Select(p => p.Code).ToListAsync()).ToHashSet();

        var defaults = new[]
        {
            new PlatformType { Code = "site",          NameI18n = new() { { "tr", "Web Sitesi" },   { "en", "Website" } },          IsMarketplace = false, IsActive = true },
            new PlatformType { Code = "trendyol",      NameI18n = new() { { "tr", "Trendyol" },     { "en", "Trendyol" } },         IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "hepsiburada",   NameI18n = new() { { "tr", "Hepsiburada" },  { "en", "Hepsiburada" } },      IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "n11",           NameI18n = new() { { "tr", "n11" },          { "en", "n11" } },              IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "amazon",        NameI18n = new() { { "tr", "Amazon" },       { "en", "Amazon" } },           IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "ciceksepeti",   NameI18n = new() { { "tr", "Çiçeksepeti" },  { "en", "Ciceksepeti" } },      IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "pazarama",      NameI18n = new() { { "tr", "Pazarama" },     { "en", "Pazarama" } },         IsMarketplace = true,  IsActive = true },
            new PlatformType { Code = "mobile_app",    NameI18n = new() { { "tr", "Mobil Uygulama" },{ "en", "Mobile App" } },      IsMarketplace = false, IsActive = true },
            new PlatformType { Code = "pos",           NameI18n = new() { { "tr", "Mağaza / POS" }, { "en", "Store / POS" } },      IsMarketplace = false, IsActive = true },
        };

        var toAdd = defaults.Where(p => !existingCodes.Contains(p.Code)).ToList();
        if (toAdd.Count > 0)
        {
            context.PlatformTypes.AddRange(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {toAdd.Count} platform tipi eklendi.");
        }
    }

    private static async Task SeedCoreAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CoreDbContext>();

        // Diğer referans veriler sadece ilk kurulumda eklenir
        if (await context.OrderStatuses.AnyAsync())
            return;

        // Sipariş durumları
        context.OrderStatuses.AddRange(
            new OrderStatus { Code = "pending", NameI18n = new() { { "tr", "Beklemede" }, { "en", "Pending" } }, Color = "#FFA500", SortOrder = 1, IsActive = true },
            new OrderStatus { Code = "confirmed", NameI18n = new() { { "tr", "Onaylandı" }, { "en", "Confirmed" } }, Color = "#2196F3", SortOrder = 2, IsActive = true },
            new OrderStatus { Code = "preparing", NameI18n = new() { { "tr", "Hazırlanıyor" }, { "en", "Preparing" } }, Color = "#9C27B0", SortOrder = 3, IsActive = true },
            new OrderStatus { Code = "shipped", NameI18n = new() { { "tr", "Kargoda" }, { "en", "Shipped" } }, Color = "#00BCD4", SortOrder = 4, IsActive = true },
            new OrderStatus { Code = "delivered", NameI18n = new() { { "tr", "Teslim Edildi" }, { "en", "Delivered" } }, Color = "#4CAF50", SortOrder = 5, IsActive = true },
            new OrderStatus { Code = "cancelled", NameI18n = new() { { "tr", "İptal Edildi" }, { "en", "Cancelled" } }, Color = "#F44336", SortOrder = 6, IsActive = true },
            new OrderStatus { Code = "returned", NameI18n = new() { { "tr", "İade Edildi" }, { "en", "Returned" } }, Color = "#795548", SortOrder = 7, IsActive = true }
        );

        // Ödeme yöntemleri
        context.PaymentMethods.AddRange(
            new PaymentMethod { Code = "credit_card", NameI18n = new() { { "tr", "Kredi Kartı" }, { "en", "Credit Card" } }, IsOnline = true, RequiresConfirmation = false, IsActive = true, SortOrder = 1 },
            new PaymentMethod { Code = "bank_transfer", NameI18n = new() { { "tr", "Havale/EFT" }, { "en", "Bank Transfer" } }, IsOnline = false, RequiresConfirmation = true, IsActive = true, SortOrder = 2 },
            new PaymentMethod { Code = "cash_on_delivery", NameI18n = new() { { "tr", "Kapıda Ödeme" }, { "en", "Cash on Delivery" } }, IsOnline = false, RequiresConfirmation = false, IsActive = true, SortOrder = 3 },
            new PaymentMethod { Code = "pos", NameI18n = new() { { "tr", "POS" }, { "en", "POS Terminal" } }, IsOnline = false, RequiresConfirmation = false, IsActive = true, SortOrder = 4 },
            new PaymentMethod { Code = "wallet", NameI18n = new() { { "tr", "Cüzdan" }, { "en", "Wallet" } }, IsOnline = true, RequiresConfirmation = false, IsActive = true, SortOrder = 5 }
        );

        // Lookup tipleri
        var genderType = new LookupType
        {
            Code = "gender",
            NameI18n = new() { { "tr", "Cinsiyet" }, { "en", "Gender" } },
            IsSystem = true
        };
        context.LookupTypes.Add(genderType);
        await context.SaveChangesAsync();

        context.LookupValues.AddRange(
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Erkek" }, { "en", "Male" } }, SortOrder = 1, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Kadın" }, { "en", "Female" } }, SortOrder = 2, IsActive = true },
            new LookupValue { LookupTypeId = genderType.Id, NameI18n = new() { { "tr", "Diğer" }, { "en", "Other" } }, SortOrder = 3, IsActive = true }
        );

        await context.SaveChangesAsync();

        Console.WriteLine("✓ Seed: Core referans verileri oluşturuldu.");
    }

    private static async Task SeedCatalogAsync(IServiceProvider sp)
    {
        var context = sp.GetRequiredService<CatalogDbContext>();

        // ImageServer ayarları
        var imageServerKeys = new Dictionary<string, string>
        {
            ["ImageServer.FtpHost"]        = "localhost",
            ["ImageServer.FtpPort"]        = "21",
            ["ImageServer.FtpUser"]        = "anonymous",
            ["ImageServer.FtpPassword"]    = "",
            ["ImageServer.FtpBasePath"]    = "/images/products/",
            ["ImageServer.PublicBaseUrl"]  = "/media/images/products/",
            ["ImageServer.LocalSavePath"]  = "/opt/ECSProsAI/media/images/products/",
            // CDN ayarları — https://cdn.example.com/img/{height}/{quality}/{fileName}
            ["ImageServer.CdnBaseUrl"]     = "",
            ["ImageServer.CdnQuality"]     = "85",
            ["ImageServer.CdnThumbHeight"] = "240",
            ["ImageServer.CdnListHeight"]  = "640",
            ["ImageServer.CdnZoomHeight"]  = "1200",
            ["VideoServer.LocalSavePath"]  = "/opt/ECSProsAI/media/videos/products/",
            ["VideoServer.PublicBaseUrl"]  = "/media/videos/products/",
            ["VideoServer.FtpHost"]        = "localhost",
            ["VideoServer.FtpPort"]        = "21",
            ["VideoServer.FtpUser"]        = "anonymous",
            ["VideoServer.FtpPassword"]    = "",
            ["VideoServer.FtpBasePath"]    = "/videos/products/"
        };

        foreach (var (key, defaultValue) in imageServerKeys)
        {
            if (!await context.CatalogSettings.AnyAsync(x => x.Key == key))
            {
                context.CatalogSettings.Add(new CatalogSetting { Key = key, Value = defaultValue });
            }
        }

        // Barkod sequence başlangıç değeri
        if (!await context.CatalogSettings.AnyAsync(x => x.Key == "barcode_sequence"))
        {
            context.CatalogSettings.Add(new CatalogSetting { Key = "barcode_sequence", Value = "1" });
        }

        await context.SaveChangesAsync();

        // Varsayılan görsel seti
        // Adım 1: 'varsayilan' gibi görünen ama farklı byte içeren eski/bozuk satırları soft-delete et
        // (Örn: i + Unicode combining dot — ASCII 'i'den farklı byte dizisi, ama görsel olarak aynı)
        await context.Database.ExecuteSqlRawAsync(@"
            UPDATE definition.image_sets
            SET ""IsDeleted"" = true, ""DeletedAt"" = NOW()
            WHERE ""IsDeleted"" = false
              AND ""Code"" LIKE 'varsay%'
              AND ""Code"" != 'varsayilan'
        ");

        // Adım 2: UPSERT — aktif saf-ASCII 'varsayilan' satırı varsa güncelle, yoksa ekle
        await context.Database.ExecuteSqlRawAsync($@"
            INSERT INTO definition.image_sets
                (""Id"", ""Code"", ""Name"", ""IsDefault"", ""FallbackSetId"",
                 ""SortPriority"", ""IsActive"", ""IsDeleted"", ""CreatedAt"",
                 ""CreatedBy"", ""UpdatedAt"", ""UpdatedBy"", ""DeletedAt"", ""DeletedBy"")
            VALUES
                ('{Guid.NewGuid()}', 'varsayilan', 'Varsayılan Resim Seti', true, NULL,
                 0, true, false, NOW(), NULL, NULL, NULL, NULL, NULL)
            ON CONFLICT (""Code"") WHERE NOT ""IsDeleted""
            DO UPDATE SET
                ""Name""      = 'Varsayılan Resim Seti',
                ""IsDefault"" = true,
                ""IsActive""  = true,
                ""IsDeleted"" = false,
                ""DeletedAt"" = NULL,
                ""DeletedBy"" = NULL
        ");

        Console.WriteLine("✓ Seed: Catalog ayarları (ImageServer, barcode_sequence, ImageSet) oluşturuldu.");

        // Temel attribute type'ları
        await SeedAttributeTypesAsync(context);

        // "Filtre Rengi" attribute type + değerleri
        await SeedFilterRengiAttributeTypeAsync(context);

        // Demografik attribute değerleri
        await SeedCinsiyetValuesAsync(context);
        await SeedYasGrubuValuesAsync(context);

        // Var/Yok tipi ikili özelliklerin değerleri
        await SeedVarYokValuesAsync(context, "fermuar", "esneklik", "balen", "dolgu", "ic_cep");

        // Ayakkabı beden değerleri
        await SeedShoeBedenValuesAsync(context);

        // Ürün grupları
        await SeedProductGroupsAsync(context);

        // Ürün grubu özellik atamaları
        await SeedProductGroupAttributesAsync(context);

        // Telemania demo — kalıcı kozmetik/kişisel bakım katalog tanımları (idempotent)
        await SeedTelemaniaProductGroupsAsync(context);
        await SeedTelemaniaAttributeValuesAsync(context);
        await SeedTelemaniaDrinkwareAttributeValuesAsync(context);
        await SeedTelemaniaProductGroupAttributesAsync(context);
        await SeedTelemaniaFilterEnrichmentAsync(context);
    }

    private static async Task SeedAttributeTypesAsync(CatalogDbContext db)
    {
        // filtre_rengi SeedFilterRengiAttributeTypeAsync tarafından ayrıca ekleniyor (Sort=40)
        // (code, name_tr, dataType, sort, useInFilter) — NameI18n sadece "tr" içerir.
        // Sort = storefront filtre alanındaki gösterim sırası (önem sırası, 10'ar adım);
        // UseInFilter=false olanlar (renk: serbest metin — filtresi filtre_rengi'nden verilir;
        // manken: bilgilendirici json) filtre alanına hiç girmez.
        var canonical = new (string Code, string Tr, string DataType, int Sort, bool UseInFilter)[]
        {
            // En önemliler — demografi + marka + temel varyant eksenleri
            ("cinsiyet",     "Cinsiyet",          "select",  10, true),
            ("marka",        "Marka",             "select",  20, true),
            ("beden",        "Beden",             "select",  30, true),
            // filtre_rengi = 40 (ayrı seed)
            // Genel ürün özellikleri
            ("kumas_turu",   "Kumaş Türü",        "select",  50, true),
            ("kalip",        "Kalıp",             "select",  60, true),
            ("desen",        "Desen",             "select",  70, true),
            ("season",       "Sezon",             "select",  80, true),
            ("yil",          "Yıl",               "select",  90, true),
            ("yas_grubu",    "Yaş Grubu",         "select", 100, true),
            // Kesim / silüet
            ("yaka_tipi",    "Yaka Tipi",         "select", 110, true),
            ("kol_tipi",     "Kol Tipi",          "select", 120, true),
            ("kol_boyu",     "Kol Boyu",          "select", 130, true),
            ("boy",          "Boy",               "select", 140, true),
            ("urun_boyu",    "Ürün Boyu",         "select", 150, true),
            // Alt giyim / ölçüler
            ("bel",          "Bel Ölçüsü",        "select", 160, true),
            ("bel_tipi",     "Bel Tipi",          "select", 170, true),
            ("basen",        "Basen Ölçüsü",      "select", 180, true),
            ("gogus",        "Göğüs Ölçüsü",      "select", 190, true),
            ("omuz_genisligi", "Omuz Genişliği",  "select", 200, true),
            ("ic_uzunluk",   "İç Uzunluk",        "select", 210, true),
            ("paca_tipi",    "Paça Tipi",         "select", 220, true),
            ("etek_tipi",    "Etek Tipi",         "select", 230, true),
            // Detaylar
            ("cep_tipi",     "Cep Tipi",          "select", 240, true),
            ("cep_sayisi",   "Cep Sayısı",        "select", 250, true),
            ("ic_cep",       "İç Cep",            "select", 260, true),
            ("kapatma_tipi", "Kapatma Tipi",      "select", 270, true),
            ("fermuar",      "Fermuar",           "select", 280, true),
            ("astar_durumu", "Astar Durumu",      "select", 290, true),
            ("kalinlik",     "Kalınlık",          "select", 300, true),
            ("dolgu",        "Dolgu",             "select", 310, true),
            ("balen",        "Balen / Tel",       "select", 320, true),
            ("esneklik",     "Esneklik",          "select", 330, true),
            ("aski_tipi",    "Askı Tipi",         "select", 340, true),
            ("aski_boyu",    "Askı Boyu",         "select", 350, true),
            // Malzeme
            ("malzeme",      "Malzeme",           "select", 360, true),
            ("dis_materyal", "Dış Materyal",      "select", 370, true),
            ("ic_yuzey",     "İç Yüzey",          "select", 380, true),
            // Ayakkabı
            ("taban_ozelligi",   "Taban Özelliği",   "select", 390, true),
            ("taban_yuksekligi", "Taban Yüksekliği", "select", 400, true),
            ("topuk_boyu",   "Topuk Boyu",        "select", 410, true),
            ("topuk_tipi",   "Topuk Tipi",        "select", 420, true),
            ("ortam",        "Ortam",             "select", 430, true),
            // Çanta
            ("canta_agzi",   "Çanta Ağzı",        "select", 440, true),
            // Kozmetik / kişisel bakım (Telemania demo — genel kullanım)
            ("hacim",        "Hacim",             "select", 1000, true),
            ("cilt_tipi",    "Cilt Tipi",         "select", 1010, true),
            ("sac_tipi",     "Saç Tipi",          "select", 1020, true),
            ("spf",          "SPF",               "select", 1030, true),
            // Telemania demo filtre zenginleştirmesi (2026-08-23) — ürün adlarından türetilir
            ("paket_adedi",  "Paket Adedi",       "select", 1040, true),
            ("yas_grubu",    "Yaş Grubu",         "select", 1050, true),
            ("bez_bedeni",   "Bez Bedeni",        "select", 1060, true),
            ("urun_formu",   "Form",              "select", 1070, true),
            ("sac_rengi",    "Saç Rengi",         "select", 1080, true),
            ("kullanim_tipi","Kullanım Tipi",     "select", 1090, true),
            ("ek_ozellik",   "Özellik",           "select", 1100, true),
            // Filtre dışı tipler
            ("renk",         "Renk",              "select", 900, false),
            // Manken (bkz. docs/manken-ozelligi-spec.md) — varyant üretmez, bilgilendirici;
            // değeri ProductAttribute.CustomValue JSONB alanında tutulur, AttributeValue havuzu kullanılmaz
            ("manken",       "Manken",            "json",   910, false),
        };

        var existingCodes = new HashSet<string>(await db.AttributeTypes.Select(a => a.Code).ToListAsync());
        int added = 0, updated = 0;

        foreach (var (code, tr, dt, sort, useInFilter) in canonical)
        {
            if (!existingCodes.Contains(code))
            {
                db.AttributeTypes.Add(new AttributeType
                {
                    Id = Guid.NewGuid(), Code = code,
                    NameI18n = new Dictionary<string, string> { ["tr"] = tr },
                    DataType = dt, IsActive = true,
                    SortOrder = sort, UseInFilter = useInFilter,
                    CreatedAt = DateTime.UtcNow
                });
                added++;
            }
        }

        if (added > 0) await db.SaveChangesAsync();
        if (added > 0 || updated > 0)
            Console.WriteLine($"✓ Seed: {added} attribute type eklendi.");
        else
            Console.WriteLine("✓ Seed: Attribute type'lar güncel.");
    }

    private static async Task SeedShoeBedenValuesAsync(CatalogDbContext db)
    {
        var bedenType = await db.AttributeTypes.FirstOrDefaultAsync(a => a.Code == "beden");
        if (bedenType == null) return;

        // Avrupa numaralandırması: bebek (16–27), çocuk (28–35), yetişkin (36–50)
        var sizes = Enumerable.Range(16, 35).Select(n => n.ToString()).ToList(); // 16–50

        var existingNames = new HashSet<string>(await db.AttributeValues
            .Where(v => v.AttributeTypeId == bedenType.Id)
            .Select(v => v.NameI18n["tr"])
            .ToListAsync());

        int added = 0;
        // Başlangıç sort: mevcut maksimumun üstünden devam et
        int sort = (await db.AttributeValues
            .Where(v => v.AttributeTypeId == bedenType.Id)
            .MaxAsync(v => (int?)v.SortOrder) ?? 0) + 10;

        foreach (var size in sizes)
        {
            if (existingNames.Contains(size)) continue;
            db.AttributeValues.Add(new AttributeValue
            {
                Id = Guid.NewGuid(),
                AttributeTypeId = bedenType.Id,
                NameI18n = new Dictionary<string, string> { ["tr"] = size, ["en"] = size },
                SortOrder = sort,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
            sort += 10;
            added++;
        }

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: {added} ayakkabı bedeni eklendi (beden attribute).");
    }

    private static async Task SeedProductGroupsAsync(CatalogDbContext db)
    {
        // (code, nameTr, sortOrder)
        var groups = new (string code, string name, int sort)[]
        {
            // ── Aktarım kökenli gruplar (grp_X) ──────────────────────────────
            ("grp_1",   "Elbise",                    1),
            ("grp_10",  "Etek",                      1),
            ("grp_11",  "Sweatshirt",                1),
            ("grp_118", "İç Giyim",                  1),
            ("grp_12",  "Hırka",                     1),
            ("grp_123", "Plaj Giyim",                1),
            ("grp_132", "Kişisel Bakım",             1),
            ("grp_137", "Makyaj Malzemeleri",        1),
            ("grp_14",  "Triko",                     1),
            ("grp_149", "Kostüm",                    1),
            ("grp_15",  "Pijama",                    1),
            ("grp_159", "Zıbın",                     1),
            ("grp_16",  "Bolero",                    1),
            ("grp_165", "Banyo Giyim",               1),
            ("grp_17",  "Yelek",                     1),
            ("grp_174", "Şal",                       1),
            ("grp_176", "Telefon ve Aksesuarları",   1),
            ("grp_177", "Bilgisayar",                1),
            ("grp_178", "Televizyon ve Aksesuar",    1),
            ("grp_179", "Elektirikli Ev Aletleri",   1),
            ("grp_18",  "Tunik",                     1),
            ("grp_180", "Beyaz Eşya",                1),
            ("grp_181", "Mobilyalar",                1),
            ("grp_182", "Dekorasyon ve Aydınlatma",  1),
            ("grp_183", "Ev Tekstil",                1),
            ("grp_184", "Mutfak Gereçleri",          1),
            ("grp_185", "Banyo ve Ev Gereçleri",     1),
            ("grp_186", "Çeyiz Setleri",             1),
            ("grp_198", "Kimono",                    1),
            ("grp_2",   "Aksesuar",                  1),
            ("grp_21",  "Bot",                       1),
            ("grp_44",  "Body",                      1),
            ("grp_24",  "Çizme",                     1),
            ("grp_25",  "Babet",                     1),
            ("grp_254", "Kapri",                     1),
            ("grp_262", "Trençkot",                  0),
            ("grp_269", "Kulaklık",                  0),
            ("grp_27",  "Sandalet",                  1),
            ("grp_3",   "Pantolon",                  1),
            ("grp_33",  "Terlik",                    1),
            ("grp_36",  "Tulum",                     1),
            ("grp_46",  "Ceket",                     1),
            ("grp_47",  "Eşofman",                   1),
            ("grp_48",  "İkili Takım",               1),
            ("grp_5",   "Gömlek",                    1),
            ("grp_6",   "Bluz",                      1),
            ("grp_63",  "Takım Elbise",              1),
            ("grp_7",   "T-Shirt",                   1),
            ("grp_70",  "Aktif Spor",                1),
            ("grp_73",  "Mont",                      1),
            ("grp_77",  "Kap",                       1),
            ("grp_80",  "Panço",                     1),
            ("grp_83",  "Çanta",                     1),
            ("grp_9",   "Bustiyer",                  1),
            ("grp_95",  "Şort",                      1),
            // ── Yeni gruplar ──────────────────────────────────────────────────
            ("spor_ayakkabi",   "Spor Ayakkabı",     1),
            ("gunluk_ayakkabi", "Günlük Ayakkabı",   1),
            ("topuklu_ayakkabi","Topuklu Ayakkabı",  1),
            ("stiletto",        "Stiletto",           1),
            ("klasik_ayakkabi", "Klasik Ayakkabı",   1),
            ("kaban",           "Kaban",              1),
            ("atlet",           "Atlet",              1),
            ("bornoz",          "Bornoz",             1),
            ("bone",            "Bone",               1),
            ("havlu",           "Havlu",              1),
            ("pestemal",        "Peştemal",           1),
            ("ferace",          "Ferace",             1),
            ("esarp",           "Eşarp",              1),
            ("pelus_terlik",        "Peluş Terlik",       1),
            ("hamile_giyim",        "Hamile Giyim",       1),
            ("sevgili_kombini",     "Sevgili Kombini",    1),
        };

        var existingCodes = new HashSet<string>(
            await db.ProductGroups.Select(x => x.Code).ToListAsync());

        int added = 0;
        foreach (var (code, name, sort) in groups)
        {
            if (existingCodes.Contains(code)) continue;
            db.ProductGroups.Add(new ProductGroup
            {
                Id        = Guid.NewGuid(),
                Code      = code,
                NameI18n  = new Dictionary<string, string> { ["tr"] = name },
                IsActive  = true,
                SortOrder = sort,
                CreatedAt = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Ürün grupları — {added} yeni eklendi, {existingCodes.Count} zaten vardı.");
    }

    /// <summary>
    /// Telemania demo (kozmetik/kişisel bakım + karma mağaza) — kalıcı ürün grupları.
    /// Her Trendyol kategorisi AYRI bir ProductGroup olur (toplama grup yok; pazaryeri
    /// kategori eşlemesi birebir yapılabilsin). Idempotent: eksik eklenir, var olan korunur.
    /// </summary>
    private static async Task SeedTelemaniaProductGroupsAsync(CatalogDbContext db)
    {
        var groups = new (string code, string name, int sort)[]
        {
            ("tlm_termos",              "Termos",                      1),
            ("tlm_sac_boyasi",          "Saç Boyası",                  1),
            ("tlm_mug",                 "Mug",                         1),
            ("tlm_prezervatif",         "Prezervatif",                 1),
            ("tlm_emzik",               "Emzik",                       1),
            ("tlm_deodorant",           "Deodorant ve Roll on",        1),
            ("tlm_sampuan",             "Şampuan",                     1),
            ("tlm_sac_spreyi",          "Saç Spreyi",                  1),
            ("tlm_yuz_kremi",           "Yüz Kremi",                   1),
            ("tlm_sac_kremi",           "Saç Kremi",                   1),
            ("tlm_maskara",             "Maskara",                     1),
            ("tlm_sac_kopugu",          "Saç Köpüğü",                  1),
            ("tlm_termal_canta",        "Termal Çanta",                1),
            ("tlm_sac_maskesi",         "Saç Maskesi",                 1),
            ("tlm_dus_jeli",            "Duş Jeli",                    1),
            ("tlm_biberon",             "Biberon",                     1),
            ("tlm_yuz_gunes_kremi",     "Yüz Güneş Kremi",             1),
            ("tlm_sac_serumu",          "Saç Serum ve Yağı",           1),
            ("tlm_hasta_bezi",          "Hasta Bezi",                  1),
            ("tlm_vucut_gunes_kremi",   "Vücut Güneş Kremi",           1),
            ("tlm_protez_dis_bakim",    "Protez Diş Bakım",            1),
            ("tlm_kayganlastirici_jel", "Kayganlaştırıcı Jel",         1),
            ("tlm_hasere_ilaci",        "Haşere İlacı",                1),
            ("tlm_dis_macunu",          "Diş Macunu",                  1),
            ("tlm_cilt_serumu",         "Cilt Serumu",                 1),
            ("tlm_sogutucu_buzluk",     "Soğutucu & Buzluk",           1),
            ("tlm_sinek_ilaci",         "Sinek İlacı ve Kovucu",       1),
            ("tlm_sarjli_dis_fircasi",  "Şarj Edilebilir Diş Fırçası", 1),
            ("tlm_kulak_ustu_kulaklik", "Kulak Üstü Kablolu Kulaklık", 1),
            ("tlm_kulak_ici_kulaklik",  "Kulak İçi Kablolu Kulaklık",  1),
            ("tlm_kapsul_kahve",        "Kapsül Kahve",                1),
            ("tlm_kamp_yemek_seti",     "Kamp Yemek Seti",             1),
            ("tlm_bebek_sampuani",      "Bebek Şampuanı",              1),
            ("tlm_yatak_koruyucu",      "Yatak Koruyucu",              1),
            ("tlm_temizlik_bezi",       "Temizlik Bezi",               1),
            ("tlm_sac_fircasi",         "Saç Fırçası ve Tarak",        1),
            ("tlm_kamp_matarasi",       "Kamp Matarası",               1),
            ("tlm_goz_kremi",           "Göz Kremi",                   1),
            ("tlm_fondoten",            "Fondöten",                    1),
            ("tlm_el_kremi",            "El Kremi",                    1),
            ("tlm_cikolata",            "Çikolata",                    1),
            ("tlm_bebek_islak_mendil",  "Bebek Islak Mendil",          1),
            ("tlm_yuz_temizleyici",     "Yüz Temizleyici",             1),
            ("tlm_sac_bakim_seti",      "Saç Bakım Seti",              1),
            ("tlm_krem_santi",          "Krem Şanti",                  1),
            ("tlm_kadeh",               "Kadeh",                       1),
            ("tlm_dripper",             "Dripper",                     1),
            ("tlm_bitki_cayi",          "Diğer Bitki Çayları",         1),
            ("tlm_bulasik_sungeri",     "Bulaşık Süngeri ve Fırçası",  1),
            ("tlm_bebek_kremi",         "Bebek Kremi ve Yağı",         1),
            ("tlm_bebek_gunes_kremi",   "Bebek Güneş Kremi",           1),
            ("tlm_bardak",              "Bardak",                      1),
            ("tlm_aydinlatici",         "Aydınlatıcı",                 1),
            ("tlm_yuzey_temizleyici",   "Yüzey Temizleyici",           1),
            ("tlm_vucut_spreyi",        "Vücut Spreyi",                1),
            ("tlm_vucut_kremi",         "Vücut Kremi",                 1),
            ("tlm_tiras_bicagi",        "Tıraş Bıçağı",                1),
            ("tlm_spor_matara",         "Spor Matara",                 1),
            ("tlm_shaker",              "Shaker & Kokteyl Seti",       1),
            ("tlm_sac_tonigi",          "Saç Toniği",                  1),
            ("tlm_sac_sekillendirici",  "Saç Şekillendirici Krem ve Wax", 1),
            ("tlm_sac_parfumu",         "Saç Parfümü",                 1),
            ("tlm_makyaj_temizleyici",  "Makyaj Temizleyici",          1),
            ("tlm_kuru_sampuan",        "Kuru Şampuan",                1),
            ("tlm_gunes_sonrasi",       "Güneş Sonrası Ürünü",         1),
            ("tlm_goz_serumu",          "Göz Serumu",                  1),
            ("tlm_filtre_kahve",        "Filtre ve Çekirdek Kahve",    1),
            ("tlm_burun_bandi",         "Burun Bandı",                 1),
            ("tlm_burun_aspiatoru",     "Burun Aspiratörü",            1),
            ("tlm_bebek_bezi",          "Bebek Bezi",                  1),
            ("tlm_bb_cc_krem",          "BB ve CC Krem",               1),
            ("tlm_banyo_lifi",          "Banyo Lifi ve Süngeri",       1),
            ("tlm_ayak_kremi",          "Ayak Kremi",                  1),
        };

        var existingCodes = new HashSet<string>(
            await db.ProductGroups.Select(x => x.Code).ToListAsync());

        int added = 0;
        foreach (var (code, name, sort) in groups)
        {
            if (existingCodes.Contains(code)) continue;
            db.ProductGroups.Add(new ProductGroup
            {
                Id        = Guid.NewGuid(),
                Code      = code,
                NameI18n  = new Dictionary<string, string> { ["tr"] = name },
                IsActive  = true,
                SortOrder = sort,
                CreatedAt = DateTime.UtcNow,
            });
            existingCodes.Add(code);
            added++;
        }

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Telemania ürün grupları — {added} yeni eklendi.");
    }

    /// <summary>Kozmetik attribute'leri için değer şablonları (idempotent).</summary>
    private static async Task SeedTelemaniaAttributeValuesAsync(CatalogDbContext db)
    {
        var pools = new (string TypeCode, string[] Values)[]
        {
            ("hacim",     new[] { "50ml", "100ml", "150ml", "200ml", "250ml", "400ml", "500ml" }),
            ("cilt_tipi", new[] { "Kuru", "Yağlı", "Karma", "Normal", "Hassas", "Tüm Cilt Tipleri" }),
            ("sac_tipi",  new[] { "Kuru", "Yağlı", "Boyalı", "Hasarlı", "Normal", "Tüm Saç Tipleri" }),
            ("spf",       new[] { "SPF 15", "SPF 20", "SPF 25", "SPF 30", "SPF 50", "SPF 50+" }),
            // 2026-08-23 filtre zenginleştirmesi
            ("sac_tipi",  new[] { "İnce Telli", "Kıvırcık", "Dökülen" }),
            ("paket_adedi", new[] { "Tekli", "2'li", "3'lü", "4'lü", "5'li", "6'lı", "8'li", "10'lu", "12'li", "20'li", "24'lü", "30'lu", "60'lı", "120'li" }),
            ("yas_grubu", new[] { "0-6 Ay", "6-18 Ay", "18+ Ay", "20+", "30+", "40+", "50+", "65+" }),
            ("bez_bedeni", new[] { "Small", "Medium", "Large", "X-Large", "XX-Large" }),
            ("urun_formu", new[] { "Roll-On", "Sprey", "Stick", "Krem", "Jel", "Köpük", "Serum", "Yağ", "Losyon", "Toz" }),
            ("sac_rengi", new[] { "Sarı", "Kumral", "Kahve", "Kestane", "Kızıl", "Bakır", "Siyah", "Gri", "Platin", "Nude" }),
            ("kullanim_tipi", new[] { "Termos Bardak", "Yemek Termosu", "Matara", "Kupa", "Kamp Seti", "Soğutucu", "Termos" }),
            ("ek_ozellik", new[] { "Pipetli", "Kaşıklı", "Kulplu", "Paslanmaz Çelik" }),
        };

        var codes = pools.Select(p => p.TypeCode).ToArray();
        var types = await db.AttributeTypes
            .Where(a => codes.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, a => a.Id);

        int added = 0;
        foreach (var (typeCode, values) in pools)
        {
            if (!types.TryGetValue(typeCode, out var typeId)) continue;
            var existingNames = new HashSet<string>(await db.AttributeValues
                .Where(v => v.AttributeTypeId == typeId)
                .Select(v => v.NameI18n["tr"])
                .ToListAsync());
            int sort = 10;
            foreach (var value in values)
            {
                if (existingNames.Contains(value)) continue;
                db.AttributeValues.Add(new AttributeValue
                {
                    Id = Guid.NewGuid(),
                    AttributeTypeId = typeId,
                    NameI18n = new Dictionary<string, string> { ["tr"] = value, ["en"] = value },
                    SortOrder = sort,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
                sort += 10;
                added++;
            }
        }

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Telemania attribute değer şablonları — {added} yeni eklendi.");
    }

    /// <summary>
    /// İçecek/soğutucu katalog değer havuzları (idempotent): hacim (litre/ml) + renk (hex'li).
    /// SortOrder gerçek kapasite (ml) olarak tutulur; böylece facet değerleri birim karışık
    /// olsa bile küçükten büyüğe sıralanır. Kozmetik hacimleri "50ml" kalıbında, içecek
    /// hacimleri doğal Türkçe perakende biçiminde ("0,35 L") tutulur — farklı gruplardır.
    /// </summary>
    private static async Task SeedTelemaniaDrinkwareAttributeValuesAsync(CatalogDbContext db)
    {
        // (gösterim, ml cinsinden sortOrder)
        var volumes = new (string Name, int Ml)[]
        {
            ("0,23 L", 230),
            ("0,35 L", 350),
            ("0,40 L", 400),
            ("0,42 L", 420),
            ("0,47 L", 470),
            ("0,53 L", 530),
            ("0,59 L", 590),
            ("0,60 L", 600),
            ("0,70 L", 700),
            ("0,71 L", 710),
            ("0,75 L", 750),
            ("0,80 L", 800),
            ("0,89 L", 890),
            ("0,94 L", 940),
            ("1 L",    1000),
            ("1,06 L", 1060),
            ("1,18 L", 1180),
            ("1,40 L", 1400),
            ("1,90 L", 1900),
            ("4 L",    4000),
            ("6,6 L",  6600),
            ("7 L",    7000),
            ("9,5 L",  9500),
            ("14 L",   14000),
            ("15,1 L", 15100),
            ("23 L",   23000),
            ("28,3 L", 28300),
            ("47 L",   47000),
            // Biberon (ml)
            ("125ml",  125),
            ("240ml",  240),
            ("260ml",  260),
            ("330ml",  330),
        };

        // (tr, en, hex, sortOrder) — filtre_rengi paletiyle aynı tonlar + içecek özgü renkler
        var colors = new (string Tr, string En, string Hex, int Sort)[]
        {
            ("Siyah",      "Black",       "#000000", 10),
            ("Beyaz",      "White",       "#FFFFFF", 20),
            ("Gri",        "Grey",        "#808080", 30),
            ("Kırmızı",    "Red",         "#E53935", 40),
            ("Pembe",      "Pink",        "#EC407A", 50),
            ("Turuncu",    "Orange",      "#FB8C00", 60),
            ("Sarı",       "Yellow",      "#FDD835", 70),
            ("Bej",        "Beige",       "#F5F0DC", 80),
            ("Krem",       "Cream",       "#FFFDD0", 90),
            ("Yeşil",      "Green",       "#43A047", 100),
            ("Haki",       "Khaki",       "#8D7156", 110),
            ("Mavi",       "Blue",        "#1E88E5", 120),
            ("Koyu Mavi",  "Dark Blue",   "#0D47A1", 130),
            ("Lacivert",   "Navy",        "#1A237E", 140),
            ("Turkuaz",    "Turquoise",   "#00BCD4", 150),
            ("Mor",        "Purple",      "#8E24AA", 160),
            ("Lila",       "Lilac",       "#CE93D8", 170),
            ("Kahverengi", "Brown",       "#6D4C41", 180),
            ("Altın",      "Gold",        "#FFD600", 190),
            ("Gümüş",      "Silver",      "#B0BEC5", 200),
            ("Bordo",      "Burgundy",    "#7B1F2D", 210),
            ("Pudra",      "Powder Pink", "#F2D5D5", 220),
            ("Fuşya",      "Fuchsia",     "#D5007F", 230),
            ("Eflatun",    "Lilac",       "#C4A0E8", 240),
            ("Kamuflaj",   "Camouflage",  "#6B705C", 250),
            ("Çelik",      "Steel",       "#90A4AE", 260),
            ("Doğal",      "Natural",     "#E8D8B8", 270),
        };

        var types = await db.AttributeTypes
            .Where(a => a.Code == "hacim" || a.Code == "renk")
            .ToDictionaryAsync(a => a.Code, a => a.Id);

        int added = 0;

        if (types.TryGetValue("hacim", out var hacimId))
        {
            var existing = new HashSet<string>(await db.AttributeValues
                .Where(v => v.AttributeTypeId == hacimId)
                .Select(v => v.NameI18n["tr"])
                .ToListAsync());
            foreach (var (name, ml) in volumes)
            {
                if (existing.Contains(name)) continue;
                db.AttributeValues.Add(new AttributeValue
                {
                    Id = Guid.NewGuid(),
                    AttributeTypeId = hacimId,
                    NameI18n = new Dictionary<string, string> { ["tr"] = name, ["en"] = name },
                    SortOrder = ml,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
                added++;
            }
        }

        if (types.TryGetValue("renk", out var renkId))
        {
            var existing = new HashSet<string>(await db.AttributeValues
                .Where(v => v.AttributeTypeId == renkId)
                .Select(v => v.NameI18n["tr"])
                .ToListAsync());
            foreach (var (tr, en, hex, sort) in colors)
            {
                if (existing.Contains(tr)) continue;
                db.AttributeValues.Add(new AttributeValue
                {
                    Id = Guid.NewGuid(),
                    AttributeTypeId = renkId,
                    NameI18n = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                    HexCode = hex,
                    SortOrder = sort,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                });
                added++;
            }
        }

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Telemania içecek/renk değer havuzu — {added} yeni eklendi.");
    }

    /// <summary>Kozmetik ürün gruplarına özellik atamaları (idempotent).</summary>
    private static async Task SeedTelemaniaProductGroupAttributesAsync(CatalogDbContext db)
    {
        var attrTypes = await db.AttributeTypes.ToDictionaryAsync(a => a.Code, a => a.Id);
        var groups = await db.ProductGroups.ToDictionaryAsync(g => g.Code, g => g.Id);

        var existingPgas = await db.ProductGroupAttributes
            .Select(x => new { x.ProductGroupId, x.AttributeTypeId }).ToListAsync();
        var pgaSet = new HashSet<(Guid, Guid)>(existingPgas.Select(x => (x.ProductGroupId, x.AttributeTypeId)));

        int added = 0;
        void Attr(string grpCode, string attrCode, bool isVariant, bool isPrimary, bool isRequired, int sort)
        {
            if (!groups.TryGetValue(grpCode, out var gid)) return;
            if (!attrTypes.TryGetValue(attrCode, out var aid)) return;
            if (pgaSet.Contains((gid, aid))) return;
            db.ProductGroupAttributes.Add(new ProductGroupAttribute
            {
                Id = Guid.NewGuid(), ProductGroupId = gid, AttributeTypeId = aid,
                IsVariant = isVariant, IsPrimaryAxis = isPrimary,
                IsRequired = isRequired, SortOrder = sort, CreatedAt = DateTime.UtcNow
            });
            pgaSet.Add((gid, aid));
            added++;
        }

        // Kozmetik grupları: hacim (spec) + tip özelliği + varsa renk (varyant ekseni).
        void Cosmetic(string g, string? tipAttr, bool renkVaryant = false)
        {
            Attr(g, "hacim", false, false, false, 1);
            if (tipAttr is not null) Attr(g, tipAttr, false, false, false, 2);
            if (renkVaryant) Attr(g, "renk", true, true, false, 3);
        }

        Cosmetic("tlm_sampuan", "sac_tipi");
        Cosmetic("tlm_kuru_sampuan", "sac_tipi");
        Cosmetic("tlm_bebek_sampuani", "sac_tipi");
        Cosmetic("tlm_sac_kremi", "sac_tipi");
        Cosmetic("tlm_sac_maskesi", "sac_tipi");
        Cosmetic("tlm_sac_bakim_seti", "sac_tipi");
        Cosmetic("tlm_sac_tonigi", "sac_tipi");
        Cosmetic("tlm_sac_serumu", "sac_tipi");
        Cosmetic("tlm_sac_spreyi", "sac_tipi");
        Cosmetic("tlm_sac_kopugu", "sac_tipi");
        Cosmetic("tlm_sac_sekillendirici", "sac_tipi");
        Cosmetic("tlm_sac_parfumu", null);
        Cosmetic("tlm_sac_boyasi", "sac_tipi", renkVaryant: true);
        Cosmetic("tlm_sac_fircasi", null);
        Cosmetic("tlm_deodorant", null);
        Cosmetic("tlm_dus_jeli", "cilt_tipi");
        Cosmetic("tlm_yuz_kremi", "cilt_tipi");
        Cosmetic("tlm_cilt_serumu", "cilt_tipi");
        Cosmetic("tlm_goz_kremi", "cilt_tipi");
        Cosmetic("tlm_goz_serumu", "cilt_tipi");
        Cosmetic("tlm_bb_cc_krem", "cilt_tipi", renkVaryant: true);
        Cosmetic("tlm_el_kremi", "cilt_tipi");
        Cosmetic("tlm_vucut_kremi", "cilt_tipi");
        Cosmetic("tlm_ayak_kremi", "cilt_tipi");
        Cosmetic("tlm_bebek_kremi", "cilt_tipi");
        Cosmetic("tlm_vucut_spreyi", "cilt_tipi");
        Cosmetic("tlm_yuz_temizleyici", "cilt_tipi");
        Cosmetic("tlm_makyaj_temizleyici", "cilt_tipi");
        Cosmetic("tlm_yuz_gunes_kremi", "spf");
        Cosmetic("tlm_vucut_gunes_kremi", "spf");
        Cosmetic("tlm_bebek_gunes_kremi", "spf");
        Cosmetic("tlm_gunes_sonrasi", "spf");
        Cosmetic("tlm_maskara", null, renkVaryant: true);
        Cosmetic("tlm_fondoten", "cilt_tipi", renkVaryant: true);
        Cosmetic("tlm_aydinlatici", "cilt_tipi", renkVaryant: true);
        Cosmetic("tlm_dis_macunu", null);
        Cosmetic("tlm_sarjli_dis_fircasi", null);
        Cosmetic("tlm_protez_dis_bakim", null);
        Cosmetic("tlm_banyo_lifi", null);

        // İçecek / soğutucu grupları: hacim (ürün seviyesi spec) + renk (varyant ekseni).
        // Biberon hacmi ml cinsinden, diğerleri litre cinsinden — değer havuzunda ikisi de var.
        void Drinkware(string g)
        {
            Attr(g, "hacim", false, false, false, 1);
            Attr(g, "renk", true, true, false, 2);
        }

        Drinkware("tlm_termos");
        Drinkware("tlm_mug");
        Drinkware("tlm_bardak");
        Drinkware("tlm_kadeh");
        Drinkware("tlm_kamp_matarasi");
        Drinkware("tlm_spor_matara");
        Drinkware("tlm_shaker");
        Drinkware("tlm_sogutucu_buzluk");
        Drinkware("tlm_termal_canta");
        Drinkware("tlm_kamp_yemek_seti");
        Drinkware("tlm_biberon");
        // Demleyicinin kapasitesi yok; yalnız renk ekseni anlamlı.
        Attr("tlm_dripper", "renk", true, true, false, 1);

        // 2026-08-23 filtre zenginleştirmesi — değerler SeedTelemaniaFilterEnrichmentAsync ile ürün adlarından türetilir
        foreach (var g in new[] { "tlm_termos", "tlm_mug", "tlm_kamp_matarasi", "tlm_spor_matara", "tlm_sogutucu_buzluk", "tlm_termal_canta", "tlm_kamp_yemek_seti", "tlm_bardak", "tlm_kadeh", "tlm_shaker" })
        { Attr(g, "kullanim_tipi", false, false, false, 10); Attr(g, "ek_ozellik", false, false, false, 11); }
        foreach (var g in new[] { "tlm_prezervatif", "tlm_emzik", "tlm_hasta_bezi", "tlm_bebek_bezi", "tlm_bebek_islak_mendil", "tlm_sac_boyasi", "tlm_maskara", "tlm_kapsul_kahve", "tlm_dis_macunu", "tlm_temizlik_bezi", "tlm_bulasik_sungeri", "tlm_tiras_bicagi", "tlm_burun_bandi", "tlm_yatak_koruyucu", "tlm_cikolata", "tlm_krem_santi", "tlm_bitki_cayi", "tlm_filtre_kahve" })
            Attr(g, "paket_adedi", false, false, false, 12);
        foreach (var g in new[] { "tlm_emzik", "tlm_biberon", "tlm_yuz_kremi", "tlm_goz_kremi", "tlm_cilt_serumu", "tlm_goz_serumu", "tlm_bebek_sampuani", "tlm_bebek_kremi", "tlm_bebek_gunes_kremi" })
            Attr(g, "yas_grubu", false, false, false, 13);
        foreach (var g in new[] { "tlm_hasta_bezi", "tlm_bebek_bezi" }) Attr(g, "bez_bedeni", false, false, false, 14);
        foreach (var g in new[] { "tlm_deodorant", "tlm_sac_spreyi", "tlm_sac_kopugu", "tlm_sac_sekillendirici", "tlm_sac_serumu", "tlm_yuz_temizleyici", "tlm_makyaj_temizleyici", "tlm_hasere_ilaci", "tlm_sinek_ilaci", "tlm_yuzey_temizleyici", "tlm_yuz_gunes_kremi", "tlm_vucut_gunes_kremi", "tlm_bebek_gunes_kremi", "tlm_gunes_sonrasi", "tlm_vucut_spreyi", "tlm_vucut_kremi", "tlm_el_kremi", "tlm_bebek_kremi" })
            Attr(g, "urun_formu", false, false, false, 15);
        Attr("tlm_sac_boyasi", "sac_rengi", false, false, false, 16);
        foreach (var g in new[] { "tlm_deodorant", "tlm_tiras_bicagi", "tlm_kulak_ustu_kulaklik", "tlm_kulak_ici_kulaklik", "tlm_vucut_spreyi", "tlm_emzik" })
            Attr(g, "cinsiyet", false, false, false, 17);
        // kozmetik gruplarında hacim zaten atanmış; hasta/bebek bezi + islak mendil + prezervatif için hacim anlamsız

        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Telemania grup özellik atamaları — {added} yeni eklendi.");
    }

    private static async Task SeedProductGroupAttributesAsync(CatalogDbContext db)
    {
        var attrTypes = await db.AttributeTypes
            .ToDictionaryAsync(a => a.Code, a => a.Id);
        var groups = await db.ProductGroups
            .ToDictionaryAsync(g => g.Code, g => g.Id);

        var existingPgas = await db.ProductGroupAttributes
            .Select(x => new { x.ProductGroupId, x.AttributeTypeId })
            .ToListAsync();
        var pgaSet = new HashSet<(Guid, Guid)>(existingPgas.Select(x => (x.ProductGroupId, x.AttributeTypeId)));

        var existingSubAttrs = await db.ProductGroupAxisSubAttributes
            .Select(x => new { x.ProductGroupId, x.AxisAttributeTypeId, x.SubAttributeTypeId })
            .ToListAsync();
        var subSet = new HashSet<(Guid, Guid, Guid)>(existingSubAttrs.Select(x => (x.ProductGroupId, x.AxisAttributeTypeId, x.SubAttributeTypeId)));

        int added = 0, subAdded = 0;

        void Attr(string grpCode, string attrCode, bool isVariant, bool isPrimary, bool isRequired, int sort)
        {
            if (!groups.TryGetValue(grpCode, out var gid)) return;
            if (!attrTypes.TryGetValue(attrCode, out var aid)) return;
            if (pgaSet.Contains((gid, aid))) return;
            db.ProductGroupAttributes.Add(new ProductGroupAttribute
            {
                Id = Guid.NewGuid(), ProductGroupId = gid, AttributeTypeId = aid,
                IsVariant = isVariant, IsPrimaryAxis = isPrimary,
                IsRequired = isRequired, SortOrder = sort, CreatedAt = DateTime.UtcNow
            });
            pgaSet.Add((gid, aid));
            added++;
        }

        void Sub(string grpCode, string axisCode, string subCode, bool isRequired, int sort)
        {
            if (!groups.TryGetValue(grpCode, out var gid)) return;
            if (!attrTypes.TryGetValue(axisCode, out var aid)) return;
            if (!attrTypes.TryGetValue(subCode, out var sid)) return;
            if (subSet.Contains((gid, aid, sid))) return;
            db.ProductGroupAxisSubAttributes.Add(new ProductGroupAxisSubAttribute
            {
                Id = Guid.NewGuid(), ProductGroupId = gid, AxisAttributeTypeId = aid,
                SubAttributeTypeId = sid, IsRequired = isRequired, SortOrder = sort,
                CreatedAt = DateTime.UtcNow
            });
            subSet.Add((gid, aid, sid));
            subAdded++;
        }

        // Giyim: renk (V,P) + beden (V) + material + cinsiyet + season + esneklik + ekstralar
        // + ürün boyu (beden eksenine bağlı ölçü sub-attribute'ü — hemen hemen tüm giyimde geçerli)
        // astar_durumu/boy/fermuar/kumas_turu/yas_grubu: 2026-07-02 legacy veri analizinde neredeyse
        // tüm giyim gruplarında gerçek değer bulunduğu görüldü (bkz. project_phase13_product_attribute_values_2026-07-02),
        // bu yüzden extras yerine ortak base attribute'e taşındı.
        void Cloth(string g, params string[] extras)
        {
            Attr(g, "renk",      true,  true,  true,  1);
            Attr(g, "beden",     true,  false, true,  2);
            Attr(g, "malzeme",  false, false, false, 3);
            Attr(g, "cinsiyet",  false, false, false, 4);
            Attr(g, "season",    false, false, false, 5);
            Attr(g, "esneklik",  false, false, false, 6);
            Attr(g, "manken",    false, false, false, 99);
            Attr(g, "astar_durumu", false, false, false, 100);
            Attr(g, "boy",          false, false, false, 101);
            Attr(g, "fermuar",      false, false, false, 102);
            Attr(g, "kumas_turu",   false, false, false, 103);
            Attr(g, "yas_grubu",    false, false, false, 104);
            int s = 7;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
            Sub(g, "beden", "urun_boyu", false, 50);
        }

        // Ayakkabı: renk (V,P) + beden (V) + material + cinsiyet + season + taban/dış materyal + ekstralar
        // astar_durumu/yas_grubu: 2026-07-02 legacy veri analizinde ayakkabı gruplarında da gerçek
        // değer bulunduğu görüldü, base attribute'e taşındı (bkz. project_phase13_product_attribute_values_2026-07-02).
        void Shoe(string g, params string[] extras)
        {
            Attr(g, "renk",     true,  true,  true,  1);
            Attr(g, "beden",    true,  false, true,  2);
            Attr(g, "malzeme", false, false, false, 3);
            Attr(g, "cinsiyet", false, false, false, 4);
            Attr(g, "season",   false, false, false, 5);
            Attr(g, "taban_ozelligi",   false, false, false, 6);
            Attr(g, "taban_yuksekligi", false, false, false, 7);
            Attr(g, "dis_materyal",     false, false, false, 8);
            Attr(g, "ic_yuzey",         false, false, false, 9);
            Attr(g, "manken",           false, false, false, 99);
            Attr(g, "astar_durumu",     false, false, false, 100);
            Attr(g, "yas_grubu",        false, false, false, 101);
            int s = 10;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
        }

        // Aksesuar/çanta: renk (V,P) + material + cinsiyet + season + ekstralar (beden yok)
        // astar_durumu/fermuar/kumas_turu/yas_grubu: 2026-07-02 legacy veri analizinde çanta/aksesuar
        // gruplarında da gerçek değer bulunduğu görüldü, base attribute'e taşındı
        // (bkz. project_phase13_product_attribute_values_2026-07-02).
        void Acc(string g, params string[] extras)
        {
            Attr(g, "renk",     true,  true,  true,  1);
            Attr(g, "malzeme", false, false, false, 2);
            Attr(g, "cinsiyet", false, false, false, 3);
            Attr(g, "season",   false, false, false, 4);
            Attr(g, "astar_durumu", false, false, false, 100);
            Attr(g, "fermuar",      false, false, false, 101);
            Attr(g, "kumas_turu",   false, false, false, 102);
            Attr(g, "yas_grubu",    false, false, false, 103);
            int s = 5;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
        }

        // Ev/tekstil/elektronik: renk (V,P) + ekstralar
        void Home(string g, params string[] extras)
        {
            Attr(g, "renk", true, true, false, 1);
            int s = 2;
            foreach (var e in extras) Attr(g, e, false, false, false, s++);
        }

        // Kolu olan üst giyim için beden eksenine bağlı ölçü sub-attribute'leri
        void UstOlcu(string g)
        {
            Sub(g, "beden", "gogus",          false, 10);
            Sub(g, "beden", "kol_boyu",       false, 11);
            Sub(g, "beden", "omuz_genisligi", false, 12);
        }

        // ── ÜST GİYİM ───────────────────────────────────────────────────
        Cloth("grp_6",   "desen", "kol_tipi", "yaka_tipi", "kalip");           // Bluz
        UstOlcu("grp_6");
        Cloth("grp_16",  "desen", "kol_tipi");                                          // Bolero
        UstOlcu("grp_16");
        Cloth("grp_9",   "desen", "kapatma_tipi", "balen", "dolgu");                    // Bustiyer
        Cloth("grp_46",  "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "cep_tipi", "fermuar", "ic_cep"); // Ceket
        UstOlcu("grp_46");
        Cloth("grp_47",  "desen", "kalip");                                     // Eşofman
        Cloth("grp_5",   "desen", "kol_tipi", "yaka_tipi", "kalip");           // Gömlek
        UstOlcu("grp_5");
        Cloth("grp_12",  "desen", "kol_tipi", "yaka_tipi", "kalip");                   // Hırka
        UstOlcu("grp_12");
        Cloth("grp_77",  "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu");   // Kap
        UstOlcu("grp_77");
        Cloth("grp_198", "desen", "kol_tipi", "kalip");                                // Kimono
        UstOlcu("grp_198");
        Cloth("grp_73",  "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "kalinlik", "cep_tipi", "fermuar", "ic_cep"); // Mont
        UstOlcu("grp_73");
        Cloth("grp_80",  "desen", "kalip");                                             // Panço
        Cloth("grp_11",  "desen", "kol_tipi", "yaka_tipi", "kalip");           // Sweatshirt
        UstOlcu("grp_11");
        Cloth("grp_7",   "desen", "kol_tipi", "yaka_tipi", "kalip");           // T-Shirt
        UstOlcu("grp_7");
        Cloth("grp_262", "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "fermuar", "ic_cep"); // Trençkot
        UstOlcu("grp_262");
        Cloth("grp_14",  "desen", "kol_tipi", "yaka_tipi", "kalip", "kalinlik");       // Triko
        UstOlcu("grp_14");
        Cloth("grp_18",  "desen", "kol_tipi", "yaka_tipi", "kalip");                   // Tunik
        UstOlcu("grp_18");
        Cloth("grp_17",  "desen", "yaka_tipi", "kalip");                               // Yelek

        // ── ALT GİYİM ───────────────────────────────────────────────────
        // Etek: paca_tipi yok; bel_tipi + etek_tipi grup seviyesinde
        Cloth("grp_10",  "desen", "kalip", "bel_tipi", "etek_tipi");
        Sub("grp_10",  "beden", "bel",   false, 1);
        Sub("grp_10",  "beden", "basen", false, 2);

        // Kapri: paca_tipi ve bel_tipi grup seviyesinde; bel ölçüsü beden sub-attr
        Cloth("grp_254", "desen", "kalip", "bel_tipi", "paca_tipi");
        Sub("grp_254", "beden", "bel",        false, 1);
        Sub("grp_254", "beden", "ic_uzunluk", false, 2);
        Sub("grp_254", "beden", "basen",      false, 3);

        // Pantolon: paca_tipi ve bel_tipi grup seviyesinde; bel + iç uzunluk beden sub-attr
        Cloth("grp_3",   "desen", "kalip", "bel_tipi", "paca_tipi", "cep_tipi");
        Sub("grp_3",   "beden", "bel",        false, 1);
        Sub("grp_3",   "beden", "ic_uzunluk", false, 2);
        Sub("grp_3",   "beden", "basen",      false, 3);

        // Şort: bel_tipi grup seviyesinde; bel ölçüsü beden sub-attr
        Cloth("grp_95",  "desen", "kalip", "bel_tipi");
        Sub("grp_95",  "beden", "bel",   false, 1);
        Sub("grp_95",  "beden", "basen", false, 2);

        // ── ELBİSE / TAM VÜCUT ──────────────────────────────────────────
        Cloth("grp_1",   "desen", "kol_tipi", "yaka_tipi", "kalip", "etek_tipi");
        Sub("grp_1",   "beden", "bel",   false, 1);
        Sub("grp_1",   "beden", "basen", false, 2);
        UstOlcu("grp_1");

        Cloth("grp_44",  "desen", "kol_tipi", "yaka_tipi", "kapatma_tipi", "balen", "dolgu"); // Body
        UstOlcu("grp_44");
        Cloth("grp_149", "desen");                                              // Kostüm
        Cloth("grp_36",  "desen", "kol_tipi", "yaka_tipi", "kalip");                   // Tulum
        UstOlcu("grp_36");

        // ── TAKIM / SET ─────────────────────────────────────────────────
        Cloth("grp_48",  "desen", "kalip");                                    // İkili Takım
        Cloth("grp_63",  "desen", "kalip", "bel_tipi", "paca_tipi");          // Takım Elbise
        Sub("grp_63",  "beden", "bel",        false, 1);
        Sub("grp_63",  "beden", "ic_uzunluk", false, 2);
        Sub("grp_63",  "beden", "basen",      false, 3);

        // ── İÇ GİYİM / PİJAMA ───────────────────────────────────────────
        Cloth("grp_118", "desen", "kapatma_tipi", "balen", "dolgu");                   // İç Giyim
        Cloth("grp_15",  "desen", "kalip");                                            // Pijama

        // ── SPOR / AKTİF GİYİM ─────────────────────────────────────────
        Cloth("grp_70",  "desen", "kalip");                                    // Aktif Spor

        // ── PLAJ / BANYO GİYİM ──────────────────────────────────────────
        Cloth("grp_123", "desen", "kalip");                                            // Plaj Giyim
        Cloth("grp_165", "desen");                                                     // Banyo Giyim

        // ── BEBEK ───────────────────────────────────────────────────────
        Cloth("grp_159", "desen", "yas_grubu");                                        // Zıbın

        // ── AYAKKABI ────────────────────────────────────────────────────
        Shoe("grp_25",   "topuk_boyu", "topuk_tipi");                                  // Babet
        Shoe("grp_21",   "topuk_boyu", "topuk_tipi", "astar_durumu", "ortam", "fermuar"); // Bot
        Shoe("grp_24",   "topuk_boyu", "topuk_tipi", "astar_durumu", "ortam", "fermuar"); // Çizme
        Shoe("grp_27",   "topuk_boyu", "topuk_tipi");                                  // Sandalet
        Shoe("grp_33",   "ortam");                                                     // Terlik

        // ── AKSESUAR / ŞAL / ÇANTA ──────────────────────────────────────
        Acc("grp_174", "desen");                                                       // Şal
        Acc("grp_2");                                                                  // Aksesuar
        Acc("grp_83",  "desen", "kapatma_tipi", "fermuar", "ic_cep", "canta_agzi", "aski_tipi", "aski_boyu"); // Çanta

        // ── EV TEKSTİL / ÇEYİZ ─────────────────────────────────────────
        Home("grp_183", "malzeme", "desen", "kumas_turu", "fermuar");         // Ev Tekstil
        Home("grp_186", "malzeme", "desen");                                          // Çeyiz Setleri
        Home("grp_185", "malzeme");                                           // Banyo ve Ev Gereçleri

        // ── ELEKTRONİK / EV ALETLERİ ───────────────────────────────────
        Home("grp_176", "malzeme");                                                   // Telefon ve Aksesuarları
        Home("grp_177", "malzeme");                                                   // Bilgisayar
        Home("grp_178");                                                               // Televizyon ve Aksesuar
        Home("grp_179");                                                               // Elektirikli Ev Aletleri
        Home("grp_180");                                                               // Beyaz Eşya
        Home("grp_181", "malzeme");                                           // Mobilyalar
        Home("grp_182", "malzeme");                                           // Dekorasyon ve Aydınlatma
        Home("grp_184", "malzeme");                                                   // Mutfak Gereçleri

        // ── KİŞİSEL BAKIM / MAKYAJ ─────────────────────────────────────
        Home("grp_132");                                                               // Kişisel Bakım
        Home("grp_137");                                                               // Makyaj Malzemeleri
        Home("grp_269", "malzeme");                                                   // Kulaklık

        // ── YENİ GRUPLAR — Ayakkabı ─────────────────────────────────────
        Shoe("spor_ayakkabi",        "ortam");                                         // Spor Ayakkabı
        Shoe("gunluk_ayakkabi",      "topuk_boyu", "topuk_tipi", "ortam");             // Günlük Ayakkabı
        Shoe("topuklu_ayakkabi",     "topuk_boyu", "topuk_tipi");                      // Topuklu Ayakkabı
        Shoe("stiletto",             "topuk_boyu", "topuk_tipi");                      // Stiletto
        Shoe("klasik_ayakkabi",      "topuk_boyu", "topuk_tipi", "astar_durumu", "ortam"); // Klasik Ayakkabı
        Shoe("pelus_terlik",         "ortam");                                         // Peluş Terlik

        // ── YENİ GRUPLAR — Giyim ────────────────────────────────────────
        Cloth("kaban",           "desen", "kol_tipi", "yaka_tipi", "kalip", "astar_durumu", "kapatma_tipi", "kalinlik", "cep_tipi", "fermuar", "ic_cep"); // Kaban
        UstOlcu("kaban");
        Cloth("atlet",           "desen", "yaka_tipi");                                 // Atlet
        Cloth("hamile_giyim",    "desen", "kol_tipi", "yaka_tipi", "kalip");    // Hamile Giyim
        UstOlcu("hamile_giyim");
        Sub("hamile_giyim", "beden", "basen", false, 1);
        Cloth("ferace",          "desen", "kol_tipi", "yaka_tipi", "kalip");            // Ferace
        UstOlcu("ferace");
        Cloth("sevgili_kombini", "desen", "kalip");                             // Sevgili Kombini
        Cloth("bornoz",          "desen", "kalip");                                     // Bornoz

        // ── YENİ GRUPLAR — Aksesuar ─────────────────────────────────────
        Acc("bone",  "desen");                                                          // Bone
        Acc("esarp", "desen");                                                          // Eşarp

        // ── YENİ GRUPLAR — Ev Tekstil ───────────────────────────────────
        Home("havlu",    "malzeme", "desen");                                          // Havlu
        Home("pestemal", "malzeme", "desen");                                          // Peştemal

        if (added > 0 || subAdded > 0)
            await db.SaveChangesAsync();

        Console.WriteLine($"✓ Seed: {added} grup özelliği, {subAdded} eksen alt özelliği eklendi.");
    }

    private static async Task SeedFilterRengiAttributeTypeAsync(CatalogDbContext context)
    {
        const string typeCode = "filtre_rengi";

        var attrType = await context.AttributeTypes.FirstOrDefaultAsync(a => a.Code == typeCode);
        if (attrType is null)
        {
            attrType = new AttributeType
            {
                Id        = Guid.NewGuid(),
                Code      = typeCode,
                NameI18n  = new Dictionary<string, string> { ["tr"] = "Filtre Rengi", ["en"] = "Filter Color" },
                DataType  = "select",
                IsActive  = true,
                // Filtre alanı sırası: cinsiyet(10) > marka(20) > beden(30) > filtre_rengi(40)
                SortOrder = 40,
                UseInFilter = true,
                CreatedAt = DateTime.UtcNow,
            };
            context.AttributeTypes.Add(attrType);
            await context.SaveChangesAsync();
            Console.WriteLine("✓ Seed: 'filtre_rengi' attribute type eklendi.");
        }

        var colors = new[]
        {
            ("siyah",      "Siyah",      "Black",       "#000000"),
            ("beyaz",      "Beyaz",      "White",       "#FFFFFF"),
            ("gri",        "Gri",        "Grey",        "#808080"),
            ("acik_gri",   "Açık Gri",   "Light Grey",  "#D3D3D3"),
            ("koyu_gri",   "Koyu Gri",   "Dark Grey",   "#404040"),
            ("kirmizi",    "Kırmızı",    "Red",         "#E53935"),
            ("pembe",      "Pembe",      "Pink",        "#EC407A"),
            ("turuncu",    "Turuncu",    "Orange",      "#FB8C00"),
            ("sari",       "Sarı",       "Yellow",      "#FDD835"),
            ("bej",        "Bej",        "Beige",       "#F5F0DC"),
            ("krem",       "Krem",       "Cream",       "#FFFDD0"),
            ("yesil",      "Yeşil",      "Green",       "#43A047"),
            ("acik_yesil", "Açık Yeşil", "Light Green", "#A5D6A7"),
            ("koyu_yesil", "Koyu Yeşil", "Dark Green",  "#1B5E20"),
            ("haki",       "Haki",       "Khaki",       "#8D7156"),
            ("mavi",       "Mavi",       "Blue",        "#1E88E5"),
            ("acik_mavi",  "Açık Mavi",  "Light Blue",  "#90CAF9"),
            ("koyu_mavi",  "Koyu Mavi",  "Dark Blue",   "#0D47A1"),
            ("lacivert",   "Lacivert",   "Navy",        "#1A237E"),
            ("turkuaz",    "Turkuaz",    "Turquoise",   "#00BCD4"),
            ("mor",        "Mor",        "Purple",      "#8E24AA"),
            ("lila",       "Lila",       "Lilac",       "#CE93D8"),
            ("kahve",      "Kahve",      "Brown",       "#6D4C41"),
            ("altin",      "Altın",      "Gold",        "#FFD600"),
            ("gumus",      "Gümüş",      "Silver",      "#B0BEC5"),
        };

        var existingNames = (await context.AttributeValues
            .Where(v => v.AttributeTypeId == attrType.Id)
            .Select(v => v.NameI18n)
            .ToListAsync())
            .Select(n => n.GetValueOrDefault("tr", "").ToUpperInvariant())
            .ToHashSet();

        int added = 0;
        foreach (var (_, tr, en, hex) in colors)
        {
            if (existingNames.Contains(tr.ToUpperInvariant())) continue;
            context.AttributeValues.Add(new AttributeValue
            {
                Id              = Guid.NewGuid(),
                AttributeTypeId = attrType.Id,
                NameI18n        = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                HexCode         = hex,
                SortOrder       = added * 10,
                IsActive        = true,
                CreatedAt       = DateTime.UtcNow,
            });
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: {added} filtre rengi değeri eklendi.");
        }
    }

    private static async Task SeedCinsiyetValuesAsync(CatalogDbContext db)
    {
        var attrType = await db.AttributeTypes.FirstOrDefaultAsync(a => a.Code == "cinsiyet");
        if (attrType is null) return;

        var values = new (string Tr, string En, int Sort)[]
        {
            ("Erkek",  "Male",   10),
            ("Kadın",  "Female", 20),
            ("Unisex", "Unisex", 30),
        };

        var existing = new HashSet<string>(
            await db.AttributeValues
                .Where(v => v.AttributeTypeId == attrType.Id)
                .Select(v => v.NameI18n["tr"])
                .ToListAsync());

        int added = 0;
        foreach (var (tr, en, sort) in values)
        {
            if (existing.Contains(tr)) continue;
            db.AttributeValues.Add(new AttributeValue
            {
                Id = Guid.NewGuid(), AttributeTypeId = attrType.Id,
                NameI18n = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                SortOrder = sort, IsActive = true, CreatedAt = DateTime.UtcNow,
            });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Cinsiyet değerleri — {added} yeni eklendi.");
    }

    private static async Task SeedYasGrubuValuesAsync(CatalogDbContext db)
    {
        var attrType = await db.AttributeTypes.FirstOrDefaultAsync(a => a.Code == "yas_grubu");
        if (attrType is null) return;

        var values = new (string Tr, string En, int Sort)[]
        {
            ("Yeni Doğan", "Newborn", 10),
            ("Bebek",      "Baby",    20),
            ("Çocuk",      "Kids",    30),
            ("Genç",       "Teen",    40),
            ("Yetişkin",   "Adult",   50),
        };

        var existing = new HashSet<string>(
            await db.AttributeValues
                .Where(v => v.AttributeTypeId == attrType.Id)
                .Select(v => v.NameI18n["tr"])
                .ToListAsync());

        int added = 0;
        foreach (var (tr, en, sort) in values)
        {
            if (existing.Contains(tr)) continue;
            db.AttributeValues.Add(new AttributeValue
            {
                Id = Guid.NewGuid(), AttributeTypeId = attrType.Id,
                NameI18n = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                SortOrder = sort, IsActive = true, CreatedAt = DateTime.UtcNow,
            });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Yaş grubu değerleri — {added} yeni eklendi.");
    }

    private static async Task SeedVarYokValuesAsync(CatalogDbContext db, params string[] codes)
    {
        var attrTypes = await db.AttributeTypes
            .Where(a => codes.Contains(a.Code))
            .ToListAsync();

        int added = 0;
        foreach (var attrType in attrTypes)
        {
            var existing = new HashSet<string>(
                await db.AttributeValues
                    .Where(v => v.AttributeTypeId == attrType.Id)
                    .Select(v => v.NameI18n["tr"])
                    .ToListAsync());

            foreach (var (tr, en, sort) in new[] { ("Var", "Yes", 10), ("Yok", "No", 20) })
            {
                if (existing.Contains(tr)) continue;
                db.AttributeValues.Add(new AttributeValue
                {
                    Id = Guid.NewGuid(), AttributeTypeId = attrType.Id,
                    NameI18n = new Dictionary<string, string> { ["tr"] = tr, ["en"] = en },
                    SortOrder = sort, IsActive = true, CreatedAt = DateTime.UtcNow,
                });
                added++;
            }
        }
        if (added > 0) await db.SaveChangesAsync();
        Console.WriteLine($"✓ Seed: Var/Yok değerleri — {added} yeni eklendi.");
    }

    /// <summary>
    /// G8: varsayılan vitrin — platformda hiç taslak blok ve yayın yoksa B6 geçici
    /// kompozisyonunun birebir karşılığını taslak bloklar olarak kurar ve v1 olarak
    /// yayınlar: duyuru şeridi (B3 metinleri) + kapsül kategori şeridi (kök kanal
    /// kategorileri; görsel = ilk ürün görseli, görselsüz kapsül basılmaz — B6 kuralı)
    /// + ilk 3 kök kategori için standart carousel (category kaynağı, 10 ürün).
    /// Böylece B6/B3 geçici kodları kaldırılınca canlı görünüm DEĞİŞMEZ; içerik artık
    /// admin Vitrin Yönetimi'nden yönetilir. Blok/yayın var olan platforma dokunulmaz.
    /// </summary>
    private static async Task SeedDefaultVitrinAsync(IServiceProvider sp)
    {
        var storefront = sp.GetRequiredService<StorefrontDbContext>();
        var core = sp.GetRequiredService<CoreDbContext>();
        var mediator = sp.GetRequiredService<MediatR.IMediator>();

        var platformlar = await core.FirmPlatforms.Where(fp => fp.IsActive)
            .Select(fp => new { fp.Id, fp.Code }).ToListAsync();
        foreach (var p in platformlar)
        {
            if (p.Code == "telemania") continue; // Telemania kendi vitrin seed'ini kullanır
            var platformId = p.Id;
            var dokunulmus = await storefront.PageBlocks.IgnoreQueryFilters().AnyAsync(b => b.FirmPlatformId == platformId)
                || await storefront.PublishedSnapshots.IgnoreQueryFilters().AnyAsync(x => x.FirmPlatformId == platformId);
            if (dokunulmus) continue;

            // Duyuru şeridi (B3 statik metinleri — artık admin'den yönetilir)
            var duyuru = new ECSPros.Storefront.Domain.Entities.PageBlock
            {
                FirmPlatformId = platformId, Placement = "global-top", BlockType = "announcement",
                TitleI18n = new() { ["tr"] = "Duyuru Şeridi" }, SortOrder = 1, IsActive = true,
            };
            var metinler = new[]
            {
                "Yeni sezon koleksiyonunu keşfet!",
                "Özel fırsatlar seni bekliyor.",
                "Sepette avantajlı ürünleri kaçırma.",
            };
            for (var i = 0; i < metinler.Length; i++)
                duyuru.Items.Add(new ECSPros.Storefront.Domain.Entities.PageBlockItem
                {
                    TitleI18n = new() { ["tr"] = metinler[i] }, SortOrder = i + 1, IsActive = true,
                });
            storefront.PageBlocks.Add(duyuru);

            // Kök kategoriler (aktif, sıralı)
            var kokler = await storefront.ChannelCategories
                .Where(c => c.FirmPlatformId == platformId && c.ParentId == null && c.Status == "published")
                .OrderBy(c => c.SortOrder)
                .Select(c => new { c.Id, c.NameI18n, c.Slug, c.DisplayImageUrl })
                .ToListAsync();

            // Kapsül görseli: kategorinin kendi görseli yoksa ilk ürün görseli (B6 kuralı)
            var kapsulOgeleri = new List<ECSPros.Storefront.Domain.Entities.PageBlockItem>();
            var carouselKokleri = new List<(Guid Id, string Ad, string Slug)>();
            foreach (var kok in kokler)
            {
                var ad = kok.NameI18n.TryGetValue("tr", out var tr) ? tr : kok.NameI18n.Values.FirstOrDefault() ?? kok.Slug;
                var urunler = await mediator.Send(
                    new ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts.GetChannelCategoryProductsQuery(kok.Id, 1, 1));
                var ilkUrunGorseli = urunler.IsSuccess ? urunler.Value!.Items.FirstOrDefault()?.MainImageUrl : null;
                if (urunler.IsSuccess && urunler.Value!.Items.Any() && carouselKokleri.Count < 3)
                    carouselKokleri.Add((kok.Id, ad, kok.Slug));

                var gorsel = kok.DisplayImageUrl ?? ilkUrunGorseli;
                if (gorsel is null) continue;
                kapsulOgeleri.Add(new ECSPros.Storefront.Domain.Entities.PageBlockItem
                {
                    TitleI18n = new() { ["tr"] = ad }, ImageUrl = gorsel, LinkUrl = "/" + kok.Slug,
                    SortOrder = kapsulOgeleri.Count + 1, IsActive = true,
                });
            }

            var sira = 0;
            if (kapsulOgeleri.Count >= 2) // B6: kapsül şeridi yalnız >=2 kategoriyle basılırdı
            {
                var kapsul = new ECSPros.Storefront.Domain.Entities.PageBlock
                {
                    FirmPlatformId = platformId, Placement = "homepage", BlockType = "categories",
                    TitleI18n = new() { ["tr"] = "Öne Çıkan Kategoriler" }, SortOrder = ++sira, IsActive = true,
                    ConfigJson = "{\"gorunum\":\"kapsul\",\"mobileCarousel\":true}",
                };
                foreach (var oge in kapsulOgeleri) kapsul.Items.Add(oge);
                storefront.PageBlocks.Add(kapsul);
            }

            foreach (var (kokId, ad, slug) in carouselKokleri)
            {
                storefront.PageBlocks.Add(new ECSPros.Storefront.Domain.Entities.PageBlock
                {
                    FirmPlatformId = platformId, Placement = "homepage", BlockType = "carousel", Template = "standart",
                    TitleI18n = new() { ["tr"] = ad },
                    SubtitleI18n = new() { ["tr"] = ad + " kategorisinden öne çıkan ürünler." },
                    SortOrder = ++sira, IsActive = true,
                    ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        productSource = new { source = "category", categoryId = kokId, limit = 10 },
                        tema = "varsayilan",
                        seeAllUrl = "/" + slug,
                    }),
                });
            }

            await storefront.SaveChangesAsync();
            var yayin = await mediator.Send(
                new ECSPros.Storefront.Application.Commands.PublishPageSnapshot.PublishPageSnapshotCommand(
                    platformId, null, "G8 varsayılan vitrin (B6 kompozisyonu)"));
            Console.WriteLine(yayin.IsSuccess
                ? $"✓ Seed: {platformId} için varsayılan vitrin yayınlandı (v{yayin.Value})."
                : $"⚠ Seed: {platformId} vitrin yayını başarısız: {yayin.Error}");
        }
    }

    /// <summary>
    /// Telemania demo ana sayfası: 5 geniş kategori kapsülü (Kozmetik, Ev ve Temizlik,
    /// Aksesuar ve Teknoloji, Sağlık ve Kişisel Bakım, Kamp ve Outdoor) + 3 merchandising
    /// carousel (Çok Satanlar / Yüksek Puanlılar / Çok Ziyaret Edilenler). Metrik verisi
    /// olmadığı için carousel'lar "random" kaynağından karışık ürün basar. Pazaryeri ürün
    /// GRUPLARI ayrı kalır — yalnız vitrin kategorileri genişler (idempotent; setup_vitrin.sql
    /// vitrin'i temizlediğinde yeniden kurulur).
    /// </summary>
    /// <summary>
    /// Telemania demo filtre zenginleştirmesi (2026-08-23, kullanıcı: "gruplara eklenebilecek yeni filtre
    /// seçeneklerini ekle"): tlm_* gruplarındaki ürünlerin ADLARINDAN deterministik kurallarla özellik
    /// değerleri türetilir ve eksik olanlar catalog.product_attributes'a yazılır (idempotent — var olan
    /// satır tekrar eklenmez, admin'in elle girdikleri ezilmez). Havuzda olmayan hacim (ör. 75ml) ve SPF
    /// değerleri havuza eklenir. Prod DB'de tlm_* gruplarında ürün olmadığından no-op.
    /// Kurallar: hacim (N ml/gr), spf (SPF/GKF N), paket_adedi (N'li / N Adet / N kapsül),
    /// urun_formu (Roll-On/Sprey/Stick/Köpük/Serum/Yağ/Jel/Losyon/Krem/Toz), cinsiyet (Kadın/Erkek/Unisex),
    /// yas_grubu (a-b Ay, a+ Ay, NN+), bez_bedeni (Small…XX-Large), sac_rengi (ton anahtar sözcüğü),
    /// kullanim_tipi + ek_ozellik (içecek kapları), sac_tipi/cilt_tipi anahtar sözcükleri.
    /// </summary>
    private static async Task SeedTelemaniaFilterEnrichmentAsync(CatalogDbContext db)
    {
        var tipler = await db.AttributeTypes.Where(t => !t.IsDeleted).ToDictionaryAsync(t => t.Code, t => t.Id);
        string[] gerekli = { "hacim", "spf", "paket_adedi", "urun_formu", "cinsiyet", "yas_grubu", "bez_bedeni", "sac_rengi", "kullanim_tipi", "ek_ozellik", "sac_tipi", "cilt_tipi" };
        if (gerekli.Any(g => !tipler.ContainsKey(g))) return;

        var urunler = await db.Products.AsNoTracking()
            .Where(p => !p.IsDeleted && p.ProductGroupId != null && p.ProductGroup!.Code.StartsWith("tlm_"))
            .Select(p => new { p.Id, p.ProductGroupId, Grup = p.ProductGroup!.Code, Ad = p.NameI18n })
            .ToListAsync();
        if (urunler.Count == 0) return;

        var degerler = await db.AttributeValues.Where(v => !v.IsDeleted && gerekli.Select(g => tipler[g]).Contains(v.AttributeTypeId))
            .Select(v => new { v.Id, v.AttributeTypeId, Ad = v.NameI18n }).ToListAsync();
        var degerByTipAd = new Dictionary<(Guid, string), Guid>();
        foreach (var v in degerler)
        {
            var ad = v.Ad.TryGetValue("tr", out var t) ? t : v.Ad.Values.FirstOrDefault() ?? "";
            degerByTipAd.TryAdd((v.AttributeTypeId, ad.Trim().ToLowerInvariant()), v.Id);
        }
        var sonSira = degerler.Count * 10 + 1000;
        Guid DegerAl(string tipKodu, string ad)
        {
            var tipId = tipler[tipKodu];
            var key = (tipId, ad.Trim().ToLowerInvariant());
            if (degerByTipAd.TryGetValue(key, out var id)) return id;
            var yeni = new AttributeValue
            {
                Id = Guid.NewGuid(), AttributeTypeId = tipId,
                NameI18n = new Dictionary<string, string> { ["tr"] = ad, ["en"] = ad },
                SortOrder = sonSira += 10, IsActive = true, CreatedAt = DateTime.UtcNow
            };
            db.AttributeValues.Add(yeni);
            degerByTipAd[key] = yeni.Id;
            return yeni.Id;
        }

        var urunIdler = urunler.Select(u => u.Id).ToList();
        var mevcut = await db.ProductAttributes.AsNoTracking()
            .Where(pa => !pa.IsDeleted && urunIdler.Contains(pa.ProductId) && pa.AttributeValueId != null)
            .Select(pa => new { pa.ProductId, pa.AttributeTypeId, AttributeValueId = pa.AttributeValueId!.Value })
            .ToListAsync();
        var mevcutTipli = mevcut.Select(m => (m.ProductId, m.AttributeTypeId)).ToHashSet();
        var mevcutSatir = mevcut.Select(m => (m.ProductId, m.AttributeTypeId, m.AttributeValueId)).ToHashSet();

        int eklenen = 0;
        void Ata(Guid urunId, string tipKodu, string deger, bool tekDeger = true)
        {
            if (string.IsNullOrWhiteSpace(deger)) return;
            var tipId = tipler[tipKodu];
            if (tekDeger && mevcutTipli.Contains((urunId, tipId))) return;   // tek değerli tipte mevcut değer korunur
            var degerId = DegerAl(tipKodu, deger);
            if (!mevcutSatir.Add((urunId, tipId, degerId))) return;
            mevcutTipli.Add((urunId, tipId));
            db.ProductAttributes.Add(new ProductAttribute { Id = Guid.NewGuid(), ProductId = urunId, AttributeTypeId = tipId, AttributeValueId = degerId, CreatedAt = DateTime.UtcNow });
            eklenen++;
        }

        static bool Var(string ad, params string[] anahtarlar) => anahtarlar.Any(k => ad.Contains(k, StringComparison.OrdinalIgnoreCase));
        static string? Ilk(string ad, params (string Anahtar, string Deger)[] kurallar)
        {
            var enIyi = (Pos: int.MaxValue, Deger: (string?)null);
            foreach (var (anahtar, deger) in kurallar)
            {
                var i = ad.IndexOf(anahtar, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && i < enIyi.Pos) enIyi = (i, deger);
            }
            return enIyi.Deger;
        }
        static string PaketEtiket(int n)
        {
            if (n <= 1) return "Tekli";
            var son = n % 10 != 0 ? n % 10 : (n % 100 != 0 ? n % 100 : n);
            var ek = son switch
            {
                1 or 2 or 5 or 7 or 8 or 20 or 50 or 70 or 80 => "li",
                3 or 4 or 100 => "lü",
                6 or 40 or 60 or 90 => "lı",
                9 or 10 or 30 => "lu",
                _ => "lı"
            };
            return $"{n}'{ek}";
        }
        var icecek = new HashSet<string> { "tlm_termos", "tlm_mug", "tlm_kamp_matarasi", "tlm_spor_matara", "tlm_sogutucu_buzluk", "tlm_termal_canta", "tlm_kamp_yemek_seti", "tlm_bardak", "tlm_kadeh", "tlm_shaker", "tlm_biberon" };
        var formGruplari = new HashSet<string> { "tlm_deodorant", "tlm_sac_spreyi", "tlm_sac_kopugu", "tlm_sac_sekillendirici", "tlm_sac_serumu", "tlm_yuz_temizleyici", "tlm_makyaj_temizleyici", "tlm_hasere_ilaci", "tlm_sinek_ilaci", "tlm_yuzey_temizleyici", "tlm_yuz_gunes_kremi", "tlm_vucut_gunes_kremi", "tlm_bebek_gunes_kremi", "tlm_gunes_sonrasi", "tlm_vucut_spreyi", "tlm_vucut_kremi", "tlm_el_kremi", "tlm_bebek_kremi" };
        var cinsiyetGruplari = new HashSet<string> { "tlm_deodorant", "tlm_tiras_bicagi", "tlm_kulak_ustu_kulaklik", "tlm_kulak_ici_kulaklik", "tlm_vucut_spreyi", "tlm_emzik" };
        var yasGruplari = new HashSet<string> { "tlm_emzik", "tlm_biberon", "tlm_yuz_kremi", "tlm_goz_kremi", "tlm_cilt_serumu", "tlm_goz_serumu", "tlm_bebek_sampuani", "tlm_bebek_kremi", "tlm_bebek_gunes_kremi" };
        var bezGruplari = new HashSet<string> { "tlm_hasta_bezi", "tlm_bebek_bezi" };
        var hacimsiz = new HashSet<string> { "tlm_hasta_bezi", "tlm_bebek_bezi", "tlm_bebek_islak_mendil", "tlm_prezervatif", "tlm_emzik", "tlm_kulak_ustu_kulaklik", "tlm_kulak_ici_kulaklik", "tlm_temizlik_bezi", "tlm_bulasik_sungeri", "tlm_tiras_bicagi", "tlm_burun_bandi", "tlm_burun_aspiatoru", "tlm_yatak_koruyucu", "tlm_kapsul_kahve", "tlm_cikolata" };
        var sacTipiGruplari = new HashSet<string> { "tlm_sampuan", "tlm_kuru_sampuan", "tlm_bebek_sampuani", "tlm_sac_kremi", "tlm_sac_maskesi", "tlm_sac_bakim_seti", "tlm_sac_tonigi", "tlm_sac_serumu", "tlm_sac_spreyi", "tlm_sac_kopugu", "tlm_sac_sekillendirici", "tlm_sac_boyasi" };
        var ciltTipiGruplari = new HashSet<string> { "tlm_dus_jeli", "tlm_yuz_kremi", "tlm_cilt_serumu", "tlm_goz_kremi", "tlm_goz_serumu", "tlm_bb_cc_krem", "tlm_el_kremi", "tlm_vucut_kremi", "tlm_ayak_kremi", "tlm_bebek_kremi", "tlm_vucut_spreyi", "tlm_yuz_temizleyici", "tlm_makyaj_temizleyici", "tlm_fondoten", "tlm_aydinlatici" };

        var rxMl = new System.Text.RegularExpressions.Regex(@"(\d+(?:[.,]\d+)?)\s*(ml|mL|ML|Ml)\b", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var rxSpf = new System.Text.RegularExpressions.Regex(@"\b(?:SPF|GKF)\s*(\d{1,3})(\+?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rxPaket = new System.Text.RegularExpressions.Regex(@"(\d{1,3})\s*(?:['’`]\s*)?(?:lu|lü|li|lı)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rxAdet = new System.Text.RegularExpressions.Regex(@"(\d{1,3})\s*(?:adet|kapsul|kapsül|kapsulluk|kapsüllük|pcs)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rxAyAralik = new System.Text.RegularExpressions.Regex(@"(\d{1,2})\s*-\s*(\d{1,2})\s*ay\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rxAyArti = new System.Text.RegularExpressions.Regex(@"(\d{1,2})\s*\+\s*ay\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var rxYasArti = new System.Text.RegularExpressions.Regex(@"\b(\d{2})\s*\+(?!\s*ay)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var u in urunler)
        {
            var ad = u.Ad.TryGetValue("tr", out var trAd) ? trAd : u.Ad.Values.FirstOrDefault() ?? "";
            if (string.IsNullOrWhiteSpace(ad)) continue;

            // hacim (kozmetik) — içecek kapları zaten litre havuzundan dolu
            if (!icecek.Contains(u.Grup) && !hacimsiz.Contains(u.Grup))
            {
                var m = rxMl.Match(ad);
                if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mlDeger) && mlDeger > 0 && mlDeger <= 5000)
                    Ata(u.Id, "hacim", mlDeger == Math.Floor(mlDeger) ? $"{(int)mlDeger}ml" : $"{mlDeger.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}ml");
            }
            // SPF
            var spf = rxSpf.Match(ad);
            if (spf.Success) Ata(u.Id, "spf", $"SPF {spf.Groups[1].Value}{spf.Groups[2].Value}");
            // paket adedi
            var paket = rxPaket.Match(ad); var adet = rxAdet.Match(ad);
            if (paket.Success && int.TryParse(paket.Groups[1].Value, out var pn) && pn > 0 && pn <= 500) Ata(u.Id, "paket_adedi", PaketEtiket(pn));
            else if (adet.Success && int.TryParse(adet.Groups[1].Value, out var an) && an > 0 && an <= 500) Ata(u.Id, "paket_adedi", PaketEtiket(an));
            // form
            if (formGruplari.Contains(u.Grup))
            {
                var form = Ilk(ad, ("Roll-On", "Roll-On"), ("Roll On", "Roll-On"), ("Rollon", "Roll-On"), ("Sprey", "Sprey"), ("Spray", "Sprey"), ("Aerosol", "Sprey"), ("Stick", "Stick"),
                    ("Köpük", "Köpük"), ("Köpüğ", "Köpük"), ("Mousse", "Köpük"), ("Serum", "Serum"), ("Yağı", "Yağ"), ("Yağ ", "Yağ"), ("Oil", "Yağ"), ("Jel", "Jel"), ("Gel", "Jel"),
                    ("Losyon", "Losyon"), ("Lotion", "Losyon"), ("Sütü", "Losyon"), ("Süt ", "Losyon"), ("Krem", "Krem"), ("Cream", "Krem"), ("Creme", "Krem"), ("Toz", "Toz"), ("Powder", "Toz"));
                if (form is not null) Ata(u.Id, "urun_formu", form);
            }
            // cinsiyet
            if (cinsiyetGruplari.Contains(u.Grup))
            {
                var c = Ilk(ad, ("Unisex", "Unisex"), ("Unısex", "Unisex"), ("Kadın", "Kadın"), ("Kadin", "Kadın"), ("Women", "Kadın"), ("Woman", "Kadın"), ("Kız", "Kadın"), ("Erkek", "Erkek"), ("Men ", "Erkek"), ("MEN ", "Erkek"), ("Man ", "Erkek"));
                if (c is not null) Ata(u.Id, "cinsiyet", c);
            }
            // yaş grubu
            if (yasGruplari.Contains(u.Grup))
            {
                var ar = rxAyAralik.Match(ad); var arti = rxAyArti.Match(ad); var yas = rxYasArti.Match(ad);
                if (ar.Success) Ata(u.Id, "yas_grubu", $"{ar.Groups[1].Value}-{ar.Groups[2].Value} Ay");
                else if (arti.Success) Ata(u.Id, "yas_grubu", $"{arti.Groups[1].Value}+ Ay");
                else if (yas.Success && int.TryParse(yas.Groups[1].Value, out var y) && y is >= 18 and <= 80) Ata(u.Id, "yas_grubu", $"{y}+");
            }
            // bez bedeni
            if (bezGruplari.Contains(u.Grup))
            {
                var b = Ilk(ad, ("XXLarge", "XX-Large"), ("XX Large", "XX-Large"), ("XXL", "XX-Large"), ("XLarge", "X-Large"), ("X Large", "X-Large"), ("X-Large", "X-Large"), ("Xlarge", "X-Large"), ("XL", "X-Large"), ("Large", "Large"), ("Medium", "Medium"), ("Small", "Small"));
                if (b is not null) Ata(u.Id, "bez_bedeni", b);
            }
            // saç rengi (saç boyası)
            if (u.Grup == "tlm_sac_boyasi")
            {
                var r = Ilk(ad, ("Kızıl", "Kızıl"), ("Kizil", "Kızıl"), ("Bakır", "Bakır"), ("Bakir", "Bakır"), ("Platin", "Platin"), ("Sarı", "Sarı"), ("Sari", "Sarı"), ("Kumral", "Kumral"), ("Kestane", "Kestane"), ("Kahve", "Kahve"), ("Siyah", "Siyah"), ("Gri", "Gri"), ("Nude", "Nude"));
                if (r is not null) Ata(u.Id, "sac_rengi", r);
            }
            // içecek kapları: kullanım tipi + özellikler
            if (icecek.Contains(u.Grup) && u.Grup != "tlm_biberon")
            {
                var kt = Ilk(ad, ("Yemek", "Yemek Termosu"), ("Kavanoz", "Yemek Termosu"), ("Soğutucu", "Soğutucu"), ("Cooler", "Soğutucu"), ("Buzluk", "Soğutucu"),
                    ("Kupa", "Kupa"), ("Mug", "Kupa"), ("Matara", "Matara"), ("Flask", "Matara"), ("Bottle", "Matara"), ("Şişe", "Matara"),
                    ("Bardak", "Termos Bardak"), ("Tumbler", "Termos Bardak"), ("Quencher", "Termos Bardak"), ("Kadeh", "Termos Bardak"), ("Cup", "Termos Bardak"), ("Set", "Kamp Seti"));
                Ata(u.Id, "kullanim_tipi", kt ?? "Termos");
                if (Var(ad, "Pipet", "Straw")) Ata(u.Id, "ek_ozellik", "Pipetli", tekDeger: false);
                if (Var(ad, "Kaşık")) Ata(u.Id, "ek_ozellik", "Kaşıklı", tekDeger: false);
                if (Var(ad, "Kulp", "Handle")) Ata(u.Id, "ek_ozellik", "Kulplu", tekDeger: false);
                if (Var(ad, "Çelik", "Steel")) Ata(u.Id, "ek_ozellik", "Paslanmaz Çelik", tekDeger: false);
            }
            // saç tipi / cilt tipi anahtar sözcükleri
            if (sacTipiGruplari.Contains(u.Grup))
            {
                var st = Ilk(ad, ("Boyalı", "Boyalı"), ("Hasarlı", "Hasarlı"), ("Yıpranmış", "Hasarlı"), ("Dökülme", "Dökülen"), ("Dökülen", "Dökülen"), ("İnce Telli", "İnce Telli"), ("Ince Telli", "İnce Telli"), ("Kıvırcık", "Kıvırcık"), ("Bukle", "Kıvırcık"), ("Kuru Saç", "Kuru"), ("Yağlı Saç", "Yağlı"), ("Tüm Saç", "Tüm Saç Tipleri"));
                if (st is not null) Ata(u.Id, "sac_tipi", st);
            }
            if (ciltTipiGruplari.Contains(u.Grup))
            {
                var ct = Ilk(ad, ("Hassas", "Hassas"), ("Karma", "Karma"), ("Yağlı", "Yağlı"), ("Kuru Cilt", "Kuru"), ("Kuru ve", "Kuru"), ("Normal", "Normal"), ("Tüm Cilt", "Tüm Cilt Tipleri"));
                if (ct is not null) Ata(u.Id, "cilt_tipi", ct);
            }
        }

        // Saç boyası VARYANT rengi (2026-08-23, kullanıcı isteği): grup ekseni `renk` (IsVariant+IsPrimaryAxis) zaten
        // atanmış; ürün adındaki ton (Sarı/Kumral/Kahve/Kızıl…) mevcut renk havuzunun en yakın değerine eşlenir ve
        // rengi olmayan aktif varyantlara product_variant_attributes satırı yazılır (mevcut yapı — yeni mekanizma yok).
        var sacBoyalari = urunler.Where(u => u.Grup == "tlm_sac_boyasi").ToList();
        if (sacBoyalari.Count > 0 && tipler.TryGetValue("renk", out var renkTipId))
        {
            var renkHavuzu = (await db.AttributeValues.Where(v => !v.IsDeleted && v.AttributeTypeId == renkTipId)
                .Select(v => new { v.Id, Ad = v.NameI18n }).ToListAsync())
                .GroupBy(v => (v.Ad.TryGetValue("tr", out var t) ? t : v.Ad.Values.FirstOrDefault() ?? "").Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id);
            var boyaIdler = sacBoyalari.Select(u => u.Id).ToList();
            var boyaVaryantlari = await db.ProductVariants.AsNoTracking()
                .Where(v => v.IsActive && !v.IsDeleted && boyaIdler.Contains(v.ProductId))
                .Select(v => new { v.Id, v.ProductId }).ToListAsync();
            var boyaVaryantIdler = boyaVaryantlari.Select(v => v.Id).ToList();
            var renkliVaryantlar = (await db.ProductVariantAttributes.AsNoTracking()
                .Where(va => va.AttributeTypeId == renkTipId && boyaVaryantIdler.Contains(va.VariantId))
                .Select(va => va.VariantId).ToListAsync()).ToHashSet();
            var varyantByUrun = boyaVaryantlari.GroupBy(v => v.ProductId).ToDictionary(g => g.Key, g => g.Select(v => v.Id).ToList());
            int renkEklenen = 0;
            foreach (var u in sacBoyalari)
            {
                var ad = u.Ad.TryGetValue("tr", out var trAd) ? trAd : u.Ad.Values.FirstOrDefault() ?? "";
                string? havuzAdi = Var(ad, "Kızıl", "Kizil") ? "Kırmızı"
                    : Var(ad, "Bakır", "Bakir") ? "Turuncu"
                    : Ilk(ad, ("Siyah", "Siyah"), ("Kahve", "Kahverengi"), ("Kestane", "Kahverengi"), ("Çikolata", "Kahverengi"), ("Fındık", "Kahverengi"),
                          ("Kumral", "Kahverengi"), ("Karamel", "Kahverengi"), ("Sarı", "Sarı"), ("Sari", "Sarı"), ("Platin", "Sarı"), ("Bal ", "Sarı"),
                          ("Nude", "Bej"), ("Gri", "Gri"), ("Mor", "Mor"), ("Beyaz", "Beyaz"));
                if (havuzAdi is null || !renkHavuzu.TryGetValue(havuzAdi.ToLowerInvariant(), out var renkDegerId)) continue;
                if (!varyantByUrun.TryGetValue(u.Id, out var vids)) continue;
                foreach (var vid in vids)
                {
                    if (!renkliVaryantlar.Add(vid)) continue;
                    db.ProductVariantAttributes.Add(new ProductVariantAttribute { Id = Guid.NewGuid(), VariantId = vid, AttributeTypeId = renkTipId, AttributeValueId = renkDegerId, CreatedAt = DateTime.UtcNow });
                    renkEklenen++;
                }
            }
            if (renkEklenen > 0) Console.WriteLine($"✓ Seed: Telemania saç boyası varyant rengi — {renkEklenen} varyant.");
        }

        // Renk filtresi (2026-08-23, kullanıcı isteği): demo DB'de (telemania platformu olan veritabanı) ham `renk`
        // tipi filtreye açılır — içecek kapları/boya/maskara renk varyantları hex'li 27 değerle swatch grubu olur.
        // Misharitalia `filtre_rengi` (kürasyonlu havuz) kullanır; orada `renk` kapalı kalır (telemania platformu yok).
        var telemaniaVar = await db.Database.SqlQuery<int>($"""SELECT 1 AS "Value" FROM core.core_firm_platforms WHERE "Code" = 'telemania' AND NOT "IsDeleted" LIMIT 1""").AnyAsync();
        if (telemaniaVar)
        {
            var renkTipi = await db.AttributeTypes.FirstOrDefaultAsync(t => t.Code == "renk");
            if (renkTipi is not null && !renkTipi.UseInFilter)
            {
                renkTipi.UseInFilter = true;
                Console.WriteLine("✓ Seed: Telemania — 'renk' özelliği filtreye açıldı (UseInFilter=true).");
            }
        }

        if (eklenen > 0 || db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            Console.WriteLine($"✓ Seed: Telemania filtre zenginleştirmesi — {eklenen} ürün-özellik değeri eklendi.");
        }
    }

    private static async Task SeedTelemaniaVitrinAsync(IServiceProvider sp)
    {
        var storefront = sp.GetRequiredService<StorefrontDbContext>();
        var core = sp.GetRequiredService<CoreDbContext>();
        var catalog = sp.GetRequiredService<CatalogDbContext>();
        var mediator = sp.GetRequiredService<MediatR.IMediator>();

        var platform = await core.FirmPlatforms
            .Where(fp => fp.IsActive && fp.Code == "telemania")
            .Select(fp => new { fp.Id })
            .FirstOrDefaultAsync();
        if (platform is null) return;

        var platformId = platform.Id;
        var dokunulmus = await storefront.PageBlocks.IgnoreQueryFilters().AnyAsync(b => b.FirmPlatformId == platformId)
            || await storefront.PublishedSnapshots.IgnoreQueryFilters().AnyAsync(x => x.FirmPlatformId == platformId);
        if (dokunulmus) return;

        // 5 geniş ana sayfa kategorisi: slug, ad, toplanan tlm_* grup kodları
        var heroTanimlar = new (string Slug, string Ad, string[] Gruplar)[]
        {
            ("kozmetik", "Kozmetik", new[]
            {
                // Saç
                "tlm_sampuan","tlm_sac_kremi","tlm_sac_maskesi","tlm_sac_serumu","tlm_sac_boyasi","tlm_sac_spreyi",
                "tlm_sac_kopugu","tlm_sac_tonigi","tlm_sac_sekillendirici","tlm_sac_parfumu","tlm_kuru_sampuan","tlm_sac_fircasi","tlm_sac_bakim_seti",
                // Cilt
                "tlm_yuz_kremi","tlm_cilt_serumu","tlm_goz_kremi","tlm_goz_serumu","tlm_yuz_temizleyici","tlm_el_kremi","tlm_ayak_kremi","tlm_burun_bandi",
                // Makyaj
                "tlm_maskara","tlm_fondoten","tlm_aydinlatici","tlm_bb_cc_krem","tlm_makyaj_temizleyici",
                // Vücut & Banyo
                "tlm_dus_jeli","tlm_deodorant","tlm_vucut_spreyi","tlm_vucut_kremi","tlm_banyo_lifi","tlm_tiras_bicagi",
                // Güneş
                "tlm_yuz_gunes_kremi","tlm_vucut_gunes_kremi","tlm_gunes_sonrasi","tlm_bebek_gunes_kremi",
            }),
            ("ev-ve-temizlik", "Ev ve Temizlik", new[]
            {
                "tlm_temizlik_bezi","tlm_yuzey_temizleyici","tlm_bulasik_sungeri","tlm_hasere_ilaci","tlm_sinek_ilaci","tlm_yatak_koruyucu",
                "tlm_termos","tlm_mug","tlm_kapsul_kahve","tlm_filtre_kahve","tlm_cikolata","tlm_krem_santi","tlm_bitki_cayi",
                "tlm_kadeh","tlm_bardak","tlm_dripper","tlm_shaker",
            }),
            ("aksesuar-ve-teknoloji", "Aksesuar ve Teknoloji", new[]
            {
                "tlm_kulak_ustu_kulaklik","tlm_kulak_ici_kulaklik",
            }),
            ("saglik-ve-kisisel-bakim", "Sağlık ve Kişisel Bakım", new[]
            {
                "tlm_prezervatif","tlm_kayganlastirici_jel","tlm_hasta_bezi",
                "tlm_dis_macunu","tlm_sarjli_dis_fircasi","tlm_protez_dis_bakim",
                "tlm_bebek_sampuani","tlm_bebek_kremi","tlm_bebek_bezi","tlm_bebek_islak_mendil","tlm_biberon","tlm_emzik","tlm_burun_aspiatoru",
            }),
            ("kamp-ve-outdoor", "Kamp ve Outdoor", new[]
            {
                "tlm_kamp_yemek_seti","tlm_kamp_matarasi","tlm_spor_matara","tlm_termal_canta","tlm_sogutucu_buzluk",
            }),
        };

        // Grup kodu → Id (eksik/çıkarılmış grup kodu yok sayılır; kapsam doğrulaması vitrin değil menü katmanında yapılır)
        var tumKodlar = heroTanimlar.SelectMany(h => h.Gruplar).Distinct().ToList();
        var groupMap = await catalog.ProductGroups
            .Where(pg => tumKodlar.Contains(pg.Code))
            .ToDictionaryAsync(pg => pg.Code, pg => pg.Id);

        var heroListesi = new List<(Guid Id, string Ad, string Slug)>();
        var sira = 0;
        foreach (var h in heroTanimlar)
        {
            var gidler = h.Gruplar.Where(groupMap.ContainsKey).Select(g => groupMap[g]).ToList();
            if (gidler.Count == 0) continue;

            var mevcut = await storefront.ChannelCategories
                .FirstOrDefaultAsync(c => c.FirmPlatformId == platformId && c.Slug == h.Slug);
            var hero = mevcut ?? new ECSPros.Storefront.Domain.Entities.ChannelCategory
            {
                FirmPlatformId = platformId,
                NameI18n = new Dictionary<string, string> { ["tr"] = h.Ad },
                Slug = h.Slug,
                Status = "published",
                FillType = "filter",
                CreatedAt = DateTime.UtcNow,
            };
            hero.NameI18n["tr"] = h.Ad;
            hero.Status = "published";
            hero.FillType = "filter";
            hero.FilterDef = new Dictionary<string, object> { ["productGroupIds"] = gidler };
            // Hero kategorilerin SortOrder'ı granül (yaprak) kategorilerin ÜZERİNDE tutulur
            // (200+) — "Kategori" filtresi yaprak grupları seçsin diye.
            hero.SortOrder = 200 + (++sira);
            if (mevcut is null) storefront.ChannelCategories.Add(hero);
            heroListesi.Add((hero.Id, h.Ad, h.Slug));
        }
        await storefront.SaveChangesAsync();

        // Duyuru şeridi
        var duyuru = new ECSPros.Storefront.Domain.Entities.PageBlock
        {
            FirmPlatformId = platformId, Placement = "global-top", BlockType = "announcement",
            TitleI18n = new() { ["tr"] = "Duyuru Şeridi" }, SortOrder = 1, IsActive = true,
        };
        var metinler = new[] { "Yeni sezon koleksiyonunu keşfet!", "Özel fırsatlar seni bekliyor.", "Sepette avantajlı ürünleri kaçırma." };
        for (var i = 0; i < metinler.Length; i++)
            duyuru.Items.Add(new ECSPros.Storefront.Domain.Entities.PageBlockItem
            {
                TitleI18n = new() { ["tr"] = metinler[i] }, SortOrder = i + 1, IsActive = true,
            });
        storefront.PageBlocks.Add(duyuru);

        // Kapsül şeridi: 5 geniş kategori (görsel = kategori ilk ürün görseli)
        var kapsul = new ECSPros.Storefront.Domain.Entities.PageBlock
        {
            FirmPlatformId = platformId, Placement = "homepage", BlockType = "categories",
            TitleI18n = new() { ["tr"] = "Alışverişe Başla" }, SortOrder = 1, IsActive = true,
            ConfigJson = "{\"gorunum\":\"kapsul\",\"mobileCarousel\":true}",
        };
        var kapsulSira = 0;
        foreach (var (heroId, ad, slug) in heroListesi)
        {
            var urunler = await mediator.Send(
                new ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts.GetChannelCategoryProductsQuery(heroId, 1, 1));
            var gorsel = urunler.IsSuccess ? urunler.Value!.Items.FirstOrDefault()?.MainImageUrl : null;
            if (gorsel is null) continue;
            kapsul.Items.Add(new ECSPros.Storefront.Domain.Entities.PageBlockItem
            {
                TitleI18n = new() { ["tr"] = ad }, ImageUrl = gorsel, LinkUrl = "/" + slug,
                SortOrder = ++kapsulSira, IsActive = true,
            });
        }
        storefront.PageBlocks.Add(kapsul);

        // Merchandising carousel'ları — "Yüksek Puanlılar" çok kaynaklı gerçek puanı
        // (own + dış kanallar) kullanır; diğerleri metrik olmadığından rastgele kalır.
        var rails = new (string Ad, string Source)[]
        {
            ("Çok Satanlar", "random"),
            ("Yüksek Puanlılar", "top-rated"),
            ("Çok Ziyaret Edilenler", "random"),
        };
        var railSira = 1;
        foreach (var (ad, src) in rails)
        {
            storefront.PageBlocks.Add(new ECSPros.Storefront.Domain.Entities.PageBlock
            {
                FirmPlatformId = platformId, Placement = "homepage", BlockType = "carousel", Template = "standart",
                TitleI18n = new() { ["tr"] = ad },
                SubtitleI18n = new() { ["tr"] = "Telemania öne çıkanlar." },
                SortOrder = ++railSira, IsActive = true,
                ConfigJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    productSource = new { source = src, limit = 12 },
                    tema = "varsayilan",
                }),
            });
        }

        await storefront.SaveChangesAsync();
        var yayin = await mediator.Send(
            new ECSPros.Storefront.Application.Commands.PublishPageSnapshot.PublishPageSnapshotCommand(
                platformId, null, "Telemania demo ana sayfası (5 kategori + random rails)"));
        Console.WriteLine(yayin.IsSuccess
            ? $"✓ Seed: Telemania ana sayfası yayınlandı (v{yayin.Value})."
            : $"⚠ Seed: Telemania ana sayfa yayını başarısız: {yayin.Error}");
    }
}
