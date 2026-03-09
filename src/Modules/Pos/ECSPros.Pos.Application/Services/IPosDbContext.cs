using ECSPros.Pos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Application.Services;

public interface IPosDbContext
{
    DbSet<PosRegister> PosRegisters { get; }
    DbSet<PosSession> PosSessions { get; }
    DbSet<PosSessionTransaction> PosSessionTransactions { get; }
    DbSet<PosQuickProduct> PosQuickProducts { get; }
    DbSet<PosReceipt> PosReceipts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
