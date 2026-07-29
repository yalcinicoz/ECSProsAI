using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Accounts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierKind",
                schema: "accounts",
                table: "current_accounts",
                type: "text",
                nullable: false,
                defaultValue: "normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierKind",
                schema: "accounts",
                table: "current_accounts");
        }
    }
}
