using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSuppliersUseCurrentAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fin_suppliers",
                schema: "finance");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                schema: "finance",
                table: "fin_supplier_transactions",
                newName: "CurrentAccountId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                schema: "finance",
                table: "fin_supplier_returns",
                newName: "CurrentAccountId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                schema: "finance",
                table: "fin_supplier_price_history",
                newName: "CurrentAccountId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                schema: "finance",
                table: "fin_supplier_payments",
                newName: "CurrentAccountId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                schema: "finance",
                table: "fin_supplier_invoices",
                newName: "CurrentAccountId");

            migrationBuilder.RenameColumn(
                name: "SupplierId",
                schema: "finance",
                table: "fin_supplier_deliveries",
                newName: "CurrentAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CurrentAccountId",
                schema: "finance",
                table: "fin_supplier_transactions",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "CurrentAccountId",
                schema: "finance",
                table: "fin_supplier_returns",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "CurrentAccountId",
                schema: "finance",
                table: "fin_supplier_price_history",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "CurrentAccountId",
                schema: "finance",
                table: "fin_supplier_payments",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "CurrentAccountId",
                schema: "finance",
                table: "fin_supplier_invoices",
                newName: "SupplierId");

            migrationBuilder.RenameColumn(
                name: "CurrentAccountId",
                schema: "finance",
                table: "fin_supplier_deliveries",
                newName: "SupplierId");

            migrationBuilder.CreateTable(
                name: "fin_suppliers",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TaxNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TaxOffice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fin_suppliers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fin_suppliers_Code",
                schema: "finance",
                table: "fin_suppliers",
                column: "Code",
                unique: true);
        }
    }
}
