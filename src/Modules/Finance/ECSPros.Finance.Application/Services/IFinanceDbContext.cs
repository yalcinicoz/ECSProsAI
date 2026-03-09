using ECSPros.Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Finance.Application.Services;

public interface IFinanceDbContext
{
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierInvoice> SupplierInvoices { get; }
    DbSet<SupplierInvoiceItem> SupplierInvoiceItems { get; }
    DbSet<SupplierDelivery> SupplierDeliveries { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<SupplierReturn> SupplierReturns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
