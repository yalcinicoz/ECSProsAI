using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.SetReceiptBatchStatus;

/// <summary>received → sorting → completed; completed → sorting (geri aç). Elle yönetilir (İ4).</summary>
public record SetReceiptBatchStatusCommand(Guid Id, string Status) : IRequest<Result<bool>>;

public class SetReceiptBatchStatusCommandHandler(IProcurementDbContext db)
    : IRequestHandler<SetReceiptBatchStatusCommand, Result<bool>>
{
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["received"] = ["sorting"],
        ["sorting"] = ["completed", "received"],
        ["completed"] = ["sorting"],
    };

    public async Task<Result<bool>> Handle(SetReceiptBatchStatusCommand request, CancellationToken ct)
    {
        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (!ReceiptBatch.Statuses.Contains(status)) return Result.Failure<bool>("Geçersiz durum.");
        var batch = await db.ReceiptBatches.FirstOrDefaultAsync(b => b.Id == request.Id, ct);
        if (batch is null) return Result.Failure<bool>("Parti bulunamadı.");
        if (!Allowed.TryGetValue(batch.Status, out var next) || !next.Contains(status))
            return Result.Failure<bool>($"'{batch.Status}' durumundan '{status}' durumuna geçilemez.");
        batch.Status = status;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
