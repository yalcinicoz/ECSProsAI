using ECSPros.Integration.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Integration.Application.Queries.ResolveErpProductRefs;

/// <summary>
/// Legacy ERP ürün Id'lerini (görsel arama servisi `urunId` döndürür) katalog referanslarına çözer.
/// `erp_variant_data.VariantId` katalog yeniden kurulumları sonrası BAYAT olabilir — güvenilir
/// eşleme payload'daki modelCode (→ products.Code) ve barcode (→ product_variants.Barcode)
/// üzerindendir (H3 kararı; her ikisi de %100 kapsam, 2026-07-22 doğrulaması).
/// </summary>
public record ResolveErpProductRefsQuery(List<int> ErpProductIds)
    : IRequest<Result<List<ErpProductRefDto>>>;

/// <summary>Bir ERP ürününün varyant satırı — ErpVariantId servisin `urunAnaVaryantId`sine denk gelir.</summary>
public record ErpProductRefDto(int ErpProductId, long ErpVariantId, string ModelCode, string? Barcode);

public class ResolveErpProductRefsQueryHandler
    : IRequestHandler<ResolveErpProductRefsQuery, Result<List<ErpProductRefDto>>>
{
    private readonly IIntegrationDbContext _db;
    public ResolveErpProductRefsQueryHandler(IIntegrationDbContext db) => _db = db;

    public async Task<Result<List<ErpProductRefDto>>> Handle(ResolveErpProductRefsQuery request, CancellationToken ct)
    {
        if (request.ErpProductIds.Count == 0)
            return Result.Success(new List<ErpProductRefDto>());

        var ids = request.ErpProductIds.Distinct().ToArray();

        // Payload Dictionary-jsonb olarak maplendiğinden ->> filtresi EF'e çevrilemez → raw SQL
        // (ifade index'i IxErpVariantDataErpProductId migration'ı ile).
        var rows = await _db.Database
            .SqlQuery<ErpRefRow>($"""
                SELECT (("Payload"->>'erpProductId')::int)     AS "ErpProductId",
                       (("Payload"->>'erpVariantId')::bigint)  AS "ErpVariantId",
                       ("Payload"->>'modelCode')               AS "ModelCode",
                       ("Payload"->>'barcode')                 AS "Barcode"
                FROM integration.erp_variant_data
                WHERE NOT "IsDeleted"
                  AND (("Payload"->>'erpProductId')::int) = ANY({ids})
                """)
            .ToListAsync(ct);

        var result = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ModelCode))
            .Select(r => new ErpProductRefDto(r.ErpProductId, r.ErpVariantId, r.ModelCode!, r.Barcode))
            .ToList();

        return Result.Success(result);
    }

    private sealed class ErpRefRow
    {
        public int ErpProductId { get; set; }
        public long ErpVariantId { get; set; }
        public string? ModelCode { get; set; }
        public string? Barcode { get; set; }
    }
}
