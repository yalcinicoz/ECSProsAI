using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementBinIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FromBinId",
                schema: "inventory",
                table: "inv_stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToBinId",
                schema: "inventory",
                table: "inv_stock_movements",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromBinId",
                schema: "inventory",
                table: "inv_stock_movements");

            migrationBuilder.DropColumn(
                name: "ToBinId",
                schema: "inventory",
                table: "inv_stock_movements");
        }
    }
}
