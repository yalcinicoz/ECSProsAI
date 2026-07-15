using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropProductIsActive : Migration
    {
        // M1 (satış görünürlüğü) Product.IsActive'i IsSaleOpen'a taşıdı ve entity property'sini
        // kaldırdı; ancak EXPAND-CONTRACT gereği eski catalog.products.IsActive KOLONU DB'de kaldı
        // (property koddan çıktığı için EF snapshot'ı diff üretmiyor — bu yüzden manuel). Kolon
        // artık maplenmiyor, NOT NULL + default'suz → yeni ürün insert'ünde gizli hata riski.
        // Bu contract adımı kolonu düşürür.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "catalog",
                table: "products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "catalog",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
