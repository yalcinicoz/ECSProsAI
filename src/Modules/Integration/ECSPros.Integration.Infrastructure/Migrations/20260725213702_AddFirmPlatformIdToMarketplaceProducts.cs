using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmPlatformIdToMarketplaceProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FirmPlatformId",
                schema: "integration",
                table: "marketplace_products",
                type: "uuid",
                nullable: true);

            // Mevcut kayıtları platforma özel sözleşme üzerinden geri doldur (firma-geneli
            // sözleşmede FirmPlatformId null'dır — o kayıtlar boş kalır, uygulama katmanı çözer).
            migrationBuilder.Sql("""
                UPDATE integration.marketplace_products mp
                SET "FirmPlatformId" = fpi."FirmPlatformId"
                FROM core.core_firm_platform_integrations fpi
                WHERE fpi."Id" = mp."FirmIntegrationId"
                  AND fpi."FirmPlatformId" IS NOT NULL
                  AND mp."FirmPlatformId" IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_products_FirmPlatformId_SyncStatus",
                schema: "integration",
                table: "marketplace_products",
                columns: new[] { "FirmPlatformId", "SyncStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_marketplace_products_FirmPlatformId_SyncStatus",
                schema: "integration",
                table: "marketplace_products");

            migrationBuilder.DropColumn(
                name: "FirmPlatformId",
                schema: "integration",
                table: "marketplace_products");
        }
    }
}
