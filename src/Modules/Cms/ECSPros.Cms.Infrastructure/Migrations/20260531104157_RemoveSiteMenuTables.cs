using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Cms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSiteMenuTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_menu_panel_items",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_menu_panel_groups",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_menu_mega_panels",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_site_menu_items",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_site_menus",
                schema: "cms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cms_site_menus",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayStyle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MenuType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_site_menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_site_menu_items",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_site_menu_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_site_menu_items_cms_site_menu_items_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "cms",
                        principalTable: "cms_site_menu_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_cms_site_menu_items_cms_site_menus_MenuId",
                        column: x => x.MenuId,
                        principalSchema: "cms",
                        principalTable: "cms_site_menus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cms_menu_mega_panels",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BackgroundImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ColumnCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomCss = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LayoutType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_menu_mega_panels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_menu_mega_panels_cms_site_menu_items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "cms",
                        principalTable: "cms_site_menu_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cms_menu_panel_groups",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MegaPanelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColumnIndex = table.Column<int>(type: "integer", nullable: false),
                    ColumnSpan = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    ShowTitle = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TitleStyle = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_menu_panel_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_menu_panel_groups_cms_menu_mega_panels_MegaPanelId",
                        column: x => x.MegaPanelId,
                        principalSchema: "cms",
                        principalTable: "cms_menu_mega_panels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cms_menu_panel_items",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PanelGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BadgeText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomHtml = table.Column<string>(type: "text", nullable: true),
                    CustomUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    GenderFilter = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ImagePosition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cms_menu_panel_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_menu_panel_items_cms_menu_panel_groups_PanelGroupId",
                        column: x => x.PanelGroupId,
                        principalSchema: "cms",
                        principalTable: "cms_menu_panel_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cms_menu_mega_panels_MenuItemId",
                schema: "cms",
                table: "cms_menu_mega_panels",
                column: "MenuItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_menu_panel_groups_MegaPanelId",
                schema: "cms",
                table: "cms_menu_panel_groups",
                column: "MegaPanelId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_menu_panel_items_PanelGroupId",
                schema: "cms",
                table: "cms_menu_panel_items",
                column: "PanelGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_site_menu_items_MenuId",
                schema: "cms",
                table: "cms_site_menu_items",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_site_menu_items_ParentId",
                schema: "cms",
                table: "cms_site_menu_items",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_site_menus_FirmPlatformId_Code",
                schema: "cms",
                table: "cms_site_menus",
                columns: new[] { "FirmPlatformId", "Code" },
                unique: true);
        }
    }
}
