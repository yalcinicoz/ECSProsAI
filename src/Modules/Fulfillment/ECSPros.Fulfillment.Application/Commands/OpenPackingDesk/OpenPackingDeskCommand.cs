using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.OpenPackingDesk;

/// <summary>
/// OP4: zimmetli koli için masa açar — masalar sanaldır, açık masalar arasında EN KÜÇÜK
/// boş numara verilir (kurgu). Koli karta "Masada (N)" düşer; log kolideki tüm siparişlere.
/// </summary>
public record OpenPackingDeskCommand(Guid BoxId, Guid ActorId) : IRequest<Result<OpenDeskDto>>;

public record OpenDeskDto(Guid DeskId, int DeskNumber, int BoxNumber);

public class OpenPackingDeskCommandHandler(IFulfillmentDbContext db)
    : IRequestHandler<OpenPackingDeskCommand, Result<OpenDeskDto>>
{
    public async Task<Result<OpenDeskDto>> Handle(OpenPackingDeskCommand request, CancellationToken ct)
    {
        var koli = await db.SortingBoxes.FirstOrDefaultAsync(b => b.Id == request.BoxId, ct);
        if (koli is null) return Result.Failure<OpenDeskDto>("Koli bulunamadı.");
        if (koli.Status == "closed") return Result.Failure<OpenDeskDto>("Kapalı koli için masa açılamaz.");
        if (koli.Status == "taken" && koli.TakenBy != request.ActorId)
            return Result.Failure<OpenDeskDto>("Koli başka personelin zimmetinde.");
        if (koli.StationId is not null)
        {
            var mevcutMasa = await db.PackingDesks.FirstOrDefaultAsync(
                d => d.Id == koli.StationId && d.Status == "open", ct);
            if (mevcutMasa is not null)
                return Result.Success(new OpenDeskDto(mevcutMasa.Id, mevcutMasa.DeskNumber, koli.BoxNumber));
        }

        var now = DateTime.UtcNow;
        // Zimmet yoksa masa açan personel otomatik zimmetlenir
        if (koli.Status == "open")
        {
            koli.Status = "taken";
            koli.TakenBy = request.ActorId;
            koli.TakenAt = now;
        }

        // Açık masaların dışındaki en küçük numara (plan bağımsız — masalar fiziken ortak,
        // numara benzersizliği tüm açık masalar arasında)
        var acikNolar = await db.PackingDesks
            .Where(d => d.Status == "open")
            .Select(d => d.DeskNumber)
            .ToListAsync(ct);
        var no = 1;
        var set = acikNolar.ToHashSet();
        while (set.Contains(no)) no++;

        var masa = new PackingDesk
        {
            PickingPlanId = koli.PickingPlanId,
            SortingBoxId = koli.Id,
            DeskNumber = no,
            Status = "open",
            OpenedBy = request.ActorId,
            OpenedAt = now,
            CreatedBy = request.ActorId
        };
        db.PackingDesks.Add(masa);
        koli.StationId = masa.Id;
        koli.StationNumber = no;
        koli.UpdatedAt = now;
        koli.UpdatedBy = request.ActorId;

        var siparisler = await db.SortingBins
            .Where(sb => sb.SortingBoxId == koli.Id && sb.OrderId != null)
            .Select(sb => sb.OrderId!.Value)
            .ToListAsync(ct);
        foreach (var orderId in siparisler)
        {
            db.OperationLogs.Add(new OperationLog
            {
                OrderId = orderId, PickingPlanId = koli.PickingPlanId, Action = "station_opened",
                ActorId = request.ActorId, CreatedBy = request.ActorId,
                Detail = new Dictionary<string, object> { ["desk"] = no, ["box"] = koli.BoxNumber }
            });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(new OpenDeskDto(masa.Id, no, koli.BoxNumber));
    }
}
