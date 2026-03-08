using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Iam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.CreateTable(
                name: "iam_admin_menus",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PermissionCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_admin_menus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_iam_admin_menus_iam_admin_menus_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "iam",
                        principalTable: "iam_admin_menus",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "iam_audit_logs",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldValues = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    NewValues = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Context = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iam_permissions",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    Module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PermissionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iam_roles",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iam_users",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    Preferences = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "iam_role_permissions",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_role_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_iam_role_permissions_iam_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "iam",
                        principalTable: "iam_permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_iam_role_permissions_iam_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "iam",
                        principalTable: "iam_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "iam_user_permissions",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_user_permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_iam_user_permissions_iam_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "iam",
                        principalTable: "iam_permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_iam_user_permissions_iam_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "iam_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "iam_user_roles",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_iam_user_roles_iam_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "iam",
                        principalTable: "iam_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_iam_user_roles_iam_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "iam_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "iam_user_sessions",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    DeviceInfo = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_iam_user_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_iam_user_sessions_iam_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "iam_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_iam_admin_menus_Code",
                schema: "iam",
                table: "iam_admin_menus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_admin_menus_ParentId",
                schema: "iam",
                table: "iam_admin_menus",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_audit_logs_EntityType_EntityId",
                schema: "iam",
                table: "iam_audit_logs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_iam_audit_logs_UserId",
                schema: "iam",
                table: "iam_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_permissions_Code",
                schema: "iam",
                table: "iam_permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_role_permissions_PermissionId",
                schema: "iam",
                table: "iam_role_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_role_permissions_RoleId_PermissionId",
                schema: "iam",
                table: "iam_role_permissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_roles_Code",
                schema: "iam",
                table: "iam_roles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_permissions_PermissionId",
                schema: "iam",
                table: "iam_user_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_permissions_UserId_PermissionId_FirmId",
                schema: "iam",
                table: "iam_user_permissions",
                columns: new[] { "UserId", "PermissionId", "FirmId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_roles_RoleId",
                schema: "iam",
                table: "iam_user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_roles_UserId_RoleId_FirmId",
                schema: "iam",
                table: "iam_user_roles",
                columns: new[] { "UserId", "RoleId", "FirmId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_sessions_TokenHash",
                schema: "iam",
                table: "iam_user_sessions",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_sessions_UserId",
                schema: "iam",
                table: "iam_user_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_iam_users_Email",
                schema: "iam",
                table: "iam_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_iam_users_Username",
                schema: "iam",
                table: "iam_users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "iam_admin_menus",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_audit_logs",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_role_permissions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_user_permissions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_user_roles",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_user_sessions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_permissions",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_roles",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "iam_users",
                schema: "iam");
        }
    }
}
