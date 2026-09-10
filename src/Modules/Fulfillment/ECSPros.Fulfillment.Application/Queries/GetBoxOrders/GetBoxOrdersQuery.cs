using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetBoxOrders;

/// <summary>
/// FAZ 15.4f (2026-09-10, eski "Set Koli Sipariş Sorgula"): set = toplama görevi (PlanNumber), koli = ayrıştırma kolisi
/// (BoxNumber), masa = paketleme masası, masa rafı = gözün masa yuvası. Sipariş numarası verilirse o siparişin kolisindeki
/// TÜM siparişler döner (eski davranış). Sipariş durumu/müşteri/fatura tarihi Order modülünden API katmanında eklenir.
/// </summary>
public record GetBoxOrdersQuery(
    string? PlanNumber, int? BoxNumber, string? OrderNumber, DateTime? From, DateTime? To, Guid? TakenBy, int Limit = 500)
    : IRequest<Result<List<BoxOrderDto>>>;

public record BoxOrderDto(
    Guid PlanId, string PlanNumber, DateTime PlannedAt, string PlanStatus,
    Guid? BoxId, int? BoxNumber, string? BoxStatus, Guid? TakenBy, DateTime? TakenAt, int? StationNumber,
    int? DeskNumber, int? DeskSlotNumber, int BinNumber, string BinStatus,
    Guid OrderId, string OrderNumber, DateTime OrderCreatedAt, int LineCount, int PickedLines);

public class GetBoxOrdersQueryHandler(IFulfillmentDbContext db) : IRequestHandler<GetBoxOrdersQuery, Result<List<BoxOrderDto>>>
{
    public async Task<Result<List<BoxOrderDto>>> Handle(GetBoxOrdersQuery r, CancellationToken ct)
    {
        var hicFiltreYok = string.IsNullOrWhiteSpace(r.PlanNumber) && r.BoxNumber is null && string.IsNullOrWhiteSpace(r.OrderNumber)
                           && r.From is null && r.To is null && r.TakenBy is null;
        if (hicFiltreYok) return Result.Failure<List<BoxOrderDto>>("En az bir ölçüt girin (set no, koli no, sipariş no, tarih ya da personel).");

        var bins = from sb in db.SortingBins.AsNoTracking()
                   join p in db.PickingPlans.AsNoTracking() on sb.PickingPlanId equals p.Id
                   join bx0 in db.SortingBoxes.AsNoTracking() on sb.SortingBoxId equals bx0.Id into bxj
                   from bx in bxj.DefaultIfEmpty()
                   where sb.OrderId != null
                   select new { sb, p, bx };

        if (!string.IsNullOrWhiteSpace(r.OrderNumber))
        {
            // Siparişin kolisi → kolideki tüm siparişler (koli yoksa yalnız o siparişin gözü)
            var no = r.OrderNumber.Trim();
            var hedef = await (from l in db.PickingPlanLines.AsNoTracking()
                               where l.OrderNumber == no
                               join sb in db.SortingBins.AsNoTracking() on new { l.PickingPlanId, OrderId = (Guid?)l.OrderId } equals new { sb.PickingPlanId, sb.OrderId }
                               select new { sb.PickingPlanId, sb.SortingBoxId, sb.OrderId }).FirstOrDefaultAsync(ct);
            if (hedef is null) return Result.Success(new List<BoxOrderDto>());
            bins = hedef.SortingBoxId is { } kutu
                ? bins.Where(x => x.sb.SortingBoxId == kutu)
                : bins.Where(x => x.sb.PickingPlanId == hedef.PickingPlanId && x.sb.OrderId == hedef.OrderId);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(r.PlanNumber)) { var pn = r.PlanNumber.Trim(); bins = bins.Where(x => x.p.PlanNumber == pn || x.p.PlanNumber.EndsWith(pn)); }
            if (r.BoxNumber is { } bn) bins = bins.Where(x => x.bx != null && x.bx.BoxNumber == bn);
            if (r.From is { } f) bins = bins.Where(x => x.p.PlannedAt >= f);
            if (r.To is { } t) bins = bins.Where(x => x.p.PlannedAt < t);
            if (r.TakenBy is { } tb) bins = bins.Where(x => x.bx != null && x.bx.TakenBy == tb);
        }

        var rows = await bins.OrderByDescending(x => x.p.PlannedAt).ThenBy(x => x.bx != null ? x.bx.BoxNumber : 0).ThenBy(x => x.sb.BinNumber)
            .Take(r.Limit).ToListAsync(ct);
        if (rows.Count == 0) return Result.Success(new List<BoxOrderDto>());

        var planIds = rows.Select(x => x.p.Id).Distinct().ToList();
        var orderIds = rows.Select(x => x.sb.OrderId!.Value).Distinct().ToList();
        var boxIds = rows.Where(x => x.bx != null).Select(x => x.bx.Id).Distinct().ToList();

        var satirlar = await db.PickingPlanLines.AsNoTracking()
            .Where(l => planIds.Contains(l.PickingPlanId) && orderIds.Contains(l.OrderId))
            .GroupBy(l => new { l.PickingPlanId, l.OrderId })
            .Select(g => new { g.Key.PickingPlanId, g.Key.OrderId, g.First().OrderNumber, g.First().OrderCreatedAt, Count = g.Count(), Picked = g.Count(l => l.PickedQuantity >= l.Quantity) })
            .ToListAsync(ct);
        var satirMap = satirlar.ToDictionary(x => (x.PickingPlanId, x.OrderId));
        var masalar = boxIds.Count == 0 ? new Dictionary<Guid, int>() : await db.PackingDesks.AsNoTracking()
            .Where(d => boxIds.Contains(d.SortingBoxId)).GroupBy(d => d.SortingBoxId)
            .Select(g => new { g.Key, DeskNumber = g.Max(d => d.DeskNumber) }).ToDictionaryAsync(x => x.Key, x => x.DeskNumber, ct);

        var sonuc = rows.Select(x =>
        {
            satirMap.TryGetValue((x.p.Id, x.sb.OrderId!.Value), out var s);
            return new BoxOrderDto(
                x.p.Id, x.p.PlanNumber, x.p.PlannedAt, x.p.Status,
                x.bx?.Id, x.bx?.BoxNumber, x.bx?.Status, x.bx?.TakenBy, x.bx?.TakenAt, x.bx?.StationNumber,
                x.bx is null ? null : (masalar.TryGetValue(x.bx.Id, out var dn) ? dn : null), x.sb.DeskSlotNumber, x.sb.BinNumber, x.sb.Status,
                x.sb.OrderId!.Value, s?.OrderNumber ?? "", s?.OrderCreatedAt ?? x.p.PlannedAt, s?.Count ?? 0, s?.Picked ?? 0);
        }).ToList();
        return Result.Success(sonuc);
    }
}
