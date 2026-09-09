using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetStockAlertsForAdmin;

/// <summary>
/// Stok alarmları DataGrid şeması (2026-09-09): beyaz listeli filtre/sıralama + adlandırılmış
/// filtreler (status/firmPlatformId) + global arama (e-posta, ürün kodu).
///
/// <para>Y3 (K2): <c>.Kanal(FirmPlatformId)</c> — alarm bir kanala aittir (kanal kolonu olan şema
/// HER ZAMAN kapsam bildirir; bkz. KanalKapsamiTests).</para>
/// <para>E-posta kişisel veridir: Excel kolonunda alan yetkisine bağlıdır.</para>
/// </summary>
public static class StockAlertGrid
{
    public static readonly string[] Statuses = { "active", "notified", "cancelled" };

    public static readonly GridSchema<StockAlert> Schema = new GridSchema<StockAlert>()
        .Kanal(a => a.FirmPlatformId)   // Y3 (K2)
        .Text("email", a => a.Email)
        .Text("productCode", a => a.ProductCode)
        .Text("variantInfo", a => a.VariantInfo)
        .Enum("status", a => a.Status, Statuses)
        .Bool("notified", a => a.NotifiedAt != null)
        .Date("createdAt", a => a.CreatedAt)
        .Date("notifiedAt", a => a.NotifiedAt)
        .Guid("firmPlatformId", a => a.FirmPlatformId)
        .Guid("memberId", a => a.MemberId)
        .Sort("email", a => a.Email)
        .Sort("productCode", a => a.ProductCode)
        .Sort("variantInfo", a => a.VariantInfo)
        .Sort("status", a => a.Status)
        .Sort("createdAt", a => a.CreatedAt)
        .Sort("notifiedAt", a => a.NotifiedAt)
        .DefaultSort(a => a.CreatedAt, desc: true)
        .TieBreaker(a => a.Id);

    public static IQueryable<StockAlert> ApplyNamed(IQueryable<StockAlert> query, StockAlertFilters f)
    {
        if (!string.IsNullOrWhiteSpace(f.Status)) query = query.Where(a => a.Status == f.Status);
        if (f.FirmPlatformId.HasValue) query = query.Where(a => a.FirmPlatformId == f.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var aranan = f.Search.Trim().ToLower();
            query = query.Where(a =>
                (a.Email != null && a.Email.ToLower().Contains(aranan)) ||
                (a.ProductCode != null && a.ProductCode.ToLower().Contains(aranan)));
        }
        return query;
    }

    public static IQueryable<StockAlert> ApplyAll(
        IQueryable<StockAlert> query, StockAlertFilters f, GridRequest? grid,
        IReadOnlyCollection<Guid>? kanalKisiti = null)
        => Schema.ApplyKanalKapsami(
            Schema.ApplyFilters(ApplyNamed(query, f), grid),
            kanalKisiti ?? grid?.KanalKisiti);

    public static string StatusLabel(string s) => s switch
    {
        "active" => "Bekliyor", "notified" => "Bildirildi", "cancelled" => "İptal", _ => s,
    };
}

public record StockAlertFilters(string? Status = null, Guid? FirmPlatformId = null, string? Search = null);

public record StockAlertExportRow(
    string? Email, string? ProductCode, string? VariantInfo, string Status, DateTime? NotifiedAt, DateTime CreatedAt);

public record ExportStockAlertsQuery(StockAlertFilters Filters, GridRequest Grid, int MaxRows)
    : IRequest<Result<GridExportSource<StockAlertExportRow>>>;

public class ExportStockAlertsQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<ExportStockAlertsQuery, Result<GridExportSource<StockAlertExportRow>>>
{
    public async Task<Result<GridExportSource<StockAlertExportRow>>> Handle(ExportStockAlertsQuery r, CancellationToken ct)
    {
        var q = StockAlertGrid.ApplyAll(db.StockAlerts.AsNoTracking(), r.Filters, r.Grid);
        var count = await q.CountAsync(ct);
        if (count > r.MaxRows)
            return Result.Failure<GridExportSource<StockAlertExportRow>>(
                $"Sonuç {count:N0} satır; dışa aktarma sınırı {r.MaxRows:N0}. Filtreyi daraltın.");
        var rows = StockAlertGrid.Schema.ApplySort(q, r.Grid).Select(a => new StockAlertExportRow(
            a.Email, a.ProductCode, a.VariantInfo, a.Status, a.NotifiedAt, a.CreatedAt));
        return Result.Success(new GridExportSource<StockAlertExportRow>(count, rows));
    }
}
