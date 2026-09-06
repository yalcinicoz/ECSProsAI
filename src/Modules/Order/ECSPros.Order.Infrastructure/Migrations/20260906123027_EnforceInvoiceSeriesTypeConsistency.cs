using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceInvoiceSeriesTypeConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FE0 sertleştirme (2026-09-06, kullanıcı: "e-Arşiv serisi KESİNLİKLE yalnız e-Arşiv için"):
            // seri tipi ile fatura tipi / kanal yuvası tipi eşleşmesi artık VERİTABANI düzeyinde bileşik
            // yabancı anahtarla zorunlu — uygulama kodu atlansa bile yanlış tipte kesim/bağ imkânsız.
            // (FirmId, Serial) tekilliği önceki migration'daki filtreli unique index ile zaten sağlanır.
            migrationBuilder.Sql("""
                ALTER TABLE "order".ord_invoice_series
                    ADD CONSTRAINT "UQ_ord_invoice_series_Id_InvoiceType" UNIQUE ("Id", "InvoiceType");

                ALTER TABLE "order".ord_invoices
                    ADD CONSTRAINT "FK_ord_invoices_series_type"
                    FOREIGN KEY ("InvoiceSeriesId", "InvoiceType")
                    REFERENCES "order".ord_invoice_series ("Id", "InvoiceType");

                ALTER TABLE "order".ord_channel_invoice_series
                    ADD CONSTRAINT "FK_ord_channel_invoice_series_series_type"
                    FOREIGN KEY ("InvoiceSeriesId", "InvoiceType")
                    REFERENCES "order".ord_invoice_series ("Id", "InvoiceType");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "order".ord_channel_invoice_series DROP CONSTRAINT IF EXISTS "FK_ord_channel_invoice_series_series_type";
                ALTER TABLE "order".ord_invoices DROP CONSTRAINT IF EXISTS "FK_ord_invoices_series_type";
                ALTER TABLE "order".ord_invoice_series DROP CONSTRAINT IF EXISTS "UQ_ord_invoice_series_Id_InvoiceType";
                """);
        }
    }
}
