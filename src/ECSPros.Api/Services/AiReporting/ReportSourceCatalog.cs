using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Controlled metadata, not database schema or model-supplied executable code.</summary>
public sealed record ReportSourceDescriptor(string Id, string Label, string Permission,
    bool ChannelScoped, string Grain, string Description);

public static class ReportSourceCatalog
{
    // Advertise only sources with an implemented, permission-enforcing executor.
    public static IReadOnlyList<ReportSourceDescriptor> Sources { get; } = Array.AsReadOnly(new[]
    {
        new ReportSourceDescriptor("stock", "Güncel stok", Permissions.InventoryView, false,
            "stock-row", "Güncel stok satırları; hareket geçmişi veya maliyet değildir."),
        new ReportSourceDescriptor("orders", "Siparişler", Permissions.OrdersView, true,
            "order", "Sipariş başlığı; tutar GrandTotal'dır, tahsilat veya net satış değildir."),
        new ReportSourceDescriptor(ProductCardReportSource.Id, "Ürün kartları ve hareket varlığı", Permissions.CatalogProductsView, false,
            "productCard", "Stok satırı olmayan kartlar dahil. Dönem hareket tarihidir; kart açılışı ayrı koşuldur."),
        new ReportSourceDescriptor(MovementReportSource.Id, "Stok hareketleri", Permissions.InventoryView, false,
            "movement", "Kayıtlı stok hareketleri; adet toplamı net stok değişimi değildir."),
        new ReportSourceDescriptor(ReturnReportSource.Id, "İadeler", Permissions.OrdersReturnsView, true,
            "return", "İade kayıt tarihi esas alınır; kayıtlı tutar ödenmiş geri ödeme değildir."),
        new ReportSourceDescriptor(StaffActivitySource.Id, "Personel işlem kayıtları", Permissions.IamUsersView, false,
            "staffActivity", "Yetkili toplama/paketleme ve panel fatura kayıtları; süre, satış cirosu veya performans puanı değildir."),
        new ReportSourceDescriptor(CustomerReportSource.Id, "Müşteriler", Permissions.CrmMembersView, false,
            "customer", "Bir müşteri kartı; dönem sipariş/iade işlemine uygulanır, müşteri açılışına değil. Sayılar yalnız yetkili kanalları kapsar.")
    });

    public static IReadOnlyList<ReportSourceDescriptor> ForPermissions(IReadOnlySet<string> permissions) =>
        !permissions.Contains(ReportDictionary.UsePermission) ? Array.Empty<ReportSourceDescriptor>()
            : Sources.Where(s => permissions.Contains(s.Permission)
                && (s.Id != CustomerReportSource.Id || permissions.Contains(CustomerReportSource.GlobalScopeCapability))
                && (s.Id != StaffActivitySource.Id || permissions.Contains(StaffActivitySource.Capability))
                && (s.Id != ProductCardReportSource.Id || permissions.Contains(ProductCardReportSource.Capability))).ToArray();

    public static IReadOnlyList<ReportField> Fields(string? subject, IReadOnlySet<string> permissions,
        IReadOnlyList<ReportField>? attributes = null)
    {
        var source = ForPermissions(permissions).SingleOrDefault(s => s.Id == subject);
        if (source is null) return Array.Empty<ReportField>();
        if (source.Id == "orders") return OrderReportRecipe.ForPermissions(permissions);
        if (source.Id == MovementReportSource.Id) return MovementReportSource.Dictionary.Describe(permissions);
        if (source.Id == ReturnReportSource.Id) return ReturnReportSource.Dictionary.Describe(permissions);
        if (source.Id == CustomerReportSource.Id) return CustomerReportRelations.Fields(permissions);
        if (source.Id == StaffActivitySource.Id) return StaffActivitySource.Dictionary.Describe(permissions);
        if (source.Id == ProductCardReportSource.Id) return ProductCardReportSource.Fields(permissions);
        // Only the controlled attribute extension can augment stock; cannot shadow a core field.
        var extra = attributes ?? Array.Empty<ReportField>();
        if (extra.Count > 256 || extra.Any(f => f is null || f.Id is null || !ReportAttributeCatalog.TryId(f.Id, out _)
            || f.Kind != "dimension" || f.Permission != Permissions.CatalogProductsView
            || f.Operators is null || f.Operators.Count != 2 || !f.Operators.Contains("eq") || !f.Operators.Contains("in"))
            || extra.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != extra.Count)
            throw new ReportCatalogException("Rapor özellik sözlüğü geçerli değil.");
        return ReportDictionary.Fields.Concat(extra).Where(f => permissions.Contains(f.Permission)).ToArray();
    }

    /// <summary>Capability discovery only. Row scopes must still be enforced by executors.</summary>
    public static IReadOnlySet<string> ResolvePermissions(EfektifYetkiler effective)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!effective.Var(ReportDictionary.UsePermission)) return result;
        result.Add(ReportDictionary.UsePermission);
        foreach (var source in Sources)
        {
            if (!effective.Var(source.Permission)) continue;
            if (source.ChannelScoped || (effective.Kanallar(source.Permission) is null
                && effective.Kanallar(ReportDictionary.UsePermission) is null)) result.Add(source.Permission);
        }
        if (effective.Var(Permissions.CatalogProductsView) && effective.Kanallar(Permissions.CatalogProductsView) is null)
            result.Add(Permissions.CatalogProductsView);
        if (effective.Var(Permissions.CrmMembersView) && effective.Kanallar(Permissions.CrmMembersView) is null)
            result.Add(Permissions.CrmMembersView);
        if (result.Contains(Permissions.OrdersView) && effective.Var(Permissions.OrdersReturnsView)) result.Add(Permissions.OrdersReturnsView);
        if (CustomerReportSource.CanAccess(effective)) result.Add(CustomerReportSource.GlobalScopeCapability);
        if (ProductCardReportSource.CanAccess(effective)) result.Add(ProductCardReportSource.Capability);
        if (StaffActivitySource.CanAccess(effective))
        {
            result.Add(StaffActivitySource.Capability);
            result.Add(Permissions.IamUsersView);
        }
        return result;
    }

    /// <summary>Null means unrestricted; empty means no records. Never accept client authorization.</summary>
    public static Guid[]? ResolveChannels(string subject, EfektifYetkiler effective)
    {
        if (subject == CustomerReportSource.Id && !CustomerReportSource.CanAccess(effective))
            throw new UnauthorizedAccessException();
        var source = Sources.SingleOrDefault(s => s.Id == subject);
        if (source is null || !ResolvePermissions(effective).Contains(source.Permission))
            throw new UnauthorizedAccessException();
        var reporting = effective.Kanallar(ReportDictionary.UsePermission);
        var domain = effective.Kanallar(source.Permission);
        return domain is null ? reporting?.ToArray()
            : reporting is null ? domain.ToArray() : domain.Intersect(reporting).ToArray();
    }
}
