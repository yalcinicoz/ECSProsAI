using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureImageSetCodeUniquePartial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Aktif satırlar arasındaki Code duplicate'leri temizle — en eskisini tut
            migrationBuilder.Sql(@"
                UPDATE catalog.catalog_image_sets
                SET ""IsDeleted"" = true, ""DeletedAt"" = NOW()
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM (
                        SELECT ""Id"",
                               ROW_NUMBER() OVER (PARTITION BY ""Code"" ORDER BY ""CreatedAt"") AS rn
                        FROM catalog.catalog_image_sets
                        WHERE ""IsDeleted"" = false
                    ) ranked
                    WHERE rn > 1
                );
            ");

            // Eski full index'i kaldır (yoksa da hata verme)
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS catalog.""IX_catalog_image_sets_Code"";");

            // Partial unique index: sadece aktif satırlarda Code benzersiz olsun
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ""IX_catalog_image_sets_Code""
                ON catalog.catalog_image_sets(""Code"")
                WHERE NOT ""IsDeleted"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS catalog.""IX_catalog_image_sets_Code"";");

            migrationBuilder.CreateIndex(
                name: "IX_catalog_image_sets_Code",
                schema: "catalog",
                table: "catalog_image_sets",
                column: "Code",
                unique: true);
        }
    }
}
