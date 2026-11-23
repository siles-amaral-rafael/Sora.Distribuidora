using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5NightOrdersAndOutboxRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_outbox_state",
                table: "notification_outbox");

            migrationBuilder.AddColumn<bool>(
                name: "is_night_order",
                table: "sales_orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "scheduled_delivery_date",
                table: "sales_orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "notification_outbox",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_retry_at_utc",
                table: "notification_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_retry_by_id",
                table: "notification_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "notification_outbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_orders_night_delivery",
                table: "sales_orders",
                sql: "is_night_order = FALSE OR (fulfillment = 'DELIVERY' AND scheduled_delivery_date IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_last_retry_by_id",
                table: "notification_outbox",
                column: "last_retry_by_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_outbox_retry_count",
                table: "notification_outbox",
                sql: "retry_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_outbox_state",
                table: "notification_outbox",
                sql: "state IN ('PENDING','SIMULATED_SENT','FAILED')");

            migrationBuilder.AddForeignKey(
                name: "FK_notification_outbox_staff_accounts_last_retry_by_id",
                table: "notification_outbox",
                column: "last_retry_by_id",
                principalTable: "staff_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notification_outbox_staff_accounts_last_retry_by_id",
                table: "notification_outbox");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_orders_night_delivery",
                table: "sales_orders");

            migrationBuilder.DropIndex(
                name: "IX_notification_outbox_last_retry_by_id",
                table: "notification_outbox");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_outbox_retry_count",
                table: "notification_outbox");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_outbox_state",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "is_night_order",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "scheduled_delivery_date",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "last_retry_at_utc",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "last_retry_by_id",
                table: "notification_outbox");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "notification_outbox");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_outbox_state",
                table: "notification_outbox",
                sql: "state IN ('PENDING','SIMULATED_SENT')");
        }
    }
}
