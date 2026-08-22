using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingConsentLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tracking_consent_log",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Analytics = table.Column<bool>(type: "boolean", nullable: false),
                    Ads = table.Column<bool>(type: "boolean", nullable: false),
                    Personalization = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_tracking_consent_log", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tracking_consent_log_ConsentId",
                schema: "integration",
                table: "tracking_consent_log",
                column: "ConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_tracking_consent_log_FirmPlatformId_CreatedAt",
                schema: "integration",
                table: "tracking_consent_log",
                columns: new[] { "FirmPlatformId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tracking_consent_log_MemberId_CreatedAt",
                schema: "integration",
                table: "tracking_consent_log",
                columns: new[] { "MemberId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tracking_consent_log",
                schema: "integration");
        }
    }
}
