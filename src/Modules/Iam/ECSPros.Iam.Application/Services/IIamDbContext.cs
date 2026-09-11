using ECSPros.Iam.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECSPros.Iam.Application.Services;

public interface IIamDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserPermission> UserPermissions { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AdminMenu> AdminMenus { get; }
    DbSet<ApiClient> ApiClients { get; }
    DbSet<ApiClientType> ApiClientTypes { get; }
    DbSet<SupplierUser> SupplierUsers { get; }
    DbSet<SupplierUserSession> SupplierUserSessions { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<int> WritePreferenceAsync(Guid userId, string key, string? json, bool addOnly, CancellationToken ct)
        => throw new NotSupportedException("Atomic preference persistence is required.");
    Task<int> CompareExchangePreferenceAsync(Guid userId, string key, string expectedJson, string? nextJson, CancellationToken ct)
        => throw new NotSupportedException("Conditional preference persistence is required.");
}
