using System.Text.RegularExpressions;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.RegisterExternalInvoice;

/// <summary>
/// FE1 §2.4: dışarıda (ERP / pazaryeri / entegratör) üretilmiş numaralı faturayı KAYIT olarak alır.
/// Seri yoktur, sayaç ilerlemez; numara kaynağı ve dış belge kimliği saklanır. İdempotent anahtar
/// (ExternalSource, InvoiceNumber): aynı kayıt ikinci kez gelirse mevcut Id döner (AlreadyExisted=true).
/// Elle giriş (panel), ERP geri yazımı (E7) ve pazaryeri adaptörü (FE7) aynı komutu kullanır.
/// </summary>
public record RegisterExternalInvoiceCommand(
    Guid OrderId,
    Guid? PackageId,
    string NumberSource,        // erp | marketplace | integrator
    string ExternalSource,      // kaynak kodu: nebim, trendyol, uyumsoft …
    string InvoiceNumber,
    string InvoiceType,
    DateTime InvoiceDate,
    Guid? Ettn,
    string? ExternalDocumentId,
    string? RecipientName,
    string? RecipientAddress,
    string? RecipientTaxOffice,
    string? RecipientTaxNumber,
    string? RecipientCompanyName,
    string? IntegratorInvoiceUrl,
    Guid CreatedBy) : IRequest<Result<RegisterExternalInvoiceResult>>;

public record RegisterExternalInvoiceResult(Guid InvoiceId, bool AlreadyExisted);

public partial class RegisterExternalInvoiceCommandHandler(IOrderDbContext db)
    : IRequestHandler<RegisterExternalInvoiceCommand, Result<RegisterExternalInvoiceResult>>
{
    [GeneratedRegex(@"^([A-Z]{3})(\d{4})(\d{9})$")]
    private static partial Regex GibNumber();

    public async Task<Result<RegisterExternalInvoiceResult>> Handle(RegisterExternalInvoiceCommand request, CancellationToken ct)
    {
        if (!InvoiceNumberSources.IsValid(request.NumberSource) || request.NumberSource == InvoiceNumberSources.Internal)
            return Result.Failure<RegisterExternalInvoiceResult>("Numara kaynağı erp, marketplace veya integrator olmalıdır.");
        if (!InvoiceTypes.IsValid(request.InvoiceType))
            return Result.Failure<RegisterExternalInvoiceResult>("Fatura tipi e_archive, e_invoice veya export olmalıdır.");
        var source = (request.ExternalSource ?? "").Trim().ToLowerInvariant();
        if (source.Length is 0 or > 50)
            return Result.Failure<RegisterExternalInvoiceResult>("Kaynak kodu zorunludur (ör. nebim, trendyol).");
        var number = (request.InvoiceNumber ?? "").Trim().ToUpperInvariant();
        if (number.Length is 0 or > 50)
            return Result.Failure<RegisterExternalInvoiceResult>("Fatura numarası zorunludur.");

        var existing = await db.Invoices.AsNoTracking()
            .Where(i => i.ExternalSource == source && i.InvoiceNumber == number)
            .Select(i => i.Id)
            .FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty)
            return Result.Success(new RegisterExternalInvoiceResult(existing, true));

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure<RegisterExternalInvoiceResult>("Sipariş bulunamadı.");

        var invoiceDateUtc = request.InvoiceDate.Kind switch
        {
            DateTimeKind.Utc => request.InvoiceDate,
            DateTimeKind.Local => request.InvoiceDate.ToUniversalTime(),
            _ => DateTime.SpecifyKind(request.InvoiceDate, DateTimeKind.Utc)
        };

        // Numara GİB kalıbındaysa parçala (raporlama için); değilse en iyi çaba — tekillik dış anahtarla sağlanır
        var m = GibNumber().Match(number);
        var serial = m.Success ? m.Groups[1].Value : number[..Math.Min(3, number.Length)];
        var year = m.Success ? m.Groups[2].Value : invoiceDateUtc.Year.ToString();
        var sequence = m.Success ? int.Parse(m.Groups[3].Value) : 0;

        var sendMethod = await db.ChannelInvoiceSettings.AsNoTracking()
            .Where(s => s.FirmPlatformId == order.FirmPlatformId)
            .Select(s => s.SendMethod)
            .FirstOrDefaultAsync(ct) ?? InvoiceSendMethods.Manual;

        var kurumsal = !string.IsNullOrWhiteSpace(request.RecipientTaxNumber ?? order.BillingTaxNumber);
        var invoice = new Invoice
        {
            OrderId = order.Id,
            PackageId = request.PackageId,
            InvoiceSeriesId = null,
            InvoiceType = request.InvoiceType,
            InvoiceSerial = serial,
            InvoiceYear = year,
            InvoiceSequence = sequence,
            InvoiceNumber = number,
            InvoiceDate = invoiceDateUtc,
            NumberSource = request.NumberSource,
            ExternalSource = source,
            ExternalDocumentId = string.IsNullOrWhiteSpace(request.ExternalDocumentId) ? null : request.ExternalDocumentId.Trim(),
            Ettn = request.Ettn,
            SendMethod = sendMethod,
            RecipientName = request.RecipientName is { Length: > 0 } rn ? rn
                : (order.BillingRecipientName is { Length: > 0 } b ? b : order.ShippingRecipientName),
            RecipientAddress = request.RecipientAddress is { Length: > 0 } ra ? ra
                : (order.BillingAddressLine is { Length: > 0 } ba ? ba : order.ShippingAddressLine),
            RecipientTaxOffice = kurumsal ? (request.RecipientTaxOffice ?? order.BillingTaxOffice) : null,
            RecipientTaxNumber = kurumsal ? (request.RecipientTaxNumber ?? order.BillingTaxNumber) : null,
            RecipientCompanyName = kurumsal ? (request.RecipientCompanyName ?? order.BillingCompanyName) : null,
            IntegratorInvoiceUrl = string.IsNullOrWhiteSpace(request.IntegratorInvoiceUrl) ? null : request.IntegratorInvoiceUrl.Trim(),
            Subtotal = order.Subtotal,
            TotalDiscount = order.TotalDiscount,
            TotalTax = order.TotalTax,
            GrandTotal = order.GrandTotal,
            // Dış kaynak faturayı zaten kesmiş/göndermiştir: bizden gönderim beklenmez
            IntegratorStatus = request.NumberSource == InvoiceNumberSources.Integrator ? "accepted" : "not_applicable",
            ErpStatus = request.NumberSource == InvoiceNumberSources.Erp ? "acknowledged" : "not_applicable",
            Status = "created",
            CreatedBy = request.CreatedBy
        };
        foreach (var i in order.Items)
            invoice.Items.Add(new InvoiceItem
            {
                OrderItemId = i.Id,
                Description = $"{i.ProductName} — {i.VariantInfo}",
                Quantity = i.Quantity, UnitPrice = i.UnitPrice, DiscountAmount = i.DiscountAmount,
                TaxRate = 0, TaxAmount = i.TaxAmount, Total = i.Total
            });

        db.Invoices.Add(invoice);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Yarış: aynı (kaynak, numara) eşzamanlı geldi — mevcut kaydı döndür
            var raced = await db.Invoices.AsNoTracking()
                .Where(i => i.ExternalSource == source && i.InvoiceNumber == number)
                .Select(i => i.Id).FirstOrDefaultAsync(ct);
            if (raced != Guid.Empty) return Result.Success(new RegisterExternalInvoiceResult(raced, true));
            throw;
        }
        return Result.Success(new RegisterExternalInvoiceResult(invoice.Id, false));
    }
}
