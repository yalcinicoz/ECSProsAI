using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUiTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_ui_translations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Namespace = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Lang = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_core_ui_translations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_ui_translations_Namespace_Key_Lang",
                schema: "core",
                table: "core_ui_translations",
                columns: new[] { "Namespace", "Key", "Lang" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_ui_translations",
                schema: "core");
        }
    }
}
