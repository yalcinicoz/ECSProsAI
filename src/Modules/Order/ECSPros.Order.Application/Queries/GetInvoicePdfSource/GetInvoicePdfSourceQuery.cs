using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoicePdfSource;

/// <summary>
/// Admin "Görüntüle" (2026-09-11): faturanın entegratör PDF adresi + kanalı (Y3 kanal kapsamı kontrolü controller'da).
/// Üye tarafındaki GetMemberInvoicePdfSource'un sahiplik şartsız, yetkili panel karşılığı; URL yine yalnız sunucuda
/// kullanılır (proxy), iptal faturanın PDF'i de gösterilir (belge geçmişi).
/// </summary>
public record GetInvoicePdfSourceQuery(Guid InvoiceId) : IRequest<Result<InvoicePdfSourceDto>>;

public record InvoicePdfSourceDto(string Url, Guid FirmPlatformId);

public class GetInvoicePdfSourceQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetInvoicePdfSourceQuery, Result<InvoicePdfSourceDto>>
{
    public async Task<Result<InvoicePdfSourceDto>> Handle(GetInvoicePdfSourceQuery request, CancellationToken ct)
    {
        var kayit = await db.Invoices.AsNoTracking()
            .Where(i => i.Id == request.InvoiceId)
            .Select(i => new { i.IntegratorInvoiceUrl, i.Order.FirmPlatformId })
            .FirstOrDefaultAsync(ct);
        if (kayit is null) return Result.Failure<InvoicePdfSourceDto>("Fatura bulunamadı.");
        if (string.IsNullOrWhiteSpace(kayit.IntegratorInvoiceUrl))
            return Result.Failure<InvoicePdfSourceDto>("Faturanın entegratör PDF adresi yok.");
        return Result.Success(new InvoicePdfSourceDto(kayit.IntegratorInvoiceUrl, kayit.FirmPlatformId));
    }
}
