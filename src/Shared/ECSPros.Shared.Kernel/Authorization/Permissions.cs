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

    // ── Layer 2: Firma kullanıcısı işlemleri ──────────────────────────────────

    public const string CatalogProductsManage    = "catalog.products.manage";
    public const string CatalogCategoriesManage  = "catalog.categories.manage";
    public const string CatalogImagesManage      = "catalog.images.manage";
    public const string CatalogSettingsManage    = "catalog.settings.manage";

    /// <summary>Depo, stok ve transfer yönetimi.</summary>
    public const string InventoryManage = "inventory.manage";

    /// <summary>Layer 2 — firm_admin rolüne atanan permission kodları.</summary>
    public static readonly IReadOnlyList<string> FirmAdminPermissions =
    [
        CatalogProductsManage,
        CatalogCategoriesManage,
        CatalogImagesManage,
        CatalogSettingsManage,
        InventoryManage,
    ];

    /// <summary>Tüm permission kodları — super_admin ve platform_admin rollerine atanır.</summary>
    public static readonly IReadOnlyList<string> AllPermissions =
    [
        CatalogPlatformManage,
        CatalogProductsManage,
        CatalogCategoriesManage,
        CatalogImagesManage,
        CatalogSettingsManage,
        InventoryManage,
    ];
}
