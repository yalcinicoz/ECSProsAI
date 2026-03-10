using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetFulfillmentDashboard;

public record GetFulfillmentDashboardQuery() : IRequest<Result<FulfillmentDashboardDto>>;

public record FulfillmentDashboardDto(
    int PendingPlans,
    int ActivePlans,
    int CompletedPlansToday,
    int TotalBins,
    int FillingBins,
    int ReadyBins,
    int TotalPackages,
    int ActivePackingStations);

public class GetFulfillmentDashboardQueryHandler : IRequestHandler<GetFulfillmentDashboardQuery, Result<FulfillmentDashboardDto>>
{
    private readonly IFulfillmentDbContext _db;

    public GetFulfillmentDashboardQueryHandler(IFulfillmentDbContext db) => _db = db;

    public async Task<Result<FulfillmentDashboardDto>> Handle(GetFulfillmentDashboardQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var pendingPlans = await _db.PickingPlans.CountAsync(p => p.Status == "pending", ct);
        var activePlans = await _db.PickingPlans.CountAsync(p => p.Status == "picking", ct);
        var completedToday = await _db.PickingPlans.CountAsync(p => p.Status == "completed" && p.CompletedAt >= today, ct);

        var totalBins = await _db.SortingBins.CountAsync(ct);
        var fillingBins = await _db.SortingBins.CountAsync(b => b.Status == "filling", ct);
        var readyBins = await _db.SortingBins.CountAsync(b => b.Status == "ready", ct);

        var totalPackages = await _db.Packages.CountAsync(ct);
        var activeStations = await _db.PackingStations.CountAsync(s => s.Status == "available" || s.Status == "busy", ct);

        var dto = new FulfillmentDashboardDto(
            pendingPlans, activePlans, completedToday,
            totalBins, fillingBins, readyBins,
            totalPackages, activeStations);

        return Result.Success(dto);
    }
}
