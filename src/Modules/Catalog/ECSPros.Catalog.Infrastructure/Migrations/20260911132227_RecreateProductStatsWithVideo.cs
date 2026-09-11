using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecreateProductStatsWithVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-09-11: "Video Durumu" filtresi — mv_product_stats'a VideoCount kolonu. MATERIALIZED VIEW ALTER edilemez →
            // düşür/yeniden oluştur (ProductStatsSql.Create güncel tanım; unique indeks CONCURRENTLY yenileme için şart).
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS catalog.mv_product_stats;");
            migrationBuilder.Sql(ProductStatsSql.Create);
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_mv_product_stats_ProductId\" ON catalog.mv_product_stats (\"ProductId\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_mv_product_stats_ImageState\" ON catalog.mv_product_stats (\"ImageState\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP MATERIALIZED VIEW IF EXISTS catalog.mv_product_stats;");
        }
    }
}
