using ECSPros.Procurement.Application.Services;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.UpdateReceiptBatch;

/// <summary>Başlık günceller; SupplierInvoiceId gevşek fatura bağıdır (bağ kur/söz — null=çöz).</summary>
public record UpdateReceiptBatchCommand(
    Guid Id,
    DateTime? ReceivedAt,
    int? PackageCount,
    string? DeliveryNoteNumber,
    Guid? SupplierInvoiceId,
    string? Notes) : IRequest<Result<bool>>;

public class UpdateReceiptBatchCommandHandler(IProcurementDbContext db)
    : IRequestHandler<UpdateReceiptBatchCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UpdateReceiptBatchCommand request, CancellationToken ct)
    {
        var batch = await db.ReceiptBatches.FirstOrDefaultAsync(b => b.Id == request.Id, ct);
        if (batch is null) return Result.Failure<bool>("Parti bulunamadı.");
        if (batch.Status == "completed") return Result.Failure<bool>("Tamamlanmış parti düzenlenemez (önce Geri Aç).");
        if (request.PackageCount is < 0) return Result.Failure<bool>("Koli sayısı negatif olamaz.");
        if (request.ReceivedAt.HasValue) batch.ReceivedAt = request.ReceivedAt.Value;
        batch.PackageCount = request.PackageCount;
        batch.DeliveryNoteNumber = request.DeliveryNoteNumber?.Trim();
        batch.SupplierInvoiceId = request.SupplierInvoiceId;
        batch.Notes = request.Notes?.Trim();
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
