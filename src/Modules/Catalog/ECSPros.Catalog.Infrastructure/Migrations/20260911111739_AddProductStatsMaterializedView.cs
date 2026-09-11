using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStatsMaterializedView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-09-11 admin ürün listesi kapsamlı filtre: görsel durumu (renk bazlı var/kısmi/yok) + stok toplamları.
            // Tam hesap ~2-3 sn (29K ürün / 334K varyant) → her istekte değil, MATERIALIZED VIEW + 5 dk'da bir
            // CONCURRENTLY yenileme (ProductStatsRefreshWorker; unique indeks CONCURRENTLY için şart).
            // Renk = grubun birincil ekseni (definition.product_group_attributes.IsPrimaryAxis); eksensiz üründe tek renk sayılır.
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
