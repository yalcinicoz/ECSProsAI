using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCartItemEffectivePriceAtAdd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EffectivePriceAtAdd",
                schema: "crm",
                table: "crm_cart_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EffectivePriceAtAdd",
                schema: "crm",
                table: "crm_cart_items");
        }
    }
}
