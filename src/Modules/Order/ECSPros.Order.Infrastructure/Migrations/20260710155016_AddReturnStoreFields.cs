using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnStoreFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CargoReturnCode",
                schema: "order",
                table: "ord_returns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "ImageUrls",
                schema: "order",
                table: "ord_returns",
                type: "text[]",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CargoReturnCode",
                schema: "order",
                table: "ord_returns");

            migrationBuilder.DropColumn(
                name: "ImageUrls",
                schema: "order",
                table: "ord_returns");
        }
    }
}
