using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPackageCodeHistory;

/// <summary>Paketin kod değişiklik izi (F4) — panelde sipariş/paket detayında gösterilir.</summary>
public record GetPackageCodeHistoryQuery(Guid PackageId) : IRequest<Result<List<PackageCodeHistoryDto>>>;

public record PackageCodeHistoryDto(
    Guid Id,
    string? OldPackageNumber,
    string? OldCargoIntegrationCode,
    string ChangeType,
    string Reason,
    DateTime ChangedAt,
    Guid? ChangedBy);

public class GetPackageCodeHistoryQueryHandler
    : IRequestHandler<GetPackageCodeHistoryQuery, Result<List<PackageCodeHistoryDto>>>
{
    private readonly IFulfillmentDbContext _context;

    public GetPackageCodeHistoryQueryHandler(IFulfillmentDbContext context) => _context = context;

    public async Task<Result<List<PackageCodeHistoryDto>>> Handle(
        GetPackageCodeHistoryQuery request, CancellationToken cancellationToken)
    {
        var list = await _context.PackageCodeHistories.AsNoTracking()
            .Where(h => h.PackageId == request.PackageId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new PackageCodeHistoryDto(
                h.Id, h.OldPackageNumber, h.OldCargoIntegrationCode,
                h.ChangeType, h.Reason, h.CreatedAt, h.CreatedBy))
            .ToListAsync(cancellationToken);

        return Result.Success(list);
    }
}
