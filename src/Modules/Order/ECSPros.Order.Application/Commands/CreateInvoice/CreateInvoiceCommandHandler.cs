using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreateInvoice;

/// <summary>
/// Bizim serimizden fatura kesimi (NumberSource=internal). FE0: seri tipi istekle uyuşmalı; numara,
/// seri×yıl sayacından atomik tahsis edilir ve fatura satırıyla AYNI transaction'da yazılır
/// (fatura yazılamazsa sayaç geri alınır — boşluk oluşmaz). Tarih-sıra kuralı FE1/K3.
/// </summary>
public class CreateInvoiceCommandHandler(
    IOrderDbContext context,
    IInvoiceNumberService numberService)
    : IRequestHandler<CreateInvoiceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
            return Result.Failure<Guid>("Sipariş bulunamadı.");

        if (!InvoiceTypes.IsValid(request.InvoiceType))
            return Result.Failure<Guid>("Fatura tipi e_archive, e_invoice veya export olmalıdır.");

        var series = await context.InvoiceSeries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.InvoiceSeriesId, cancellationToken);
        if (series is null)
            return Result.Failure<Guid>("Fatura serisi bulunamadı.");
        if (!series.IsActive)
            return Result.Failure<Guid>($"{series.Serial} serisi pasif; pasif seriden fatura kesilemez.");
        if (series.InvoiceType != request.InvoiceType)
            return Result.Failure<Guid>(
                $"Seri tipi uyuşmuyor: {series.Serial} {InvoiceTypes.Label(series.InvoiceType)} serisidir, {InvoiceTypes.Label(request.InvoiceType)} faturası kesilemez.");

        var invoiceDateUtc = request.InvoiceDate.Kind switch
        {
            DateTimeKind.Utc => request.InvoiceDate,
            DateTimeKind.Local => request.InvoiceDate.ToUniversalTime(),
            _ => DateTime.SpecifyKind(request.InvoiceDate, DateTimeKind.Utc)
        };

        // FE1: tarih-sıra kuralı (gelecek tarih / geriye tarih / eski yıl yasak)
        var counters = await context.InvoiceSeriesCounters.AsNoTracking()
            .Where(c => c.InvoiceSeriesId == series.Id)
            .Select(c => new { c.Year, c.LastInvoiceDate })
            .ToListAsync(cancellationToken);
        var dateError = InvoiceDateRules.Validate(
            invoiceDateUtc, counters.Select(c => (c.Year, c.LastInvoiceDate)).ToList(), DateTime.UtcNow);
        if (dateError is not null) return Result.Failure<Guid>(dateError);

        var sendMethod = await context.ChannelInvoiceSettings.AsNoTracking()
            .Where(s => s.FirmPlatformId == order.FirmPlatformId)
            .Select(s => s.SendMethod)
            .FirstOrDefaultAsync(cancellationToken) ?? InvoiceSendMethods.Manual;

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

        await using var tx = await context.BeginTransactionAsync(cancellationToken);
        var no = await numberService.AllocateAsync(series.Id, series.Serial, invoiceDateUtc, cancellationToken);

        var invoice = new Invoice
        {
            OrderId = request.OrderId,
            PackageId = request.PackageId,
            InvoiceSeriesId = series.Id,
            InvoiceType = request.InvoiceType,
            InvoiceSerial = no.Serial,
            InvoiceYear = no.Year,
            InvoiceSequence = no.Sequence,
            InvoiceNumber = no.Number,
            InvoiceDate = invoiceDateUtc,
            NumberSource = InvoiceNumberSources.Internal,
            SendMethod = sendMethod,
            IntegrationContractId = series.IntegrationContractId,
            RecipientName = request.RecipientName,
            RecipientTaxOffice = request.RecipientTaxOffice,
            RecipientTaxNumber = request.RecipientTaxNumber,
            RecipientCompanyName = request.RecipientCompanyName,
            RecipientAddress = request.RecipientAddress,
            Subtotal = order.Subtotal,
            TotalDiscount = order.TotalDiscount,
            TotalTax = order.TotalTax,
            GrandTotal = order.GrandTotal,
            IntegratorStatus = sendMethod == InvoiceSendMethods.IntegratorApi ? "pending" : "not_applicable",
            ErpStatus = sendMethod == InvoiceSendMethods.Erp ? "pending" : "not_applicable",
            Status = "created",
            CreatedBy = request.CreatedBy
        };
        foreach (var item in items) invoice.Items.Add(item);

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Success(invoice.Id);
    }
}
