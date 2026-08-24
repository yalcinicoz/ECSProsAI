using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using ECSPros.Shared.Kernel.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Commands.UpsertPurchaseOrderItems;

public record PurchaseOrderItemInput(
    Guid? Id,               // null → yeni kalem
    Guid? VariantId,
    string? ModelText,
    string? ColorText,
    string? SizeText,
    decimal Quantity,
    decimal UnitPrice,
    string? Notes);

/// <summary>
/// Kalem ekle/güncelle (K4: form satırı + Excel'den panoya yapıştırma aynı ucu kullanır — yapıştırma
/// yalnız yeni kalemler üretir). Append semantiği: Id'siz girdiler eklenir, Id'liler güncellenir;
/// listede olmayan mevcut kalemler SİLİNMEZ (silme ayrı komut).
/// </summary>
public record UpsertPurchaseOrderItemsCommand(Guid PurchaseOrderId, List<PurchaseOrderItemInput> Items)
    : IRequest<Result<int>>;

public class UpsertPurchaseOrderItemsCommandHandler(IProcurementDbContext db)
    : IRequestHandler<UpsertPurchaseOrderItemsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpsertPurchaseOrderItemsCommand request, CancellationToken ct)
    {
        var po = await db.PurchaseOrders.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, ct);
        if (po is null) return Result.Failure<int>("Satın alma bulunamadı.");
        if (po.Status is "closed" or "cancelled") return Result.Failure<int>("Kapalı/iptal satın almaya kalem eklenemez.");

        var n = 0;
        var maxSort = po.Items.Count == 0 ? 0 : po.Items.Max(i => i.SortOrder);
        foreach (var input in request.Items)
        {
            if (input.Quantity <= 0) return Result.Failure<int>("Adet 0'dan büyük olmalı.");
            if (input.UnitPrice < 0) return Result.Failure<int>("Birim fiyat negatif olamaz.");
            var hasIdentity = input.VariantId.HasValue
                || !string.IsNullOrWhiteSpace(input.ModelText)
                || !string.IsNullOrWhiteSpace(input.ColorText)
                || !string.IsNullOrWhiteSpace(input.SizeText);
            if (!hasIdentity) return Result.Failure<int>("Kalemde en az model/renk/beden metni ya da varyant olmalı.");

            if (input.Id.HasValue)
            {
                var item = po.Items.FirstOrDefault(i => i.Id == input.Id.Value);
                if (item is null) return Result.Failure<int>("Kalem bulunamadı.");
                item.VariantId = input.VariantId;
                item.ModelText = input.ModelText?.Trim();
                item.ColorText = input.ColorText?.Trim();
                item.SizeText = input.SizeText?.Trim();
                item.Quantity = input.Quantity;
                item.UnitPrice = input.UnitPrice;
                item.Notes = input.Notes?.Trim();
                item.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.PurchaseOrderItems.Add(new PurchaseOrderItem
                {
                    PurchaseOrderId = po.Id,
                    VariantId = input.VariantId,
                    ModelText = input.ModelText?.Trim(),
                    ColorText = input.ColorText?.Trim(),
                    SizeText = input.SizeText?.Trim(),
                    Quantity = input.Quantity,
                    UnitPrice = input.UnitPrice,
                    Notes = input.Notes?.Trim(),
                    SortOrder = ++maxSort,
                });
            }
            n++;
        }
        po.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(n);
    }
}
