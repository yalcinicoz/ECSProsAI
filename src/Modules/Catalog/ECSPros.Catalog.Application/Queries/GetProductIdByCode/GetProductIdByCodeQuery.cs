using ECSPros.Catalog.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Catalog.Application.Queries.GetProductIdByCode;

/// <summary>
/// Ürün kodundan Id çözer — satış durumundan (IsSaleOpen) BAĞIMSIZ. Satışa kapalı ürünün
/// detay yerine kategorisine 301 yönlendirmesi için kullanılır (kapalı ürün detay sorgusundan
/// düşer ama kategori zinciri için Id gerekir). Bulunamazsa null.
/// </summary>
public record GetProductIdByCodeQuery(string Code) : IRequest<Result<Guid?>>;

public class GetProductIdByCodeQueryHandler(ICatalogDbContext db)
    : IRequestHandler<GetProductIdByCodeQuery, Result<Guid?>>
{
    public async Task<Result<Guid?>> Handle(GetProductIdByCodeQuery request, CancellationToken ct)
    {
        var id = await db.Products.AsNoTracking()
            .Where(p => p.Code == request.Code)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        return Result.Success(id);
    }
}
