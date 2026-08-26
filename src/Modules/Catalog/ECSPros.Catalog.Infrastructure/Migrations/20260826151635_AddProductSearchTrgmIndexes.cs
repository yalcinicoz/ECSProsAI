using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSearchTrgmIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dayanıklılık Faz 2 (2026-08-26): arama LIKE '%…%' taramaları için pg_trgm GIN
            // ifade indeksleri — EF üretimi SQL ile birebir aynı ifadeler:
            // lower("Code") ve lower(jsonb_extract_path_text("NameI18n", 'tr')).
            // pg_trgm PG13+ "trusted" olduğundan süper kullanıcı gerektirmez.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_products_Code_trgm\" ON catalog.products " +
                "USING gin (lower(\"Code\") gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_products_NameTr_trgm\" ON catalog.products " +
                "USING gin (lower(jsonb_extract_path_text(\"NameI18n\", 'tr')) gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.\"IX_products_NameTr_trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS catalog.\"IX_products_Code_trgm\";");
        }
    }
}
