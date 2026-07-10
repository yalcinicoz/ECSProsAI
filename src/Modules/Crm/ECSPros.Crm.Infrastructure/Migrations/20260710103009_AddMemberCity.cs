using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberCity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                schema: "crm",
                table: "crm_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_crm_members_CityId",
                schema: "crm",
                table: "crm_members",
                column: "CityId");

            migrationBuilder.AddForeignKey(
                name: "FK_crm_members_crm_cities_CityId",
                schema: "crm",
                table: "crm_members",
                column: "CityId",
                principalSchema: "crm",
                principalTable: "crm_cities",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_crm_members_crm_cities_CityId",
                schema: "crm",
                table: "crm_members");

            migrationBuilder.DropIndex(
                name: "IX_crm_members_CityId",
                schema: "crm",
                table: "crm_members");

            migrationBuilder.DropColumn(
                name: "CityId",
                schema: "crm",
                table: "crm_members");
        }
    }
}
