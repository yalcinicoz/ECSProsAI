using ECSPros.Finance.Application.Services;
using ECSPros.Finance.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Commands.CreateSupplier;

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Result<Guid>>
{
    private readonly IFinanceDbContext _context;

    public CreateSupplierCommandHandler(IFinanceDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Suppliers.AnyAsync(s => s.Code == request.Code, cancellationToken);
        if (exists)
            return Result.Failure<Guid>($"'{request.Code}' tedarikçi kodu zaten mevcut.");

        var supplier = new Supplier
        {
            Code = request.Code,
            Name = request.Name,
            TaxOffice = request.TaxOffice,
            TaxNumber = request.TaxNumber,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            ContactPerson = request.ContactPerson,
            Notes = request.Notes,
            IsActive = true
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(supplier.Id);
    }
}
