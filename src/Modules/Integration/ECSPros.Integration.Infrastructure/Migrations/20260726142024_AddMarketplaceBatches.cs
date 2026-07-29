using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                schema: "integration",
                table: "marketplace_products",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSentPayloadHash",
                schema: "integration",
                table: "marketplace_products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedCategoryExternalId",
                schema: "integration",
                table: "marketplace_products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "marketplace_batch_items",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorRaw = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SuggestedCategoryExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_marketplace_batch_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_batches",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalBatchId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BatchType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    ResolvedCount = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    PollAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextPollAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_marketplace_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marketplace_error_patterns",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Marketplace = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Pattern = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SuggestedCategoryGroup = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_marketplace_error_patterns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_batch_items_BatchId_Barcode",
                schema: "integration",
                table: "marketplace_batch_items",
                columns: new[] { "BatchId", "Barcode" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_batch_items_BatchId_Status",
                schema: "integration",
                table: "marketplace_batch_items",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_batches_FirmPlatformId_SubmittedAt",
                schema: "integration",
                table: "marketplace_batches",
                columns: new[] { "FirmPlatformId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_batches_Status_NextPollAt",
                schema: "integration",
                table: "marketplace_batches",
                columns: new[] { "Status", "NextPollAt" });

            migrationBuilder.CreateIndex(
                name: "IX_marketplace_error_patterns_Marketplace_IsActive",
                schema: "integration",
                table: "marketplace_error_patterns",
                columns: new[] { "Marketplace", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_batch_items",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "marketplace_batches",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "marketplace_error_patterns",
                schema: "integration");

            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                schema: "integration",
                table: "marketplace_products");

            migrationBuilder.DropColumn(
                name: "LastSentPayloadHash",
                schema: "integration",
                table: "marketplace_products");

            migrationBuilder.DropColumn(
                name: "SuggestedCategoryExternalId",
                schema: "integration",
                table: "marketplace_products");
        }
    }
}
