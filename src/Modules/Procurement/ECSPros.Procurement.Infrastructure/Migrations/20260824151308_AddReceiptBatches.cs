using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Procurement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipt_batches",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageCount = table.Column<int>(type: "integer", nullable: true),
                    DeliveryNoteNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SupplierInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_receipt_batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "receipt_batch_items",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    DescriptionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_receipt_batch_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_receipt_batch_items_receipt_batches_ReceiptBatchId",
                        column: x => x.ReceiptBatchId,
                        principalSchema: "procurement",
                        principalTable: "receipt_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receipt_batch_purchase_orders",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_receipt_batch_purchase_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_receipt_batch_purchase_orders_receipt_batches_ReceiptBatchId",
                        column: x => x.ReceiptBatchId,
                        principalSchema: "procurement",
                        principalTable: "receipt_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_batch_items_ReceiptBatchId",
                schema: "procurement",
                table: "receipt_batch_items",
                column: "ReceiptBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_batch_purchase_orders_PurchaseOrderId",
                schema: "procurement",
                table: "receipt_batch_purchase_orders",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_batch_purchase_orders_ReceiptBatchId_PurchaseOrderId",
                schema: "procurement",
                table: "receipt_batch_purchase_orders",
                columns: new[] { "ReceiptBatchId", "PurchaseOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_batches_Code",
                schema: "procurement",
                table: "receipt_batches",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_batches_SupplierId_Status",
                schema: "procurement",
                table: "receipt_batches",
                columns: new[] { "SupplierId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_batch_items",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "receipt_batch_purchase_orders",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "receipt_batches",
                schema: "procurement");
        }
    }
}
