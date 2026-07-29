using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyMemberId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LegacyMemberId",
                schema: "crm",
                table: "crm_members",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_crm_members_LegacyMemberId",
                schema: "crm",
                table: "crm_members",
                column: "LegacyMemberId",
                unique: true,
                filter: "\"LegacyMemberId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_crm_members_LegacyMemberId",
                schema: "crm",
                table: "crm_members");

            migrationBuilder.DropColumn(
                name: "LegacyMemberId",
                schema: "crm",
                table: "crm_members");
        }
    }
}
