using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetMemberInvoicePdfSource;

public class GetMemberInvoicePdfSourceQueryHandler
    : IRequestHandler<GetMemberInvoicePdfSourceQuery, Result<string>>
{
    private readonly IOrderDbContext _context;

    public GetMemberInvoicePdfSourceQueryHandler(IOrderDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(
        GetMemberInvoicePdfSourceQuery request, CancellationToken cancellationToken)
    {
        var kayit = await _context.Invoices
            .Where(i => i.Id == request.InvoiceId
                        && i.OrderId == request.OrderId
                        && i.Status != "cancelled"
                        && i.Order.MemberId == request.MemberId)
            .Select(i => new { i.IntegratorInvoiceUrl })
            .FirstOrDefaultAsync(cancellationToken);

        // Sahiplik tutmayan istek de "bulunamadı"dır — başka üyenin faturasının varlığı sızmaz.
        if (kayit is null)
            return Result.Failure<string>("Fatura bulunamadı.");

        if (string.IsNullOrWhiteSpace(kayit.IntegratorInvoiceUrl))
            return Result.Failure<string>("Faturanın PDF adresi henüz oluşmamış.");

        return Result.Success(kayit.IntegratorInvoiceUrl);
    }
}
