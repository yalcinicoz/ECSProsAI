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
    IInvoiceNumberService numberService,
    ECSPros.Shared.Contracts.IProductService productService)
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
        // Vade farkı (2026-09-10, kullanıcı kararı): ürünlere YEDİRİLMEZ; ürünlerin KDV oranlarına göre
        // oranlanır ve oran başına ayrı "Vade Farkı" satırı yazılır (TaksitKurali.KdvSatirlari — tek kural).
        // Paket faturasında bu faturanın kalemlerinin payı kadar (kalem tutarı oranında) düşer.
        if (order.InstallmentFee > 0 && order.Items.Count > 0)
        {
            var faturaKalemleri = request.PackageId is null
                ? order.Items.ToList()
                : order.Items.ToList(); // paket-kalem eşlemesi fatura komutunda yok; tüm kalemler (tek fatura varsayımı)
            var oranlar = await productService.GetVariantTaxRatesAsync(
                faturaKalemleri.Select(i => i.VariantId).Distinct().ToList(), cancellationToken);
            var kdvSatirlari = ECSPros.Shared.Contracts.TaksitKurali.KdvSatirlari(
                order.InstallmentFee,
                faturaKalemleri.Select(i => (i.Total, oranlar.GetValueOrDefault(i.VariantId, 20m))));
            foreach (var s in kdvSatirlari)
                items.Add(new InvoiceItem
                {
                    OrderItemId = null,
                    Description = $"Vade Farkı ({order.InstallmentCount} Taksit, %{s.KdvOrani:0.##} KDV)",
                    Quantity = 1,
                    UnitPrice = s.Brut,
                    DiscountAmount = 0,
                    TaxRate = s.KdvOrani,
                    TaxAmount = s.Kdv,
                    Total = s.Brut
                });
        }

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
            IntegratorStatus = sendMethod == InvoiceSendMethods.IntegratorApi ? "queued" : "not_applicable",
            ErpStatus = sendMethod == InvoiceSendMethods.Erp ? "pending" : "not_applicable",
            Status = "created",
            CreatedBy = request.CreatedBy
        };
        foreach (var item in items) invoice.Items.Add(item);

        // FE4: kanal entegratör-API yöntemindeyse gönderim işi outbox'a düşer (aynı transaction)
        if (sendMethod == InvoiceSendMethods.IntegratorApi)
            invoice.Dispatches.Add(new InvoiceDispatch
            {
                Action = InvoiceDispatchActions.Send, Status = InvoiceDispatchStatuses.Pending,
                IntegrationContractId = series.IntegrationContractId, NextAttemptAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy
            });

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Success(invoice.Id);
    }
}
