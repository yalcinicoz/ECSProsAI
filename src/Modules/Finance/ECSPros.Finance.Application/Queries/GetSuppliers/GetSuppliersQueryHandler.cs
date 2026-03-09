using ECSPros.Finance.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Queries.GetSuppliers;

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, Result<List<SupplierDto>>>
{
    private readonly IFinanceDbContext _context;

    public GetSuppliersQueryHandler(IFinanceDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SupplierDto>>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Suppliers.AsQueryable();

        if (request.ActiveOnly)
            query = query.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(s) || x.Code.ToLower().Contains(s));
        }

        var items = await query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(s.Id, s.Code, s.Name, s.TaxNumber, s.Phone, s.Email, s.ContactPerson, s.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
