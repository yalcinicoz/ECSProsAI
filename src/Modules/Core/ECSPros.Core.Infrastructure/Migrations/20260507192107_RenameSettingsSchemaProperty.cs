using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameSettingsSchemaProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SettingsSchema",
                schema: "core",
                table: "core_platform_types",
                newName: "settings_schema");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "settings_schema",
                schema: "core",
                table: "core_platform_types",
                newName: "SettingsSchema");
        }
    }
}
