using ECSPros.Pos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Services;

public interface IPosDbContext
{
    DbSet<PosRegister> PosRegisters { get; }
    DbSet<PosSession> PosSessions { get; }
    DbSet<PosSessionTransaction> PosSessionTransactions { get; }
    DbSet<PosQuickProduct> PosQuickProducts { get; }
    DbSet<PosSale> PosSales { get; }
    DbSet<PosSaleItem> PosSaleItems { get; }
    DbSet<PosSalePayment> PosSalePayments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
