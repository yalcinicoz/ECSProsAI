using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpReferenceItemMappedTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MappedTargetId",
                schema: "integration",
                table: "erp_reference_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MappedTargetKind",
                schema: "integration",
                table: "erp_reference_items",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MappedTargetLabel",
                schema: "integration",
                table: "erp_reference_items",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MappedTargetId",
                schema: "integration",
                table: "erp_reference_items");

            migrationBuilder.DropColumn(
                name: "MappedTargetKind",
                schema: "integration",
                table: "erp_reference_items");

            migrationBuilder.DropColumn(
                name: "MappedTargetLabel",
                schema: "integration",
                table: "erp_reference_items");
        }
    }
}
