using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestructureFirmPlatformIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_cargo_rules_core_firm_integrations_FirmIntegrationId",
                schema: "core",
                table: "core_cargo_rules");

            migrationBuilder.DropTable(
                name: "core_firm_integrations",
                schema: "core");

            migrationBuilder.RenameColumn(
                name: "FirmIntegrationId",
                schema: "core",
                table: "core_cargo_rules",
                newName: "FirmPlatformIntegrationId");

            migrationBuilder.RenameIndex(
                name: "IX_core_cargo_rules_FirmIntegrationId",
                schema: "core",
                table: "core_cargo_rules",
                newName: "IX_core_cargo_rules_FirmPlatformIntegrationId");

            migrationBuilder.CreateTable(
                name: "core_firm_platform_integrations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Credentials = table.Column<string>(type: "text", nullable: false),
                    Settings = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Terms = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_firm_platform_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_firm_platform_integrations_core_firm_platforms_FirmPla~",
                        column: x => x.FirmPlatformId,
                        principalSchema: "core",
                        principalTable: "core_firm_platforms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_core_firm_platform_integrations_core_firms_FirmId",
                        column: x => x.FirmId,
                        principalSchema: "core",
                        principalTable: "core_firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_firm_platform_integrations_core_integration_services_I~",
                        column: x => x.IntegrationServiceId,
                        principalSchema: "core",
                        principalTable: "core_integration_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_platform_integrations_FirmId_FirmPlatformId",
                schema: "core",
                table: "core_firm_platform_integrations",
                columns: new[] { "FirmId", "FirmPlatformId" });

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_platform_integrations_FirmPlatformId",
                schema: "core",
                table: "core_firm_platform_integrations",
                column: "FirmPlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_platform_integrations_IntegrationServiceId",
                schema: "core",
                table: "core_firm_platform_integrations",
                column: "IntegrationServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_core_cargo_rules_core_firm_platform_integrations_FirmPlatfo~",
                schema: "core",
                table: "core_cargo_rules",
                column: "FirmPlatformIntegrationId",
                principalSchema: "core",
                principalTable: "core_firm_platform_integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_cargo_rules_core_firm_platform_integrations_FirmPlatfo~",
                schema: "core",
                table: "core_cargo_rules");

            migrationBuilder.DropTable(
                name: "core_firm_platform_integrations",
                schema: "core");

            migrationBuilder.RenameColumn(
                name: "FirmPlatformIntegrationId",
                schema: "core",
                table: "core_cargo_rules",
                newName: "FirmIntegrationId");

            migrationBuilder.RenameIndex(
                name: "IX_core_cargo_rules_FirmPlatformIntegrationId",
                schema: "core",
                table: "core_cargo_rules",
                newName: "IX_core_cargo_rules_FirmIntegrationId");

            migrationBuilder.CreateTable(
                name: "core_firm_integrations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContractNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Credentials = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Settings = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Terms = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_firm_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_firm_integrations_core_firms_FirmId",
                        column: x => x.FirmId,
                        principalSchema: "core",
                        principalTable: "core_firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_firm_integrations_core_integration_services_Integratio~",
                        column: x => x.IntegrationServiceId,
                        principalSchema: "core",
                        principalTable: "core_integration_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_integrations_FirmId",
                schema: "core",
                table: "core_firm_integrations",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_integrations_IntegrationServiceId",
                schema: "core",
                table: "core_firm_integrations",
                column: "IntegrationServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_core_cargo_rules_core_firm_integrations_FirmIntegrationId",
                schema: "core",
                table: "core_cargo_rules",
                column: "FirmIntegrationId",
                principalSchema: "core",
                principalTable: "core_firm_integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
