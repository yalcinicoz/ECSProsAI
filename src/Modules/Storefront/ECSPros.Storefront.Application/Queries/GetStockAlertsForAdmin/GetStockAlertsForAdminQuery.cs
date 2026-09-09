using ECSPros.Shared.Kernel.Common;
using ECSPros.Shared.Kernel.Grid;
using ECSPros.Storefront.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Queries.GetStockAlertsForAdmin;

/// <summary>P5: stok alarmı izleme (admin) — durum/platform filtreli, sayfalı.
/// Gönderim durumu Status + NotifiedAt'tan okunur (C9/H8).</summary>
public record GetStockAlertsForAdminQuery(
    string? Status = null,
    Guid? FirmPlatformId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    // Y3 (K2): kullanıcının görebileceği kanallar; null = kısıt yok, boş = hiçbir kayıt.
    IReadOnlyCollection<Guid>? KanalKisiti = null,
    GridRequest? Grid = null) : IRequest<Result<PagedResult<AdminStockAlertDto>>>;
    // Grid (2026-09-09, DataGrid): beyaz listeli f.* filtreleri + sort/dir (StockAlertGrid.Schema)

public record AdminStockAlertDto(
    Guid Id,
    Guid FirmPlatformId,
    Guid MemberId,
    string? Email,
    string? ProductCode,
    string? VariantInfo,
    string Status,
    DateTime? NotifiedAt,
    DateTime CreatedAt);

public class GetStockAlertsForAdminQueryHandler(IStorefrontDbContext db)
    : IRequestHandler<GetStockAlertsForAdminQuery, Result<PagedResult<AdminStockAlertDto>>>
{
    public async Task<Result<PagedResult<AdminStockAlertDto>>> Handle(
        GetStockAlertsForAdminQuery request, CancellationToken ct)
    {
        // Y3 kanal kapsamı + adlandırılmış + grid filtreleri TEK yerden (liste ve Excel aynı sonucu verir).
        var q = StockAlertGrid.ApplyAll(
            db.StockAlerts.AsNoTracking(),
            new StockAlertFilters(request.Status, request.FirmPlatformId, request.Search),
            request.Grid,
            request.KanalKisiti ?? request.Grid?.KanalKisiti);

        var toplam = await q.CountAsync(ct);
        var kayitlar = await StockAlertGrid.Schema.ApplySort(q, request.Grid)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AdminStockAlertDto(
                a.Id, a.FirmPlatformId, a.MemberId, a.Email, a.ProductCode,
                a.VariantInfo, a.Status, a.NotifiedAt, a.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<AdminStockAlertDto>(
            kayitlar, toplam, request.Page, request.PageSize));
    }
}
