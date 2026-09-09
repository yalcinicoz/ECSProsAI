using ECSPros.Catalog.Application.Queries.GetAdminProductSubmissions;

namespace ECSPros.Api.Grid;

/// <summary>Ürün gönderimleri Excel kolonları (tedarikçi ürün kodu kilitli).</summary>
public static class ProductSubmissionExportColumns
{
    public static readonly IReadOnlyList<GridExportColumn<ProductSubmissionExportRow>> All = new GridExportColumn<ProductSubmissionExportRow>[]
    {
        new("supplierProductCode", "Tedarikçi Ürün Kodu", r => r.SupplierProductCode, Locked: true),
        new("name", "Ad", r => r.Name),
        new("groupCode", "Grup", r => r.GroupCode),
        new("variantCount", "Varyant", r => r.VariantCount),
        new("status", "Durum", r => ProductSubmissionGrid.StatusLabel(r.Status)),
        new("productCode", "Ürün Kodu", r => r.ProductCode),
        new("reviewNote", "İnceleme Notu", r => r.ReviewNote),
        new("submittedAt", "Gönderim", r => GridExportWriter.ToIstanbul(r.SubmittedAt)),
        new("reviewedAt", "İnceleme", r => r.ReviewedAt.HasValue ? GridExportWriter.ToIstanbul(r.ReviewedAt.Value) : null),
    };
}
