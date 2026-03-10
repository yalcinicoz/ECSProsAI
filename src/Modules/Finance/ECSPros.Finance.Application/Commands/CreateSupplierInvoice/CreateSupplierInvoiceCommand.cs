using ECSPros.Finance.Application.Services;
using ECSPros.Finance.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Commands.CreateSupplierInvoice;

public record CreateSupplierInvoiceCommand(
    Guid SupplierId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string? Notes,
    List<CreateSupplierInvoiceItemDto> Items
) : IRequest<Result<Guid>>;

public record CreateSupplierInvoiceItemDto(
    Guid? VariantId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountRate,
    decimal TaxRate);

public class CreateSupplierInvoiceCommandHandler : IRequestHandler<CreateSupplierInvoiceCommand, Result<Guid>>
{
    private readonly IFinanceDbContext _db;

    public CreateSupplierInvoiceCommandHandler(IFinanceDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateSupplierInvoiceCommand request, CancellationToken ct)
    {
        var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId, ct);
        if (!supplierExists)
            return Result.Failure<Guid>("Tedarikçi bulunamadı.");

        if (!request.Items.Any())
            return Result.Failure<Guid>("Fatura için en az bir kalem eklenmelidir.");

        decimal subtotal = 0, totalDiscount = 0, totalTax = 0;
        var invoiceItems = new List<SupplierInvoiceItem>();

        foreach (var item in request.Items)
        {
            var lineTotal = item.Quantity * item.UnitPrice;
            var discountAmount = lineTotal * item.DiscountRate / 100;
            var netAmount = lineTotal - discountAmount;
            var taxAmount = netAmount * item.TaxRate / 100;
            var total = netAmount + taxAmount;

            subtotal += lineTotal;
            totalDiscount += discountAmount;
            totalTax += taxAmount;

            invoiceItems.Add(new SupplierInvoiceItem
            {
                Id = Guid.NewGuid(),
                VariantId = item.VariantId,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountRate = item.DiscountRate,
                DiscountAmount = discountAmount,
                TaxRate = item.TaxRate,
                TaxAmount = taxAmount,
                Total = total,
                CreatedAt = DateTime.UtcNow
            });
        }

        var invoice = new SupplierInvoice
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            InvoiceNumber = request.InvoiceNumber,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            Subtotal = subtotal,
            TotalDiscount = totalDiscount,
            TotalTax = totalTax,
            GrandTotal = subtotal - totalDiscount + totalTax,
            Status = "draft",
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in invoiceItems)
            item.InvoiceId = invoice.Id;

        _db.SupplierInvoices.Add(invoice);
        _db.SupplierInvoiceItems.AddRange(invoiceItems);
        await _db.SaveChangesAsync(ct);

        return Result.Success(invoice.Id);
    }
}
