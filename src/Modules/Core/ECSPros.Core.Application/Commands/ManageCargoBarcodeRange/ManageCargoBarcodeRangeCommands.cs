using ECSPros.Core.Application.Services;
using ECSPros.Core.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Core.Application.Commands.ManageCargoBarcodeRange;

// ── Listeleme ──────────────────────────────────────────────────────────────────

public record GetCargoBarcodeRangesQuery(Guid? FirmPlatformIntegrationId = null)
    : IRequest<Result<List<CargoBarcodeRangeDto>>>;

public record CargoBarcodeRangeDto(
    Guid Id,
    Guid FirmPlatformIntegrationId,
    long RangeStart,
    long RangeEnd,
    long NextValue,
    bool IsActive,
    DateTime? ExhaustedAt,
    long Total,
    long Used); // doluluk göstergesi: Used/Total

public class GetCargoBarcodeRangesQueryHandler
    : IRequestHandler<GetCargoBarcodeRangesQuery, Result<List<CargoBarcodeRangeDto>>>
{
    private readonly ICoreDbContext _db;
    public GetCargoBarcodeRangesQueryHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<List<CargoBarcodeRangeDto>>> Handle(
        GetCargoBarcodeRangesQuery request, CancellationToken ct)
    {
        var query = _db.CargoBarcodeRanges.AsNoTracking();
        if (request.FirmPlatformIntegrationId is { } id)
            query = query.Where(r => r.FirmPlatformIntegrationId == id);

        var list = await query
            .OrderBy(r => r.FirmPlatformIntegrationId).ThenBy(r => r.RangeStart)
            .Select(r => new CargoBarcodeRangeDto(
                r.Id, r.FirmPlatformIntegrationId, r.RangeStart, r.RangeEnd, r.NextValue,
                r.IsActive, r.ExhaustedAt,
                r.RangeEnd - r.RangeStart + 1,
                r.NextValue - r.RangeStart))
            .ToListAsync(ct);

        return Result.Success(list);
    }
}

// ── Oluşturma ──────────────────────────────────────────────────────────────────

public record CreateCargoBarcodeRangeCommand(
    Guid FirmPlatformIntegrationId,
    long RangeStart,
    long RangeEnd) : IRequest<Result<Guid>>;

public class CreateCargoBarcodeRangeCommandHandler
    : IRequestHandler<CreateCargoBarcodeRangeCommand, Result<Guid>>
{
    private readonly ICoreDbContext _db;
    public CreateCargoBarcodeRangeCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateCargoBarcodeRangeCommand request, CancellationToken ct)
    {
        if (request.RangeStart <= 0 || request.RangeEnd < request.RangeStart)
            return Result.Failure<Guid>("Geçersiz aralık: başlangıç pozitif, bitiş başlangıçtan küçük olmamalı.");

        var entegrasyon = await _db.FirmPlatformIntegrations
            .Include(i => i.IntegrationService)
            .FirstOrDefaultAsync(i => i.Id == request.FirmPlatformIntegrationId, ct);
        if (entegrasyon is null)
            return Result.Failure<Guid>("Kargo entegrasyonu bulunamadı.");
        if (entegrasyon.IntegrationService.ServiceType != "cargo")
            return Result.Failure<Guid>("Barkod aralığı yalnız kargo entegrasyonlarına tanımlanabilir.");

        // Aynı entegrasyonda çakışan aralık engellenir (aynı barkod iki kez tahsis edilemez)
        var cakisan = await _db.CargoBarcodeRanges.AnyAsync(r =>
            r.FirmPlatformIntegrationId == request.FirmPlatformIntegrationId &&
            r.RangeStart <= request.RangeEnd && request.RangeStart <= r.RangeEnd, ct);
        if (cakisan)
            return Result.Failure<Guid>("Bu entegrasyonda verilen aralıkla çakışan bir aralık zaten tanımlı.");

        var range = new CargoBarcodeRange
        {
            FirmPlatformIntegrationId = request.FirmPlatformIntegrationId,
            RangeStart = request.RangeStart,
            RangeEnd = request.RangeEnd,
            NextValue = request.RangeStart,
            IsActive = true
        };
        _db.CargoBarcodeRanges.Add(range);
        await _db.SaveChangesAsync(ct);
        return Result.Success(range.Id);
    }
}

// ── Aktif/Pasif ────────────────────────────────────────────────────────────────

/// <summary>Aralık yalnız aktif/pasif yapılabilir; sınırlar ve sayaç DEĞİŞTİRİLEMEZ —
/// tahsis edilen barkod havuza geri dönmez (karar 2026-07-19).</summary>
public record SetCargoBarcodeRangeActiveCommand(Guid Id, bool IsActive) : IRequest<Result<bool>>;

public class SetCargoBarcodeRangeActiveCommandHandler
    : IRequestHandler<SetCargoBarcodeRangeActiveCommand, Result<bool>>
{
    private readonly ICoreDbContext _db;
    public SetCargoBarcodeRangeActiveCommandHandler(ICoreDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(SetCargoBarcodeRangeActiveCommand request, CancellationToken ct)
    {
        var range = await _db.CargoBarcodeRanges.FirstOrDefaultAsync(r => r.Id == request.Id, ct);
        if (range is null)
            return Result.Failure<bool>("Barkod aralığı bulunamadı.");

        range.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
