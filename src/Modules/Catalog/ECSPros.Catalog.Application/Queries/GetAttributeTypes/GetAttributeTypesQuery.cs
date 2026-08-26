using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ECSPros.Catalog.Application.Queries.GetAttributeTypes;

public record GetAttributeTypesQuery(bool ActiveOnly = true, bool IncludeCounts = true) : IRequest<Result<List<AttributeTypeDto>>>;

public record AttributeTypeDto(
    Guid Id,
    string Code,
    Dictionary<string, string> NameI18n,
    string DataType,
    bool IsActive,
    int SortOrder,
    bool UseInFilter,
    List<AttributeValueDto> Values,
    List<AxisSubAttributeSchemaDto> AxisSubAttributeSchema
);

public record AttributeValueDto(
    Guid Id,
    Dictionary<string, string> NameI18n,
    bool IsActive,
    int SortOrder,
    string? HexCode,
    int UsedInProductCount = 0
);

public record AxisSubAttributeSchemaDto(
    Guid SubAttributeTypeId,
    string SubAttributeTypeCode,
    Dictionary<string, string> SubAttributeTypeNameI18n,
    bool IsRequired
);

public class GetAttributeTypesQueryHandler : IRequestHandler<GetAttributeTypesQuery, Result<List<AttributeTypeDto>>>
{
    private readonly ICatalogDbContext _db;
    private readonly IMemoryCache _cache;

    public GetAttributeTypesQueryHandler(ICatalogDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<List<AttributeTypeDto>>> Handle(GetAttributeTypesQuery request, CancellationToken ct)
    {
        var query = _db.AttributeTypes
            .AsNoTracking()
            .AsSplitQuery()   // Faz 2: kardeş koleksiyon Include kartezyeni önlenir
            .Include(a => a.Values.Where(v => !v.IsDeleted))
            .Include(a => a.AxisSubAttributes.Where(s => !s.IsDeleted))
                .ThenInclude(s => s.SubAttributeType)
            .AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(a => a.IsActive);

        var list = await query.OrderBy(a => a.SortOrder).ThenBy(a => a.Code).ToListAsync(ct);

        // "Kaç üründe kullanılıyor" sayısı sadece Özellik Tipi Detay sayfasındaki temizlik/kullanım
        // özetinde gösteriliyor (bkz. admin/src/pages/catalog/AttributeTypeDetailPage.tsx) — ürün
        // detay sayfasının Özellikler sekmesi bu bilgiyi hiç kullanmıyor, sadece dropdown seçenekleri
        // için id+isim istiyor. O sayfa her açıldığında ~708K product_attributes + ~2.4M
        // product_variant_attributes üzerinden TÜM 7900+ değer için bu sayıyı hesaplamak gereksiz
        // yavaşlığa yol açıyordu (bkz. project_group_schema_completion_2026-07-02) — bu yüzden
        // IncludeCounts=false ile atlanabilir hale getirildi.
        // Sayım 708K product_attributes + 2.4M product_variant_attributes tarar (5-15 sn) — liste
        // sayfasının her açılışında tekrarlamamak için 10 dk süreyle süreç içi önbelleğe alınır
        // (bilgilendirme amaçlı sayı; bayatlık zararsız, B-10 panel testi bulgusu).
        var countMap = new Dictionary<Guid, int>();
        if (request.IncludeCounts && _cache.TryGetValue("attrtype-usage-counts", out Dictionary<Guid, int>? cached) && cached is not null)
        {
            countMap = cached;
        }
        else if (request.IncludeCounts)
        {
            // Not: GroupBy içinde .Select(...).Distinct().Count() kullanmak EF Core'da grup başına
            // korelasyonlu bir alt sorguya çevrilir (yüz binlerce product_attributes satırında saniyeler
            // sürer) — önce (AttributeValueId, ProductId) çiftlerini distinct'leyip SONRA grupla/say,
            // bu şekilde tek bir düz GROUP BY + COUNT üretiliyor.
            var allValueIds = list.SelectMany(a => a.Values).Select(v => v.Id).ToList();

            var directCounts = await _db.ProductAttributes
                .Where(pa => pa.AttributeValueId.HasValue && allValueIds.Contains(pa.AttributeValueId.Value))
                .Select(pa => new { ValueId = pa.AttributeValueId!.Value, pa.ProductId })
                .Distinct()
                .GroupBy(pa => pa.ValueId)
                .Select(g => new { ValueId = g.Key, ProductCount = g.Count() })
                .ToListAsync(ct);

            var variantCounts = await _db.ProductVariantAttributes
                .Where(pva => allValueIds.Contains(pva.AttributeValueId))
                .Select(pva => new { ValueId = pva.AttributeValueId, ProductId = pva.Variant.ProductId })
                .Distinct()
                .GroupBy(pva => pva.ValueId)
                .Select(g => new { ValueId = g.Key, ProductCount = g.Count() })
                .ToListAsync(ct);

            foreach (var row in directCounts)
                countMap[row.ValueId] = row.ProductCount;
            foreach (var row in variantCounts)
                countMap[row.ValueId] = (countMap.GetValueOrDefault(row.ValueId)) + row.ProductCount;

            _cache.Set("attrtype-usage-counts", countMap, TimeSpan.FromMinutes(10));
        }

        // Eksen alt özellik şeması: aynı SubAttributeTypeId birden fazla gruptan gelebilir, distinct al
        var dto = list.Select(a => new AttributeTypeDto(
            a.Id, a.Code, a.NameI18n, a.DataType, a.IsActive, a.SortOrder, a.UseInFilter,
            a.Values
                .OrderBy(v => v.SortOrder)
                .Select(v => new AttributeValueDto(
                    v.Id, v.NameI18n, v.IsActive, v.SortOrder,
                    v.HexCode,
                    countMap.GetValueOrDefault(v.Id, 0)))
                .ToList(),
            a.AxisSubAttributes
                .GroupBy(s => s.SubAttributeTypeId)
                .Select(g => g.First())
                .OrderBy(s => s.SortOrder)
                .Select(s => new AxisSubAttributeSchemaDto(
                    s.SubAttributeTypeId,
                    s.SubAttributeType.Code,
                    s.SubAttributeType.NameI18n,
                    s.IsRequired))
                .ToList()
        )).ToList();

        return Result.Success(dto);
    }
}
