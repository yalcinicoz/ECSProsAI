using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCargoCarrierFieldsToIntegrationService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                schema: "core",
                table: "core_integration_services",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingUrlTemplate",
                schema: "core",
                table: "core_integration_services",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUrl",
                schema: "core",
                table: "core_integration_services");

            migrationBuilder.DropColumn(
                name: "TrackingUrlTemplate",
                schema: "core",
                table: "core_integration_services");
        }
    }
}
