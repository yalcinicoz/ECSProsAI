using ECSPros.Finance.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Queries.GetSupplierDetail;

public class GetSupplierDetailQueryHandler : IRequestHandler<GetSupplierDetailQuery, Result<SupplierDetailDto>>
{
    private readonly IFinanceDbContext _context;

    public GetSupplierDetailQueryHandler(IFinanceDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SupplierDetailDto>> Handle(GetSupplierDetailQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);

        if (supplier is null)
            return Result.Failure<SupplierDetailDto>("Tedarikçi bulunamadı.");

        return Result.Success(new SupplierDetailDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.TaxOffice,
            supplier.TaxNumber,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.ContactPerson,
            supplier.Notes,
            supplier.IsActive,
            supplier.CreatedAt));
    }
}
