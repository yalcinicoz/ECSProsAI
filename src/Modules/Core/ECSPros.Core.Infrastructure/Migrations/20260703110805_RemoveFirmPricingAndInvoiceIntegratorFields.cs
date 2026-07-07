using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFirmPricingAndInvoiceIntegratorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_firms_core_firm_integrations_InvoiceIntegratorId",
                schema: "core",
                table: "core_firms");

            migrationBuilder.DropIndex(
                name: "IX_core_firms_InvoiceIntegratorId",
                schema: "core",
                table: "core_firms");

            migrationBuilder.DropColumn(
                name: "InvoiceIntegratorId",
                schema: "core",
                table: "core_firms");

            migrationBuilder.DropColumn(
                name: "PriceMultiplier",
                schema: "core",
                table: "core_firms");

            migrationBuilder.DropColumn(
                name: "PriceType",
                schema: "core",
                table: "core_firms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceIntegratorId",
                schema: "core",
                table: "core_firms",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceMultiplier",
                schema: "core",
                table: "core_firms",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceType",
                schema: "core",
                table: "core_firms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_core_firms_InvoiceIntegratorId",
                schema: "core",
                table: "core_firms",
                column: "InvoiceIntegratorId");

            migrationBuilder.AddForeignKey(
                name: "FK_core_firms_core_firm_integrations_InvoiceIntegratorId",
                schema: "core",
                table: "core_firms",
                column: "InvoiceIntegratorId",
                principalSchema: "core",
                principalTable: "core_firm_integrations",
                principalColumn: "Id");
        }
    }
}
