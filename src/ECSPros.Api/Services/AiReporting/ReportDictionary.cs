using ECSPros.Shared.Kernel.Authorization;

namespace ECSPros.Api.Services.AiReporting;

/// <summary>Shared metadata for future prompt, validator and executor. No model-provided formulas.</summary>
public static class ReportDictionary
{
    public const int Version = 1;
    public const string Subject = "stock";
    public const string UsePermission = "reports.ai.use";
    public const string ExportPermission = "reports.ai.export";
    public const string SharePermission = "reports.ai.share";
    public static IReadOnlyList<ReportField> ForSubject(string subject, IReadOnlySet<string> permissions, IReadOnlyList<ReportField>? attributes = null) =>
        ReportSourceCatalog.Fields(subject, permissions, attributes);
    public static bool CanReport(IReadOnlySet<string> permissions) =>
        ReportSourceCatalog.ForPermissions(permissions).Count > 0;

    // Initial stock-only contract. Sales/cost formulas are not yet approved.
    public static IReadOnlyList<ReportField> Fields { get; } = Array.AsReadOnly(new[]
    {
        Metric("stock.quantity", "Stok miktarı", "Stok satırlarının Quantity toplamı; fiziksel/sanal ayrımı stockType ile yapılır."),
        Metric("stock.reserved", "Rezerve miktar", "Stok satırlarının ReservedQuantity toplamı; rezervasyon tablosuyla join yapılmaz."),
        Metric("stock.available", "Kullanılabilir miktar", "SUM(Quantity - ReservedQuantity); negatif değer sıfıra kırpılmaz."),
        Dimension("productCode", "Ürün kodu"),
        Dimension("warehouseId", "Depo"),
        Dimension("stockType", "Stok türü")
    });

    private static ReportField Metric(string id, string label, string description) =>
        new(id, label, "metric", Permissions.InventoryView, description, Array.Empty<string>());

    private static ReportField Dimension(string id, string label) =>
        new(id, label, "dimension", Permissions.InventoryView, label,
            Array.AsReadOnly(new[] { "eq", "in" }));

    public static IReadOnlyList<ReportField> ForPermissions(IReadOnlySet<string> permissions,
        IReadOnlyList<ReportField>? attributes = null) =>
        ReportSourceCatalog.Fields(Subject, permissions, attributes);
}
