using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InScope",
                schema: "storefront",
                table: "channel_products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExcluded",
                schema: "storefront",
                table: "channel_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScopeSource",
                schema: "storefront",
                table: "channel_products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.CreateTable(
                name: "channel_scopes",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    FillType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FilterDef = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MatchedCount = table.Column<int>(type: "integer", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_channel_scopes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channel_products_FirmPlatformId_InScope_IsExcluded",
                schema: "storefront",
                table: "channel_products",
                columns: new[] { "FirmPlatformId", "InScope", "IsExcluded" });

            migrationBuilder.CreateIndex(
                name: "IX_channel_scopes_FirmPlatformId",
                schema: "storefront",
                table: "channel_scopes",
                column: "FirmPlatformId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_scopes",
                schema: "storefront");

            migrationBuilder.DropIndex(
                name: "IX_channel_products_FirmPlatformId_InScope_IsExcluded",
                schema: "storefront",
                table: "channel_products");

            migrationBuilder.DropColumn(
                name: "InScope",
                schema: "storefront",
                table: "channel_products");

            migrationBuilder.DropColumn(
                name: "IsExcluded",
                schema: "storefront",
                table: "channel_products");

            migrationBuilder.DropColumn(
                name: "ScopeSource",
                schema: "storefront",
                table: "channel_products");
        }
    }
}
