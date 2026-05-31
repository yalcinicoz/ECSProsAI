using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroupCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Önce nullable ekle (mevcut satırlar için)
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "catalog",
                table: "catalog_product_groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Mevcut satırlara benzersiz geçici kod ata (id'nin ilk 8 hex karakteri)
            migrationBuilder.Sql(@"
                UPDATE catalog.catalog_product_groups
                SET ""Code"" = 'grp_' || substr(replace(""Id""::text, '-', ''), 1, 8)
                WHERE ""Code"" IS NULL;
            ");

            // NOT NULL yap
            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "catalog",
                table: "catalog_product_groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            // Benzersiz index ekle
            migrationBuilder.CreateIndex(
                name: "IX_catalog_product_groups_Code",
                schema: "catalog",
                table: "catalog_product_groups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_catalog_product_groups_Code",
                schema: "catalog",
                table: "catalog_product_groups");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "catalog",
                table: "catalog_product_groups");
        }
    }
}
