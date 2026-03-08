using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Cms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cms");

            migrationBuilder.CreateTable(
                name: "cms_page_templates",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    TemplateType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DefaultLayout = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_cms_page_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_product_lists",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ListType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FilterRules = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    ProductLimit = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_cms_product_lists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_section_types",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    SettingsSchema = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    SupportsItems = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_cms_section_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_site_menus",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    MenuType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayStyle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_cms_site_menus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cms_pages",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmPlatformId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    SlugI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    PageType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetGender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TargetCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetaTitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    MetaDescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PublishAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnpublishAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_cms_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_pages_cms_page_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "cms",
                        principalTable: "cms_page_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cms_product_list_items",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductListId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_cms_product_list_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_product_list_items_cms_product_lists_ProductListId",
                        column: x => x.ProductListId,
                        principalSchema: "cms",
                        principalTable: "cms_product_lists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cms_site_menu_items",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                name: "cms_page_sections",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    SubtitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    Settings = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    LayoutSettings = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    BackgroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BackgroundImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomCss = table.Column<string>(type: "text", nullable: true),
                    VisibleFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VisibleUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_cms_page_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_page_sections_cms_pages_PageId",
                        column: x => x.PageId,
                        principalSchema: "cms",
                        principalTable: "cms_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cms_page_sections_cms_section_types_SectionTypeId",
                        column: x => x.SectionTypeId,
                        principalSchema: "cms",
                        principalTable: "cms_section_types",
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
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LayoutType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ColumnCount = table.Column<int>(type: "integer", nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BackgroundImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CustomCss = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                name: "cms_page_section_items",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    SubtitleI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageAltI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    MobileImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    VideoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LinkType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LinkTargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ButtonTextI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    ButtonStyle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomHtmlI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    BadgeTextI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    BadgeColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_cms_page_section_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cms_page_section_items_cms_page_sections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "cms",
                        principalTable: "cms_page_sections",
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
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    ColumnIndex = table.Column<int>(type: "integer", nullable: false),
                    ColumnSpan = table.Column<int>(type: "integer", nullable: false),
                    ShowTitle = table.Column<bool>(type: "boolean", nullable: false),
                    TitleStyle = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DescriptionI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    ItemType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImagePosition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BadgeText = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BadgeColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CustomHtml = table.Column<string>(type: "text", nullable: true),
                    GenderFilter = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                name: "IX_cms_page_section_items_SectionId",
                schema: "cms",
                table: "cms_page_section_items",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_page_sections_PageId",
                schema: "cms",
                table: "cms_page_sections",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_page_sections_SectionTypeId",
                schema: "cms",
                table: "cms_page_sections",
                column: "SectionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_page_templates_Code",
                schema: "cms",
                table: "cms_page_templates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_pages_FirmPlatformId_Code",
                schema: "cms",
                table: "cms_pages",
                columns: new[] { "FirmPlatformId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_pages_TemplateId",
                schema: "cms",
                table: "cms_pages",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_product_list_items_ProductListId",
                schema: "cms",
                table: "cms_product_list_items",
                column: "ProductListId");

            migrationBuilder.CreateIndex(
                name: "IX_cms_product_lists_FirmPlatformId_Code",
                schema: "cms",
                table: "cms_product_lists",
                columns: new[] { "FirmPlatformId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cms_section_types_Code",
                schema: "cms",
                table: "cms_section_types",
                column: "Code",
                unique: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cms_menu_panel_items",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_page_section_items",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_product_list_items",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_menu_panel_groups",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_page_sections",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_product_lists",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_menu_mega_panels",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_pages",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_section_types",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_site_menu_items",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_page_templates",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "cms_site_menus",
                schema: "cms");
        }
    }
}
