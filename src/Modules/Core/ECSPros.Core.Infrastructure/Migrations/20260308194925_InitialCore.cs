using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "core_contents",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_core_contents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_expense_types",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    IsItemLevel = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultTaxRate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_core_expense_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_integration_services",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsSchema = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_core_integration_services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_languages",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NativeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_core_languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_lookup_types",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_core_lookup_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_notification_types",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    DefaultChannels = table.Column<List<string>>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_core_notification_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_order_item_statuses",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_core_order_item_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_order_statuses",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_core_order_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_payment_methods",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresConfirmation = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_core_payment_methods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_platform_types",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    IsMarketplace = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SettingsSchema = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("PK_core_platform_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_return_reasons",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    RequiresInspection = table.Column<bool>(type: "boolean", nullable: false),
                    IsCustomerFault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_core_return_reasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_lookup_values",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LookupTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExtraData = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_core_lookup_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_lookup_values_core_lookup_types_LookupTypeId",
                        column: x => x.LookupTypeId,
                        principalSchema: "core",
                        principalTable: "core_lookup_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_notification_templates",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_core_notification_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_notification_templates_core_notification_types_Notific~",
                        column: x => x.NotificationTypeId,
                        principalSchema: "core",
                        principalTable: "core_notification_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_cargo_rules",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PaymentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NeighborhoodId = table.Column<Guid>(type: "uuid", nullable: true),
                    CityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_core_cargo_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_firm_integrations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Credentials = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    Settings = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_core_firm_integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_firm_integrations_core_integration_services_Integratio~",
                        column: x => x.IntegrationServiceId,
                        principalSchema: "core",
                        principalTable: "core_integration_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_firms",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    TaxOffice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PriceMultiplier = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    InvoiceIntegratorId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_core_firms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_firms_core_firm_integrations_InvoiceIntegratorId",
                        column: x => x.InvoiceIntegratorId,
                        principalSchema: "core",
                        principalTable: "core_firm_integrations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "core_firm_notification_settings",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Channels = table.Column<List<string>>(type: "jsonb", nullable: false),
                    SmsProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    PushProviderId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_core_firm_notification_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_firm_notification_settings_core_firms_FirmId",
                        column: x => x.FirmId,
                        principalSchema: "core",
                        principalTable: "core_firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_firm_notification_settings_core_notification_types_Not~",
                        column: x => x.NotificationTypeId,
                        principalSchema: "core",
                        principalTable: "core_notification_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_firm_platforms",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameI18n = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    Credentials = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    Settings = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    PriceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PriceMultiplier = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    InvoiceSeriesId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_core_firm_platforms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_firm_platforms_core_firms_FirmId",
                        column: x => x.FirmId,
                        principalSchema: "core",
                        principalTable: "core_firms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_firm_platforms_core_platform_types_PlatformTypeId",
                        column: x => x.PlatformTypeId,
                        principalSchema: "core",
                        principalTable: "core_platform_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_cargo_rules_FirmId",
                schema: "core",
                table: "core_cargo_rules",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_core_cargo_rules_FirmIntegrationId",
                schema: "core",
                table: "core_cargo_rules",
                column: "FirmIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_core_contents_EntityType_EntityId_FieldName_LanguageCode",
                schema: "core",
                table: "core_contents",
                columns: new[] { "EntityType", "EntityId", "FieldName", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_expense_types_Code",
                schema: "core",
                table: "core_expense_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_integrations_FirmId",
                schema: "core",
                table: "core_firm_integrations",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_integrations_IntegrationServiceId",
                schema: "core",
                table: "core_firm_integrations",
                column: "IntegrationServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_notification_settings_FirmId_NotificationTypeId",
                schema: "core",
                table: "core_firm_notification_settings",
                columns: new[] { "FirmId", "NotificationTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_notification_settings_NotificationTypeId",
                schema: "core",
                table: "core_firm_notification_settings",
                column: "NotificationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_platforms_Code",
                schema: "core",
                table: "core_firm_platforms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_platforms_FirmId",
                schema: "core",
                table: "core_firm_platforms",
                column: "FirmId");

            migrationBuilder.CreateIndex(
                name: "IX_core_firm_platforms_PlatformTypeId",
                schema: "core",
                table: "core_firm_platforms",
                column: "PlatformTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_core_firms_Code",
                schema: "core",
                table: "core_firms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_firms_InvoiceIntegratorId",
                schema: "core",
                table: "core_firms",
                column: "InvoiceIntegratorId");

            migrationBuilder.CreateIndex(
                name: "IX_core_integration_services_Code",
                schema: "core",
                table: "core_integration_services",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_languages_Code",
                schema: "core",
                table: "core_languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_lookup_types_Code",
                schema: "core",
                table: "core_lookup_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_lookup_values_LookupTypeId_Code",
                schema: "core",
                table: "core_lookup_values",
                columns: new[] { "LookupTypeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_notification_templates_NotificationTypeId_LanguageCode~",
                schema: "core",
                table: "core_notification_templates",
                columns: new[] { "NotificationTypeId", "LanguageCode", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_notification_types_Code",
                schema: "core",
                table: "core_notification_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_order_item_statuses_Code",
                schema: "core",
                table: "core_order_item_statuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_order_statuses_Code",
                schema: "core",
                table: "core_order_statuses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_payment_methods_Code",
                schema: "core",
                table: "core_payment_methods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_platform_types_Code",
                schema: "core",
                table: "core_platform_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_return_reasons_Code",
                schema: "core",
                table: "core_return_reasons",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_core_cargo_rules_core_firm_integrations_FirmIntegrationId",
                schema: "core",
                table: "core_cargo_rules",
                column: "FirmIntegrationId",
                principalSchema: "core",
                principalTable: "core_firm_integrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_cargo_rules_core_firms_FirmId",
                schema: "core",
                table: "core_cargo_rules",
                column: "FirmId",
                principalSchema: "core",
                principalTable: "core_firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_firm_integrations_core_firms_FirmId",
                schema: "core",
                table: "core_firm_integrations",
                column: "FirmId",
                principalSchema: "core",
                principalTable: "core_firms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_firms_core_firm_integrations_InvoiceIntegratorId",
                schema: "core",
                table: "core_firms");

            migrationBuilder.DropTable(
                name: "core_cargo_rules",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_contents",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_expense_types",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_firm_notification_settings",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_firm_platforms",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_languages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_lookup_values",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_notification_templates",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_order_item_statuses",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_order_statuses",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_payment_methods",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_return_reasons",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_platform_types",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_lookup_types",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_notification_types",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_firm_integrations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_firms",
                schema: "core");

            migrationBuilder.DropTable(
                name: "core_integration_services",
                schema: "core");
        }
    }
}
