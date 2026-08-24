using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.DeleteReceiptBatchItem;

public record DeleteReceiptBatchItemCommand(Guid ReceiptBatchId, Guid ItemId) : IRequest<Result<bool>>;

public class DeleteReceiptBatchItemCommandHandler(IProcurementDbContext db)
    : IRequestHandler<DeleteReceiptBatchItemCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteReceiptBatchItemCommand request, CancellationToken ct)
    {
        var batch = await db.ReceiptBatches.FirstOrDefaultAsync(b => b.Id == request.ReceiptBatchId, ct);
        if (batch is null) return Result.Failure<bool>("Parti bulunamadı.");
        if (batch.Status == "completed") return Result.Failure<bool>("Tamamlanmış partiden kalem silinemez (önce Geri Aç).");
        var item = await db.ReceiptBatchItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.ReceiptBatchId == request.ReceiptBatchId, ct);
        if (item is null) return Result.Failure<bool>("Kalem bulunamadı.");
        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
