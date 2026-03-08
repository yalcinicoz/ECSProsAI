using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "inv_stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_stock_movements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inv_warehouses",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    WarehouseType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    IsSellableOnline = table.Column<bool>(type: "boolean", nullable: false),
                    ReservePriority = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_inv_warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inv_transfer_requests",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_inv_transfer_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_transfer_requests_inv_warehouses_FromWarehouseId",
                        column: x => x.FromWarehouseId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inv_transfer_requests_inv_warehouses_ToWarehouseId",
                        column: x => x.ToWarehouseId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inv_warehouse_locations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LocationType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReservePriority = table.Column<int>(type: "integer", nullable: false),
                    PickingOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_inv_warehouse_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_warehouse_locations_inv_warehouse_locations_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouse_locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inv_warehouse_locations_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inv_transfer_request_items",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedQuantity = table.Column<int>(type: "integer", nullable: false),
                    PickedQuantity = table.Column<int>(type: "integer", nullable: false),
                    DeliveredQuantity = table.Column<int>(type: "integer", nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_inv_transfer_request_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_transfer_request_items_inv_transfer_requests_TransferRe~",
                        column: x => x.TransferRequestId,
                        principalSchema: "inventory",
                        principalTable: "inv_transfer_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inv_transfer_tracking",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inv_transfer_tracking", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_transfer_tracking_inv_transfer_requests_TransferRequest~",
                        column: x => x.TransferRequestId,
                        principalSchema: "inventory",
                        principalTable: "inv_transfer_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inv_stocks",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StockType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_inv_stocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_stocks_inv_warehouse_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouse_locations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_inv_stocks_inv_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "inventory",
                        principalTable: "inv_warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inv_stock_reservations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_inv_stock_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inv_stock_reservations_inv_stocks_StockId",
                        column: x => x.StockId,
                        principalSchema: "inventory",
                        principalTable: "inv_stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inv_stock_movements_ReferenceType_ReferenceId",
                schema: "inventory",
                table: "inv_stock_movements",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_stock_movements_VariantId",
                schema: "inventory",
                table: "inv_stock_movements",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_stock_reservations_ReferenceType_ReferenceId",
                schema: "inventory",
                table: "inv_stock_reservations",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_inv_stock_reservations_StockId",
                schema: "inventory",
                table: "inv_stock_reservations",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_stocks_LocationId",
                schema: "inventory",
                table: "inv_stocks",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_stocks_VariantId_WarehouseId_LocationId_StockType",
                schema: "inventory",
                table: "inv_stocks",
                columns: new[] { "VariantId", "WarehouseId", "LocationId", "StockType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_stocks_WarehouseId",
                schema: "inventory",
                table: "inv_stocks",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transfer_request_items_TransferRequestId",
                schema: "inventory",
                table: "inv_transfer_request_items",
                column: "TransferRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transfer_requests_Code",
                schema: "inventory",
                table: "inv_transfer_requests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_transfer_requests_FromWarehouseId",
                schema: "inventory",
                table: "inv_transfer_requests",
                column: "FromWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transfer_requests_ToWarehouseId",
                schema: "inventory",
                table: "inv_transfer_requests",
                column: "ToWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_transfer_tracking_TransferRequestId",
                schema: "inventory",
                table: "inv_transfer_tracking",
                column: "TransferRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_warehouse_locations_Barcode",
                schema: "inventory",
                table: "inv_warehouse_locations",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_warehouse_locations_ParentId",
                schema: "inventory",
                table: "inv_warehouse_locations",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_inv_warehouse_locations_WarehouseId_Code",
                schema: "inventory",
                table: "inv_warehouse_locations",
                columns: new[] { "WarehouseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inv_warehouses_Code",
                schema: "inventory",
                table: "inv_warehouses",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inv_stock_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_stock_reservations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_transfer_request_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_transfer_tracking",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_stocks",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_transfer_requests",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_warehouse_locations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inv_warehouses",
                schema: "inventory");
        }
    }
}
