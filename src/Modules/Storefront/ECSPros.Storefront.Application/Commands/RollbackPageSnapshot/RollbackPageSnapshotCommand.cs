using ECSPros.Shared.Kernel.Common;
using ECSPros.Storefront.Application.Services;
using ECSPros.Storefront.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Storefront.Application.Commands.RollbackPageSnapshot;

/// <summary>
/// G4/G6: rollback — hedef versiyonun snapshot'ını yeniden aktif eder (spec: v5 sorunlu
/// → v4 tekrar aktif; JsonData asla değiştirilmez, yeni versiyon üretilmez). Mevcut
/// aktif rolledback olur; işlem publish_logs'a status=rollback yazılır.
/// </summary>
public record RollbackPageSnapshotCommand(
    Guid FirmPlatformId,
    int TargetVersion,
    Guid? PublishedBy = null,
    string? Note = null) : IRequest<Result>;

public class RollbackPageSnapshotCommandHandler(IStorefrontDbContext db)
    : IRequestHandler<RollbackPageSnapshotCommand, Result>
{
    public async Task<Result> Handle(RollbackPageSnapshotCommand request, CancellationToken ct)
    {
        var hedef = await db.PublishedSnapshots.FirstOrDefaultAsync(
            s => s.FirmPlatformId == request.FirmPlatformId && s.Version == request.TargetVersion, ct);
        if (hedef is null)
            return Result.Failure($"v{request.TargetVersion} bulunamadı.");
        if (hedef.IsActive)
            return Result.Failure($"v{request.TargetVersion} zaten aktif yayın.");

        var aktif = await db.PublishedSnapshots.FirstOrDefaultAsync(
            s => s.FirmPlatformId == request.FirmPlatformId && s.IsActive, ct);
        if (aktif is not null)
        {
            aktif.IsActive = false;
            aktif.Status = "rolledback";
        }

        hedef.IsActive = true;
        hedef.Status = "published";

        db.PublishLogs.Add(new PublishLog
        {
            FirmPlatformId = request.FirmPlatformId,
            Version = request.TargetVersion,
            PreviousVersion = aktif?.Version,
            PublishedBy = request.PublishedBy,
            PublishedAt = DateTime.UtcNow,
            Status = "rollback",
            Note = request.Note,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
