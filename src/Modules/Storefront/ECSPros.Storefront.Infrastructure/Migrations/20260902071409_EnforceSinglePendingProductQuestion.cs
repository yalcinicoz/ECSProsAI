using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSinglePendingProductQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski sürümde yarışla oluşmuş birden fazla pending kayıt varsa veri silmeden
            // en eskiyi bekleyen bırak, sonraki kayıtları gizle; unique index güvenle kurulabilsin.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "FirmPlatformId", "MemberId", "ProductCode"
                        ORDER BY "CreatedAt", "Id") AS rn
                    FROM storefront.product_questions
                    WHERE "Status" = 'pending' AND NOT "IsDeleted"
                )
                UPDATE storefront.product_questions q
                SET "Status" = 'hidden', "UpdatedAt" = now()
                FROM ranked r
                WHERE q."Id" = r."Id" AND r.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_product_questions_single_pending",
                schema: "storefront",
                table: "product_questions",
                columns: new[] { "FirmPlatformId", "MemberId", "ProductCode" },
                unique: true,
                filter: "\"Status\" = 'pending' AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_product_questions_single_pending",
                schema: "storefront",
                table: "product_questions");
        }
    }
}
