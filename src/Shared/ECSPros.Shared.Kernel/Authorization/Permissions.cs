namespace ECSPros.Shared.Kernel.Authorization;

/// <summary>
/// Sistem genelinde kullanılan permission kodları.
/// Layer 1 (platform yönetimi) ve Layer 2 (firma işlemleri) olarak ikiye ayrılır.
/// </summary>
public static class Permissions
{
    // ── Layer 1: Geliştirici/Platform yönetimi ─────────────────────────────────
    // Yalnızca platform_admin ve super_admin rollerine tanımlanır.
    // Firma kullanıcıları bu işlemleri yapamaz.

    /// <summary>Özellik tipleri, değerleri, ürün grupları ve grup konfigürasyonu yönetimi.</summary>
    public const string CatalogPlatformManage = "catalog.platform.manage";

    /// <summary>Definition şeması yönetimi (servis kataloğu vb.) — geliştirici firma
    /// tarafından doldurulan, kullanıcı firmanın operasyonundan bağımsız tanım verisi.</summary>
    public const string DefinitionManage = "definition.manage";

    // ── Layer 2: Firma kullanıcısı işlemleri ──────────────────────────────────

    public const string CatalogProductsManage    = "catalog.products.manage";
    public const string CatalogCategoriesManage  = "catalog.categories.manage";
    public const string CatalogImagesManage      = "catalog.images.manage";
    public const string CatalogSettingsManage    = "catalog.settings.manage";

    /// <summary>Depo, stok ve transfer yönetimi.</summary>
    public const string InventoryManage = "inventory.manage";

    /// <summary>Tedarik: satın alma / mal kabul yönetimi (docs/urun-tedarik-is-akisi.md T1).</summary>
    public const string ProcurementManage = "procurement.manage";
    /// <summary>Tedarik: ayrıştırma + yerleştirme (depo personeli).</summary>
    public const string ProcurementSort = "procurement.sort";

    /// <summary>Paket birleştirme / tek fatura istisna akışı — normal akış paket
    /// başına faturadır; bu izin bilinçli istisna işlemi içindir (karar 2026-07-19).
    /// firm_admin'e varsayılan verilmez.</summary>
    public const string OrderPackagesMerge = "order.packages.merge";

    /// <summary>Firma entegrasyon kimlik bilgilerini (API key/secret vb.) açık metin olarak
    /// görme — "Göster" düğmesi. Her görüntüleme iam.audit_logs'a yazılır (2026-08-29).
    /// firm_admin'e varsayılan verilmez; super_admin/platform_admin'de vardır.</summary>
    public const string IntegrationCredentialsReveal = "integration.credentials.reveal";

    // ── Yetkilendirme yönetimi (2026-09-09, Y0) ───────────────────────────────
    /// <summary>Yetki grupları / kullanıcı yetkileri / yetki içerikleri ekranları.
    /// Eskalasyon kuralı (tasarım §O.4): bu yetkiye sahip kullanıcı YALNIZ kendi sahip
    /// olduğu permission ve kanalları dağıtabilir; süper admin bu kısıttan muaftır.</summary>
    public const string IamPermissionsManage = "iam.permissions.manage";

    /// <summary>"Bu kullanıcı panelde ne görüyor?" simülasyonu — salt okunur, loglanır.</summary>
    public const string IamPermissionsSimulate = "iam.permissions.simulate";

    /// <summary>Yetki logları ekranı.</summary>
    public const string IamAuditView = "iam.audit.view";

    // ── Hassas alan yetkileri (K6 — v1'de bu beş alanla SINIRLI) ──────────────
    // Çapraz yetkilerdir: bir sayfaya değil, aynı veriyi gösteren TÜM yüzeylere uygulanır
    // (liste, detay, export, yazdırma). Yetkisi olmayana veri ÜRETİLMEZ (tasarım §C.3).
    public const string FieldViewCost            = "common.view_cost";
    public const string FieldViewMargin          = "common.view_margin";
    public const string FieldViewCustomerPhone   = "customer.view_phone";
    public const string FieldViewCustomerAddress = "customer.view_address";
    public const string FieldViewInternalNotes   = "common.view_internal_notes";


