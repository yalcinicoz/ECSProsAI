using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetInvoiceSeries;

/// <summary>
/// Fatura serileri DataGrid şeması (2026-09-09, tur 11).
///
/// ★ AYRI UÇ: mevcut <c>GET /orders/invoice-series</c> TAM liste döner ve üç seçicinin kaynağıdır
/// (kanal yuvaları, fatura formu, "pasife alırken yerine geçecek seri"); sayfalamak onları kırar.
/// Liste ekranı <c>/orders/invoice-series/grid</c> kullanır.
///
/// <para>Sütun sınırları (kasıtlı):
/// • <c>firma</c> adı Core modülünde çözülür → sıralanamaz; süzgeç firma KİMLİĞİ üzerinden
///   (ekranın firma seçicisi), Mal Kabul/Satın Alma ile aynı kalıp.
/// • <c>sozlesme</c> adı da firma entegrasyonlarından gelir → yalnız <c>sozlesmeVar</c> bayrağı süzülür.
/// • "kullanan kanallar" kod listesi ekranda kanal ayarlarından çizilir; süzme/sıralama
///   <c>kanalSayisi</c> (ChannelBindings) üzerinden yapılır — sayfalı listede doğru olan tek yol.</para>
/// </summary>
public static class InvoiceSeriesGrid
{
    public static readonly string[] Tipler = { "e_archive", "e_invoice", "export" };

    public static readonly GridSchema<InvoiceSeries> Schema = new GridSchema<InvoiceSeries>()
        .Text("serial", s => s.Serial)
        .Text("ad", s => s.Name)
        .Text("aciklama", s => s.Description)
        .Enum("invoiceType", s => s.InvoiceType, Tipler)
        .Guid("firmId", s => s.FirmId)
        .Bool("aktif", s => s.IsActive)
        .Bool("sozlesmeVar", s => s.IntegrationContractId != null)
        .Bool("kanalaBagli", s => s.ChannelBindings.Any())
        .Bool("kullanilmis", s => s.Counters.Any(c => c.LastSequence > 0))
        .Number("kanalSayisi", s => s.ChannelBindings.Count())
        .Number("sonSira", s => s.Counters.OrderByDescending(c => c.Year).Select(c => c.LastSequence).FirstOrDefault())
        .Date("sonFatura", s => s.Counters.OrderByDescending(c => c.Year).Select(c => c.LastInvoiceDate).FirstOrDefault())
        .Date("olusturma", s => s.CreatedAt)
        .Sort("serial", s => s.Serial)
        .Sort("ad", s => s.Name)
        .Sort("invoiceType", s => s.InvoiceType)
        .Sort("aktif", s => s.IsActive)
        .Sort("kanalSayisi", s => s.ChannelBindings.Count())
        .Sort("sonSira", s => s.Counters.OrderByDescending(c => c.Year).Select(c => c.LastSequence).FirstOrDefault())
        .Sort("sonFatura", s => s.Counters.OrderByDescending(c => c.Year).Select(c => c.LastInvoiceDate).FirstOrDefault())
        .Sort("olusturma", s => s.CreatedAt)
        // Eski ekranın sırası firma → tip → seri; firma adı burada çözülemediği için tip → seri.
        .DefaultSort(s => s.InvoiceType, desc: false)
        .TieBreaker(s => s.Serial);

    public static IQueryable<InvoiceSeries> ApplyNamed(IQueryable<InvoiceSeries> query, InvoiceSeriesFiltreleri f)
    {
        if (f.FirmId is not null) query = query.Where(s => s.FirmId == f.FirmId);
        if (!string.IsNullOrWhiteSpace(f.InvoiceType)) query = query.Where(s => s.InvoiceType == f.InvoiceType);
        if (f.Durum == "active") query = query.Where(s => s.IsActive);
        else if (f.Durum == "passive") query = query.Where(s => !s.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Arama))
        {
            var t = f.Arama.Trim().ToLower();
            query = query.Where(s => s.Serial.ToLower().Contains(t)
                || (s.Name != null && s.Name.ToLower().Contains(t))
                || (s.Description != null && s.Description.ToLower().Contains(t)));
        }
        return query;
    }

    public static IQueryable<InvoiceSeries> ApplyAll(IQueryable<InvoiceSeries> query, InvoiceSeriesFiltreleri f, GridRequest? grid)
        => Schema.ApplyFilters(ApplyNamed(query, f), grid);
}

/// <param name="Durum">active | passive | boş (tümü) — ekranın üç durumlu seçicisi.</param>
public record InvoiceSeriesFiltreleri(
    Guid? FirmId = null, string? InvoiceType = null, string? Durum = "active", string? Arama = null);

