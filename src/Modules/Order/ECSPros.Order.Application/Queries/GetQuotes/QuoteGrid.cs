using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetQuotes;

/// <summary>Teklifler DataGrid şeması: beyaz listeli sıralama/filtre + adlandırılmış filtreler + global arama (teklif no); liste ve export aynı modeli kullanır.</summary>
public static class QuoteGrid
{
    public static readonly string[] Statuses = { "draft", "sent", "accepted", "rejected", "converted", "expired" };

    public static readonly GridSchema<Quote> Schema = new GridSchema<Quote>()
        .Text("quoteNumber", q => q.QuoteNumber)
        .Enum("status", q => q.Status, Statuses)
        .Number("total", q => q.GrandTotal)
        .Date("validUntil", q => q.ValidUntil)
        .Date("sentAt", q => q.SentAt)
        .Date("createdAt", q => q.CreatedAt)
        .Guid("memberId", q => q.MemberId)
        .Guid("firmPlatformId", q => q.FirmPlatformId)
        .Kanal(q => q.FirmPlatformId)   // Y3: kanal kapsamı kolonu (K2)
        .Bool("converted", q => q.ConvertedOrderId != null)
        .Sort("quoteNumber", q => q.QuoteNumber)
        .Sort("total", q => q.GrandTotal)
        .Sort("validUntil", q => q.ValidUntil)
        .Sort("sentAt", q => q.SentAt)
        .Sort("createdAt", q => q.CreatedAt)
        .Sort("status", q => q.Status)
        .DefaultSort(q => q.CreatedAt, desc: true)
        .TieBreaker(q => q.Id);

    public static IQueryable<Quote> ApplyNamed(IQueryable<Quote> query, QuoteListFilters f, bool includeStatus = true)
    {
        if (f.MemberId.HasValue) query = query.Where(q => q.MemberId == f.MemberId.Value);
        if (includeStatus && !string.IsNullOrEmpty(f.Status)) query = query.Where(q => q.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(q => q.QuoteNumber.ToLower().Contains(term));
        }
        return query;
    }

    public static IQueryable<Quote> ApplyAll(IQueryable<Quote> query, QuoteListFilters f, GridRequest? grid, bool includeStatus = true)
    {
        query = ApplyNamed(query, f, includeStatus);
        // Y3 (K2): kanal kapsamı — erişilmeyen kanalın satırı hiçbir yüzeyde görünmez.
        return Schema.ApplyKanalKapsami(includeStatus ? Schema.ApplyFilters(query, grid) : Schema.ApplyFilters(query, grid, "status"), grid?.KanalKisiti);
    }

    public static string StatusLabel(string s) => s switch
    {
        "draft" => "Taslak", "sent" => "Gönderildi", "accepted" => "Kabul Edildi", "rejected" => "Reddedildi", "converted" => "Siparişe Dönüştü", "expired" => "Süresi Doldu", _ => s,
    };
}

public record QuoteListFilters(Guid? MemberId = null, string? Status = null, string? Search = null);

public record QuoteExportRow(
    string QuoteNumber, string Status, DateTime CreatedAt, DateTime ValidUntil, DateTime? SentAt, DateTime? ViewedAt, DateTime? RespondedAt,
    decimal Subtotal, decimal TotalDiscount, decimal TotalTax, decimal GrandTotal, string CurrencyCode, bool Converted, Guid MemberId, string? NotesToCustomer);

public record ExportQuotesQuery(QuoteListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<QuoteExportRow>>>;

public class ExportQuotesQueryHandler(IOrderDbContext db) : IRequestHandler<ExportQuotesQuery, Result<GridExportSource<QuoteExportRow>>>
{
    public async Task<Result<GridExportSource<QuoteExportRow>>> Handle(ExportQuotesQuery r, CancellationToken ct)
    {
        var q = QuoteGrid.ApplyAll(db.Quotes.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<QuoteExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = QuoteGrid.Schema.ApplySort(q, r.Grid).Select(x => new QuoteExportRow(
            x.QuoteNumber, x.Status, x.CreatedAt, x.ValidUntil, x.SentAt, x.ViewedAt, x.RespondedAt,
            x.Subtotal, x.TotalDiscount, x.TotalTax, x.GrandTotal, x.CurrencyCode, x.ConvertedOrderId != null, x.MemberId, x.NotesToCustomer));
        return Result.Success(new GridExportSource<QuoteExportRow>(count, rows));
    }
}
