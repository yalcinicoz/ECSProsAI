using ECSPros.Finance.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Commands.UpdateSupplier;

public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, Result<bool>>
{
    private readonly IFinanceDbContext _context;

    public UpdateSupplierCommandHandler(IFinanceDbContext context)
    {
        _context = context;
    }

    public async Task<Result<bool>> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (supplier is null)
            return Result.Failure<bool>("Tedarikçi bulunamadı.");

        supplier.Name = request.Name;
        supplier.TaxOffice = request.TaxOffice;
        supplier.TaxNumber = request.TaxNumber;
        supplier.Phone = request.Phone;
        supplier.Email = request.Email;
        supplier.Address = request.Address;
        supplier.ContactPerson = request.ContactPerson;
        supplier.Notes = request.Notes;
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = request.UpdatedBy;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
