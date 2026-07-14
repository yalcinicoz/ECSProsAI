using ECSPros.Shared.Kernel.Common;
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
    int PageSize = 20) : IRequest<Result<PagedResult<AdminStockAlertDto>>>;

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
        var q = db.StockAlerts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(a => a.Status == request.Status);
        if (request.FirmPlatformId.HasValue)
            q = q.Where(a => a.FirmPlatformId == request.FirmPlatformId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var aranan = request.Search.Trim().ToLower();
            q = q.Where(a =>
                (a.Email != null && a.Email.ToLower().Contains(aranan)) ||
                (a.ProductCode != null && a.ProductCode.ToLower().Contains(aranan)));
        }

        var toplam = await q.CountAsync(ct);
        var kayitlar = await q
            .OrderByDescending(a => a.CreatedAt)
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
