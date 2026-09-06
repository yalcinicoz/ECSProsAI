using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDispatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ord_invoice_dispatches",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IntegrationContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestSnapshot = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    ResponseSnapshot = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_ord_invoice_dispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_invoice_dispatches_ord_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "order",
                        principalTable: "ord_invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoice_dispatches_InvoiceId",
                schema: "order",
                table: "ord_invoice_dispatches",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoice_dispatches_Status_NextAttemptAt",
                schema: "order",
                table: "ord_invoice_dispatches",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ord_invoice_dispatches",
                schema: "order");
        }
    }
}
