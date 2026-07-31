using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveCampaignTypesToDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prm_campaigns_prm_campaign_types_CampaignTypeId",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_prm_campaign_types",
                schema: "promotion",
                table: "prm_campaign_types");

            migrationBuilder.EnsureSchema(
                name: "definition");

            migrationBuilder.RenameTable(
                name: "prm_campaign_types",
                schema: "promotion",
                newName: "campaign_types",
                newSchema: "definition");

            migrationBuilder.RenameIndex(
                name: "IX_prm_campaign_types_Code",
                schema: "definition",
                table: "campaign_types",
                newName: "IX_campaign_types_Code");

            migrationBuilder.AddColumn<bool>(
                name: "ProductPriceDisplay",
                schema: "definition",
                table: "campaign_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "definition",
                table: "campaign_types",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_campaign_types",
                schema: "definition",
                table: "campaign_types",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_prm_campaigns_campaign_types_CampaignTypeId",
                schema: "promotion",
                table: "prm_campaigns",
                column: "CampaignTypeId",
                principalSchema: "definition",
                principalTable: "campaign_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_prm_campaigns_campaign_types_CampaignTypeId",
                schema: "promotion",
                table: "prm_campaigns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_campaign_types",
                schema: "definition",
                table: "campaign_types");

            migrationBuilder.DropColumn(
                name: "ProductPriceDisplay",
                schema: "definition",
                table: "campaign_types");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "definition",
                table: "campaign_types");

            migrationBuilder.RenameTable(
                name: "campaign_types",
                schema: "definition",
                newName: "prm_campaign_types",
                newSchema: "promotion");

            migrationBuilder.RenameIndex(
                name: "IX_campaign_types_Code",
                schema: "promotion",
                table: "prm_campaign_types",
                newName: "IX_prm_campaign_types_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_prm_campaign_types",
                schema: "promotion",
                table: "prm_campaign_types",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_prm_campaigns_prm_campaign_types_CampaignTypeId",
                schema: "promotion",
                table: "prm_campaigns",
                column: "CampaignTypeId",
                principalSchema: "promotion",
                principalTable: "prm_campaign_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
