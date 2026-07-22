using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetMemberMonthlySpend;

/// <summary>
/// H10 sade statü bloğu: üyenin verilen tarihten bu yana sipariş toplamı (iptaller hariç).
/// GrandTotal DB tarafında toplanır — sipariş listesi yüklenmez.
/// </summary>
public record GetMemberMonthlySpendQuery(Guid MemberId, DateTime Since) : IRequest<Result<decimal>>;

public class GetMemberMonthlySpendQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetMemberMonthlySpendQuery, Result<decimal>>
{
    public async Task<Result<decimal>> Handle(GetMemberMonthlySpendQuery request, CancellationToken ct)
    {
        var toplam = await db.Orders
            .AsNoTracking()
            .Where(o => o.MemberId == request.MemberId
                     && o.Status != "cancelled"
                     && o.CreatedAt >= request.Since)
            .SumAsync(o => (decimal?)o.GrandTotal, ct) ?? 0m;

        return Result.Success(toplam);
    }
}
