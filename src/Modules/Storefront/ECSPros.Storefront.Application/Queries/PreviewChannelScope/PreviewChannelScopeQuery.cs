using ECSPros.Catalog.Application.Helpers;
using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services.ChannelScoping;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.PreviewChannelScope;

/// <summary>Kaydetmeden filtreyi çalıştırır: eşleşen ürün sayısı (+ toplam görselli katalog).</summary>
public record PreviewChannelScopeQuery(Guid FirmPlatformId, string FillType, Dictionary<string, object>? FilterDef)
    : IRequest<Result<ChannelScopePreviewDto>>;

public record ChannelScopePreviewDto(int MatchedCount, int CatalogCount);

public class PreviewChannelScopeQueryHandler(ChannelScopeResolver resolver, ICatalogDbContext catDb)
    : IRequestHandler<PreviewChannelScopeQuery, Result<ChannelScopePreviewDto>>
{
    public async Task<Result<ChannelScopePreviewDto>> Handle(PreviewChannelScopeQuery request, CancellationToken ct)
    {
        var catalogCount = await catDb.Products.AsNoTracking()
            .CountAsync(p => catDb.ProductImages.Any(img => img.ProductId == p.Id), ct);
        if ((request.FillType ?? "all").ToLowerInvariant() == "all")
            return Result.Success(new ChannelScopePreviewDto(catalogCount, catalogCount));
        var rules = CategoryFilterRules.From(request.FilterDef);
        var matched = await resolver.ResolveAsync(request.FirmPlatformId, rules, ct);
        return Result.Success(new ChannelScopePreviewDto(matched.Count, catalogCount));
    }
}
