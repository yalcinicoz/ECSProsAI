using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Iam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionCatalogAndSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_iam_user_permissions_UserId_PermissionId_FirmId",
                schema: "iam",
                table: "iam_user_permissions");

            migrationBuilder.AddColumn<bool>(
                name: "IsSuperAdmin",
                schema: "iam",
                table: "iam_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "ChannelIds",
                schema: "iam",
                table: "iam_user_permissions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "ChannelIds",
                schema: "iam",
                table: "iam_role_permissions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChannelScoped",
                schema: "iam",
                table: "iam_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Mevcut 11 kayıt katalogda TANIMLI kabul edilir; açılıştaki katalog senkronu
            // (PermissionKatalogSenkronu) katalogda olmayanları false + pasif yapar.
            migrationBuilder.AddColumn<bool>(
                name: "IsCodeDefined",
                schema: "iam",
                table: "iam_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "iam",
                table: "iam_permissions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "action");

            migrationBuilder.AddColumn<string>(
                name: "PageCode",
                schema: "iam",
                table: "iam_permissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Veri adımı (K5): bugün super_admin ROLÜNDE olan kullanıcılara süper admin BAYRAĞI.
            // Bayrak asıl kaynaktır; rol üzerinden gelen bypass Y1'de tamamen kalkacak.
            migrationBuilder.Sql("""
                UPDATE iam.iam_users u
                   SET "IsSuperAdmin" = true
                  FROM iam.iam_user_roles ur
                  JOIN iam.iam_roles r ON r."Id" = ur."RoleId"
                 WHERE ur."UserId" = u."Id"
                   AND r."Code" = 'super_admin'
                   AND NOT ur."IsDeleted" AND NOT r."IsDeleted";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_permissions_UserId_PermissionId",
                schema: "iam",
                table: "iam_user_permissions",
                columns: new[] { "UserId", "PermissionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_iam_user_permissions_UserId_PermissionId",
                schema: "iam",
                table: "iam_user_permissions");

            migrationBuilder.DropColumn(
                name: "IsSuperAdmin",
                schema: "iam",
                table: "iam_users");

            migrationBuilder.DropColumn(
                name: "ChannelIds",
                schema: "iam",
                table: "iam_user_permissions");

            migrationBuilder.DropColumn(
                name: "ChannelIds",
                schema: "iam",
                table: "iam_role_permissions");

            migrationBuilder.DropColumn(
                name: "ChannelScoped",
                schema: "iam",
                table: "iam_permissions");

            migrationBuilder.DropColumn(
                name: "IsCodeDefined",
                schema: "iam",
                table: "iam_permissions");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "iam",
                table: "iam_permissions");

            migrationBuilder.DropColumn(
                name: "PageCode",
                schema: "iam",
                table: "iam_permissions");

            migrationBuilder.CreateIndex(
                name: "IX_iam_user_permissions_UserId_PermissionId_FirmId",
                schema: "iam",
                table: "iam_user_permissions",
                columns: new[] { "UserId", "PermissionId", "FirmId" },
                unique: true);
        }
    }
}
