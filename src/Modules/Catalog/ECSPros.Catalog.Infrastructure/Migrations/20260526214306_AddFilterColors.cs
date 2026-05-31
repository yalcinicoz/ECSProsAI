using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_filter_colors",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    HexCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_catalog_filter_colors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "catalog_attribute_value_filter_colors",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilterColorId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_catalog_attribute_value_filter_colors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_catalog_attribute_value_filter_colors_catalog_attribute_val~",
                        column: x => x.AttributeValueId,
                        principalSchema: "catalog",
                        principalTable: "catalog_attribute_values",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_catalog_attribute_value_filter_colors_catalog_filter_colors~",
                        column: x => x.FilterColorId,
                        principalSchema: "catalog",
                        principalTable: "catalog_filter_colors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_value_filter_colors_AttributeValueId_Filt~",
                schema: "catalog",
                table: "catalog_attribute_value_filter_colors",
                columns: new[] { "AttributeValueId", "FilterColorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalog_attribute_value_filter_colors_FilterColorId",
                schema: "catalog",
                table: "catalog_attribute_value_filter_colors",
                column: "FilterColorId");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_filter_colors_Code",
                schema: "catalog",
                table: "catalog_filter_colors",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_attribute_value_filter_colors",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "catalog_filter_colors",
                schema: "catalog");
        }
    }
}
