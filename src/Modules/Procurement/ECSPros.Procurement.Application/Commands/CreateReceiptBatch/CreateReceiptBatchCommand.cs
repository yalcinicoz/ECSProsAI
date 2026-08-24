using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.CreateReceiptBatch;

/// <summary>Parti açılışı: KALEM BİLGİSİ ZORUNSUZ (İ2) — koli geldi, kayıt açıldı, ayrıştırma başlayabilir.</summary>
public record CreateReceiptBatchCommand(
    Guid SupplierId,
    Guid WarehouseId,
    DateTime? ReceivedAt,
    int? PackageCount,
    string? DeliveryNoteNumber,
    string? Notes,
    Guid? ReceivedBy) : IRequest<Result<Guid>>;

public class CreateReceiptBatchCommandHandler(IProcurementDbContext db)
    : IRequestHandler<CreateReceiptBatchCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateReceiptBatchCommand request, CancellationToken ct)
    {
        if (request.PackageCount is < 0) return Result.Failure<Guid>("Koli sayısı negatif olamaz.");
        var prefix = $"MK-{DateTime.UtcNow:yyyyMMdd}-";
        var count = await db.ReceiptBatches.CountAsync(b => b.Code.StartsWith(prefix), ct);
        var batch = new ReceiptBatch
        {
            Code = $"{prefix}{count + 1:D4}",
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            ReceivedAt = request.ReceivedAt ?? DateTime.UtcNow,
            PackageCount = request.PackageCount,
            DeliveryNoteNumber = request.DeliveryNoteNumber?.Trim(),
            Notes = request.Notes?.Trim(),
            ReceivedBy = request.ReceivedBy,
            Status = "received",
        };
        db.ReceiptBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return Result.Success(batch.Id);
    }
}
