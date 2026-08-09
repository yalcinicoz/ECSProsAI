using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Queries.GetSortingWall;

/// <summary>OP3: koli duvarı — aktif (open/taken) koli kartları: sipariş/ürün sayıları,
/// tamamlanma yüzdesi (koli içi gerçek ihtiyaçtan), renk (tüm-ürünleri-kolide sipariş
/// oranı, profil eşikleri), zimmet/masa bilgisi, son okutma.</summary>
public record GetSortingWallQuery(Guid PlanId) : IRequest<Result<SortingWallDto>>;

public record SortingBoxCardDto(
    Guid BoxId,
    int BoxNumber,
    int Generation,
    string Status,
    Guid? TakenBy,
    DateTime? TakenAt,
    int? StationNumber,
    int SiparisSayisi,
    int TamamSiparis,      // tüm ürünleri kolide olan sipariş sayısı
    int GirenUrun,
    int GerekenUrun,
    int TamamlanmaYuzde,   // giren/gereken (koli içi gerçek ihtiyaç)
    string Renk,           // green | yellow | red (profil eşikleri)
    DateTime? SonOkutma);

public record SortingWallDto(
    List<SortingBoxCardDto> Koliler,
    int KolisizSiparis,
    int KapaliKoli,
    int YesilEsik,
    int SariEsik);

public class GetSortingWallQueryHandler(IFulfillmentDbContext db)
    : IRequestHandler<GetSortingWallQuery, Result<SortingWallDto>>
{
    public async Task<Result<SortingWallDto>> Handle(GetSortingWallQuery request, CancellationToken ct)
    {
        var profil = await db.OperationProfiles.AsNoTracking().FirstOrDefaultAsync(ct)
                     ?? new OperationProfile();

        var koliler = await db.SortingBoxes.AsNoTracking()
            .Where(b => b.PickingPlanId == request.PlanId && b.Status != "closed")
            .OrderBy(b => b.BoxNumber)
            .ToListAsync(ct);
        var kapali = await db.SortingBoxes.AsNoTracking()
            .CountAsync(b => b.PickingPlanId == request.PlanId && b.Status == "closed", ct);

        var binler = await db.SortingBins.AsNoTracking()
            .Where(sb => sb.PickingPlanId == request.PlanId && sb.OrderId != null)
            .ToListAsync(ct);
        var satirlar = await db.PickingPlanLines.AsNoTracking()
            .Where(l => l.PickingPlanId == request.PlanId)
            .Select(l => new { l.OrderId, l.Quantity, l.SortedQuantity })
            .ToListAsync(ct);
        var satirByOrder = satirlar.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.ToList());

        var kartlar = new List<SortingBoxCardDto>();
        foreach (var koli in koliler)
        {
            var koliSiparisleri = binler.Where(b => b.SortingBoxId == koli.Id)
                .Select(b => b.OrderId!.Value).Distinct().ToList();
            int giren = 0, gereken = 0, tamam = 0;
            foreach (var oid in koliSiparisleri)
            {
                var ls = satirByOrder.GetValueOrDefault(oid) ?? [];
                giren += ls.Sum(l => l.SortedQuantity);
                gereken += ls.Sum(l => l.Quantity);
                if (ls.Count > 0 && ls.All(l => l.SortedQuantity >= l.Quantity)) tamam++;
            }
            var yuzde = gereken == 0 ? 0 : giren * 100 / gereken;
            var tamOran = koliSiparisleri.Count == 0 ? 0 : tamam * 100 / koliSiparisleri.Count;
            var renk = tamOran >= profil.BoxGreenPct ? "green"
                : tamOran >= profil.BoxYellowPct ? "yellow" : "red";
            kartlar.Add(new SortingBoxCardDto(koli.Id, koli.BoxNumber, koli.Generation, koli.Status,
                koli.TakenBy, koli.TakenAt, koli.StationNumber,
                koliSiparisleri.Count, tamam, giren, gereken, yuzde, renk, koli.UpdatedAt));
        }

        var kolisiz = binler.Count(b => b.SortingBoxId == null && b.OrderId != null);
        return Result.Success(new SortingWallDto(kartlar, kolisiz, kapali,
            profil.BoxGreenPct, profil.BoxYellowPct));
    }
}
