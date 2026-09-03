using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyImportCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legacy_import_checkpoints",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slice = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PlatformId = table.Column<int>(type: "integer", nullable: false),
                    WatermarkUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSourceId = table.Column<long>(type: "bigint", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_legacy_import_checkpoints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_checkpoints_PlatformId_Slice",
                schema: "integration",
                table: "legacy_import_checkpoints",
                columns: new[] { "PlatformId", "Slice" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legacy_import_checkpoints",
                schema: "integration");
        }
    }
}
