using ECSPros.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Application.Services;

public interface IProcurementDbContext
{
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
    DbSet<ReceiptBatch> ReceiptBatches { get; }
    DbSet<ReceiptBatchItem> ReceiptBatchItems { get; }
    DbSet<ReceiptBatchPurchaseOrder> ReceiptBatchPurchaseOrders { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
