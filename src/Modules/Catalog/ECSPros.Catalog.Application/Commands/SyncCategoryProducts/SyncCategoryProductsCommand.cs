using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Catalog.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Commands.SyncCategoryProducts;

/// <summary>
/// fillType=filter veya mixed olan kategorilerde FilterRules'u çalıştırır,
/// CategoryProducts tablosunu günceller.
/// Dönen int: eklenen ürün sayısı.
/// </summary>
public record SyncCategoryProductsCommand(Guid CategoryId) : IRequest<Result<int>>;

public class SyncCategoryProductsCommandHandler(ICatalogDbContext db)
    : IRequestHandler<SyncCategoryProductsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SyncCategoryProductsCommand request, CancellationToken ct)
    {
        var category = await db.Categories
            .Include(c => c.FilterPreset)
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);

        if (category is null) return Result.Failure<int>("Kategori bulunamadı.");

        if (category.FillType == "manual")
            return Result.Failure<int>("Manuel kategorilerde sync çalıştırılamaz.");

        // Preset varsa preset FilterDef'ini, yoksa doğrudan FilterRules kullan
        var effectiveDef = (category.FilterPreset?.FilterDef is { Count: > 0 } presetDef && category.FilterRules is null or { Count: 0 })
            ? presetDef
            : category.FilterRules;
        var rules = CategoryFilterRules.From(effectiveDef);
        if (rules is null) return Result.Failure<int>("FilterRules tanımlı değil.");

        // Filter kurallarına göre ürünleri sorgula
        var matchedIds = await BuildFilterQuery(db, rules, ct);

        // Mevcut kayıtları yükle
        var existing = await db.CategoryProducts
            .Where(cp => cp.CategoryId == request.CategoryId)
            .ToListAsync(ct);

        var existingProductIds = existing.Select(cp => cp.ProductId).ToHashSet();

        // mixed modda pinned olanları koru, filter modda hepsini temizle (pinned dahil)
        if (category.FillType == "filter")
        {
            foreach (var e in existing)
            {
                e.IsDeleted = true;
                e.DeletedAt = DateTime.UtcNow;
            }
            existingProductIds.Clear();
        }
        else // mixed: sadece pinned olmayanları temizle
        {
            var unpinned = existing.Where(e => !e.IsPinned).ToList();
            foreach (var e in unpinned)
            {
                e.IsDeleted = true;
                e.DeletedAt = DateTime.UtcNow;
            }
            existingProductIds = existing.Where(e => e.IsPinned).Select(e => e.ProductId).ToHashSet();
        }

        // Yeni eşleşen ürünleri ekle
        var toAdd = matchedIds.Where(id => !existingProductIds.Contains(id)).ToList();
        for (int i = 0; i < toAdd.Count; i++)
        {
            db.CategoryProducts.Add(new CategoryProduct
            {
                Id = Guid.NewGuid(),
                CategoryId = request.CategoryId,
                ProductId = toAdd[i],
                SortOrder = i,
                IsPinned = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(toAdd.Count);
    }

    private static async Task<List<Guid>> BuildFilterQuery(
        ICatalogDbContext db, CategoryFilterRules rules, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().Where(p => p.IsActive);

        if (rules.ProductGroupIds is { Count: > 0 })
            q = q.Where(p => rules.ProductGroupIds.Contains(p.ProductGroupId));

        if (rules.PriceMin.HasValue)
            q = q.Where(p => p.BasePrice >= rules.PriceMin.Value);

        if (rules.PriceMax.HasValue)
            q = q.Where(p => p.BasePrice <= rules.PriceMax.Value);

        if (rules.AttributeFilters is { Count: > 0 })
        {
            foreach (var af in rules.AttributeFilters)
            {
                var typeId = af.AttributeTypeId;
                var valueIds = af.ValueIds;
                q = q.Where(p => p.Attributes.Any(a =>
                    a.AttributeTypeId == typeId && valueIds.Contains(a.AttributeValueId!.Value)));
            }
        }

        return await q.Select(p => p.Id).ToListAsync(ct);
    }
}
