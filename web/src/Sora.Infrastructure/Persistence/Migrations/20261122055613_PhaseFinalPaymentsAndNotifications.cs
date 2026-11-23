using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseFinalPaymentsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    recipient_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_outbox", x => x.id);
                    table.CheckConstraint("ck_notification_outbox_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_notification_outbox_state", "state IN ('PENDING','SIMULATED_SENT')");
                    table.ForeignKey(
                        name: "FK_notification_outbox_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_outbox_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_outbox_staff_accounts_processed_by_id",
                        column: x => x.processed_by_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recorded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payments", x => x.id);
                    table.CheckConstraint("ck_order_payments_amount", "amount > 0");
                    table.ForeignKey(
                        name: "FK_order_payments_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_payments_staff_accounts_recorded_by_id",
                        column: x => x.recorded_by_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "staff_permissions",
                columns: new[] { "code", "display_name" },
                values: new object[,]
                {
                    { "notifications.manage", "Consultar y simular avisos al cliente" },
                    { "payments.record", "Registrar pagos y abonos de caja" }
                });

            migrationBuilder.InsertData(
                table: "staff_role_permissions",
                columns: new[] { "permission_code", "role_code" },
                values: new object[,]
                {
                    { "notifications.manage", "ADMIN" },
                    { "payments.record", "ADMIN" },
                    { "payments.record", "SELLER" },
                    { "payments.record", "WAREHOUSE" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_customer_id",
                table: "notification_outbox",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_order_id",
                table: "notification_outbox",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_processed_by_id",
                table: "notification_outbox",
                column: "processed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_outbox_state_created",
                table: "notification_outbox",
                columns: new[] { "state", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_order_payments_order",
                table: "order_payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_payments_recorded_by_id",
                table: "order_payments",
                column: "recorded_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_outbox");

            migrationBuilder.DropTable(
                name: "order_payments");

            migrationBuilder.DeleteData(
                table: "staff_role_permissions",
                keyColumns: new[] { "permission_code", "role_code" },
                keyValues: new object[] { "notifications.manage", "ADMIN" });

            migrationBuilder.DeleteData(
                table: "staff_role_permissions",
                keyColumns: new[] { "permission_code", "role_code" },
                keyValues: new object[] { "payments.record", "ADMIN" });

            migrationBuilder.DeleteData(
                table: "staff_role_permissions",
                keyColumns: new[] { "permission_code", "role_code" },
                keyValues: new object[] { "payments.record", "SELLER" });

            migrationBuilder.DeleteData(
                table: "staff_role_permissions",
                keyColumns: new[] { "permission_code", "role_code" },
                keyValues: new object[] { "payments.record", "WAREHOUSE" });

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "code",
                keyValue: "notifications.manage");

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "code",
                keyValue: "payments.record");
        }
    }
}