public record GetInvoiceSeriesGridQuery(
    InvoiceSeriesFiltreleri Filtreler, int Page = 1, int PageSize = 50, GridRequest? Grid = null)
    : IRequest<Result<PagedResult<InvoiceSeriesDto>>>;

public class GetInvoiceSeriesGridQueryHandler(IOrderDbContext db, IFirmResolver firmResolver)
    : IRequestHandler<GetInvoiceSeriesGridQuery, Result<PagedResult<InvoiceSeriesDto>>>
{
    public async Task<Result<PagedResult<InvoiceSeriesDto>>> Handle(GetInvoiceSeriesGridQuery r, CancellationToken ct)
    {
        var q = InvoiceSeriesGrid.ApplyAll(db.InvoiceSeries.AsNoTracking(), r.Filtreler, r.Grid);
        var toplam = await q.CountAsync(ct);   // sayım SAYFALAMADAN ÖNCE
        var rows = await InvoiceSeriesGrid.Schema.ApplySort(q, r.Grid)
            .Skip((Math.Max(1, r.Page) - 1) * r.PageSize).Take(r.PageSize)
            .Select(s => new
            {
                s.Id, s.FirmId, s.Serial, s.InvoiceType, s.Name, s.Description, s.IntegrationContractId,
                s.IsActive, s.RetiredAt,
                ChannelCount = s.ChannelBindings.Count(),
                Last = s.Counters.OrderByDescending(c => c.Year)
                    .Select(c => new { c.Year, c.LastSequence, c.LastInvoiceDate }).FirstOrDefault(),
            })
            .ToListAsync(ct);

        // Sözleşme adı firma entegrasyonlarından gelir (başka modül) → sayfa satırları için sözlükten çözülür.
        var contracts = (await firmResolver.GetEInvoiceContractsAsync(null, ct)).ToDictionary(c => c.Id);

        var satirlar = rows.Select(s => new InvoiceSeriesDto(
            s.Id, s.FirmId, s.Serial, s.InvoiceType, s.Name, s.Description, s.IntegrationContractId,
            s.IntegrationContractId is not null && contracts.TryGetValue(s.IntegrationContractId.Value, out var c)
                ? (c.Name ?? c.ServiceCode) : null,
            s.IsActive, s.RetiredAt, s.ChannelCount,
            s.Last?.Year, s.Last?.LastSequence ?? 0, s.Last?.LastInvoiceDate)).ToList();

        return Result.Success(new PagedResult<InvoiceSeriesDto>(satirlar, toplam, r.Page, r.PageSize));
    }
}

public record InvoiceSeriesExportRow(
    string Serial, string InvoiceType, string? Ad, string? Aciklama, string? Sozlesme,
    int KanalSayisi, string? SonYil, int SonSira, DateTime? SonFatura, bool Aktif, DateTime? PasifeAlma);

public record ExportInvoiceSeriesQuery(InvoiceSeriesFiltreleri Filtreler, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<InvoiceSeriesExportRow>>>;

public class ExportInvoiceSeriesQueryHandler(IOrderDbContext db, IFirmResolver firmResolver)
    : IRequestHandler<ExportInvoiceSeriesQuery, Result<GridExportSource<InvoiceSeriesExportRow>>>
{
    public async Task<Result<GridExportSource<InvoiceSeriesExportRow>>> Handle(ExportInvoiceSeriesQuery r, CancellationToken ct)
    {
        var q = InvoiceSeriesGrid.ApplyAll(db.InvoiceSeries.AsNoTracking(), r.Filtreler, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<InvoiceSeriesExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");

        var contracts = (await firmResolver.GetEInvoiceContractsAsync(null, ct)).ToDictionary(c => c.Id);
        var ham = await InvoiceSeriesGrid.Schema.ApplySort(q, r.Grid)
            .Select(s => new
            {
                s.Serial, s.InvoiceType, s.Name, s.Description, s.IntegrationContractId, s.IsActive, s.RetiredAt,
                ChannelCount = s.ChannelBindings.Count(),
                Last = s.Counters.OrderByDescending(c => c.Year)
                    .Select(c => new { c.Year, c.LastSequence, c.LastInvoiceDate }).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var rows = ham.Select(s => new InvoiceSeriesExportRow(
            s.Serial, s.InvoiceType, s.Name, s.Description,
            s.IntegrationContractId is not null && contracts.TryGetValue(s.IntegrationContractId.Value, out var c)
                ? (c.Name ?? c.ServiceCode) : null,
            s.ChannelCount, s.Last?.Year, s.Last?.LastSequence ?? 0, s.Last?.LastInvoiceDate,
            s.IsActive, s.RetiredAt));
        return Result.Success(new GridExportSource<InvoiceSeriesExportRow>(count, rows.AsQueryable()));
    }
}
