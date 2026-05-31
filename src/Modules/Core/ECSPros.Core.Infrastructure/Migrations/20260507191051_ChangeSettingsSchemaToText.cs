using System.Collections.Generic;
using ECSPros.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSettingsSchemaToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SettingsSchema",
                schema: "core",
                table: "core_platform_types",
                type: "text",
                nullable: true,
                oldClrType: typeof(List<PlatformSchemaField>),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<PlatformSchemaField>>(
                name: "SettingsSchema",
                schema: "core",
                table: "core_platform_types",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
