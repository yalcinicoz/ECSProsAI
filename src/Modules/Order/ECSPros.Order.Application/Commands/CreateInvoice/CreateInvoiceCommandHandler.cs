using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateInvoice;

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Result<Guid>>
{
    private readonly IOrderDbContext _context;

    public CreateInvoiceCommandHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        var series = await _context.InvoiceSeries
            .FirstOrDefaultAsync(s => s.Id == request.InvoiceSeriesId && s.IsActive, cancellationToken);

        if (series is null)
            return Result.Failure<Guid>("Aktif fatura serisi bulunamadı.");

        var year = request.InvoiceDate.Year.ToString();
        var serial = request.InvoiceType switch
        {
            "e_archive" => series.EArchiveSerial,
            "e_invoice"  => series.EInvoiceSerial,
            "export"     => series.ExportSerial,
            _            => series.EArchiveSerial
        };

        // Seri için son sıra numarasını bul
        var lastSequence = await _context.Invoices
            .Where(i => i.InvoiceSeriesId == request.InvoiceSeriesId
                        && i.InvoiceYear == year
                        && i.InvoiceType == request.InvoiceType)
            .MaxAsync(i => (int?)i.InvoiceSequence, cancellationToken) ?? 0;

        var sequence = lastSequence + 1;
        var invoiceNumber = $"{serial}{year}{sequence:D9}";

        // Sipariş kalemlerinden fatura kalemleri oluştur
        var items = order.Items.Select(i => new InvoiceItem
        {
            OrderItemId = i.Id,
            Description = $"{i.ProductName} — {i.VariantInfo}",
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            DiscountAmount = i.DiscountAmount,
            TaxRate = 0,
            TaxAmount = i.TaxAmount,
            Total = i.Total
        }).ToList();

        var invoice = new Invoice
        {
            OrderId = request.OrderId,
            InvoiceSeriesId = request.InvoiceSeriesId,
            InvoiceType = request.InvoiceType,
            InvoiceSerial = serial,
            InvoiceYear = year,
            InvoiceSequence = sequence,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = request.InvoiceDate,
            RecipientName = request.RecipientName,
            RecipientTaxOffice = request.RecipientTaxOffice,
            RecipientTaxNumber = request.RecipientTaxNumber,
            RecipientCompanyName = request.RecipientCompanyName,
            RecipientAddress = request.RecipientAddress,
            Subtotal = order.Subtotal,
            TotalDiscount = order.TotalDiscount,
            TotalTax = order.TotalTax,
            GrandTotal = order.GrandTotal,
            IntegratorStatus = "pending",
            ErpStatus = "pending",
            Status = "created",
            CreatedBy = request.CreatedBy
        };

        foreach (var item in items)
            invoice.Items.Add(item);

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(invoice.Id);
    }
}
