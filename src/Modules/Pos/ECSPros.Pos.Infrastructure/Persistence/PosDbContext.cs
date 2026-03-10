using ECSPros.Pos.Application.Services;
using ECSPros.Pos.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Pos.Infrastructure.Persistence;

public class PosDbContext : DbContext, IPosDbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    public DbSet<PosRegister> PosRegisters => Set<PosRegister>();
    public DbSet<PosSession> PosSessions => Set<PosSession>();
    public DbSet<PosSessionTransaction> PosSessionTransactions => Set<PosSessionTransaction>();
    public DbSet<PosQuickProduct> PosQuickProducts => Set<PosQuickProduct>();
    public DbSet<PosSale> PosSales => Set<PosSale>();
    public DbSet<PosSaleItem> PosSaleItems => Set<PosSaleItem>();
    public DbSet<PosSalePayment> PosSalePayments => Set<PosSalePayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pos");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);
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
}
