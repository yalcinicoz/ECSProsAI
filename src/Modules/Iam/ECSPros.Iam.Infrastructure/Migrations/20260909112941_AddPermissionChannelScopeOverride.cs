using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Iam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionChannelScopeOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ChannelScopedOverridden",
                schema: "iam",
                table: "iam_permissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelScopedOverridden",
                schema: "iam",
                table: "iam_permissions");
        }
    }
}
