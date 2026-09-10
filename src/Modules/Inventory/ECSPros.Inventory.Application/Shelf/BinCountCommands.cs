using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Shelf;

// ─── R5 Raf Sayımı: Start → Scan* → Finish → Apply | Cancel ───────────────────

/// <summary>Göz için sayım oturumu açar; açık oturum varsa onu döner (idempotent). Beklenen = o anki sistem adedi.</summary>
public record StartBinCountCommand(Guid BinId, Guid? StartedBy) : IRequest<Result<Guid>>;

public class StartBinCountCommandHandler(IInventoryDbContext db) : IRequestHandler<StartBinCountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StartBinCountCommand request, CancellationToken ct)
    {
        var bin = await ShelfOps.FindBinAsync(db, request.BinId, null, ct);
        if (bin is null) return Result.Failure<Guid>("Göz bulunamadı.");
        var acik = await db.BinCounts.FirstOrDefaultAsync(c => c.BinId == bin.Bin.Id && c.Status == BinCountStatus.Open, ct);
        if (acik is not null) return Result.Success(acik.Id);

        var stoklar = await db.Stocks.AsNoTracking().Where(s => s.BinId == bin.Bin.Id && s.Quantity > 0)
            .Select(s => new { s.VariantId, s.Quantity }).ToListAsync(ct);
        var sayim = new BinCount
        {
            WarehouseId = bin.Warehouse.Id, BinId = bin.Bin.Id, Status = BinCountStatus.Open,
            StartedBy = request.StartedBy, StartedAt = DateTime.UtcNow, ExpectedTotal = stoklar.Sum(s => s.Quantity),
            CreatedBy = request.StartedBy,
        };
        foreach (var s in stoklar)
            sayim.Lines.Add(new BinCountLine { VariantId = s.VariantId, ExpectedQuantity = s.Quantity, CountedQuantity = 0 });
        db.BinCounts.Add(sayim);
        await db.SaveChangesAsync(ct);
        return Result.Success(sayim.Id);
    }
}

/// <summary>Ürün okutma: sayılan adet <paramref name="Delta"/> kadar değişir (−1 = geri al; 0'ın altına inmez).
/// Beklenen listede olmayan ürün "beklenmeyen" satır olarak açılır (beklenen 0).</summary>
public record ScanBinCountCommand(Guid CountId, Guid VariantId, int Delta = 1) : IRequest<Result<BinCountLineDto>>;

public class ScanBinCountCommandHandler(IInventoryDbContext db) : IRequestHandler<ScanBinCountCommand, Result<BinCountLineDto>>
{
    public async Task<Result<BinCountLineDto>> Handle(ScanBinCountCommand request, CancellationToken ct)
    {
        if (request.Delta == 0) return Result.Failure<BinCountLineDto>("Delta 0 olamaz.");
        var sayim = await db.BinCounts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CountId, ct);
        if (sayim is null) return Result.Failure<BinCountLineDto>("Sayım oturumu bulunamadı.");
        if (sayim.Status != BinCountStatus.Open) return Result.Failure<BinCountLineDto>("Sayım oturumu açık değil.");

        var satir = sayim.Lines.FirstOrDefault(l => l.VariantId == request.VariantId);
        if (satir is null)
        {
            if (request.Delta < 0) return Result.Failure<BinCountLineDto>("Bu ürün henüz sayılmadı.");
            satir = new BinCountLine { VariantId = request.VariantId, ExpectedQuantity = 0, CountedQuantity = 0 };
            sayim.Lines.Add(satir);
        }
        var yeni = satir.CountedQuantity + request.Delta;
        if (yeni < 0) return Result.Failure<BinCountLineDto>("Sayılan adet 0'ın altına inemez.");
        satir.CountedQuantity = yeni;
        sayim.CountedTotal = sayim.Lines.Sum(l => l.CountedQuantity);
        sayim.DiffTotal = sayim.CountedTotal - sayim.ExpectedTotal;
        sayim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(new BinCountLineDto(satir.Id, satir.VariantId, satir.ExpectedQuantity, satir.CountedQuantity, satir.Diff, null, null, null, null));
    }
}

/// <summary>Sayımı bitirir (fark raporu kesinleşir; uygulanmadı).</summary>
public record FinishBinCountCommand(Guid CountId, string? Notes) : IRequest<Result<bool>>;

public class FinishBinCountCommandHandler(IInventoryDbContext db) : IRequestHandler<FinishBinCountCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(FinishBinCountCommand request, CancellationToken ct)
    {
        var sayim = await db.BinCounts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CountId, ct);
        if (sayim is null) return Result.Failure<bool>("Sayım oturumu bulunamadı.");
        if (sayim.Status != BinCountStatus.Open) return Result.Failure<bool>("Yalnız açık sayım bitirilebilir.");
        sayim.Status = BinCountStatus.Finished;
        sayim.FinishedAt = DateTime.UtcNow;
        sayim.CountedTotal = sayim.Lines.Sum(l => l.CountedQuantity);
        sayim.DiffTotal = sayim.CountedTotal - sayim.ExpectedTotal;
        if (!string.IsNullOrWhiteSpace(request.Notes)) sayim.Notes = request.Notes;
        sayim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

