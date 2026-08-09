using ECSPros.Order.Application.Commands.CreateInvoice;
using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreatePackageInvoiceAuto;

/// <summary>
/// OP2 (K-11): paket kapanışında OTOMATİK fatura — seri, kanalın firmasının aktif fatura
/// serisinden; alıcı bilgisi siparişin fatura (yoksa teslimat) alanlarından. Mevcut
/// CreateInvoiceCommand'a delege eder (paket bazlı tutar hesabı orada). Seri tanımlı
/// değilse anlaşılır hata döner (örn. eldi firması serisi girilene dek).
/// </summary>
public record CreatePackageInvoiceAutoCommand(
    Guid OrderId,
    Guid PackageId,
    Guid CreatedBy) : IRequest<Result<AutoInvoiceDto>>;

public record AutoInvoiceDto(Guid InvoiceId, string InvoiceNumber);

public class CreatePackageInvoiceAutoCommandHandler(
    IOrderDbContext db,
    IFirmResolver firmResolver,
    ISender sender)
    : IRequestHandler<CreatePackageInvoiceAutoCommand, Result<AutoInvoiceDto>>
{
    public async Task<Result<AutoInvoiceDto>> Handle(CreatePackageInvoiceAutoCommand request, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Result.Failure<AutoInvoiceDto>("Sipariş bulunamadı.");

        var firmId = await firmResolver.GetFirmIdAsync(order.FirmPlatformId, ct);
        if (firmId is null) return Result.Failure<AutoInvoiceDto>("Kanalın firması çözülemedi.");

        var seri = await db.InvoiceSeries.AsNoTracking()
            .Where(s => s.FirmId == firmId && s.IsActive)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (seri is null)
            return Result.Failure<AutoInvoiceDto>("Firma için aktif fatura serisi tanımlı değil — Ayarlar'dan seri girin.");

        // Kurumsal fatura bilgisi girilmişse e-fatura alanlarıyla, değilse e-arşiv
        var kurumsal = !string.IsNullOrWhiteSpace(order.BillingTaxNumber);
        var sonuc = await sender.Send(new CreateInvoiceCommand(
            OrderId: request.OrderId,
            InvoiceSeriesId: seri.Id,
            InvoiceType: "e_archive",
            InvoiceDate: DateTime.UtcNow,
            RecipientName: order.BillingRecipientName is { Length: > 0 } b ? b : order.ShippingRecipientName,
            RecipientAddress: order.BillingAddressLine is { Length: > 0 } ba ? ba : order.ShippingAddressLine,
            RecipientTaxOffice: kurumsal ? order.BillingTaxOffice : null,
            RecipientTaxNumber: kurumsal ? order.BillingTaxNumber : null,
            RecipientCompanyName: kurumsal ? order.BillingCompanyName : null,
            CreatedBy: request.CreatedBy,
            PackageId: request.PackageId), ct);
        if (sonuc.IsFailure) return Result.Failure<AutoInvoiceDto>(sonuc.Error!);

        var no = await db.Invoices.AsNoTracking()
            .Where(i => i.Id == sonuc.Value)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct);
        return Result.Success(new AutoInvoiceDto(sonuc.Value!, no ?? ""));
    }
}
