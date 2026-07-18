using ECSPros.Inventory.Application.Services;
using ECSPros.Inventory.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Inventory.Application.Commands.ManageWarehouseSections;

// ── Kısım (Section) ─────────────────────────────────────────────────────────────

public record CreateWarehouseSectionCommand(
    Guid WarehouseId, string Code, string Name,
    bool IsSellableOnline = true, int PickingOrder = 0) : IRequest<Result<Guid>>;

public record UpdateWarehouseSectionCommand(
    Guid Id, string Name, bool IsSellableOnline, bool IsActive, int PickingOrder) : IRequest<Result<bool>>;

// ── Birim/Raf (Bin) ─────────────────────────────────────────────────────────────

public record CreateWarehouseBinCommand(
    Guid SectionId, string Code, string Barcode, string? Name = null) : IRequest<Result<Guid>>;

public record UpdateWarehouseBinCommand(
    Guid Id, string? Name, string Barcode, bool IsActive) : IRequest<Result<bool>>;

public class CreateWarehouseSectionCommandHandler : IRequestHandler<CreateWarehouseSectionCommand, Result<Guid>>
{
    private readonly IInventoryDbContext _db;
    public CreateWarehouseSectionCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateWarehouseSectionCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
            return Result.Failure<Guid>("Kısım kodu ve adı zorunludur.");
        if (!await _db.Warehouses.AnyAsync(w => w.Id == r.WarehouseId, ct))
            return Result.Failure<Guid>("Depo bulunamadı.");
        if (await _db.WarehouseSections.AnyAsync(s => s.WarehouseId == r.WarehouseId && s.Code == r.Code, ct))
            return Result.Failure<Guid>($"'{r.Code}' kodlu kısım bu depoda zaten var.");

        var section = new WarehouseSection
        {
            WarehouseId = r.WarehouseId,
            Code = r.Code.Trim(),
            Name = r.Name.Trim(),
            IsSellableOnline = r.IsSellableOnline,
            PickingOrder = r.PickingOrder,
            IsActive = true
        };
        _db.WarehouseSections.Add(section);
        await _db.SaveChangesAsync(ct);
        return Result.Success(section.Id);
    }
}

public class UpdateWarehouseSectionCommandHandler : IRequestHandler<UpdateWarehouseSectionCommand, Result<bool>>
{
    private readonly IInventoryDbContext _db;
    public UpdateWarehouseSectionCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateWarehouseSectionCommand r, CancellationToken ct)
    {
        var section = await _db.WarehouseSections.FirstOrDefaultAsync(s => s.Id == r.Id, ct);
        if (section is null)
            return Result.Failure<bool>("Kısım bulunamadı.");
        if (string.IsNullOrWhiteSpace(r.Name))
            return Result.Failure<bool>("Kısım adı zorunludur.");

        section.Name = r.Name.Trim();
        section.IsSellableOnline = r.IsSellableOnline;   // site stok görünürlüğünün yönetim noktası
        section.IsActive = r.IsActive;
        section.PickingOrder = r.PickingOrder;
        section.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

public class CreateWarehouseBinCommandHandler : IRequestHandler<CreateWarehouseBinCommand, Result<Guid>>
{
    private readonly IInventoryDbContext _db;
    public CreateWarehouseBinCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateWarehouseBinCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Barcode))
            return Result.Failure<Guid>("Birim kodu ve barkodu zorunludur.");
        var section = await _db.WarehouseSections.FirstOrDefaultAsync(s => s.Id == r.SectionId, ct);
        if (section is null)
            return Result.Failure<Guid>("Kısım bulunamadı.");
        if (await _db.WarehouseBins.AnyAsync(b => b.SectionId == r.SectionId && b.Code == r.Code, ct))
            return Result.Failure<Guid>($"'{r.Code}' kodlu birim bu kısımda zaten var.");
        if (await _db.WarehouseBins.AnyAsync(b => b.Barcode == r.Barcode, ct))
            return Result.Failure<Guid>($"'{r.Barcode}' barkodu başka bir birimde kayıtlı.");

        var bin = new WarehouseBin
        {
            SectionId = r.SectionId,
            Code = r.Code.Trim(),
            Barcode = r.Barcode.Trim(),
            Name = string.IsNullOrWhiteSpace(r.Name) ? null : r.Name.Trim(),
            IsActive = true
        };
        _db.WarehouseBins.Add(bin);
        await _db.SaveChangesAsync(ct);
        return Result.Success(bin.Id);
    }
}

public class UpdateWarehouseBinCommandHandler : IRequestHandler<UpdateWarehouseBinCommand, Result<bool>>
{
    private readonly IInventoryDbContext _db;
    public UpdateWarehouseBinCommandHandler(IInventoryDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(UpdateWarehouseBinCommand r, CancellationToken ct)
    {
        var bin = await _db.WarehouseBins.FirstOrDefaultAsync(b => b.Id == r.Id, ct);
        if (bin is null)
            return Result.Failure<bool>("Birim bulunamadı.");
        if (string.IsNullOrWhiteSpace(r.Barcode))
            return Result.Failure<bool>("Barkod zorunludur.");
        if (await _db.WarehouseBins.AnyAsync(b => b.Barcode == r.Barcode && b.Id != r.Id, ct))
            return Result.Failure<bool>($"'{r.Barcode}' barkodu başka bir birimde kayıtlı.");

        bin.Name = string.IsNullOrWhiteSpace(r.Name) ? null : r.Name.Trim();
        bin.Barcode = r.Barcode.Trim();
        bin.IsActive = r.IsActive;
        bin.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
