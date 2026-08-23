using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelCapabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "capabilities",
                schema: "core",
                table: "core_platform_types",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "capability_overrides",
                schema: "core",
                table: "core_firm_platforms",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capabilities",
                schema: "core",
                table: "core_platform_types");

            migrationBuilder.DropColumn(
                name: "capability_overrides",
                schema: "core",
                table: "core_firm_platforms");
        }
    }
}
