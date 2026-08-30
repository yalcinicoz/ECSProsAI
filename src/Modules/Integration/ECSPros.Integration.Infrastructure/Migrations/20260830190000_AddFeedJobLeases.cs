using ECSPros.Integration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Integration.Infrastructure.Migrations;

/// <summary>
/// FAZ 11 / K0 — mevcut feed işleri kaybedilmeden kalıcı durum/lease kuyruğuna geçiş.
/// Aynı kanal için eski kuyrukta birden fazla bekleyen satır varsa en eskisi pending
/// kalır; diğerleri completed olarak arşivlenir ve partial unique index kurulabilir.
/// </summary>
[DbContext(typeof(IntegrationDbContext))]
[Migration("20260830190000_AddFeedJobLeases")]
public sealed class AddFeedJobLeases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AttemptCount", schema: "integration", table: "feed_jobs",
            type: "integer", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTime>(
            name: "CompletedAt", schema: "integration", table: "feed_jobs",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "LastError", schema: "integration", table: "feed_jobs",
            type: "character varying(2000)", maxLength: 2000, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "LeaseOwner", schema: "integration", table: "feed_jobs",
            type: "character varying(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "LeaseUntil", schema: "integration", table: "feed_jobs",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "StartedAt", schema: "integration", table: "feed_jobs",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "Status", schema: "integration", table: "feed_jobs",
            type: "character varying(20)", maxLength: 20, nullable: false,
            defaultValue: "pending");

        migrationBuilder.DropIndex(
            name: "IX_feed_jobs_FirmPlatformId", schema: "integration", table: "feed_jobs");

        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT "Id",
                       row_number() OVER (
                           PARTITION BY "FirmPlatformId"
                           ORDER BY "RequestedAt", "CreatedAt", "Id") AS rn
                FROM integration.feed_jobs
                WHERE "IsDeleted" = false
            )
            UPDATE integration.feed_jobs AS jobs
            SET "Status" = 'completed',
                "CompletedAt" = NOW(),
                "LastError" = 'Migration sırasında yinelenen bekleyen tetik birleştirildi.',
                "UpdatedAt" = NOW()
            FROM ranked
            WHERE jobs."Id" = ranked."Id" AND ranked.rn > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_feed_jobs_Status_RequestedAt_LeaseUntil",
            schema: "integration", table: "feed_jobs",
            columns: new[] { "Status", "RequestedAt", "LeaseUntil" });
        migrationBuilder.CreateIndex(
            name: "IX_feed_jobs_FirmPlatformId_Active",
            schema: "integration", table: "feed_jobs", column: "FirmPlatformId",
            unique: true,
            filter: "\"Status\" IN ('pending', 'processing') AND \"IsDeleted\" = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_feed_jobs_FirmPlatformId_Active", schema: "integration", table: "feed_jobs");
        migrationBuilder.DropIndex(
            name: "IX_feed_jobs_Status_RequestedAt_LeaseUntil", schema: "integration", table: "feed_jobs");

        migrationBuilder.DropColumn(name: "AttemptCount", schema: "integration", table: "feed_jobs");
        migrationBuilder.DropColumn(name: "CompletedAt", schema: "integration", table: "feed_jobs");
        migrationBuilder.DropColumn(name: "LastError", schema: "integration", table: "feed_jobs");
        migrationBuilder.DropColumn(name: "LeaseOwner", schema: "integration", table: "feed_jobs");
        migrationBuilder.DropColumn(name: "LeaseUntil", schema: "integration", table: "feed_jobs");
        migrationBuilder.DropColumn(name: "StartedAt", schema: "integration", table: "feed_jobs");
        migrationBuilder.DropColumn(name: "Status", schema: "integration", table: "feed_jobs");

        migrationBuilder.CreateIndex(
            name: "IX_feed_jobs_FirmPlatformId",
            schema: "integration", table: "feed_jobs", column: "FirmPlatformId");
    }
}
