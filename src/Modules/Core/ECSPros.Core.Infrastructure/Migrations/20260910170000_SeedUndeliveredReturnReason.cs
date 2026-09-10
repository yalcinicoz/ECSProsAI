using ECSPros.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <summary>
    /// İade akışı planı (2026-09-10): sistem iade nedeni "Teslim Edilemedi" — Teslimatsız İade kalemleri
    /// SABİT Id (Order.Domain ReturnConstants.UndeliveredReasonId = a1d3c0de-7e51-4d1e-9a9e-0000f1ade001) ile
    /// açılır. Pasif (müşteri formunda görünmez), panel adını lookup'tan çözer. Veri-only, idempotent; model
    /// değişikliği yok (snapshot dokunulmadı). `return_reason` tipi yoksa hiçbir şey yazmaz (seed sonra tamamlar).
    /// </summary>
    [DbContext(typeof(CoreDbContext))]
    [Migration("20260910170000_SeedUndeliveredReturnReason")]
    public partial class SeedUndeliveredReturnReason : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO core.core_lookup_values
                    ("Id", "LookupTypeId", "NameI18n", "Color", "Icon", "ExtraData", "IsDefault", "IsActive", "SortOrder",
                     "CreatedAt", "IsDeleted")
                SELECT 'a1d3c0de-7e51-4d1e-9a9e-0000f1ade001'::uuid, t."Id",
                       '{"tr": "Teslim Edilemedi", "en": "Undelivered"}'::jsonb, NULL, NULL,
                       '{"systemCode": "undelivered", "subReasons": []}'::jsonb, false, false, 900,
                       now() at time zone 'utc', false
                FROM core.core_lookup_types t
                WHERE t."Code" = 'return_reason' AND t."IsDeleted" = false
                  AND NOT EXISTS (SELECT 1 FROM core.core_lookup_values v WHERE v."Id" = 'a1d3c0de-7e51-4d1e-9a9e-0000f1ade001'::uuid)
                LIMIT 1;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM core.core_lookup_values WHERE "Id" = 'a1d3c0de-7e51-4d1e-9a9e-0000f1ade001'::uuid;""");
        }
    }
}
