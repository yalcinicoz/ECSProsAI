using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoiceSeries;

// P1d: fatura serileri — fatura oluşturma formunun seri seçicisi + seri yönetimi
public record GetInvoiceSeriesQuery(bool ActiveOnly = true) : IRequest<Result<List<InvoiceSeriesDto>>>;

public record InvoiceSeriesDto(
    Guid Id,
    Guid FirmId,
    string? Name,
    string EArchiveSerial,
    string EInvoiceSerial,
    string ExportSerial,
    bool IsActive);

public class GetInvoiceSeriesQueryHandler(IOrderDbContext db)
    : IRequestHandler<GetInvoiceSeriesQuery, Result<List<InvoiceSeriesDto>>>
{
    public async Task<Result<List<InvoiceSeriesDto>>> Handle(GetInvoiceSeriesQuery request, CancellationToken ct)
    {
        var query = db.InvoiceSeries.AsNoTracking();
        if (request.ActiveOnly) query = query.Where(s => s.IsActive);

        return Result.Success(await query
            .OrderBy(s => s.Name)
            .Select(s => new InvoiceSeriesDto(
                s.Id, s.FirmId, s.Name, s.EArchiveSerial, s.EInvoiceSerial, s.ExportSerial, s.IsActive))
            .ToListAsync(ct));
    }
}
