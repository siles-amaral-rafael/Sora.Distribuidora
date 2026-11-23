using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase34SettlementAndDeliveryOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_orders_result",
                table: "dispatch_orders");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "order_payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "CASH");

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_fee_amount",
                table: "dispatch_orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "result_recorded_by_id",
                table: "dispatch_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_balance",
                table: "customers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "order_cancellation_resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resolved_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    cancellation_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_cancellation_resolutions", x => x.id);
                    table.CheckConstraint("ck_order_cancellation_resolution_amounts", "resolved_amount >= 0 AND cancellation_fee >= 0");
                    table.CheckConstraint("ck_order_cancellation_resolution_kind", "resolution IN ('NONE','REFUND','CREDIT')");
                    table.ForeignKey(
                        name: "FK_order_cancellation_resolutions_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellation_resolutions_staff_accounts_actor_id",
                        column: x => x.actor_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_orders_result_recorded_by_id",
                table: "dispatch_orders",
                column: "result_recorded_by_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_orders_cancellation_fee",
                table: "dispatch_orders",
                sql: "cancellation_fee_amount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_orders_result",
                table: "dispatch_orders",
                sql: "result IS NULL OR result IN ('DELIVERED','NOT_FOUND','REJECTED','CANCELLED')");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_resolutions_actor_id",
                table: "order_cancellation_resolutions",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ux_order_cancellation_resolution_order",
                table: "order_cancellation_resolutions",
                column: "order_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_dispatch_orders_staff_accounts_result_recorded_by_id",
                table: "dispatch_orders",
                column: "result_recorded_by_id",
                principalTable: "staff_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dispatch_orders_staff_accounts_result_recorded_by_id",
                table: "dispatch_orders");

            migrationBuilder.DropTable(
                name: "order_cancellation_resolutions");

            migrationBuilder.DropIndex(
                name: "IX_dispatch_orders_result_recorded_by_id",
                table: "dispatch_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_orders_cancellation_fee",
                table: "dispatch_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_dispatch_orders_result",
                table: "dispatch_orders");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "order_payments");

            migrationBuilder.DropColumn(
                name: "cancellation_fee_amount",
                table: "dispatch_orders");

            migrationBuilder.DropColumn(
                name: "result_recorded_by_id",
                table: "dispatch_orders");

            migrationBuilder.DropColumn(
                name: "credit_balance",
                table: "customers");

            migrationBuilder.AddCheckConstraint(
                name: "ck_dispatch_orders_result",
                table: "dispatch_orders",
                sql: "result IS NULL OR result IN ('DELIVERED','NOT_FOUND','REJECTED')");
        }
    }
}
