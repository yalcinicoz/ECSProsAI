using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductIsSaleOpen : Migration
    {
        // EXPAND-CONTRACT (paylaşılan canlı DB): rename YERİNE yeni IsSaleOpen kolonunu ekle +
        // IsActive'ten doldur. Böylece eski binary (IsActive) ile yeni binary (IsSaleOpen) aynı
        // DB üzerinde EŞ ZAMANLI çalışır — canlı kesinti/izole test kırılması olmaz. Eski IsActive
        // kolonu, yeni binary canlıda doğrulandıktan SONRA ayrı temizlik migration'ında düşürülür.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSaleOpen",
                schema: "catalog",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Başlangıç değeri: mevcut IsActive (interneteAcik && satisaAcik) taşınır.
            // Nihai global anahtar değerleri (apurunler.satisaAcik) değer-aktarımı adımında düzeltilecek.
            migrationBuilder.Sql("UPDATE catalog.products SET \"IsSaleOpen\" = \"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSaleOpen",
                schema: "catalog",
                table: "products");
        }
    }
}
