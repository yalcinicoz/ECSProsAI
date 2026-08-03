using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignBadgeColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BadgeColor",
                schema: "promotion",
                table: "prm_campaigns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BadgeColor",
                schema: "promotion",
                table: "prm_campaigns");
        }
    }
}
