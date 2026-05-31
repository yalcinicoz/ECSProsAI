using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLookupValueCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_core_lookup_values_LookupTypeId_Code",
                schema: "core",
                table: "core_lookup_values");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "core",
                table: "core_lookup_values");

            migrationBuilder.CreateIndex(
                name: "IX_core_lookup_values_LookupTypeId",
                schema: "core",
                table: "core_lookup_values",
                column: "LookupTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_core_lookup_values_LookupTypeId",
                schema: "core",
                table: "core_lookup_values");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "core",
                table: "core_lookup_values",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_core_lookup_values_LookupTypeId_Code",
                schema: "core",
                table: "core_lookup_values",
                columns: new[] { "LookupTypeId", "Code" },
                unique: true);
        }
    }
}
