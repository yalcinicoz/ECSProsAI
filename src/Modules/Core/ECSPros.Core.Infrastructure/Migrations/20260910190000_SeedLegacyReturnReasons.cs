using ECSPros.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <summary>
    /// İade planı legacy düzeltmesi (2026-09-10): eski dfiadenedenleri 4 (Defo) ve 5 (Kalitesiz) için hedef neden
    /// kayıtları — LegacyReturnMappings.ReasonCode bunlara eşler; yoksa import "hedef iade nedeni yok" engeli verirdi.
    /// Veri-only, idempotent; model değişikliği yok.
    /// </summary>
    [DbContext(typeof(CoreDbContext))]
    [Migration("20260910190000_SeedLegacyReturnReasons")]
    public partial class SeedLegacyReturnReasons : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO core.core_return_reasons ("Id","Code","NameI18n","RequiresInspection","IsCustomerFault","IsActive","SortOrder","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(), v.code, v.name::jsonb, false, false, true, v.sira, now() at time zone 'utc', false
                  FROM (VALUES
                        ('legacy_defective',   '{"tr": "Defo (Eski Sistem)", "en": "Defective (Legacy)"}', 905),
                        ('legacy_low_quality', '{"tr": "Kalitesiz (Eski Sistem)", "en": "Low Quality (Legacy)"}', 906)
                       ) AS v(code, name, sira)
                 WHERE NOT EXISTS (SELECT 1 FROM core.core_return_reasons r WHERE r."Code" = v.code);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM core.core_return_reasons WHERE "Code" IN ('legacy_defective','legacy_low_quality');""");
        }
    }
}
