using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.DeactivateInvoiceSeries;

/// <summary>
/// Seri pasifleştirme (plan §0.4): seriyi kullanan kanal bağı varsa yerine geçecek seri ZORUNLU;
/// yerine geçecek seri aktif, aynı firma ve AYNI TİPTE olmalı. Bağlar tek SaveChanges'te taşınır.
/// </summary>
public record DeactivateInvoiceSeriesCommand(Guid Id, Guid? ReplacementSeriesId, Guid UserId)
    : IRequest<Result<DeactivateInvoiceSeriesResult>>;

public record DeactivateInvoiceSeriesResult(int ReboundChannels);

public class DeactivateInvoiceSeriesCommandHandler(IOrderDbContext db)
    : IRequestHandler<DeactivateInvoiceSeriesCommand, Result<DeactivateInvoiceSeriesResult>>
{
    public async Task<Result<DeactivateInvoiceSeriesResult>> Handle(DeactivateInvoiceSeriesCommand request, CancellationToken ct)
    {
        var series = await db.InvoiceSeries.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (series is null) return Result.Failure<DeactivateInvoiceSeriesResult>("Fatura serisi bulunamadı.");
        if (!series.IsActive) return Result.Failure<DeactivateInvoiceSeriesResult>("Seri zaten pasif.");

        var bindings = await db.ChannelInvoiceSeriesBindings
            .Where(b => b.InvoiceSeriesId == series.Id)
            .ToListAsync(ct);

        var rebound = 0;
        if (bindings.Count > 0)
        {
            if (request.ReplacementSeriesId is null)
                return Result.Failure<DeactivateInvoiceSeriesResult>(
                    $"{series.Serial} serisi {bindings.Count} satış kanalında kullanılıyor; pasife almak için yerine geçecek seri seçin.");

            var replacement = await db.InvoiceSeries.FirstOrDefaultAsync(s => s.Id == request.ReplacementSeriesId.Value, ct);
            if (replacement is null) return Result.Failure<DeactivateInvoiceSeriesResult>("Yerine geçecek seri bulunamadı.");
            if (replacement.Id == series.Id) return Result.Failure<DeactivateInvoiceSeriesResult>("Yerine geçecek seri pasife alınan serinin kendisi olamaz.");
            if (!replacement.IsActive) return Result.Failure<DeactivateInvoiceSeriesResult>("Yerine geçecek seri pasif; önce aktifleştirin.");
            if (replacement.FirmId != series.FirmId) return Result.Failure<DeactivateInvoiceSeriesResult>("Yerine geçecek seri aynı firmaya ait olmalıdır.");
            if (replacement.InvoiceType != series.InvoiceType)
                return Result.Failure<DeactivateInvoiceSeriesResult>(
                    $"Yerine geçecek seri aynı tipte olmalıdır: {series.Serial} {InvoiceTypes.Label(series.InvoiceType)} serisidir, {replacement.Serial} ise {InvoiceTypes.Label(replacement.InvoiceType)}.");

            foreach (var b in bindings)
            {
                b.InvoiceSeriesId = replacement.Id;
                b.UpdatedAt = DateTime.UtcNow;
                b.UpdatedBy = request.UserId;
            }
            rebound = bindings.Count;
        }

        series.IsActive = false;
        series.RetiredAt = DateTime.UtcNow;
        series.UpdatedAt = DateTime.UtcNow;
        series.UpdatedBy = request.UserId;
        await db.SaveChangesAsync(ct);
        return Result.Success(new DeactivateInvoiceSeriesResult(rebound));
    }
}