    // ── Panel sayfa/aksiyon yetkileri (Y2, 2026-09-09) ────────────────────────
    // Panel sayfa envanterinden (sidebarNavigation) türetildi: her alan için görüntüleme
    // (.view) ve işlem (.manage) çifti. Kanal kapsamlı olanlar katalogda işaretlidir.
    // Yazma ucu hem .view hem .manage ister — gruplara ikisi birlikte verilir.
    public const string PanelDashboardView = "panel.dashboard.view";
    public const string RequestsView = "requests.view";
    public const string RequestsManage = "requests.manage";
    public const string CatalogProductsView = "catalog.products.view";
    public const string StorefrontChannelsView = "storefront.channels.view";
    public const string StorefrontChannelsManage = "storefront.channels.manage";
    public const string MarketplacesView = "marketplaces.view";
    public const string MarketplacesManage = "marketplaces.manage";
    public const string OrdersView = "orders.view";
    public const string OrdersManage = "orders.manage";
    public const string OrdersReturnsView = "orders.returns.view";
    public const string OrdersReturnsManage = "orders.returns.manage";
    public const string OrdersInvoicesView = "orders.invoices.view";
    public const string OrdersInvoicesManage = "orders.invoices.manage";
    public const string OrdersQuotesView = "orders.quotes.view";
    public const string OrdersQuotesManage = "orders.quotes.manage";
    public const string PosView = "pos.view";
    public const string PosManage = "pos.manage";
    public const string FulfillmentView = "fulfillment.view";
    public const string FulfillmentManage = "fulfillment.manage";
    public const string InventoryView = "inventory.view";
    public const string ProcurementView = "procurement.view";
    public const string CrmMembersView = "crm.members.view";
    public const string CrmMembersManage = "crm.members.manage";
    public const string CrmTicketsView = "crm.tickets.view";
    public const string CrmTicketsManage = "crm.tickets.manage";
    public const string StorefrontModerationView = "storefront.moderation.view";
    public const string StorefrontModerationManage = "storefront.moderation.manage";
    public const string AccountsView = "accounts.view";
    public const string AccountsManage = "accounts.manage";
    public const string CommissionView = "commission.view";
    public const string CommissionManage = "commission.manage";
    public const string FinanceView = "finance.view";
    public const string FinanceManage = "finance.manage";
    public const string PromotionView = "promotion.view";
    public const string PromotionManage = "promotion.manage";
    public const string StorefrontNotificationsView = "storefront.notifications.view";
    public const string StorefrontNotificationsManage = "storefront.notifications.manage";
    public const string MarketingTrackingView = "marketing.tracking.view";
    public const string MarketingTrackingManage = "marketing.tracking.manage";
    public const string StorefrontContentView = "storefront.content.view";
    public const string StorefrontContentManage = "storefront.content.manage";
    public const string CmsView = "cms.view";
    public const string CmsManage = "cms.manage";
    public const string DefinitionsView = "definitions.view";
    public const string DefinitionsManage = "definitions.manage";
    public const string SystemFirmsView = "system.firms.view";
    public const string SystemFirmsManage = "system.firms.manage";
    public const string SystemTranslationsManage = "system.translations.manage";
    public const string SystemMigrationManage = "system.migration.manage";
    public const string SystemIntegrationsView = "system.integrations.view";
    public const string SystemIntegrationsManage = "system.integrations.manage";
    public const string IamUsersView = "iam.users.view";
    public const string IamUsersManage = "iam.users.manage";

    /// <summary>Layer 2 — firm_admin rolüne atanan permission kodları.</summary>
    public static readonly IReadOnlyList<string> FirmAdminPermissions =
    [
        CatalogProductsManage,
        CatalogCategoriesManage,
        CatalogImagesManage,
        CatalogSettingsManage,
        InventoryManage,
        ProcurementManage,
        ProcurementSort,
    ];

    /// <summary>Tüm permission kodları — super_admin ve platform_admin rollerine atanır.
    /// DİKKAT (2026-09-09, K5): süper admin artık kullanıcı üzerindeki BAYRAKLA bypass eder;
    /// bu liste yalnız platform_admin gibi normal grupların seed'i içindir. Yeni eklenen
    /// yetkilendirme/alan yetkileri bilinçli olarak bu listede DEĞİLDİR — kimseye otomatik
    /// verilmez (default deny), süper admin panelden dağıtır.</summary>
    public static readonly IReadOnlyList<string> AllPermissions =
    [
        CatalogPlatformManage,
        DefinitionManage,
        CatalogProductsManage,
        CatalogCategoriesManage,
        CatalogImagesManage,
        CatalogSettingsManage,
        InventoryManage,
        OrderPackagesMerge,
        ProcurementManage,
        ProcurementSort,
        IntegrationCredentialsReveal,
    ];
}
