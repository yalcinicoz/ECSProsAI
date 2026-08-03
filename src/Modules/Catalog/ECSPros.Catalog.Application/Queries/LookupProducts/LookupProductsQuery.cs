using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.LookupProducts;

/// <summary>
/// Kod listesinden ve/veya Id listesinden ürünleri toplu çözer (kampanya manuel ürün
/// kapsamı gibi "yapıştır/dosyadan yükle" akışları için). Kod eşleşmesi büyük/küçük
/// harf duyarsızdır; eşleşmeyen kodlar NotFoundCodes ile geri döner.
/// </summary>
public record LookupProductsQuery(List<string>? Codes, List<Guid>? Ids)
    : IRequest<Result<LookupProductsResult>>;

public record ProductLookupDto(Guid Id, string Code, Dictionary<string, string> NameI18n);

public record LookupProductsResult(List<ProductLookupDto> Items, List<string> NotFoundCodes);

public class LookupProductsQueryHandler(ICatalogDbContext db)
    : IRequestHandler<LookupProductsQuery, Result<LookupProductsResult>>
{
    private const int MaxInput = 5000;

    public async Task<Result<LookupProductsResult>> Handle(LookupProductsQuery request, CancellationToken ct)
    {
        var codes = (request.Codes ?? [])
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var ids = (request.Ids ?? []).Distinct().ToList();

        if (codes.Count + ids.Count > MaxInput)
            return Result.Failure<LookupProductsResult>($"Tek istekte en fazla {MaxInput} kod/Id çözülebilir.");

        if (codes.Count == 0 && ids.Count == 0)
            return Result.Success(new LookupProductsResult([], []));

        var loweredCodes = codes.Select(c => c.ToLower()).ToList();
        var items = await db.Products.AsNoTracking()
            .Where(p => loweredCodes.Contains(p.Code.ToLower()) || ids.Contains(p.Id))
            .Select(p => new ProductLookupDto(p.Id, p.Code, p.NameI18n))
            .ToListAsync(ct);

        var foundCodes = items.Select(i => i.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notFound = codes.Where(c => !foundCodes.Contains(c)).ToList();

        return Result.Success(new LookupProductsResult(items, notFound));
    }
}
