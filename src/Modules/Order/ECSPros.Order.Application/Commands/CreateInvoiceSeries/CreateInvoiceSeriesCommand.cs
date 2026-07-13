using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;

namespace ECSPros.Order.Application.Commands.CreateInvoiceSeries;

// P1d: panelden fatura serisi tanımı (fatura numaralandırması seriden türetilir)
public record CreateInvoiceSeriesCommand(
    Guid FirmId,
    string? Name,
    string EArchiveSerial,
    string EInvoiceSerial,
    string ExportSerial,
    Guid CreatedBy) : IRequest<Result<Guid>>;

public class CreateInvoiceSeriesCommandHandler(IOrderDbContext db)
    : IRequestHandler<CreateInvoiceSeriesCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateInvoiceSeriesCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.EArchiveSerial))
            return Result.Failure<Guid>("e-Arşiv seri kodu zorunludur.");

        var series = new InvoiceSeries
        {
            FirmId = request.FirmId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            EArchiveSerial = request.EArchiveSerial.Trim().ToUpperInvariant(),
            EInvoiceSerial = (string.IsNullOrWhiteSpace(request.EInvoiceSerial)
                ? request.EArchiveSerial : request.EInvoiceSerial).Trim().ToUpperInvariant(),
            ExportSerial = (string.IsNullOrWhiteSpace(request.ExportSerial)
                ? request.EArchiveSerial : request.ExportSerial).Trim().ToUpperInvariant(),
            IsActive = true,
            CreatedBy = request.CreatedBy
        };

        db.InvoiceSeries.Add(series);
        await db.SaveChangesAsync(ct);
        return Result.Success(series.Id);
    }
}
