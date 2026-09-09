using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Queries.GetSortingEntries;

public record GetSortingEntriesQuery(
    Guid? ReceiptBatchId = null,
    bool? Unbatched = null,          // true → yalnız partisiz kayıtlar
    string? PutawayStatus = null,    // pending | placed
    int Page = 1,
    int PageSize = 50,
    string? Arama = null,            // ürün adı / kod / SKU / barkod (CATALOG'da çözülür)
    GridRequest? Grid = null) : IRequest<Result<PagedResult<SortingEntryRowDto>>>;

public record SortingEntryRowDto(
    Guid Id, Guid? ReceiptBatchId, Guid VariantId, string ProductCode, string Name,
    string Sku, string? Barcode, decimal Quantity, decimal? UnitCost,
    bool LabelPrinted, int LabelCount, string PutawayStatus, Guid? PlacedBinId, DateTime CreatedAt);

public class GetSortingEntriesQueryHandler(IProcurementDbContext db, ICatalogDbContext catDb)
    : IRequestHandler<GetSortingEntriesQuery, Result<PagedResult<SortingEntryRowDto>>>
{
    public async Task<Result<PagedResult<SortingEntryRowDto>>> Handle(GetSortingEntriesQuery request, CancellationToken ct)
    {
        var q = db.SortingEntries.AsNoTracking();
        if (request.ReceiptBatchId.HasValue) q = q.Where(e => e.ReceiptBatchId == request.ReceiptBatchId.Value);
        else if (request.Unbatched == true) q = q.Where(e => e.ReceiptBatchId == null);
        if (!string.IsNullOrWhiteSpace(request.PutawayStatus)) q = q.Where(e => e.PutawayStatus == request.PutawayStatus);

        // DataGrid (2026-09-09): başlık süzgeçleri beyaz listeden (SortingEntryGrid.Schema).
        q = SortingEntryGrid.Schema.ApplyFilters(q, request.Grid);

        // Arama ürün alanlarındadır ve varyant CATALOG'da çözülür → terim önce VARYANT KİMLİKLERİNE
        // çevrilir, sonra sayım kayıtlarına uygulanır. Aksi hâlde sayfalamadan sonra bellekte süzmek
        // gerekir ve toplam sayı yanlış çıkar (yetki logunda yaşanan hata).
        if (!string.IsNullOrWhiteSpace(request.Arama))
        {
            var terim = request.Arama.Trim().ToLower();
            var eslesenVaryantlar = await catDb.ProductVariants.AsNoTracking()
                .Where(v => v.Sku.ToLower().Contains(terim)
                    || (v.Barcode != null && v.Barcode.ToLower().Contains(terim))
                    || catDb.Products.Any(p => p.Id == v.ProductId
                        && (p.Code.ToLower().Contains(terim)
                            || PgJsonFunctions.JsonText(p.NameI18n, "tr")!.ToLower().Contains(terim))))
                .Select(v => v.Id)
                .ToListAsync(ct);
            q = q.Where(e => eslesenVaryantlar.Contains(e.VariantId));
        }

        var total = await q.CountAsync(ct);   // sayım SAYFALAMADAN ÖNCE
        var rows = await SortingEntryGrid.Schema.ApplySort(q, request.Grid)
            .Skip((Math.Max(1, request.Page) - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(ct);

        var variantIds = rows.Select(r => r.VariantId).Distinct().ToList();
        var variants = await catDb.ProductVariants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .Select(v => new
            {
                v.Id, v.Sku, v.Barcode,
                ProductCode = catDb.Products.Where(p => p.Id == v.ProductId).Select(p => p.Code).FirstOrDefault() ?? "",
                Name = catDb.Products.Where(p => p.Id == v.ProductId)
                    .Select(p => PgJsonFunctions.JsonText(p.NameI18n, "tr")).FirstOrDefault() ?? "",
            })
            .ToListAsync(ct);
        var byId = variants.ToDictionary(v => v.Id);

        var dto = rows.Select(e =>
        {
            byId.TryGetValue(e.VariantId, out var v);
            return new SortingEntryRowDto(e.Id, e.ReceiptBatchId, e.VariantId,
                v?.ProductCode ?? "", v?.Name ?? "", v?.Sku ?? "", v?.Barcode,
                e.Quantity, e.UnitCost, e.LabelPrinted, e.LabelCount, e.PutawayStatus, e.PlacedBinId, e.CreatedAt);
        }).ToList();
        return Result.Success(new PagedResult<SortingEntryRowDto>(dto, total, request.Page, request.PageSize));
    }
}
