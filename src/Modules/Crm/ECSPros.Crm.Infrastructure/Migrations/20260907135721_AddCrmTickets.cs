using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "crm_ticket_tracking_seq",
                schema: "crm",
                startValue: 50000L);

            migrationBuilder.CreateTable(
                name: "crm_ticket_legacy_staff",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegacyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_crm_ticket_legacy_staff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_ticket_notifications",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNo = table.Column<long>(type: "bigint", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LegacyId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_crm_ticket_notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_ticket_reads",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LegacyId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_crm_ticket_reads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_ticket_statuses",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ExemptFromDuplicateCheck = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LegacyId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_crm_ticket_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_ticket_subjects",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredFields = table.Column<List<string>>(type: "jsonb", nullable: false),
                    LegacyId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_crm_ticket_subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "crm_tickets",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingNo = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    LegacyMemberId = table.Column<int>(type: "integer", nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CallerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CallerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: true),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    Attachments = table.Column<List<string>>(type: "jsonb", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityCount = table.Column<int>(type: "integer", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    LegacyId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_crm_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_tickets_crm_ticket_statuses_StatusId",
                        column: x => x.StatusId,
                        principalSchema: "crm",
                        principalTable: "crm_ticket_statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_tickets_crm_ticket_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "crm",
                        principalTable: "crm_ticket_subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crm_ticket_activities",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    Attachments = table.Column<List<string>>(type: "jsonb", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatusId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaggedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaggedUserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    LegacyId = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_crm_ticket_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_ticket_activities_crm_tickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "crm",
                        principalTable: "crm_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_activities_LegacyId",
                schema: "crm",
                table: "crm_ticket_activities",
                column: "LegacyId",
                unique: true,
                filter: "\"LegacyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_activities_TaggedUserId",
                schema: "crm",
                table: "crm_ticket_activities",
                column: "TaggedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_activities_TicketId_CreatedAt",
                schema: "crm",
                table: "crm_ticket_activities",
                columns: new[] { "TicketId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_legacy_staff_LegacyId",
                schema: "crm",
                table: "crm_ticket_legacy_staff",
                column: "LegacyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_notifications_LegacyId",
                schema: "crm",
                table: "crm_ticket_notifications",
                column: "LegacyId",
                unique: true,
                filter: "\"LegacyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_notifications_UserId_CreatedAt",
                schema: "crm",
                table: "crm_ticket_notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_notifications_UserId_TicketId",
                schema: "crm",
                table: "crm_ticket_notifications",
                columns: new[] { "UserId", "TicketId" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_reads_ActivityId_UserId",
                schema: "crm",
                table: "crm_ticket_reads",
                columns: new[] { "ActivityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_reads_TicketId_UserId",
                schema: "crm",
                table: "crm_ticket_reads",
                columns: new[] { "TicketId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_statuses_Code",
                schema: "crm",
                table: "crm_ticket_statuses",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_crm_ticket_subjects_LegacyId",
                schema: "crm",
                table: "crm_ticket_subjects",
                column: "LegacyId",
                unique: true,
                filter: "\"LegacyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_CallerPhone",
                schema: "crm",
                table: "crm_tickets",
                column: "CallerPhone");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_CreatedAt",
                schema: "crm",
                table: "crm_tickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_CustomerPhone",
                schema: "crm",
                table: "crm_tickets",
                column: "CustomerPhone");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_LegacyId",
                schema: "crm",
                table: "crm_tickets",
                column: "LegacyId",
                unique: true,
                filter: "\"LegacyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_MemberId",
                schema: "crm",
                table: "crm_tickets",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_OrderId",
                schema: "crm",
                table: "crm_tickets",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_OrderNumber",
                schema: "crm",
                table: "crm_tickets",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_StatusId_LastActivityAt",
                schema: "crm",
                table: "crm_tickets",
                columns: new[] { "StatusId", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_SubjectId",
                schema: "crm",
                table: "crm_tickets",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_tickets_TrackingNo",
                schema: "crm",
                table: "crm_tickets",
                column: "TrackingNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crm_ticket_activities",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "crm_ticket_legacy_staff",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "crm_ticket_notifications",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "crm_ticket_reads",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "crm_tickets",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "crm_ticket_statuses",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "crm_ticket_subjects",
                schema: "crm");

            migrationBuilder.DropSequence(
                name: "crm_ticket_tracking_seq",
                schema: "crm");
        }
    }
}
