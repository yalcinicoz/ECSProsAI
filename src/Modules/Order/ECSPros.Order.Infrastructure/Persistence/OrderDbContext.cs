using ECSPros.Order.Application.Services;
using ECSPros.Order.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Infrastructure.Persistence;

public class OrderDbContext : DbContext, IOrderDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Domain.Entities.Order> Orders => Set<Domain.Entities.Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderDiscount> OrderDiscounts => Set<OrderDiscount>();
    public DbSet<OrderExpense> OrderExpenses => Set<OrderExpense>();
    public DbSet<OrderTax> OrderTaxes => Set<OrderTax>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();
    public DbSet<InvoiceSeriesCounter> InvoiceSeriesCounters => Set<InvoiceSeriesCounter>();
    public DbSet<ChannelInvoiceSettings> ChannelInvoiceSettings => Set<ChannelInvoiceSettings>();
    public DbSet<ChannelInvoiceSeriesBinding> ChannelInvoiceSeriesBindings => Set<ChannelInvoiceSeriesBinding>();
    public DbSet<InvoiceDispatch> InvoiceDispatches => Set<InvoiceDispatch>();
    public DbSet<OrderNumberSeries> OrderNumberSeries => Set<OrderNumberSeries>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<OrderConfirmation> OrderConfirmations => Set<OrderConfirmation>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<OrderNotification> OrderNotifications => Set<OrderNotification>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<GiftCardTransaction> GiftCardTransactions => Set<GiftCardTransaction>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<ReturnRefund> ReturnRefunds => Set<ReturnRefund>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
    public DbSet<OrderGift> OrderGifts => Set<OrderGift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // DataGrid: jsonb sözlük alanlarında filtre/sıralama (GridJson.Text → jsonb_extract_path_text, yerleşik PG fonksiyonu)
        modelBuilder.HasDbFunction(ECSPros.Shared.Kernel.Grid.GridJson.TextMethod).HasName("jsonb_extract_path_text").IsBuiltIn();
        // 15.4h: CustomerNotes.note — Dictionary<string, object> parametresi sağlayıcıda tipsiz kalır; mağaza tipi jsonb verilir.
        modelBuilder.HasDbFunction(ECSPros.Shared.Kernel.Grid.GridJson.TextObjMethod).HasName("jsonb_extract_path_text").IsBuiltIn()
            .HasParameter("dict").HasStoreType("jsonb");
        modelBuilder.HasDefaultSchema("order");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task<IOrderTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null) return JoinedTransactionScope.Instance;
        var tx = await Database.BeginTransactionAsync(cancellationToken);
        return new OwnedTransactionScope(tx);
    }

    private sealed class JoinedTransactionScope : IOrderTransactionScope
    {
        public static readonly JoinedTransactionScope Instance = new();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OwnedTransactionScope(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) : IOrderTransactionScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => tx.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
