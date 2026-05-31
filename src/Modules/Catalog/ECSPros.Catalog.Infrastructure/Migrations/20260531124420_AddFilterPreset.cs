using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FilterPresetId",
                schema: "catalog",
                table: "catalog_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalog_filter_presets",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FilterDef = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_catalog_filter_presets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_categories_FilterPresetId",
                schema: "catalog",
                table: "catalog_categories",
                column: "FilterPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_filter_presets_Code",
                schema: "catalog",
                table: "catalog_filter_presets",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_catalog_categories_catalog_filter_presets_FilterPresetId",
                schema: "catalog",
                table: "catalog_categories",
                column: "FilterPresetId",
                principalSchema: "catalog",
                principalTable: "catalog_filter_presets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_catalog_categories_catalog_filter_presets_FilterPresetId",
                schema: "catalog",
                table: "catalog_categories");

            migrationBuilder.DropTable(
                name: "catalog_filter_presets",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_catalog_categories_FilterPresetId",
                schema: "catalog",
                table: "catalog_categories");

            migrationBuilder.DropColumn(
                name: "FilterPresetId",
                schema: "catalog",
                table: "catalog_categories");
        }
    }
}
