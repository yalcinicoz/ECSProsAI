using ECSPros.Core.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Queries.GetPaymentMethods;

public class GetPaymentMethodsQueryHandler : IRequestHandler<GetPaymentMethodsQuery, Result<List<PaymentMethodDto>>>
{
    private readonly ICoreDbContext _context;

    public GetPaymentMethodsQueryHandler(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PaymentMethodDto>>> Handle(GetPaymentMethodsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PaymentMethods.AsQueryable();
        if (request.ActiveOnly)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new PaymentMethodDto(x.Id, x.Code, x.NameI18n, x.IsOnline, x.RequiresConfirmation, x.IsActive, x.SortOrder))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
