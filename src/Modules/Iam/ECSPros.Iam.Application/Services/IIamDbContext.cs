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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
