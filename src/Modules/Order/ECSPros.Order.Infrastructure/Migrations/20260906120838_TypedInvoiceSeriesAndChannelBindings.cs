using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Order.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TypedInvoiceSeriesAndChannelBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ord_invoices_ord_invoice_series_InvoiceSeriesId",
                schema: "order",
                table: "ord_invoices");

            // FE0 (2026-09-06): üçlü set → tekil TİPLİ seri. Mevcut satır e-Arşiv serisi olarak kalır
            // (Id korunur → mevcut fatura FK'ları bozulmaz); e-Fatura/İhracat harfleri farklıysa ayrı
            // satır olarak açılır. Eski kolonlar veri taşındıktan SONRA düşer (aşağıda).
            migrationBuilder.RenameColumn(
                name: "EArchiveSerial",
                schema: "order",
                table: "ord_invoice_series",
                newName: "Serial");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceSeriesId",
                schema: "order",
                table: "ord_invoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "Ettn",
                schema: "order",
                table: "ord_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalDocumentId",
                schema: "order",
                table: "ord_invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSource",
                schema: "order",
                table: "ord_invoices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationContractId",
                schema: "order",
                table: "ord_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumberSource",
                schema: "order",
                table: "ord_invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "internal");

            migrationBuilder.AddColumn<string>(
                name: "SendMethod",
                schema: "order",
                table: "ord_invoices",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "order",
                table: "ord_invoice_series",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationContractId",
                schema: "order",
                table: "ord_invoice_series",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceType",
                schema: "order",
                table: "ord_invoice_series",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                schema: "order",
                table: "ord_invoice_series",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                -- 1) Mevcut satır = e-Arşiv serisi
                UPDATE "order".ord_invoice_series SET "InvoiceType" = 'e_archive' WHERE "InvoiceType" = '';

                -- 2) e-Fatura harfi farklıysa ayrı seri (aynı harf firma içinde zaten varsa AÇILMAZ — kullanıcı elle tanımlar)
                INSERT INTO "order".ord_invoice_series
                    ("Id","FirmId","Serial","InvoiceType","Name","IsActive","CreatedAt","CreatedBy","IsDeleted")
                SELECT gen_random_uuid(), s."FirmId", s."EInvoiceSerial", 'e_invoice',
                       COALESCE(s."Name",'') || ' (e-Fatura)', s."IsActive", timezone('utc', now()), s."CreatedBy", false
                  FROM "order".ord_invoice_series s
                 WHERE NOT s."IsDeleted" AND s."InvoiceType" = 'e_archive'
                   AND s."EInvoiceSerial" <> '' AND s."EInvoiceSerial" <> s."Serial"
                   AND NOT EXISTS (SELECT 1 FROM "order".ord_invoice_series x
                                    WHERE x."FirmId" = s."FirmId" AND x."Serial" = s."EInvoiceSerial" AND NOT x."IsDeleted");

                -- 3) İhracat harfi farklıysa ayrı seri
                INSERT INTO "order".ord_invoice_series
                    ("Id","FirmId","Serial","InvoiceType","Name","IsActive","CreatedAt","CreatedBy","IsDeleted")
                SELECT gen_random_uuid(), s."FirmId", s."ExportSerial", 'export',
                       COALESCE(s."Name",'') || ' (İhracat)', s."IsActive", timezone('utc', now()), s."CreatedBy", false
                  FROM "order".ord_invoice_series s
                 WHERE NOT s."IsDeleted" AND s."InvoiceType" = 'e_archive'
                   AND s."ExportSerial" <> '' AND s."ExportSerial" <> s."Serial" AND s."ExportSerial" <> s."EInvoiceSerial"
                   AND NOT EXISTS (SELECT 1 FROM "order".ord_invoice_series x
                                    WHERE x."FirmId" = s."FirmId" AND x."Serial" = s."ExportSerial" AND NOT x."IsDeleted");

                -- 4) Tipi uyuşmayan mevcut faturaları aynı harfli doğru tipteki seriye bağla (varsa)
                UPDATE "order".ord_invoices i
                   SET "InvoiceSeriesId" = s2."Id"
                  FROM "order".ord_invoice_series s1, "order".ord_invoice_series s2
                 WHERE i."InvoiceSeriesId" = s1."Id" AND s1."InvoiceType" <> i."InvoiceType"
                   AND s2."FirmId" = s1."FirmId" AND s2."Serial" = i."InvoiceSerial" AND s2."InvoiceType" = i."InvoiceType"
                   AND NOT s2."IsDeleted";
                """);

            migrationBuilder.DropColumn(
                name: "EInvoiceSerial",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.DropColumn(
                name: "ExportSerial",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.CreateTable(
                name: "ord_channel_invoice_series",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InvoiceSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ord_channel_invoice_series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_channel_invoice_series_ord_invoice_series_InvoiceSeries~",
                        column: x => x.InvoiceSeriesId,
                        principalSchema: "order",
                        principalTable: "ord_invoice_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ord_channel_invoice_settings",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    SendMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ord_channel_invoice_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ord_invoice_series_counters",
                schema: "order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    LastSequence = table.Column<int>(type: "integer", nullable: false),
                    LastInvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ord_invoice_series_counters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ord_invoice_series_counters_ord_invoice_series_InvoiceSerie~",
                        column: x => x.InvoiceSeriesId,
                        principalSchema: "order",
                        principalTable: "ord_invoice_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_Ettn",
                schema: "order",
                table: "ord_invoices",
                column: "Ettn",
                unique: true,
                filter: "\"Ettn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_ExternalSource_InvoiceNumber",
                schema: "order",
                table: "ord_invoices",
                columns: new[] { "ExternalSource", "InvoiceNumber" },
                unique: true,
                filter: "\"ExternalSource\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoices_IntegratorStatus",
                schema: "order",
                table: "ord_invoices",
                column: "IntegratorStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoice_series_FirmId_Serial",
                schema: "order",
                table: "ord_invoice_series",
                columns: new[] { "FirmId", "Serial" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoice_series_IntegrationContractId",
                schema: "order",
                table: "ord_invoice_series",
                column: "IntegrationContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_channel_invoice_series_FirmPlatformId_InvoiceType",
                schema: "order",
                table: "ord_channel_invoice_series",
                columns: new[] { "FirmPlatformId", "InvoiceType" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ord_channel_invoice_series_InvoiceSeriesId",
                schema: "order",
                table: "ord_channel_invoice_series",
                column: "InvoiceSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ord_channel_invoice_settings_FirmPlatformId",
                schema: "order",
                table: "ord_channel_invoice_settings",
                column: "FirmPlatformId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ord_invoice_series_counters_InvoiceSeriesId_Year",
                schema: "order",
                table: "ord_invoice_series_counters",
                columns: new[] { "InvoiceSeriesId", "Year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ord_invoices_ord_invoice_series_InvoiceSeriesId",
                schema: "order",
                table: "ord_invoices",
                column: "InvoiceSeriesId",
                principalSchema: "order",
                principalTable: "ord_invoice_series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                -- 5) Sayaçlar: mevcut faturalardan seri×yıl son sıra + son tarih
                INSERT INTO "order".ord_invoice_series_counters
                    ("Id","InvoiceSeriesId","Year","LastSequence","LastInvoiceDate","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(), i."InvoiceSeriesId", i."InvoiceYear",
                       max(i."InvoiceSequence"), max(i."InvoiceDate"), timezone('utc', now()), false
                  FROM "order".ord_invoices i
                 WHERE i."InvoiceSeriesId" IS NOT NULL AND NOT i."IsDeleted"
                 GROUP BY i."InvoiceSeriesId", i."InvoiceYear";

                -- 6) Kanal ayarı: her kanal 'manual' (yalnız kayıt) ile başlar
                INSERT INTO "order".ord_channel_invoice_settings
                    ("Id","FirmPlatformId","SendMethod","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(), fp."Id", 'manual', timezone('utc', now()), false
                  FROM core.core_firm_platforms fp
                 WHERE NOT fp."IsDeleted";

                -- 7) Kanal yuvaları: eski davranışı (firmanın ilk aktif serisi) korumak için her kanala,
                --    her tip için firmasının EN ESKİ aktif aynı-tipli serisi bağlanır (varsa)
                INSERT INTO "order".ord_channel_invoice_series
                    ("Id","FirmPlatformId","InvoiceType","InvoiceSeriesId","CreatedAt","IsDeleted")
                SELECT gen_random_uuid(), fp."Id", t.typ, s."Id", timezone('utc', now()), false
                  FROM core.core_firm_platforms fp
                 CROSS JOIN (VALUES ('e_archive'),('e_invoice'),('export')) AS t(typ)
                  JOIN LATERAL (
                        SELECT x."Id" FROM "order".ord_invoice_series x
                         WHERE x."FirmId" = fp."FirmId" AND x."InvoiceType" = t.typ AND x."IsActive" AND NOT x."IsDeleted"
                         ORDER BY x."CreatedAt" LIMIT 1) s ON true
                 WHERE NOT fp."IsDeleted";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ord_invoices_ord_invoice_series_InvoiceSeriesId",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropTable(
                name: "ord_channel_invoice_series",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_channel_invoice_settings",
                schema: "order");

            migrationBuilder.DropTable(
                name: "ord_invoice_series_counters",
                schema: "order");

            migrationBuilder.DropIndex(
                name: "IX_ord_invoices_Ettn",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropIndex(
                name: "IX_ord_invoices_ExternalSource_InvoiceNumber",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropIndex(
                name: "IX_ord_invoices_IntegratorStatus",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropIndex(
                name: "IX_ord_invoice_series_FirmId_Serial",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.DropIndex(
                name: "IX_ord_invoice_series_IntegrationContractId",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.DropColumn(
                name: "Ettn",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "ExternalDocumentId",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "ExternalSource",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "IntegrationContractId",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "NumberSource",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "SendMethod",
                schema: "order",
                table: "ord_invoices");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.DropColumn(
                name: "IntegrationContractId",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.DropColumn(
                name: "InvoiceType",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                schema: "order",
                table: "ord_invoice_series");

            migrationBuilder.RenameColumn(
                name: "Serial",
                schema: "order",
                table: "ord_invoice_series",
                newName: "EArchiveSerial");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceSeriesId",
                schema: "order",
                table: "ord_invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExportSerial",
                schema: "order",
                table: "ord_invoice_series",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EInvoiceSerial",
                schema: "order",
                table: "ord_invoice_series",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_ord_invoices_ord_invoice_series_InvoiceSeriesId",
                schema: "order",
                table: "ord_invoices",
                column: "InvoiceSeriesId",
                principalSchema: "order",
                principalTable: "ord_invoice_series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
