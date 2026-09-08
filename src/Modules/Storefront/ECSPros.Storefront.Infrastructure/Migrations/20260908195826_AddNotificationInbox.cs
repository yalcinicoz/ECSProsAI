using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECSPros.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DismissOnOpen",
                schema: "storefront",
                table: "push_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ExpiresDays",
                schema: "storefront",
                table: "push_templates",
                type: "integer",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                schema: "storefront",
                table: "push_templates",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "info");

            migrationBuilder.AddColumn<bool>(
                name: "Inbox",
                schema: "storefront",
                table: "push_templates",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "DeviceId",
                schema: "storefront",
                table: "push_notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "DismissOnOpen",
                schema: "storefront",
                table: "push_notifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DismissedAt",
                schema: "storefront",
                table: "push_notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                schema: "storefront",
                table: "push_notifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() + interval '90 days'");

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                schema: "storefront",
                table: "push_notifications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "info");

            migrationBuilder.AddColumn<bool>(
                name: "Inbox",
                schema: "storefront",
                table: "push_notifications",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                schema: "storefront",
                table: "push_notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_notifications_dedup_member_inbox",
                schema: "storefront",
                table: "push_notifications",
                columns: new[] { "DedupId", "MemberId" },
                unique: true,
                filter: "\"DeviceId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_push_notifications_inbox",
                schema: "storefront",
                table: "push_notifications",
                columns: new[] { "MemberId", "CreatedAt" },
                filter: "\"Inbox\" AND \"DismissedAt\" IS NULL");

            // Veri: mevcut şablonlara §5 ikonları + pazarlama 30 gün; mevcut bildirimlere şablon ikonu + oluşturma bazlı süre
            migrationBuilder.Sql(@"
UPDATE storefront.push_templates SET ""Icon"" = CASE ""Type""
  WHEN 'order_created' THEN 'order' WHEN 'order_confirmed' THEN 'order' WHEN 'order_cancelled' THEN 'order'
  WHEN 'order_shipped' THEN 'cargo' WHEN 'order_delivered' THEN 'cargo'
  WHEN 'order_payment_pending' THEN 'payment' WHEN 'wallet_credit' THEN 'payment'
  WHEN 'return_status' THEN 'return'
  WHEN 'favorite_price_drop' THEN 'favorite' WHEN 'favorite_back_in_stock' THEN 'favorite' WHEN 'favorite_low_stock' THEN 'favorite'
  WHEN 'stock_alert' THEN 'stock'
  WHEN 'cart_reminder' THEN 'cart' WHEN 'cart_price_drop' THEN 'cart'
  WHEN 'question_answered' THEN 'question'
  WHEN 'order_review_invite' THEN 'review' WHEN 'review_approved' THEN 'review' WHEN 'review_rejected' THEN 'review'
  WHEN 'coupon_assigned' THEN 'coupon' WHEN 'coupon_expiring' THEN 'coupon'
  WHEN 'campaign' THEN 'campaign' WHEN 'welcome' THEN 'campaign' WHEN 'winback' THEN 'campaign' WHEN 'viewed_reminder' THEN 'campaign'
  ELSE 'info' END;
UPDATE storefront.push_templates SET ""ExpiresDays"" = 30 WHERE ""Class"" = 'marketing';
UPDATE storefront.push_notifications n SET ""Icon"" = t.""Icon"" FROM storefront.push_templates t WHERE t.""Type"" = n.""Type"";
UPDATE storefront.push_notifications SET ""ExpiresAt"" = ""CreatedAt"" + CASE WHEN ""Class"" = 'marketing' THEN interval '30 days' ELSE interval '90 days' END;
UPDATE storefront.push_notifications SET ""ReadAt"" = ""OpenedAt"" WHERE ""OpenedAt"" IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_push_notifications_dedup_member_inbox",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.DropIndex(
                name: "ix_push_notifications_inbox",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.DropColumn(
                name: "DismissOnOpen",
                schema: "storefront",
                table: "push_templates");

            migrationBuilder.DropColumn(
                name: "ExpiresDays",
                schema: "storefront",
                table: "push_templates");

            migrationBuilder.DropColumn(
                name: "Icon",
                schema: "storefront",
                table: "push_templates");

            migrationBuilder.DropColumn(
                name: "Inbox",
                schema: "storefront",
                table: "push_templates");

            migrationBuilder.DropColumn(
                name: "DismissOnOpen",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.DropColumn(
                name: "DismissedAt",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.DropColumn(
                name: "Icon",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.DropColumn(
                name: "Inbox",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                schema: "storefront",
                table: "push_notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "DeviceId",
                schema: "storefront",
                table: "push_notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
