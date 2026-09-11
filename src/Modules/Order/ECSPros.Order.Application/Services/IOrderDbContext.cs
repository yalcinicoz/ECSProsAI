using ECSPros.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Services;

public interface IOrderDbContext
{
    DbSet<Domain.Entities.Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    IQueryable<OrderReportLine> ReportLines() => throw new NotSupportedException("Rapor satır kaynağı bu bağlamda desteklenmiyor.");
    IQueryable<Domain.Entities.Order> FilterOrdersByProduct(IQueryable<Domain.Entities.Order> query, string barcode, string productCode)
        => throw new NotSupportedException("Ürün filtresi bu bağlamda desteklenmiyor.");
    DbSet<OrderDiscount> OrderDiscounts { get; }
    DbSet<OrderExpense> OrderExpenses { get; }
    DbSet<OrderTax> OrderTaxes { get; }
    DbSet<OrderPayment> OrderPayments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<OrderConfirmation> OrderConfirmations { get; }
    DbSet<ShipmentItem> ShipmentItems { get; }
    DbSet<ShipmentEvent> ShipmentEvents { get; }
    DbSet<OrderNotification> OrderNotifications { get; }
    DbSet<Return> Returns { get; }
    DbSet<ReturnItem> ReturnItems { get; }
    DbSet<ReturnRefund> ReturnRefunds { get; }
    DbSet<InvoiceSeries> InvoiceSeries { get; }
    DbSet<InvoiceSeriesCounter> InvoiceSeriesCounters { get; }
    DbSet<ChannelInvoiceSettings> ChannelInvoiceSettings { get; }
    DbSet<ChannelInvoiceSeriesBinding> ChannelInvoiceSeriesBindings { get; }
    DbSet<InvoiceDispatch> InvoiceDispatches { get; }
    DbSet<OrderNumberSeries> OrderNumberSeries { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<QuoteItem> QuoteItems { get; }
    DbSet<GiftCard> GiftCards { get; }
    DbSet<GiftCardTransaction> GiftCardTransactions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Numara tahsisi + fatura yazımı gibi bölünmez adımlar için. Zaten açık bir
    /// transaction varsa ona katılır (Commit/Dispose no-op) — iç içe çağrılarda güvenli.</summary>
    Task<IOrderTransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IOrderTransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
