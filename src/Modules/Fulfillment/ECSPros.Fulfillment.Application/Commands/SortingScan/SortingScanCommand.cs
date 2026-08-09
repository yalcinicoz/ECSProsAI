using ECSPros.Fulfillment.Application.Services;
using ECSPros.Fulfillment.Domain.Entities;
using ECSPros.Fulfillment.Domain.Events;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Fulfillment.Application.Commands.SortingScan;

/// <summary>
/// OP3: ara ayrıştırma okutması — okutulan ürünün verileceği sipariş kullanıcı kurgusundaki
/// koşul sırasıyla seçilir: (1) kolisi belli olan, (2) tüm ürünleri toplanmış, (3) tümü
/// yığında, (4) ayrıştırma sonrası en az ürüne ihtiyacı kalan, (5) en az ürünlü,
/// (6) eşitlikte EN ESKİ sipariş (K-12). Sipariş kolisizse: toplanma oranı eşiğin (profil
/// LowChanceThresholdPct) üzerindeyse EN KÜÇÜK numaralı uygun koli, altındaysa en büyük;
/// uygun koli yoksa yeni koli oturumu açılır (kapalı numaralar Generation+1 ile yeniden
/// kullanılır). "Taken" koli hâlâ AYNI kolidir (kurgu) — dolmaya devam edebilir.
/// Bir siparişin tüm ürünleri AYNI koliye gider. Eşleşme yoksa hata (depo iadesi).
/// </summary>
public record SortingScanCommand(Guid PlanId, string Barcode, Guid ActorId)
    : IRequest<Result<SortingScanResultDto>>;

public record SortingScanResultDto(
    int BoxNumber,            // seslendirilecek numara
    string OrderNumber,
    int SiparisKalan,         // bu siparişin koliye girmesi gereken kalan ürün sayısı
    int KoliSiparisSayisi,
    bool YeniKoli);

