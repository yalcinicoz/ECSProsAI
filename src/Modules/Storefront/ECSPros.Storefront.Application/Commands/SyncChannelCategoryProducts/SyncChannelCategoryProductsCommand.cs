using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.SyncChannelCategoryProducts;

/// <summary>
/// FillType=filter veya mixed olan kanal kategorilerinde FilterDef'i çalıştırır,
/// ChannelCategoryProducts tablosunu günceller.
/// </summary>
public record SyncChannelCategoryProductsCommand(Guid ChannelCategoryId) : IRequest<Result<int>>;

public class SyncChannelCategoryProductsCommandHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb,
    IStockService stockService)
    : IRequestHandler<SyncChannelCategoryProductsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SyncChannelCategoryProductsCommand request, CancellationToken ct)
    {
        var cat = await sfDb.ChannelCategories
            .FirstOrDefaultAsync(c => c.Id == request.ChannelCategoryId, ct);

        if (cat is null) return Result.Failure<int>("Kanal kategorisi bulunamadı.");
        if (cat.FillType == "manual") return Result.Failure<int>("Manuel kategorilerde sync çalıştırılamaz.");

        var rules = CategoryFilterRules.From(cat.FilterDef);
        if (rules is null) return Result.Failure<int>("FilterDef tanımlı değil.");

        HashSet<Guid>? productIdsInStockRange = null;
        if (rules.StockMin.HasValue || rules.StockMax.HasValue)
            productIdsInStockRange = await ProductFilterHelper
                .ResolveStockRangeProductIds(catDb, stockService, rules.StockMin, rules.StockMax, ct);

        if (!rules.IsActive.HasValue) rules.IsActive = true;

        var matchedIds = await ProductFilterHelper
            .BuildFilterQuery(catDb, rules, cat.FirmPlatformId, productIdsInStockRange)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var existing = await sfDb.ChannelCategoryProducts
            .Where(p => p.ChannelCategoryId == request.ChannelCategoryId)
            .ToListAsync(ct);

        // Hariç tutulanları koru; diğerlerini temizle
        var toRemove = existing.Where(e => !e.IsExcluded).ToList();
        sfDb.ChannelCategoryProducts.RemoveRange(toRemove);

        var excludedProductIds = existing.Where(e => e.IsExcluded).Select(e => e.ProductId).ToHashSet();

        var toAdd = matchedIds
            .Where(id => !excludedProductIds.Contains(id))
            .ToList();

        for (int i = 0; i < toAdd.Count; i++)
        {
            sfDb.ChannelCategoryProducts.Add(new ChannelCategoryProduct
            {
                ChannelCategoryId = request.ChannelCategoryId,
                ProductId         = toAdd[i],
                SortOrder         = i,
                IsExcluded        = false,
            });
        }

        await sfDb.SaveChangesAsync(ct);
        return Result.Success(toAdd.Count);
    }
}
