using ECSPros.Order.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoiceSeries;

/// <summary>FE0: tekil tipli seriler — seri yönetimi + kanal yuvası/fatura formu seçicileri (tipe göre süzülür).</summary>
public record GetInvoiceSeriesQuery(bool ActiveOnly = true, Guid? FirmId = null, string? InvoiceType = null)
    : IRequest<Result<List<InvoiceSeriesDto>>>;

public record InvoiceSeriesDto(
    Guid Id,
    Guid FirmId,
    string Serial,
    string InvoiceType,
    string? Name,
    string? Description,
    Guid? IntegrationContractId,
    string? IntegrationContractName,
    bool IsActive,
    DateTime? RetiredAt,
    int ChannelCount,
    string? LastYear,
    int LastSequence,
    DateTime? LastInvoiceDate);

public class GetInvoiceSeriesQueryHandler(IOrderDbContext db, IFirmResolver firmResolver)
    : IRequestHandler<GetInvoiceSeriesQuery, Result<List<InvoiceSeriesDto>>>
{
    public async Task<Result<List<InvoiceSeriesDto>>> Handle(GetInvoiceSeriesQuery request, CancellationToken ct)
    {
        var query = db.InvoiceSeries.AsNoTracking();
        if (request.ActiveOnly) query = query.Where(s => s.IsActive);
        if (request.FirmId is not null) query = query.Where(s => s.FirmId == request.FirmId);
        if (!string.IsNullOrWhiteSpace(request.InvoiceType)) query = query.Where(s => s.InvoiceType == request.InvoiceType);

        var rows = await query
            .OrderBy(s => s.FirmId).ThenBy(s => s.InvoiceType).ThenBy(s => s.Serial)
            .Select(s => new
            {
                s.Id, s.FirmId, s.Serial, s.InvoiceType, s.Name, s.Description, s.IntegrationContractId,
                s.IsActive, s.RetiredAt,
                ChannelCount = s.ChannelBindings.Count(),
                Last = s.Counters.OrderByDescending(c => c.Year).Select(c => new { c.Year, c.LastSequence, c.LastInvoiceDate }).FirstOrDefault()
            })
            .ToListAsync(ct);

        var contracts = (await firmResolver.GetEInvoiceContractsAsync(null, ct)).ToDictionary(c => c.Id);

        return Result.Success(rows.Select(s => new InvoiceSeriesDto(
            s.Id, s.FirmId, s.Serial, s.InvoiceType, s.Name, s.Description, s.IntegrationContractId,
            s.IntegrationContractId is not null && contracts.TryGetValue(s.IntegrationContractId.Value, out var c)
                ? (c.Name ?? c.ServiceCode) : null,
            s.IsActive, s.RetiredAt, s.ChannelCount,
            s.Last?.Year, s.Last?.LastSequence ?? 0, s.Last?.LastInvoiceDate)).ToList());
    }
}
