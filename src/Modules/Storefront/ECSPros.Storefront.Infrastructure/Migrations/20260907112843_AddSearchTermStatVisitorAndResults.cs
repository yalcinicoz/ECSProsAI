using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchTermStatVisitorAndResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_search_term_stats_FirmPlatformId_Term_Day",
                schema: "storefront",
                table: "search_term_stats");

            migrationBuilder.AddColumn<int>(
                name: "ResultCount",
                schema: "storefront",
                table: "search_term_stats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisitorHash",
                schema: "storefront",
                table: "search_term_stats",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_search_term_stats_FirmPlatformId_Term_Day_VisitorHash",
                schema: "storefront",
                table: "search_term_stats",
                columns: new[] { "FirmPlatformId", "Term", "Day", "VisitorHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_search_term_stats_FirmPlatformId_Term_Day_VisitorHash",
                schema: "storefront",
                table: "search_term_stats");

            migrationBuilder.DropColumn(
                name: "ResultCount",
                schema: "storefront",
                table: "search_term_stats");

            migrationBuilder.DropColumn(
                name: "VisitorHash",
                schema: "storefront",
                table: "search_term_stats");

            migrationBuilder.CreateIndex(
                name: "IX_search_term_stats_FirmPlatformId_Term_Day",
                schema: "storefront",
                table: "search_term_stats",
                columns: new[] { "FirmPlatformId", "Term", "Day" },
                unique: true);
        }
    }
}
