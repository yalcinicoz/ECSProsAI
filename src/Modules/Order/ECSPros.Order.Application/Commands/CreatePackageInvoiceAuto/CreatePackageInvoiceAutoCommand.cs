using ECSPros.Order.Application.Commands.CreateInvoice;
using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.CreatePackageInvoiceAuto;

/// <summary>
/// OP2 (K-11): paket kapanışında OTOMATİK fatura. FE0 (2026-09-06): seri artık firmanın "ilk aktif
/// serisi"nden değil, KANALIN e-arşiv yuvasına bağlı seriden alınır (plan §2.3; tip böylece kesin
/// eşleşir). Yuva boşsa anlaşılır hata döner — paket durur, panel kanal Faturalama ayarına yönlendirir.
/// Tip seçimi (e-fatura mükellef sorgusu) K5'e kadar e-arşiv; gönderim yöntemi FE4'e kadar okunmaz.
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

        var seri = await db.ChannelInvoiceSeriesBindings.AsNoTracking()
            .Where(b => b.FirmPlatformId == order.FirmPlatformId && b.InvoiceType == InvoiceTypes.EArchive)
            .Select(b => b.InvoiceSeries)
            .FirstOrDefaultAsync(ct);
        if (seri is null)
            return Result.Failure<AutoInvoiceDto>("Kanalın e-Arşiv fatura serisi bağlı değil — Ayarlar → Satış Kanalları → Faturalama'dan seri bağlayın.");
        if (!seri.IsActive)
            return Result.Failure<AutoInvoiceDto>($"Kanalın e-Arşiv serisi ({seri.Serial}) pasif — yerine aktif bir seri bağlayın.");

        // Kurumsal fatura bilgisi girilmişse e-fatura alanlarıyla, değilse e-arşiv
        var kurumsal = !string.IsNullOrWhiteSpace(order.BillingTaxNumber);
        var sonuc = await sender.Send(new CreateInvoiceCommand(
            OrderId: request.OrderId,
            InvoiceSeriesId: seri.Id,
            InvoiceType: InvoiceTypes.EArchive,
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