/// <summary>Farkı stoğa uygular (yetki: inventory.count.apply). Eksikler rezerve adedin altına düşüremez — o durumda
/// hiçbir satır uygulanmaz, ürünler listelenir. Hareket: <c>adjustment</c>, ReferenceType=bin_count.</summary>
public record ApplyBinCountCommand(Guid CountId, Guid AppliedBy) : IRequest<Result<int>>;

public class ApplyBinCountCommandHandler(IInventoryDbContext db) : IRequestHandler<ApplyBinCountCommand, Result<int>>
{
    public async Task<Result<int>> Handle(ApplyBinCountCommand request, CancellationToken ct)
    {
        var sayim = await db.BinCounts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CountId, ct);
        if (sayim is null) return Result.Failure<int>("Sayım oturumu bulunamadı.");
        if (sayim.Status != BinCountStatus.Finished) return Result.Failure<int>("Yalnız bitirilmiş sayım uygulanabilir.");
        var bin = await ShelfOps.FindBinAsync(db, sayim.BinId, null, ct);
        if (bin is null) return Result.Failure<int>("Göz bulunamadı.");
        if (ShelfOps.YazilabilirMi(bin) is { } hata) return Result.Failure<int>(hata);

        var farklilar = sayim.Lines.Where(l => l.Diff != 0).ToList();
        if (farklilar.Count == 0)
        {
            sayim.Status = BinCountStatus.Applied; sayim.AppliedAt = DateTime.UtcNow; sayim.AppliedBy = request.AppliedBy;
            await db.SaveChangesAsync(ct);
            return Result.Success(0);
        }

        string? err = null; var uygulanan = 0;
        await StockTx.RunAsync(db, farklilar.Select(l => l.VariantId), async () =>
        {
            var b = (await ShelfOps.FindBinAsync(db, sayim.BinId, null, ct))!;
            var s2 = await db.BinCounts.Include(c => c.Lines).FirstAsync(c => c.Id == request.CountId, ct);
            var engeller = new List<string>();
            var planlar = new List<(Stock St, int Diff, Guid VariantId)>();
            foreach (var l in s2.Lines.Where(l => l.Diff != 0))
            {
                var st = await ShelfOps.GetOrCreateAsync(db, l.VariantId, b, ct);
                var hedef = l.CountedQuantity;   // sayılan = yeni sistem adedi (beklenen sayım anındaki değerdi; ara hareket olduysa fark yine sayılan üstünden)
                var diff = hedef - st.Quantity;
                if (hedef < st.ReservedQuantity) { engeller.Add($"{l.VariantId}: sayılan {hedef} < rezerve {st.ReservedQuantity}"); continue; }
                if (diff != 0) planlar.Add((st, diff, l.VariantId));
            }
            if (engeller.Count > 0) { err = "Rezerve adedin altına düşürülemez — önce rezervasyonlar çözülmeli: " + string.Join("; ", engeller.Take(5)); return; }
            foreach (var (st, diff, vid) in planlar)
            {
                st.Quantity += diff; st.UpdatedAt = DateTime.UtcNow;
                db.StockMovements.Add(ShelfOps.Hareket(vid, StokHareketTipi.Adjustment, Math.Abs(diff),
                    diff < 0 ? b.Warehouse.Id : null, diff < 0 ? b.Bin.Id : null, diff > 0 ? b.Warehouse.Id : null, diff > 0 ? b.Bin.Id : null,
                    "bin_count", s2.Id, $"Raf sayımı farkı ({(diff > 0 ? "+" : "")}{diff})", request.AppliedBy));
                ShelfOps.BosSatiriKaldir(db, st);
                uygulanan++;
            }
            s2.Status = BinCountStatus.Applied; s2.AppliedAt = DateTime.UtcNow; s2.AppliedBy = request.AppliedBy; s2.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }, ct);
        return err is null ? Result.Success(uygulanan) : Result.Failure<int>(err);
    }
}

public record CancelBinCountCommand(Guid CountId, string? Reason) : IRequest<Result<bool>>;

public class CancelBinCountCommandHandler(IInventoryDbContext db) : IRequestHandler<CancelBinCountCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(CancelBinCountCommand request, CancellationToken ct)
    {
        var sayim = await db.BinCounts.FirstOrDefaultAsync(c => c.Id == request.CountId, ct);
        if (sayim is null) return Result.Failure<bool>("Sayım oturumu bulunamadı.");
        if (sayim.Status is not (BinCountStatus.Open or BinCountStatus.Finished)) return Result.Failure<bool>("Bu durumda iptal edilemez.");
        sayim.Status = BinCountStatus.Cancelled; sayim.Notes = request.Reason ?? sayim.Notes; sayim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
