using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveIntegrationServicesToDefinitionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_firm_platform_integrations_core_integration_services_I~",
                schema: "core",
                table: "core_firm_platform_integrations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_core_integration_services",
                schema: "core",
                table: "core_integration_services");

            migrationBuilder.EnsureSchema(
                name: "definition");

            migrationBuilder.RenameTable(
                name: "core_integration_services",
                schema: "core",
                newName: "integration_services",
                newSchema: "definition");

            migrationBuilder.RenameIndex(
                name: "IX_core_integration_services_Code",
                schema: "definition",
                table: "integration_services",
                newName: "IX_integration_services_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_integration_services",
                schema: "definition",
                table: "integration_services",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_firm_platform_integrations_integration_services_Integr~",
                schema: "core",
                table: "core_firm_platform_integrations",
                column: "IntegrationServiceId",
                principalSchema: "definition",
                principalTable: "integration_services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_firm_platform_integrations_integration_services_Integr~",
                schema: "core",
                table: "core_firm_platform_integrations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_integration_services",
                schema: "definition",
                table: "integration_services");

            migrationBuilder.RenameTable(
                name: "integration_services",
                schema: "definition",
                newName: "core_integration_services",
                newSchema: "core");

            migrationBuilder.RenameIndex(
                name: "IX_integration_services_Code",
                schema: "core",
                table: "core_integration_services",
                newName: "IX_core_integration_services_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_core_integration_services",
                schema: "core",
                table: "core_integration_services",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_firm_platform_integrations_core_integration_services_I~",
                schema: "core",
                table: "core_firm_platform_integrations",
                column: "IntegrationServiceId",
                principalSchema: "core",
                principalTable: "core_integration_services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
