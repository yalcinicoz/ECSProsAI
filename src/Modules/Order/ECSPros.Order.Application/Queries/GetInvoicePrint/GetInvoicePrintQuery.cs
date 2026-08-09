using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoicePrint;

/// <summary>OP2: fatura yazdırma verisi — fatura başlığı + sipariş kalemleri.
/// Paket faturasında yalnız paket kalemleri basılmalıdır; kalem elemesi API katmanında
/// paket içeriğiyle yapılır (OrderItemIds filtresi).</summary>
public record GetInvoicePrintQuery(Guid InvoiceId) : IRequest<Result<InvoicePrintDto>>;

public record InvoicePrintItemDto(
    Guid OrderItemId, string Sku, string ProductName, string VariantInfo,
    int Quantity, decimal UnitPrice, decimal DiscountAmount, decimal TaxAmount, decimal Total);

public record InvoicePrintDto(
    Guid InvoiceId,
    string InvoiceNumber,
    string InvoiceType,
    DateTime InvoiceDate,
    string OrderNumber,
    Guid OrderId,
    Guid? PackageId,
    string RecipientName,
    string RecipientAddress,
    string? RecipientTaxOffice,
    string? RecipientTaxNumber,
    string? RecipientCompanyName,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalTax,
    decimal GrandTotal,
    List<InvoicePrintItemDto> Items);

public class GetInvoicePrintQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetInvoicePrintQuery, Result<InvoicePrintDto>>
{
    public async Task<Result<InvoicePrintDto>> Handle(GetInvoicePrintQuery request, CancellationToken ct)
    {
        var fatura = await db.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (fatura is null) return Result.Failure<InvoicePrintDto>("Fatura bulunamadı.");

        var siparis = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == fatura.OrderId, ct);
        if (siparis is null) return Result.Failure<InvoicePrintDto>("Sipariş bulunamadı.");

        return Result.Success(new InvoicePrintDto(
            fatura.Id, fatura.InvoiceNumber, fatura.InvoiceType, fatura.InvoiceDate,
            siparis.OrderNumber, siparis.Id, fatura.PackageId,
            fatura.RecipientName, fatura.RecipientAddress,
            fatura.RecipientTaxOffice, fatura.RecipientTaxNumber, fatura.RecipientCompanyName,
            fatura.Subtotal, fatura.TotalDiscount, fatura.TotalTax, fatura.GrandTotal,
            siparis.Items.Select(i => new InvoicePrintItemDto(i.Id, i.Sku, i.ProductName,
                i.VariantInfo, i.Quantity, i.UnitPrice, i.DiscountAmount, i.TaxAmount, i.Total)).ToList()));
    }
}
