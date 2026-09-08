using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Queries.GetGiftCards;

/// <summary>
/// Hediye kartları DataGrid şeması: beyaz listeli filtre/sıralama + adlandırılmış filtreler (status) + global arama (kod).
/// validFrom/validUntil DateOnly kolonlardır (GridSchema.Date gün karşılaştırması yapar).
/// </summary>
public static class GiftCardGrid
{
    public static readonly string[] Statuses = { "active", "used", "depleted", "expired", "cancelled" };

    public static readonly GridSchema<GiftCard> Schema = new GridSchema<GiftCard>()
        .Text("code", g => g.Code)
        .Enum("status", g => g.Status, Statuses)
        .Number("originalAmount", g => g.OriginalAmount)
        .Number("remainingAmount", g => g.RemainingAmount)
        .Bool("isSingleUse", g => g.IsSingleUse)
        .Bool("hasBalance", g => g.RemainingAmount > 0)
        .Date("createdAt", g => g.CreatedAt)
        .Date("validFrom", g => g.ValidFrom)
        .Date("validUntil", g => g.ValidUntil)
        .Guid("memberId", g => g.CreatedForMemberId)
        .Guid("orderId", g => g.CreatedFromOrderId)
        .Guid("firmId", g => g.FirmId)
        .Sort("code", g => g.Code)
        .Sort("originalAmount", g => g.OriginalAmount)
        .Sort("remainingAmount", g => g.RemainingAmount)
        .Sort("validFrom", g => g.ValidFrom)
        .Sort("validUntil", g => g.ValidUntil)
        .Sort("isSingleUse", g => g.IsSingleUse)
        .Sort("status", g => g.Status)
        .Sort("createdAt", g => g.CreatedAt)
        .DefaultSort(g => g.CreatedAt, desc: true)
        .TieBreaker(g => g.Id);

    public static IQueryable<GiftCard> ApplyNamed(IQueryable<GiftCard> query, GiftCardListFilters f)
    {
        if (!string.IsNullOrEmpty(f.Status)) query = query.Where(g => g.Status == f.Status);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = f.Search.Trim().ToLower();
            query = query.Where(g => g.Code.ToLower().Contains(term));
        }
        return query;
    }

    public static IQueryable<GiftCard> ApplyAll(IQueryable<GiftCard> query, GiftCardListFilters f, GridRequest? grid)
    {
        query = ApplyNamed(query, f);
        return Schema.ApplyFilters(query, grid);
    }

    public static string StatusLabel(string s) => s switch
    {
        "active" => "Aktif", "used" => "Kullanıldı", "depleted" => "Bakiye Bitti", "expired" => "Süresi Doldu", "cancelled" => "İptal", _ => s,
    };
}

public record GiftCardListFilters(string? Status = null, string? Search = null);

public record GiftCardExportRow(
    string Code, string Status, decimal OriginalAmount, decimal RemainingAmount, string CurrencyCode,
    DateOnly ValidFrom, DateOnly? ValidUntil, bool IsSingleUse, Guid? CreatedForMemberId, Guid? CreatedFromOrderId, DateTime CreatedAt);

public record ExportGiftCardsQuery(GiftCardListFilters Filters, GridRequest Grid, int MaxRows) : IRequest<Result<GridExportSource<GiftCardExportRow>>>;

public class ExportGiftCardsQueryHandler(IOrderDbContext db) : IRequestHandler<ExportGiftCardsQuery, Result<GridExportSource<GiftCardExportRow>>>
{
    public async Task<Result<GridExportSource<GiftCardExportRow>>> Handle(ExportGiftCardsQuery r, CancellationToken ct)
    {
        var q = GiftCardGrid.ApplyAll(db.GiftCards.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<GiftCardExportRow>>($"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = GiftCardGrid.Schema.ApplySort(q, r.Grid).Select(g => new GiftCardExportRow(
            g.Code, g.Status, g.OriginalAmount, g.RemainingAmount, g.CurrencyCode, g.ValidFrom, g.ValidUntil, g.IsSingleUse,
            g.CreatedForMemberId, g.CreatedFromOrderId, g.CreatedAt));
        return Result.Success(new GridExportSource<GiftCardExportRow>(count, rows));
    }
}
