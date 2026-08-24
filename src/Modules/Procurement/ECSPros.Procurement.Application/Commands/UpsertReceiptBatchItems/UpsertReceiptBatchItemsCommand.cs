using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.UpsertReceiptBatchItems;

public record ReceiptBatchItemInput(Guid? Id, string DescriptionText, decimal? Quantity, decimal? UnitPrice);

/// <summary>Kaba evrak kalemleri (opsiyonel; yalnız mutabakat girdisi). Append semantiği (SA kalemleriyle aynı).</summary>
public record UpsertReceiptBatchItemsCommand(Guid ReceiptBatchId, List<ReceiptBatchItemInput> Items)
    : IRequest<Result<int>>;

public class UpsertReceiptBatchItemsCommandHandler(IProcurementDbContext db)
    : IRequestHandler<UpsertReceiptBatchItemsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpsertReceiptBatchItemsCommand request, CancellationToken ct)
    {
        var batch = await db.ReceiptBatches.Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == request.ReceiptBatchId, ct);
        if (batch is null) return Result.Failure<int>("Parti bulunamadı.");
        if (batch.Status == "completed") return Result.Failure<int>("Tamamlanmış partiye kalem eklenemez (önce Geri Aç).");

        var n = 0;
        var maxSort = batch.Items.Count == 0 ? 0 : batch.Items.Max(i => i.SortOrder);
        foreach (var input in request.Items)
        {
            if (string.IsNullOrWhiteSpace(input.DescriptionText)) return Result.Failure<int>("Kalem açıklaması boş olamaz.");
            if (input.Quantity is <= 0) return Result.Failure<int>("Adet verilirse 0'dan büyük olmalı.");
            if (input.UnitPrice is < 0) return Result.Failure<int>("Birim fiyat negatif olamaz.");
            if (input.Id.HasValue)
            {
                var item = batch.Items.FirstOrDefault(i => i.Id == input.Id.Value);
                if (item is null) return Result.Failure<int>("Kalem bulunamadı.");
                item.DescriptionText = input.DescriptionText.Trim();
                item.Quantity = input.Quantity;
                item.UnitPrice = input.UnitPrice;
                item.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.ReceiptBatchItems.Add(new ReceiptBatchItem
                {
                    ReceiptBatchId = batch.Id,
                    DescriptionText = input.DescriptionText.Trim(),
                    Quantity = input.Quantity,
                    UnitPrice = input.UnitPrice,
                    SortOrder = ++maxSort,
                });
            }
            n++;
        }
        batch.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(n);
    }
}
