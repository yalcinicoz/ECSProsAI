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
    public DbSet<SortingEntry> SortingEntries => Set<SortingEntry>();
    public DbSet<MissingCardNotice> MissingCardNotices => Set<MissingCardNotice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // DataGrid: jsonb sözlük alanlarında filtre/sıralama (GridJson.Text → jsonb_extract_path_text,
        // yerleşik PG fonksiyonu). Şema değiştirmez, migration gerektirmez; kaydı olmayan DbContext'te
        // i18n ad üzerinden filtre/sıralama SQL'e çevrilemez (2026-09-09'da Core'da bu yaşandı).
        modelBuilder.HasDbFunction(ECSPros.Shared.Kernel.Grid.GridJson.TextMethod).HasName("jsonb_extract_path_text").IsBuiltIn();
        modelBuilder.HasDefaultSchema("procurement");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProcurementDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
