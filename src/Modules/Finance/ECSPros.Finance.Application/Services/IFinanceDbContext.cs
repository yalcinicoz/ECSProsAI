using ECSPros.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Services;

public interface IFinanceDbContext
{
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierInvoice> SupplierInvoices { get; }
    DbSet<SupplierInvoiceItem> SupplierInvoiceItems { get; }
    DbSet<SupplierDelivery> SupplierDeliveries { get; }
    DbSet<SupplierDeliveryItem> SupplierDeliveryItems { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<SupplierReturn> SupplierReturns { get; }
    DbSet<SupplierReturnItem> SupplierReturnItems { get; }
    DbSet<SupplierTransaction> SupplierTransactions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
