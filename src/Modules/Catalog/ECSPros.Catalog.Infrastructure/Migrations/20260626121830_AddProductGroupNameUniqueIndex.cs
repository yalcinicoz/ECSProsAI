using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroupNameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // JSONB expression üzerinde partial unique index:
            // IsDeleted=FALSE olan gruplar arasında Türkçe isim benzersiz olmalı.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ""IX_catalog_product_groups_NameTr""
                ON catalog.catalog_product_groups ((""NameI18n""->>'tr'))
                WHERE ""IsDeleted"" = FALSE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS catalog.""IX_catalog_product_groups_NameTr"";
            ");
        }
    }
}
