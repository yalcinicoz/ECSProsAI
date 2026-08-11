using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignSupplierCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresSupplierOptIn",
                schema: "promotion",
                table: "prm_campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplierCommissionRate",
                schema: "promotion",
                table: "prm_campaigns",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SupplierDiscountSharePercent",
                schema: "promotion",
                table: "prm_campaigns",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "prm_campaign_supplier_participations",
                schema: "promotion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductIds = table.Column<List<Guid>>(type: "jsonb", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_prm_campaign_supplier_participations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prm_campaign_supplier_participations_prm_campaigns_Campaign~",
                        column: x => x.CampaignId,
                        principalSchema: "promotion",
                        principalTable: "prm_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prm_campaign_supplier_participations_CampaignId_SupplierAcc~",
                schema: "promotion",
                table: "prm_campaign_supplier_participations",
                columns: new[] { "CampaignId", "SupplierAccountId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prm_campaign_supplier_participations",
                schema: "promotion");

            migrationBuilder.DropColumn(
                name: "RequiresSupplierOptIn",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropColumn(
                name: "SupplierCommissionRate",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropColumn(
                name: "SupplierDiscountSharePercent",
                schema: "promotion",
                table: "prm_campaigns");
        }
    }
}
