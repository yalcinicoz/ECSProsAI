using ECSPros.Finance.Application.Services;
using ECSPros.Finance.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Commands.CreateSupplierReturn;

public record CreateSupplierReturnCommand(
    Guid SupplierId,
    DateOnly ReturnDate,
    string Reason,
    string? Notes,
    List<CreateSupplierReturnItemDto> Items
) : IRequest<Result<Guid>>;

public record CreateSupplierReturnItemDto(
    Guid VariantId,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate);

public class CreateSupplierReturnCommandHandler : IRequestHandler<CreateSupplierReturnCommand, Result<Guid>>
{
    private readonly IFinanceDbContext _db;

    public CreateSupplierReturnCommandHandler(IFinanceDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateSupplierReturnCommand request, CancellationToken ct)
    {
        var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct);
        if (!supplierExists)
            return Result.Failure<Guid>("Tedarikçi bulunamadı.");

        if (!request.Items.Any())
            return Result.Failure<Guid>("İade için en az bir kalem eklenmelidir.");

        var count = await _db.SupplierReturns.CountAsync(ct);
        var returnNumber = $"SR-{DateTime.UtcNow:yyyyMMdd}-{(count + 1):D4}";

        decimal subtotal = 0, totalTax = 0;
        var returnItems = new List<SupplierReturnItem>();

        foreach (var item in request.Items)
        {
            var lineTotal = item.Quantity * item.UnitPrice;
            var taxAmount = lineTotal * item.TaxRate / 100;
            subtotal += lineTotal;
            totalTax += taxAmount;

            returnItems.Add(new SupplierReturnItem
            {
                Id = Guid.NewGuid(),
                VariantId = item.VariantId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxRate = item.TaxRate,
                TaxAmount = taxAmount,
                Total = lineTotal + taxAmount,
                CreatedAt = DateTime.UtcNow
            });
        }

        var supplierReturn = new SupplierReturn
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            ReturnNumber = returnNumber,
            ReturnDate = request.ReturnDate,
            Reason = request.Reason,
            Notes = request.Notes,
            Status = "draft",
            Subtotal = subtotal,
            TotalTax = totalTax,
            GrandTotal = subtotal + totalTax,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in returnItems)
            item.ReturnId = supplierReturn.Id;

        _db.SupplierReturns.Add(supplierReturn);
        _db.SupplierReturnItems.AddRange(returnItems);
        await _db.SaveChangesAsync(ct);

        return Result.Success(supplierReturn.Id);
    }
}
