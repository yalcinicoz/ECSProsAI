using ECSPros.Procurement.Application.Services;
using ECSPros.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Procurement.Infrastructure.Persistence;

public class ProcurementDbContext(DbContextOptions<ProcurementDbContext> options)
    : DbContext(options), IProcurementDbContext
{
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<ReceiptBatch> ReceiptBatches => Set<ReceiptBatch>();
    public DbSet<ReceiptBatchItem> ReceiptBatchItems => Set<ReceiptBatchItem>();
    public DbSet<ReceiptBatchPurchaseOrder> ReceiptBatchPurchaseOrders => Set<ReceiptBatchPurchaseOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("procurement");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcurementDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
