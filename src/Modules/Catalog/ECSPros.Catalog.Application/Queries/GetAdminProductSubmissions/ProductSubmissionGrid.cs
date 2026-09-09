using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetAdminProductSubmissions;

/// <summary>
/// Tedarikçi ürün gönderimleri DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama +
/// adlandırılmış filtreler (status/supplierId) + global arama (tedarikçi ürün kodu, ad, grup kodu).
/// </summary>
public static class ProductSubmissionGrid
{
    public static readonly string[] Statuses = { "pending", "approved", "rejected" };

    public static readonly GridSchema<ProductSubmission> Schema = new GridSchema<ProductSubmission>()
        .Text("supplierProductCode", s => s.SupplierProductCode)
        .Text("name", s => GridJson.Text(s.Name, "tr"))
        .Text("groupCode", s => s.GroupCode)
        .Text("productCode", s => s.ProductCode)
        .Text("reviewNote", s => s.ReviewNote)
        .Enum("status", s => s.Status, Statuses)
        .Number("variantCount", s => s.VariantCount)
        .Bool("reviewed", s => s.ReviewedAt != null)
        .Bool("hasProduct", s => s.ProductCode != null && s.ProductCode != "")
        .Date("submittedAt", s => s.CreatedAt)
        .Date("reviewedAt", s => s.ReviewedAt)
        .Guid("supplierId", s => s.SupplierId)
        .Guid("apiClientId", s => s.ApiClientId)
        .Sort("supplierProductCode", s => s.SupplierProductCode)
        .Sort("name", s => GridJson.Text(s.Name, "tr"))
        .Sort("groupCode", s => s.GroupCode)
        .Sort("productCode", s => s.ProductCode)
        .Sort("status", s => s.Status)
        .Sort("variantCount", s => s.VariantCount)
        .Sort("submittedAt", s => s.CreatedAt)
        .Sort("reviewedAt", s => s.ReviewedAt)
        .DefaultSort(s => s.CreatedAt, desc: true)
        .TieBreaker(s => s.Id);

    public static IQueryable<ProductSubmission> ApplyNamed(IQueryable<ProductSubmission> query, ProductSubmissionFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(s => s.Status == f.Status);
        if (f.SupplierId.HasValue) query = query.Where(s => s.SupplierId == f.SupplierId);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            query = query.Where(s => s.SupplierProductCode.ToLower().Contains(t)
                || s.GroupCode.ToLower().Contains(t)
                || GridJson.Text(s.Name, "tr").ToLower().Contains(t));
        }
        return query;
    }

    public static IQueryable<ProductSubmission> ApplyAll(IQueryable<ProductSubmission> query, ProductSubmissionFilters f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);

    public static string StatusLabel(string s) => s switch
    {
        "pending" => "Onay Bekliyor", "approved" => "Onaylandı", "rejected" => "Reddedildi", _ => s,
    };
}

public record ProductSubmissionFilters(string? Status = null, Guid? SupplierId = null, string? Search = null);

public record ProductSubmissionExportRow(
    string SupplierProductCode, string Name, string GroupCode, int VariantCount, string Status,
    string? ProductCode, string? ReviewNote, DateTime SubmittedAt, DateTime? ReviewedAt);

public record ExportProductSubmissionsQuery(ProductSubmissionFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<ProductSubmissionExportRow>>>;

public class ExportProductSubmissionsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<ExportProductSubmissionsQuery, Result<GridExportSource<ProductSubmissionExportRow>>>
{
    public async Task<Result<GridExportSource<ProductSubmissionExportRow>>> Handle(
        ExportProductSubmissionsQuery r, CancellationToken ct)
    {
        var q = ProductSubmissionGrid.ApplyAll(db.ProductSubmissions.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<ProductSubmissionExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var rows = ProductSubmissionGrid.Schema.ApplySort(q, r.Grid).Select(s => new ProductSubmissionExportRow(
            s.SupplierProductCode, GridJson.Text(s.Name, "tr"), s.GroupCode, s.VariantCount, s.Status,
            s.ProductCode, s.ReviewNote, s.CreatedAt, s.ReviewedAt));
        return Result.Success(new GridExportSource<ProductSubmissionExportRow>(count, rows));
    }
}
