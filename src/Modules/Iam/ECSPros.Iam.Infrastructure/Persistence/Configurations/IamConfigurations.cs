using ECSPros.Iam.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECSPros.Iam.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("iam_users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.Department).HasMaxLength(100);
        builder.Property(x => x.JobTitle).HasMaxLength(200);
        builder.Property(x => x.Preferences).HasColumnType("jsonb");
        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.UserRoles).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        builder.HasMany(x => x.UserPermissions).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        builder.HasMany(x => x.Sessions).WithOne(x => x.User).HasForeignKey(x => x.UserId);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("iam_roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.RolePermissions).WithOne(x => x.Role).HasForeignKey(x => x.RoleId);
        builder.HasMany(x => x.UserRoles).WithOne(x => x.Role).HasForeignKey(x => x.RoleId);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("iam_permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DescriptionI18n).HasColumnType("jsonb");
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PermissionType).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.RolePermissions).WithOne(x => x.Permission).HasForeignKey(x => x.PermissionId);
        builder.HasMany(x => x.UserPermissions).WithOne(x => x.Permission).HasForeignKey(x => x.PermissionId);
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("iam_role_permissions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("iam_user_roles");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.RoleId, x.FirmId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("iam_user_permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GrantType).HasMaxLength(10).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.PermissionId, x.FirmId }).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("iam_user_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DeviceInfo).HasColumnType("jsonb");
        builder.HasIndex(x => x.TokenHash);
        builder.HasIndex(x => x.UserId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("iam_audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OldValues).HasColumnType("jsonb");
        builder.Property(x => x.NewValues).HasColumnType("jsonb");
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.Context).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.UserId);
    }
}

public class ApiClientTypeConfiguration : IEntityTypeConfiguration<ApiClientType>
{
    public void Configure(EntityTypeBuilder<ApiClientType> builder)
    {
        // definition şeması — platformca (superadmin) doldurulan tip kataloğu; altın kural:
        // veri aktarımları/eşlemeler bu tabloya kayıt EKLEYEMEZ (bkz. CLAUDE.md).
        builder.ToTable("api_client_types", "definition");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DefaultClientType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RequiredOwnerType).HasMaxLength(30);
        builder.Property(x => x.BaseScopes).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ApiClientConfiguration : IEntityTypeConfiguration<ApiClient>
{
    public void Configure(EntityTypeBuilder<ApiClient> builder)
    {
        builder.ToTable("api_clients"); // varsayılan şema: iam
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SecretHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SecretHint).HasMaxLength(50);
        builder.Property(x => x.ClientType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ApiClientTypeCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OwnerType).HasMaxLength(30);
        builder.Property(x => x.FulfillmentMode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IpAllowList).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.LastUsedIp).HasMaxLength(50);
        builder.HasIndex(x => x.ClientId).IsUnique();
        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierUserConfiguration : IEntityTypeConfiguration<SupplierUser>
{
    public void Configure(EntityTypeBuilder<SupplierUser> builder)
    {
        builder.ToTable("supplier_users"); // varsayılan şema: iam
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.CurrentAccountId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SupplierUserSessionConfiguration : IEntityTypeConfiguration<SupplierUserSession>
{
    public void Configure(EntityTypeBuilder<SupplierUserSession> builder)
    {
        builder.ToTable("supplier_user_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RefreshTokenHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasOne(x => x.SupplierUser).WithMany().HasForeignKey(x => x.SupplierUserId);
    }
}

public class AdminMenuConfiguration : IEntityTypeConfiguration<AdminMenu>
{
    public void Configure(EntityTypeBuilder<AdminMenu> builder)
    {
        builder.ToTable("iam_admin_menus");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameI18n).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(100);
        builder.Property(x => x.Route).HasMaxLength(200);
        builder.Property(x => x.PermissionCode).HasMaxLength(200);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .IsRequired(false);
    }
}
