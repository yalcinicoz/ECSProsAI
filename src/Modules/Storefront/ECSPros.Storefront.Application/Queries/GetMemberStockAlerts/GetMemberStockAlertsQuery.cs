using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetMemberStockAlerts;

/// <summary>
/// C9: üyenin aktif stok haber kayıtları — sepet sayfası tükendi satırlarındaki
/// butonun "haber verilecek" durumunu ilk yüklemede işaretlemek için.
/// VariantIds verilirse yalnız o varyantlarla kesişim döner.
/// </summary>
public record GetMemberStockAlertsQuery(
    Guid FirmPlatformId,
    Guid MemberId,
    List<Guid>? VariantIds = null) : IRequest<Result<List<Guid>>>;

public class GetMemberStockAlertsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetMemberStockAlertsQuery, Result<List<Guid>>>
{
    public async Task<Result<List<Guid>>> Handle(GetMemberStockAlertsQuery request, CancellationToken ct)
    {
        var sorgu = db.StockAlerts.Where(a =>
            a.FirmPlatformId == request.FirmPlatformId
            && a.MemberId == request.MemberId
            && a.Status == "active");

        if (request.VariantIds is { Count: > 0 })
            sorgu = sorgu.Where(a => request.VariantIds.Contains(a.VariantId));

        var liste = await sorgu.Select(a => a.VariantId).Distinct().ToListAsync(ct);
        return Result.Success(liste);
    }
}
