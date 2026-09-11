using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Promotion.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prm_games",
                schema: "promotion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    SubtitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    RulesTextI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    CtaLabel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThemeColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    AccentColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    RequiresLogin = table.Column<bool>(type: "boolean", nullable: false),
                    AlwaysWin = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LimitPeriod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LimitCount = table.Column<int>(type: "integer", nullable: false),
                    CouponValidDays = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    LabelAvailable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LabelCooldown = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LabelExhausted = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LabelLoginRequired = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LabelEnded = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WinMessage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WinSubMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LoseMessage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LoseSubMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_prm_games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prm_game_prizes",
                schema: "promotion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortLabel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CouponType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CouponValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    MinimumCartTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_prm_game_prizes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prm_game_prizes_prm_games_GameId",
                        column: x => x.GameId,
                        principalSchema: "promotion",
                        principalTable: "prm_games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prm_game_plays",
                schema: "promotion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrizeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Won = table.Column<bool>(type: "boolean", nullable: false),
                    CouponId = table.Column<Guid>(type: "uuid", nullable: true),
                    CouponCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PointsGiven = table.Column<int>(type: "integer", nullable: true),
                    CellsJson = table.Column<string>(type: "text", nullable: true),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_prm_game_plays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prm_game_plays_prm_game_prizes_PrizeId",
                        column: x => x.PrizeId,
                        principalSchema: "promotion",
                        principalTable: "prm_game_prizes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prm_game_plays_prm_games_GameId",
                        column: x => x.GameId,
                        principalSchema: "promotion",
                        principalTable: "prm_games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prm_game_plays_GameId_MemberId_PeriodKey",
                schema: "promotion",
                table: "prm_game_plays",
                columns: new[] { "GameId", "MemberId", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "IX_prm_game_plays_MemberId_PlayedAt",
                schema: "promotion",
                table: "prm_game_plays",
                columns: new[] { "MemberId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_prm_game_plays_PrizeId",
                schema: "promotion",
                table: "prm_game_plays",
                column: "PrizeId");

            migrationBuilder.CreateIndex(
                name: "IX_prm_game_prizes_GameId",
                schema: "promotion",
                table: "prm_game_prizes",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_prm_games_FirmPlatformId_Code",
                schema: "promotion",
                table: "prm_games",
                columns: new[] { "FirmPlatformId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prm_game_plays",
                schema: "promotion");

            migrationBuilder.DropTable(
                name: "prm_game_prizes",
                schema: "promotion");

            migrationBuilder.DropTable(
                name: "prm_games",
                schema: "promotion");
        }
    }
}
