using ECSPros.Order.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Order.Application.Services;

public interface IOrderDbContext
{
    DbSet<Domain.Entities.Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderDiscount> OrderDiscounts { get; }
    DbSet<OrderExpense> OrderExpenses { get; }
    DbSet<OrderTax> OrderTaxes { get; }
    DbSet<OrderPayment> OrderPayments { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentItem> ShipmentItems { get; }
    DbSet<Return> Returns { get; }
    DbSet<ReturnItem> ReturnItems { get; }
    DbSet<ReturnRefund> ReturnRefunds { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<QuoteItem> QuoteItems { get; }
    DbSet<GiftCard> GiftCards { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
