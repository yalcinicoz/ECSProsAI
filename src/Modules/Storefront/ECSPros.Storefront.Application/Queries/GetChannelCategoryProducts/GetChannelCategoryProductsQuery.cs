using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Contracts;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetChannelCategoryProducts;

public record GetChannelCategoryProductsQuery(
    Guid ChannelCategoryId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ChannelCategoryProductItemDto>>>;

public record ChannelCategoryProductItemDto(
    Guid ProductId,
    string Code,
    Dictionary<string, string> NameI18n,
    string? MainImageUrl,
    decimal BasePrice,
    bool IsActive,
    int SortOrder,
    bool IsExcluded);

public class GetChannelCategoryProductsQueryHandler(
    IStorefrontDbContext sfDb,
    ICatalogDbContext catDb,
    IStockService stockService)
    : IRequestHandler<GetChannelCategoryProductsQuery, Result<PagedResult<ChannelCategoryProductItemDto>>>
{
    public async Task<Result<PagedResult<ChannelCategoryProductItemDto>>> Handle(
        GetChannelCategoryProductsQuery request, CancellationToken ct)
    {
        var cat = await sfDb.ChannelCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChannelCategoryId, ct);

        if (cat is null)
            return Result.Failure<PagedResult<ChannelCategoryProductItemDto>>("Kanal kategorisi bulunamadı.");

        IQueryable<Catalog.Domain.Entities.Product> productQuery;

        if (cat.FillType == "manual")
        {
            var manualIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && !p.IsExcluded)
                .OrderBy(p => p.SortOrder)
                .Select(p => p.ProductId)
                .ToListAsync(ct);

            productQuery = catDb.Products
                .AsNoTracking()
                .Where(p => manualIds.Contains(p.Id));
        }
        else if (cat.FillType == "filter")
        {
            var rules = CategoryFilterRules.From(cat.FilterDef);
            HashSet<Guid>? stockRange = null;
            if (rules?.StockMin.HasValue == true || rules?.StockMax.HasValue == true)
                stockRange = await ProductFilterHelper
                    .ResolveStockRangeProductIds(catDb, stockService, rules!.StockMin, rules.StockMax, ct);

            var excludedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && p.IsExcluded)
                .Select(p => p.ProductId)
                .ToListAsync(ct);

            productQuery = ProductFilterHelper
                .BuildFilterQuery(catDb, rules, cat.FirmPlatformId, stockRange)
                .Where(p => !excludedIds.Contains(p.Id));
        }
        else // mixed
        {
            var rules = CategoryFilterRules.From(cat.FilterDef);
            HashSet<Guid>? stockRange = null;
            if (rules?.StockMin.HasValue == true || rules?.StockMax.HasValue == true)
                stockRange = await ProductFilterHelper
                    .ResolveStockRangeProductIds(catDb, stockService, rules!.StockMin, rules.StockMax, ct);

            var excludedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && p.IsExcluded)
                .Select(p => p.ProductId)
                .ToListAsync(ct);

            var filteredIds = await ProductFilterHelper
                .BuildFilterQuery(catDb, rules, cat.FirmPlatformId, stockRange)
                .Select(p => p.Id).ToListAsync(ct);

            var pinnedIds = await sfDb.ChannelCategoryProducts
                .Where(p => p.ChannelCategoryId == request.ChannelCategoryId && !p.IsExcluded)
                .Select(p => p.ProductId).ToListAsync(ct);

            var allIds = filteredIds.Union(pinnedIds).Except(excludedIds).ToList();
            productQuery = catDb.Products.AsNoTracking().Where(p => allIds.Contains(p.Id));
        }

        var total = await productQuery.CountAsync(ct);

        var products = await productQuery
            .Include(p => p.Variants).ThenInclude(v => v.Images)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var manualProductMap = await sfDb.ChannelCategoryProducts
            .Where(p => p.ChannelCategoryId == request.ChannelCategoryId)
            .ToDictionaryAsync(p => p.ProductId, p => p, ct);

        var items = products.Select(p =>
        {
            var mainImage = p.Variants
                .Where(v => v.IsActive)
                .SelectMany(v => v.Images)
                .Where(i => i.IsMain)
                .OrderBy(i => i.SortOrder)
                .FirstOrDefault()?.ImageUrl;

            manualProductMap.TryGetValue(p.Id, out var manualEntry);

            return new ChannelCategoryProductItemDto(
                p.Id, p.Code, p.NameI18n, mainImage, p.BasePrice, p.IsActive,
                manualEntry?.SortOrder ?? 0,
                manualEntry?.IsExcluded ?? false);
        }).ToList();

        return Result.Success(new PagedResult<ChannelCategoryProductItemDto>(
            items, total, request.Page, request.PageSize));
    }
}
