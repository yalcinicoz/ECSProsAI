using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetChannelInvoiceSettings;

/// <summary>Kanal faturalama ayarları (tüm kanallar ya da tek kanal) + eksik yuva uyarıları.</summary>
public record GetChannelInvoiceSettingsQuery(Guid? FirmPlatformId = null)
    : IRequest<Result<List<ChannelInvoiceSettingsDto>>>;

public record ChannelInvoiceSeriesBindingDto(
    string InvoiceType, Guid SeriesId, string Serial, string? SeriesName, bool SeriesActive);

public record ChannelInvoiceSettingsDto(
    Guid FirmPlatformId,
    Guid FirmId,
    string ChannelCode,
    string ChannelName,
    bool ChannelActive,
    string SendMethod,
    List<ChannelInvoiceSeriesBindingDto> Bindings,
    List<string> MissingTypes,
    List<string> Warnings);

public class GetChannelInvoiceSettingsQueryHandler(IOrderDbContext db, IFirmResolver firmResolver)
    : IRequestHandler<GetChannelInvoiceSettingsQuery, Result<List<ChannelInvoiceSettingsDto>>>
{
    public async Task<Result<List<ChannelInvoiceSettingsDto>>> Handle(GetChannelInvoiceSettingsQuery request, CancellationToken ct)
    {
        IReadOnlyList<ChannelInfo> channels;
        if (request.FirmPlatformId is not null)
        {
            var one = await firmResolver.GetChannelAsync(request.FirmPlatformId.Value, ct);
            if (one is null) return Result.Failure<List<ChannelInvoiceSettingsDto>>("Satış kanalı bulunamadı.");
            channels = [one];
        }
        else channels = await firmResolver.GetChannelsAsync(ct);

        var ids = channels.Select(c => c.Id).ToList();
        var settings = await db.ChannelInvoiceSettings.AsNoTracking()
            .Where(s => ids.Contains(s.FirmPlatformId)).ToDictionaryAsync(s => s.FirmPlatformId, ct);
        var bindings = await db.ChannelInvoiceSeriesBindings.AsNoTracking()
            .Where(b => ids.Contains(b.FirmPlatformId))
            .Select(b => new { b.FirmPlatformId, b.InvoiceType, b.InvoiceSeriesId, b.InvoiceSeries.Serial, b.InvoiceSeries.Name, b.InvoiceSeries.IsActive })
            .ToListAsync(ct);

        var result = channels.Select(c =>
        {
            var method = settings.TryGetValue(c.Id, out var s) ? s.SendMethod : InvoiceSendMethods.Manual;
            var bs = bindings.Where(b => b.FirmPlatformId == c.Id)
                .Select(b => new ChannelInvoiceSeriesBindingDto(b.InvoiceType, b.InvoiceSeriesId, b.Serial, b.Name, b.IsActive))
                .OrderBy(b => Array.IndexOf(InvoiceTypes.All, b.InvoiceType))
                .ToList();
            var missing = InvoiceTypes.All.Where(t => bs.All(b => b.InvoiceType != t)).ToList();
            var warnings = new List<string>();
            // Kullanıcı kuralı (2026-09-06): her aktif satış kanalı ÜÇ tipte de seriye bağlı olmalı
            if (c.IsActive && missing.Count > 0)
                warnings.Add($"Eksik seri yuvası: {string.Join(", ", missing.Select(InvoiceTypes.Label))} — kanal her üç tipte de seriye bağlı olmalı.");
            if (c.IsActive && missing.Contains(InvoiceTypes.EArchive))
                warnings.Add("e-Arşiv serisi bağlı olmadığından paket kapanışında otomatik fatura kesilemez.");
            foreach (var b in bs.Where(b => !b.SeriesActive))
                warnings.Add($"{InvoiceTypes.Label(b.InvoiceType)} yuvasındaki {b.Serial} serisi pasif.");
            return new ChannelInvoiceSettingsDto(c.Id, c.FirmId, c.Code, c.Name, c.IsActive, method, bs, missing, warnings);
        }).ToList();

        return Result.Success(result);
    }
}
