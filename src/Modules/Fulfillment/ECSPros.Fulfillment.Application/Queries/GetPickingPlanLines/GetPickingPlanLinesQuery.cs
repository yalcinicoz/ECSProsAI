using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPickingPlanLines;

/// <summary>OP1: görev satırları — dağıtım ekranı (rota sıralı) ve personel toplama listesi
/// (AssignedTo filtresiyle yalnız kendi satırları).</summary>
public record GetPickingPlanLinesQuery(
    Guid PlanId,
    Guid? AssignedTo = null) : IRequest<Result<List<PickingPlanLineDto>>>;

public record PickingPlanLineDto(
    Guid Id,
    Guid OrderId,
    Guid OrderItemId,
    string OrderNumber,
    string DisplayName,
    string Sku,
    string VariantBarcode,
    int Quantity,
    int PickedQuantity,
    string? SourceBinCode,
    string? PickedBinCode,
    Guid? AssignedTo,
    DateTime? AssignedAt,
    Guid? PickedBy,
    DateTime? PickedAt,
    string Status,
    int RouteOrder);

public class GetPickingPlanLinesQueryHandler(IFulfillmentDbContext db)
    : IRequestHandler<GetPickingPlanLinesQuery, Result<List<PickingPlanLineDto>>>
{
    public async Task<Result<List<PickingPlanLineDto>>> Handle(GetPickingPlanLinesQuery request, CancellationToken ct)
    {
        var sorgu = db.PickingPlanLines.AsNoTracking()
            .Where(l => l.PickingPlanId == request.PlanId);
        if (request.AssignedTo is { } kisi)
            sorgu = sorgu.Where(l => l.AssignedTo == kisi);

        var liste = await sorgu
            .OrderBy(l => l.RouteOrder)
            .Select(l => new PickingPlanLineDto(l.Id, l.OrderId, l.OrderItemId, l.OrderNumber,
                l.DisplayName, l.Sku, l.VariantBarcode, l.Quantity, l.PickedQuantity,
                l.SourceBinCode, l.PickedBinCode, l.AssignedTo, l.AssignedAt,
                l.PickedBy, l.PickedAt, l.Status, l.RouteOrder))
            .ToListAsync(ct);
        return Result.Success(liste);
    }
}
