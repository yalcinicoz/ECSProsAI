using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Iam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserSessionSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "iam",
                table: "iam_user_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "iam",
                table: "iam_user_sessions");
        }
    }
}
