using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPageBlocksAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "page_blocks",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Placement = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BlockType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Template = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    SubtitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RuleJson = table.Column<string>(type: "jsonb", nullable: true),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_page_blocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "publish_logs",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PreviousVersion = table.Column<int>(type: "integer", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_publish_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "published_snapshots",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    JsonData = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_published_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "page_block_items",
                schema: "storefront",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageBlockId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    SubtitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MobileImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    ButtonTextI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    BadgeLabel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RuleJson = table.Column<string>(type: "jsonb", nullable: true),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_page_block_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_page_block_items_page_blocks_PageBlockId",
                        column: x => x.PageBlockId,
                        principalSchema: "storefront",
                        principalTable: "page_blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_page_block_items_PageBlockId_SortOrder",
                schema: "storefront",
                table: "page_block_items",
                columns: new[] { "PageBlockId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_page_blocks_FirmPlatformId_Placement_SortOrder",
                schema: "storefront",
                table: "page_blocks",
                columns: new[] { "FirmPlatformId", "Placement", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_publish_logs_FirmPlatformId_PublishedAt",
                schema: "storefront",
                table: "publish_logs",
                columns: new[] { "FirmPlatformId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_published_snapshots_FirmPlatformId_IsActive",
                schema: "storefront",
                table: "published_snapshots",
                columns: new[] { "FirmPlatformId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_published_snapshots_FirmPlatformId_Version",
                schema: "storefront",
                table: "published_snapshots",
                columns: new[] { "FirmPlatformId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "page_block_items",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "publish_logs",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "published_snapshots",
                schema: "storefront");

            migrationBuilder.DropTable(
                name: "page_blocks",
                schema: "storefront");
        }
    }
}
