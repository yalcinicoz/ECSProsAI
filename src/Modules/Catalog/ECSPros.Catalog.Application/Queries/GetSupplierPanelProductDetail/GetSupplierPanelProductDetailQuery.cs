using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetSupplierPanelProductDetail;

/// <summary>
/// Satıcı paneli ürün detayı — owner-scoped: canlı ürün (varsa varyantlarıyla) +
/// bu koda ait TÜM gönderim geçmişi (red notları dahil). Kod hiç yoksa hata.
/// </summary>
public record GetSupplierPanelProductDetailQuery(Guid SupplierId, string SupplierProductCode)
    : IRequest<Result<SupplierPanelProductDetailDto>>;

public record SupplierPanelProductDetailDto(
    string SupplierProductCode,
    SupplierPanelLiveProductDto? Product,          // null → henüz canlı değil
    List<SupplierPanelSubmissionDto> Submissions); // yeni→eski

public record SupplierPanelLiveProductDto(
    string Code,
    Dictionary<string, string> Name,
    string GroupCode,
    Dictionary<string, string> GroupName,
    decimal BasePrice,
    int TaxRate,
    bool IsSaleOpen,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<SupplierPanelVariantDto> Variants);

public record SupplierPanelVariantDto(
    string Sku,
    string? Barcode,
    decimal BasePrice,
    bool IsActive,
    List<SupplierPanelVariantAxisDto> Axes);       // örn. Renk: Kırmızı, Beden: M

public record SupplierPanelVariantAxisDto(
    Dictionary<string, string> TypeName,
    Dictionary<string, string> ValueName);

public record SupplierPanelSubmissionDto(
    Guid Id,
    string Status,                                  // pending | approved | rejected
    int VariantCount,
    string? ReviewNote,
    string Source,                                  // api | panel
    DateTime SubmittedAt,
    DateTime? ReviewedAt);

public class GetSupplierPanelProductDetailQueryHandler
    : IRequestHandler<GetSupplierPanelProductDetailQuery, Result<SupplierPanelProductDetailDto>>
{
    private readonly ICatalogDbContext _db;
    public GetSupplierPanelProductDetailQueryHandler(ICatalogDbContext db) => _db = db;

    public async Task<Result<SupplierPanelProductDetailDto>> Handle(
        GetSupplierPanelProductDetailQuery request, CancellationToken ct)
    {
        var code = request.SupplierProductCode.Trim();

        // Canlı ürün (owner-scope) — SupplierProductCode ya da iç kod üzerinden
        var product = await _db.Products
            .Include(p => p.ProductGroup)
            .Include(p => p.Variants.Where(v => !v.IsDeleted))
                .ThenInclude(v => v.VariantAttributes)
                    .ThenInclude(va => va.AttributeType)
            .Include(p => p.Variants.Where(v => !v.IsDeleted))
                .ThenInclude(v => v.VariantAttributes)
                    .ThenInclude(va => va.AttributeValue)
            .FirstOrDefaultAsync(p => p.SupplierId == request.SupplierId
                && (p.SupplierProductCode == code || p.Code == code), ct);

        // Gönderim geçmişi (owner-scope), yeni→eski
        var submissions = await _db.ProductSubmissions
            .Where(s => s.SupplierId == request.SupplierId && s.SupplierProductCode == code)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SupplierPanelSubmissionDto(
                s.Id, s.Status, s.VariantCount, s.ReviewNote,
                s.ApiClientId != null ? "api" : "panel",
                s.CreatedAt, s.ReviewedAt))
            .ToListAsync(ct);

        if (product is null && submissions.Count == 0)
            return Result.Failure<SupplierPanelProductDetailDto>(
                $"'{code}' kodlu ürün ya da gönderim bulunamadı.");

        SupplierPanelLiveProductDto? live = null;
        if (product is not null)
        {
            var variants = product.Variants
                .OrderBy(v => v.Sku)
                .Select(v => new SupplierPanelVariantDto(
                    v.Sku, v.Barcode, v.BasePrice, v.IsActive,
                    v.VariantAttributes
                        .Select(va => new SupplierPanelVariantAxisDto(
                            va.AttributeType.NameI18n, va.AttributeValue.NameI18n))
                        .ToList()))
                .ToList();

            live = new SupplierPanelLiveProductDto(
                product.Code, product.NameI18n, product.ProductGroup.Code, product.ProductGroup.NameI18n,
                product.BasePrice, product.TaxRate, product.IsSaleOpen,
                product.CreatedAt, product.UpdatedAt, variants);
        }

        return Result.Success(new SupplierPanelProductDetailDto(code, live, submissions));
    }
}