public class SortingScanCommandHandler(IFulfillmentDbContext db, IPublisher publisher)
    : IRequestHandler<SortingScanCommand, Result<SortingScanResultDto>>
{
    public async Task<Result<SortingScanResultDto>> Handle(SortingScanCommand request, CancellationToken ct)
    {
        var barkod = request.Barcode.Trim();
        if (barkod.Length == 0) return Result.Failure<SortingScanResultDto>("Barkod boş.");

        // Bu ürüne ihtiyacı olan (koliye eksik yazılmış) satırlar
        var adayOrderIdler = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == request.PlanId
                        && l.VariantBarcode == barkod
                        && l.SortedQuantity < l.Quantity
                        && l.Status != "returned")
            .Select(l => l.OrderId)
            .Distinct()
            .ToListAsync(ct);
        if (adayOrderIdler.Count == 0)
            return Result.Failure<SortingScanResultDto>(
                "Bu ürüne bu görevdeki hiçbir siparişin ihtiyacı yok — depo iadesine ayırın.");

        // Aday siparişlerin tüm satırları + koli eşlemeleri tek sorguda
        var satirlar = await db.PickingPlanLines
            .Where(l => l.PickingPlanId == request.PlanId && adayOrderIdler.Contains(l.OrderId))
            .ToListAsync(ct);
        var binler = await db.SortingBins
            .Where(b => b.PickingPlanId == request.PlanId && b.OrderId != null
                        && adayOrderIdler.Contains(b.OrderId.Value))
            .ToListAsync(ct);
        var binByOrder = binler.ToDictionary(b => b.OrderId!.Value);

        var siparisler = satirlar.GroupBy(l => l.OrderId).Select(g =>
        {
            var hasBox = binByOrder.TryGetValue(g.Key, out var bin) && bin.SortingBoxId != null;
            var allPicked = g.All(l => l.PickedQuantity >= l.Quantity || l.Status == "returned");
            return new
            {
                OrderId = g.Key,
                OrderNumber = g.First().OrderNumber,
                OrderCreatedAt = g.First().OrderCreatedAt,
                HasBox = hasBox,
                AllPicked = allPicked,
                AllInPile = allPicked && g.All(l => l.SortedQuantity == 0), // tümü henüz yığında
                KalanSonrasi = g.Sum(l => l.Quantity - l.SortedQuantity) - 1,
                ToplamUrun = g.Sum(l => l.Quantity),
                ToplananOran = g.Sum(l => l.Quantity) == 0 ? 0
                    : g.Sum(l => Math.Min(l.PickedQuantity, l.Quantity)) * 100 / g.Sum(l => l.Quantity)
            };
        })
        .OrderByDescending(s => s.HasBox)       // Koşul 1
        .ThenByDescending(s => s.AllPicked)     // Koşul 2
        .ThenByDescending(s => s.AllInPile)     // Koşul 3
        .ThenBy(s => s.KalanSonrasi)            // Koşul 4
        .ThenBy(s => s.ToplamUrun)              // Koşul 5
        .ThenBy(s => s.OrderCreatedAt)          // Koşul 6: en eski (K-12)
        .First();

        // Profil parametreleri (kayıt yoksa varsayılanlar; tek firma pratiği — ilk kayıt)
        var profil = await db.OperationProfiles.AsNoTracking().FirstOrDefaultAsync(ct)
                     ?? new OperationProfile();

        // Koli seçimi / oluşturma
        var bin2 = binByOrder.GetValueOrDefault(siparisler.OrderId);
        SortingBox? koli = null;
        var yeniKoli = false;
        if (bin2?.SortingBoxId is { } mevcutKoliId)
        {
            koli = await db.SortingBoxes.FirstAsync(b => b.Id == mevcutKoliId, ct);
        }
        else
        {
            // Açık/taken koliler + sipariş sayıları
            var koliler = await db.SortingBoxes
                .Where(b => b.PickingPlanId == request.PlanId && b.Status != "closed")
                .ToListAsync(ct);
            var sayilar = await db.SortingBins
                .Where(sb => sb.PickingPlanId == request.PlanId && sb.SortingBoxId != null)
                .GroupBy(sb => sb.SortingBoxId!.Value)
                .Select(g => new { KoliId = g.Key, Adet = g.Count() })
                .ToDictionaryAsync(g => g.KoliId, g => g.Adet, ct);
            var uygunlar = koliler
                .Where(b => sayilar.GetValueOrDefault(b.Id) < profil.MaxOrdersPerBox)
                .OrderBy(b => b.BoxNumber)
                .ToList();

            if (uygunlar.Count > 0)
            {
                koli = siparisler.ToplananOran >= profil.LowChanceThresholdPct
                    ? uygunlar.First()      // yüksek ihtimal → en küçük numara
                    : uygunlar.Last();      // düşük ihtimal → son bölge (K-12 kararı)
            }
            else
            {
                // Yeni koli oturumu: aktif numaraların dışındaki en küçük pozitif numara;
                // kapalı numara Generation+1 ile yeniden kullanılır
                var aktifNolar = koliler.Select(b => b.BoxNumber).ToHashSet();
                var no = 1;
                while (aktifNolar.Contains(no)) no++;
                var sonGen = await db.SortingBoxes.IgnoreQueryFilters()
                    .Where(b => b.PickingPlanId == request.PlanId && b.BoxNumber == no)
                    .MaxAsync(b => (int?)b.Generation, ct) ?? 0;
                koli = new SortingBox
                {
                    PickingPlanId = request.PlanId, BoxNumber = no, Generation = sonGen + 1,
                    Status = "open", CreatedBy = request.ActorId
                };
                db.SortingBoxes.Add(koli);
                yeniKoli = true;
            }

            // Sipariş → koli eşlemesi (SortingBin güncelle; yoksa oluştur)
            if (bin2 is null)
            {
                bin2 = new SortingBin
                {
                    PickingPlanId = request.PlanId, OrderId = siparisler.OrderId,
                    BinNumber = koli.BoxNumber, Status = "filling", CreatedBy = request.ActorId
                };
                db.SortingBins.Add(bin2);
            }
            bin2.SortingBoxId = koli.Id;
            bin2.BinNumber = koli.BoxNumber;
            bin2.Status = "filling";
        }

        // Okutulan satıra +1 (rota sıralı ilk eksik satır)
        var satir = satirlar
            .Where(l => l.OrderId == siparisler.OrderId && l.VariantBarcode == barkod
                        && l.SortedQuantity < l.Quantity)
            .OrderBy(l => l.RouteOrder)
            .First();
        var now = DateTime.UtcNow;
        satir.SortedQuantity += 1;
        satir.UpdatedAt = now;
        satir.UpdatedBy = request.ActorId;
        koli.UpdatedAt = now; // koli kartındaki "son okutma"

        db.OperationLogs.Add(new OperationLog
        {
            OrderId = siparisler.OrderId, OrderItemId = satir.OrderItemId,
            PickingPlanId = request.PlanId, Action = "sorting_scanned",
            ActorId = request.ActorId, CreatedBy = request.ActorId,
            Detail = new Dictionary<string, object>
            {
                ["box"] = koli.BoxNumber, ["generation"] = koli.Generation,
                ["sku"] = satir.Sku, ["sorted"] = satir.SortedQuantity, ["quantity"] = satir.Quantity
            }
        });
        await db.SaveChangesAsync(ct);

        await publisher.Publish(new PickingLinesSortedEvent(
            request.PlanId, request.ActorId, siparisler.OrderId, satir.OrderItemId, bin2.Id, 1), ct);

        var siparisKalan = satirlar.Where(l => l.OrderId == siparisler.OrderId)
            .Sum(l => l.Quantity - l.SortedQuantity);
        var koliSiparis = await db.SortingBins.CountAsync(
            sb => sb.SortingBoxId == koli.Id, ct);

        return Result.Success(new SortingScanResultDto(
            koli.BoxNumber, siparisler.OrderNumber, siparisKalan, koliSiparis, yeniKoli));
    }
}
