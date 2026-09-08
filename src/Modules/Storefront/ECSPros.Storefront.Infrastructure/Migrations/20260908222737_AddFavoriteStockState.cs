using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteStockState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WasOutOfStock",
                schema: "storefront",
                table: "favorites",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WasOutOfStock",
                schema: "storefront",
                table: "favorites");
        }
    }
}
