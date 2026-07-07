using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractFieldsToFirmIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                schema: "core",
                table: "core_firm_integrations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                schema: "core",
                table: "core_firm_integrations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                schema: "core",
                table: "core_firm_integrations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                schema: "core",
                table: "core_firm_integrations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentUrl",
                schema: "core",
                table: "core_firm_integrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                schema: "core",
                table: "core_firm_integrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                schema: "core",
                table: "core_firm_integrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "core",
                table: "core_firm_integrations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "draft");

            migrationBuilder.AddColumn<Dictionary<string, object>>(
                name: "Terms",
                schema: "core",
                table: "core_firm_integrations",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContactEmail",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "ContactName",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "ContractNumber",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "DocumentUrl",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "core",
                table: "core_firm_integrations");

            migrationBuilder.DropColumn(
                name: "Terms",
                schema: "core",
                table: "core_firm_integrations");
        }
    }
}
