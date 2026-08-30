using ECSPros.Iam.Application.Services;
using ECSPros.Iam.Domain.Entities;
using ECSPros.Shared.Kernel.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Infrastructure.Persistence;

public class IamDbContext : DbContext, IIamDbContext, IDataProtectionKeyContext
{
    public IamDbContext(DbContextOptions<IamDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AdminMenu> AdminMenus => Set<AdminMenu>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<ApiClientType> ApiClientTypes => Set<ApiClientType>();
    public DbSet<SupplierUser> SupplierUsers => Set<SupplierUser>();
    public DbSet<SupplierUserSession> SupplierUserSessions => Set<SupplierUserSession>();

    // FAZ 10 / A1: Data Protection key ring — düğümler arası ortak depo.
    // Anahtarlar DB yedeğiyle birlikte yedeklenir; ~/.ecspros/dp-keys dosya deposu
    // bir sürüm boyunca salt-okunur geri dönüş yolu olarak kalır (Program.cs).
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("iam");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IamDbContext).Assembly);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
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
