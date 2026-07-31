using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CampaignPlatformAndFillType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirmId",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropColumn(
                name: "ProductSelectionType",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.RenameColumn(
                name: "ProductFilter",
                schema: "promotion",
                table: "prm_campaigns",
                newName: "FilterDef");

            migrationBuilder.AddColumn<string>(
                name: "BadgeLabel",
                schema: "promotion",
                table: "prm_campaigns",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FillType",
                schema: "promotion",
                table: "prm_campaigns",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FirmPlatformId",
                schema: "promotion",
                table: "prm_campaigns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_prm_campaigns_FirmPlatformId_IsActive",
                schema: "promotion",
                table: "prm_campaigns",
                columns: new[] { "FirmPlatformId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_prm_campaigns_FirmPlatformId_IsActive",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropColumn(
                name: "BadgeLabel",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropColumn(
                name: "FillType",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropColumn(
                name: "FirmPlatformId",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.RenameColumn(
                name: "FilterDef",
                schema: "promotion",
                table: "prm_campaigns",
                newName: "ProductFilter");

            migrationBuilder.AddColumn<Guid>(
                name: "FirmId",
                schema: "promotion",
                table: "prm_campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductSelectionType",
                schema: "promotion",
                table: "prm_campaigns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
