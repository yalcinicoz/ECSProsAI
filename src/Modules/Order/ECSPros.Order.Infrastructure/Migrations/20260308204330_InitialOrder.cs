using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "order");

            migrationBuilder.CreateTable(
                name: "ord_gift_cards",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    IsSingleUse = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedForMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedFromOrderId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ord_gift_cards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ord_invoice_series",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EArchiveSerial = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EInvoiceSerial = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExportSerial = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_ord_invoice_series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ord_orders",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CartId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentTermsDays = table.Column<int>(type: "integer", nullable: true),
                    PaymentDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPosSale = table.Column<bool>(type: "boolean", nullable: false),
                    PosSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PosRegisterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InvoiceCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ShippingRecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShippingRecipientPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShippingCountryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingCityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingDistrictId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippingNeighborhoodId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShippingAddressLine = table.Column<string>(type: "text", nullable: false),
                    ShippingPostalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ShippingDeliveryNotes = table.Column<string>(type: "text", nullable: true),
                    BillingSameAsShipping = table.Column<bool>(type: "boolean", nullable: false),
                    BillingRecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingTaxOffice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingTaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BillingCompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BillingCountryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingCityId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingDistrictId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingAddressLine = table.Column<string>(type: "text", nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalExpense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultCargoFirmId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerNotes = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    ConfirmationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PickingPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortingBinId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackingStationCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PackingSlotNumber = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_ord_orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ord_quotes",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NotesToCustomer = table.Column<string>(type: "text", nullable: true),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedOrderId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ord_quotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ord_returns",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CustomerNotes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReturnCargoFirmId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReturnTrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReturnCargoSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnCargoReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InspectionNotes = table.Column<string>(type: "text", nullable: true),
                    InspectionCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InspectionCompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RefundStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ord_returns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ord_gift_card_transactions",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GiftCardId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ord_gift_card_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_gift_card_transactions_ord_gift_cards_GiftCardId",
                        column: x => x.GiftCardId,
                        principalSchema: "order",
                        principalTable: "ord_gift_cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_invoices",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvoiceSerial = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InvoiceYear = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    InvoiceSequence = table.Column<int>(type: "integer", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientTaxOffice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecipientTaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RecipientCompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecipientAddress = table.Column<string>(type: "text", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDiscount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IntegratorStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IntegratorSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IntegratorResponse = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    IntegratorInvoiceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErpStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ErpSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErpReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CancelledByInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancelsInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ord_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_invoices_ord_invoice_series_InvoiceSeriesId",
                        column: x => x.InvoiceSeriesId,
                        principalSchema: "order",
                        principalTable: "ord_invoice_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ord_invoices_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_order_discounts",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DiscountSourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ord_order_discounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_order_discounts_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_order_expenses",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ord_order_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_order_expenses_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_order_items",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VariantInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PickAssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    PickAssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PickedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    PickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortingBinQuantity = table.Column<int>(type: "integer", nullable: false),
                    FinalSortQuantity = table.Column<int>(type: "integer", nullable: false),
                    FinalScanBy = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalScanAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalScanQuantity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ord_order_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_order_items_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_order_notifications",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Recipient = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderResponse = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ord_order_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_order_notifications_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_order_payments",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Details = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_ord_order_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_order_payments_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_order_taxes",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderExpenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaxType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ord_order_taxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_order_taxes_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_shipments",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TrackingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CargoStatusRaw = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ApiStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ApiRequestPayload = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    ApiResponsePayload = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    ApiSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliverySignature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeliveryNotes = table.Column<string>(type: "text", nullable: true),
                    PackageCount = table.Column<int>(type: "integer", nullable: false),
                    TotalWeight = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    TotalDesi = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
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
                    table.PrimaryKey("PK_ord_shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_shipments_ord_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "order",
                        principalTable: "ord_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_quote_items",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VariantInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ord_quote_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_quote_items_ord_quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "order",
                        principalTable: "ord_quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_return_items",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ReturnReasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerNotes = table.Column<string>(type: "text", nullable: true),
                    UnitRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InspectionResult = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    InspectionNotes = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_ord_return_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_return_items_ord_returns_ReturnId",
                        column: x => x.ReturnId,
                        principalSchema: "order",
                        principalTable: "ord_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_return_refunds",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Details = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    OriginalPaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WalletTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ord_return_refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_return_refunds_ord_returns_ReturnId",
                        column: x => x.ReturnId,
                        principalSchema: "order",
                        principalTable: "ord_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_invoice_items",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_ord_invoice_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_invoice_items_ord_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "order",
                        principalTable: "ord_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_shipment_events",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawData = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ord_shipment_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_shipment_events_ord_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "order",
                        principalTable: "ord_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ord_shipment_items",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ord_shipment_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_shipment_items_ord_shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalSchema: "order",
                        principalTable: "ord_shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ord_gift_card_transactions_GiftCardId",
                schema: "order",
                table: "ord_gift_card_transactions",
                column: "GiftCardId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_gift_cards_Code",
                schema: "order",
                table: "ord_gift_cards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoice_items_InvoiceId",
                schema: "order",
                table: "ord_invoice_items",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_InvoiceSerial_InvoiceYear_InvoiceSequence",
                schema: "order",
                table: "ord_invoices",
                columns: new[] { "InvoiceSerial", "InvoiceYear", "InvoiceSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_InvoiceSeriesId",
                schema: "order",
                table: "ord_invoices",
                column: "InvoiceSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_OrderId",
                schema: "order",
                table: "ord_invoices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_discounts_OrderId",
                schema: "order",
                table: "ord_order_discounts",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_expenses_OrderId",
                schema: "order",
                table: "ord_order_expenses",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_items_OrderId",
                schema: "order",
                table: "ord_order_items",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_notifications_OrderId",
                schema: "order",
                table: "ord_order_notifications",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_payments_OrderId",
                schema: "order",
                table: "ord_order_payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_order_taxes_OrderId",
                schema: "order",
                table: "ord_order_taxes",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_orders_OrderNumber",
                schema: "order",
                table: "ord_orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_quote_items_QuoteId",
                schema: "order",
                table: "ord_quote_items",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_quotes_QuoteNumber",
                schema: "order",
                table: "ord_quotes",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_return_items_ReturnId",
                schema: "order",
                table: "ord_return_items",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_return_refunds_ReturnId",
                schema: "order",
                table: "ord_return_refunds",
                column: "ReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_returns_ReturnNumber",
                schema: "order",
                table: "ord_returns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ord_shipment_events_ShipmentId",
                schema: "order",
                table: "ord_shipment_events",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_shipment_items_ShipmentId",
                schema: "order",
                table: "ord_shipment_items",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_shipments_OrderId",
                schema: "order",
                table: "ord_shipments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_shipments_ShipmentNumber",
                schema: "order",
                table: "ord_shipments",
                column: "ShipmentNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ord_gift_card_transactions",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_invoice_items",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_order_discounts",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_order_expenses",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_order_items",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_order_notifications",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_order_payments",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_order_taxes",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_quote_items",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_return_items",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_return_refunds",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_shipment_events",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_shipment_items",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_gift_cards",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_invoices",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_quotes",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_returns",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_shipments",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_invoice_series",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_orders",
                schema: "order");
        }
    }
}
