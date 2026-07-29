using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SentPrice",
                schema: "integration",
                table: "marketplace_batch_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SentStock",
                schema: "integration",
                table: "marketplace_batch_items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "marketplace_issues",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ConditionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SuggestedAction = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_marketplace_issues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_issues_FirmPlatformId_ConditionKey",
                schema: "integration",
                table: "marketplace_issues",
                columns: new[] { "FirmPlatformId", "ConditionKey" },
                unique: true,
                filter: "\"Status\" = 'open' AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_issues_FirmPlatformId_Status",
                schema: "integration",
                table: "marketplace_issues",
                columns: new[] { "FirmPlatformId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_issues",
                schema: "integration");

            migrationBuilder.DropColumn(
                name: "SentPrice",
                schema: "integration",
                table: "marketplace_batch_items");

            migrationBuilder.DropColumn(
                name: "SentStock",
                schema: "integration",
                table: "marketplace_batch_items");
        }
    }
}
