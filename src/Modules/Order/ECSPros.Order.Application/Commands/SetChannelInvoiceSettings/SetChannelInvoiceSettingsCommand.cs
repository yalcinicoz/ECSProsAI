using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Commands.SetChannelInvoiceSettings;

/// <summary>
/// Kanal faturalama ayarı (plan §2.3): gönderim yöntemi + üç yuva. Her yuvaya yalnız kanalın
/// firmasına ait, aktif ve YUVAYLA AYNI TİPTEKİ seri bağlanabilir; null = bağ kaldırılır.
/// </summary>
public record SetChannelInvoiceSettingsCommand(
    Guid FirmPlatformId,
    string SendMethod,
    Guid? EArchiveSeriesId,
    Guid? EInvoiceSeriesId,
    Guid? ExportSeriesId,
    Guid UserId) : IRequest<Result<bool>>;

public class SetChannelInvoiceSettingsCommandHandler(IOrderDbContext db, IFirmResolver firmResolver)
    : IRequestHandler<SetChannelInvoiceSettingsCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(SetChannelInvoiceSettingsCommand request, CancellationToken ct)
    {
        var channel = await firmResolver.GetChannelAsync(request.FirmPlatformId, ct);
        if (channel is null) return Result.Failure<bool>("Satış kanalı bulunamadı.");
        if (!InvoiceSendMethods.IsValid(request.SendMethod))
            return Result.Failure<bool>("Gönderim yöntemi manual, integrator_api, erp veya marketplace olmalıdır.");

        var slots = new (string Type, Guid? SeriesId)[]
        {
            (InvoiceTypes.EArchive, request.EArchiveSeriesId),
            (InvoiceTypes.EInvoice, request.EInvoiceSeriesId),
            (InvoiceTypes.Export, request.ExportSeriesId),
        };

        var wantedIds = slots.Where(s => s.SeriesId is not null).Select(s => s.SeriesId!.Value).Distinct().ToList();
        var seriesMap = await db.InvoiceSeries.AsNoTracking()
            .Where(s => wantedIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        foreach (var (type, seriesId) in slots)
        {
            if (seriesId is null) continue;
            if (!seriesMap.TryGetValue(seriesId.Value, out var series))
                return Result.Failure<bool>($"{InvoiceTypes.Label(type)} yuvası için seçilen seri bulunamadı.");
            if (series.FirmId != channel.FirmId)
                return Result.Failure<bool>($"{series.Serial} serisi kanalın firmasına ait değil.");
            if (!series.IsActive)
                return Result.Failure<bool>($"{series.Serial} serisi pasif; pasif seri kanala bağlanamaz.");
            if (series.InvoiceType != type)
                return Result.Failure<bool>(
                    $"Seri tipi yuvayla uyuşmuyor: {series.Serial} {InvoiceTypes.Label(series.InvoiceType)} serisidir, {InvoiceTypes.Label(type)} yuvasına bağlanamaz.");
        }

        var settings = await db.ChannelInvoiceSettings.FirstOrDefaultAsync(s => s.FirmPlatformId == channel.Id, ct);
        if (settings is null)
        {
            settings = new ChannelInvoiceSettings { FirmPlatformId = channel.Id, CreatedBy = request.UserId };
            db.ChannelInvoiceSettings.Add(settings);
        }
        settings.SendMethod = request.SendMethod;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedBy = request.UserId;

        var bindings = await db.ChannelInvoiceSeriesBindings
            .Where(b => b.FirmPlatformId == channel.Id)
            .ToListAsync(ct);

        foreach (var (type, seriesId) in slots)
        {
            var existing = bindings.FirstOrDefault(b => b.InvoiceType == type);
            if (seriesId is null)
            {
                if (existing is not null) db.ChannelInvoiceSeriesBindings.Remove(existing);
                continue;
            }
            if (existing is null)
            {
                db.ChannelInvoiceSeriesBindings.Add(new ChannelInvoiceSeriesBinding
                {
                    FirmPlatformId = channel.Id, InvoiceType = type, InvoiceSeriesId = seriesId.Value, CreatedBy = request.UserId
                });
            }
            else if (existing.InvoiceSeriesId != seriesId.Value)
            {
                existing.InvoiceSeriesId = seriesId.Value;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = request.UserId;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
