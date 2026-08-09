using ECSPros.Fulfillment.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetPackingDesks;

/// <summary>OP4: masa izleme + masa ekranı durumu — açık masalar (veya tek masa) ve
/// slot/sipariş ilerlemeleri.</summary>
public record GetPackingDesksQuery(Guid? PlanId = null, Guid? DeskId = null)
    : IRequest<Result<List<PackingDeskDto>>>;

public record DeskSlotDto(
    int SlotNumber, Guid OrderId, string OrderNumber,
    int FinalSorted, int FinalScanned, int Quantity, bool Paketlenebilir);

public record PackingDeskDto(
    Guid DeskId, int DeskNumber, string Status, Guid OpenedBy, DateTime OpenedAt,
    Guid SortingBoxId, int BoxNumber, int KoliSiparis, int Paketlenen, int ObmSayisi,
    DateTime? SonIslem, List<DeskSlotDto> Slotlar);

public class GetPackingDesksQueryHandler(IFulfillmentDbContext db)
    : IRequestHandler<GetPackingDesksQuery, Result<List<PackingDeskDto>>>
{
    public async Task<Result<List<PackingDeskDto>>> Handle(GetPackingDesksQuery request, CancellationToken ct)
    {
        var sorgu = db.PackingDesks.AsNoTracking().Where(d => d.Status == "open");
        if (request.PlanId is { } pid) sorgu = sorgu.Where(d => d.PickingPlanId == pid);
        if (request.DeskId is { } did) sorgu = db.PackingDesks.AsNoTracking().Where(d => d.Id == did);
        var masalar = await sorgu.OrderBy(d => d.DeskNumber).ToListAsync(ct);
        if (masalar.Count == 0) return Result.Success(new List<PackingDeskDto>());

        var koliIdler = masalar.Select(m => m.SortingBoxId).ToList();
        var koliler = await db.SortingBoxes.AsNoTracking()
            .Where(b => koliIdler.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
        var binler = await db.SortingBins.AsNoTracking()
            .Where(sb => sb.SortingBoxId != null && koliIdler.Contains(sb.SortingBoxId.Value) && sb.OrderId != null)
            .ToListAsync(ct);
        var planIdler = masalar.Select(m => m.PickingPlanId).Distinct().ToList();
        var orderIdler = binler.Select(b => b.OrderId!.Value).Distinct().ToList();
        var satirlar = await db.PickingPlanLines.AsNoTracking()
            .Where(l => planIdler.Contains(l.PickingPlanId) && orderIdler.Contains(l.OrderId))
            .Select(l => new { l.OrderId, l.OrderNumber, l.Quantity, l.FinalSortedQuantity, l.FinalScannedQuantity })
            .ToListAsync(ct);
        var satirByOrder = satirlar.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.ToList());

        var sonuc = new List<PackingDeskDto>();
        foreach (var masa in masalar)
        {
            var koli = koliler.GetValueOrDefault(masa.SortingBoxId);
            var koliBinleri = binler.Where(b => b.SortingBoxId == masa.SortingBoxId).ToList();
            var slotlar = new List<DeskSlotDto>();
            int paketlenen = 0, obmSayisi = 0;
            foreach (var bin in koliBinleri)
            {
                var ls = satirByOrder.GetValueOrDefault(bin.OrderId!.Value) ?? [];
                var tamam = ls.Count > 0 && ls.All(l => l.FinalScannedQuantity >= l.Quantity);
                if (tamam) paketlenen++;
                if (bin.ObmTransferred) obmSayisi++;
                if (bin.DeskSlotNumber is { } slot)
                    slotlar.Add(new DeskSlotDto(slot, bin.OrderId.Value,
                        ls.FirstOrDefault()?.OrderNumber ?? "",
                        ls.Sum(l => l.FinalSortedQuantity), ls.Sum(l => l.FinalScannedQuantity),
                        ls.Sum(l => l.Quantity),
                        ls.Count > 0 && ls.All(l => l.FinalSortedQuantity >= l.Quantity)));
            }
            sonuc.Add(new PackingDeskDto(masa.Id, masa.DeskNumber, masa.Status, masa.OpenedBy,
                masa.OpenedAt, masa.SortingBoxId, koli?.BoxNumber ?? 0, koliBinleri.Count,
                paketlenen, obmSayisi, masa.UpdatedAt,
                slotlar.OrderBy(s => s.SlotNumber).ToList()));
        }
        return Result.Success(sonuc);
    }
}
