using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Iam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "definition");

            migrationBuilder.CreateTable(
                name: "api_client_types",
                schema: "definition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DefaultClientType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequiredOwnerType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    BaseScopes = table.Column<List<string>>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_api_client_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "api_clients",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SecretHint = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClientType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApiClientTypeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    FulfillmentMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IpAllowList = table.Column<List<string>>(type: "jsonb", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_api_clients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_client_types_Code",
                schema: "definition",
                table: "api_client_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_clients_ClientId",
                schema: "iam",
                table: "api_clients",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_clients_OwnerType_OwnerId",
                schema: "iam",
                table: "api_clients",
                columns: new[] { "OwnerType", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_client_types",
                schema: "definition");

            migrationBuilder.DropTable(
                name: "api_clients",
                schema: "iam");
        }
    }
}
