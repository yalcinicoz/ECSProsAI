using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyAddressIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacyAddressId",
                schema: "crm",
                table: "crm_addresses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_crm_addresses_LegacyAddressId",
                schema: "crm",
                table: "crm_addresses",
                column: "LegacyAddressId",
                unique: true,
                filter: "\"LegacyAddressId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_crm_addresses_LegacyAddressId",
                schema: "crm",
                table: "crm_addresses");

            migrationBuilder.DropColumn(
                name: "LegacyAddressId",
                schema: "crm",
                table: "crm_addresses");
        }
    }
}
