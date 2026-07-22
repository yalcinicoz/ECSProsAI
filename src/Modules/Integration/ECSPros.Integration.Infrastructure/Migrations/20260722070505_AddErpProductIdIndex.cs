using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpProductIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // H3 görsel arama: servis legacy urunId döndürür; erp_variant_data'da
            // Payload->>'erpProductId' ile arama yapılır — ifade index'i olmadan 327K satır seq scan.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_erp_variant_data_ErpProductId\" " +
                "ON integration.erp_variant_data (((\"Payload\"->>'erpProductId')::int));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS integration.\"IX_erp_variant_data_ErpProductId\";");
        }
    }
}
