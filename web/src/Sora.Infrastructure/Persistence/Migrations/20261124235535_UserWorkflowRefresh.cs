using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserWorkflowRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_outbox");

            migrationBuilder.Sql("DELETE FROM staff_role_permissions WHERE permission_code = 'notifications.manage';");

            migrationBuilder.DeleteData(
                table: "staff_role_permissions",
                keyColumns: new[] { "permission_code", "role_code" },
                keyValues: new object[] { "notifications.manage", "ADMIN" });

            migrationBuilder.DeleteData(
                table: "staff_permissions",
                keyColumn: "code",
                keyValue: "notifications.manage");

            migrationBuilder.CreateTable(
                name: "supplier_products",
                columns: table => new
                {
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_products", x => new { x.supplier_id, x.product_id });
                    table.ForeignKey(
                        name: "FK_supplier_products_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_supplier_products_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_products_product_id",
                table: "supplier_products",
                column: "product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_products");

            migrationBuilder.CreateTable(
                name: "notification_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    last_error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    last_retry_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_retry_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recipient_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_outbox", x => x.id);
                    table.CheckConstraint("ck_notification_outbox_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_notification_outbox_retry_count", "retry_count >= 0");
                    table.CheckConstraint("ck_notification_outbox_state", "state IN ('PENDING','SIMULATED_SENT','FAILED')");
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
                        name: "FK_notification_outbox_staff_accounts_last_retry_by_id",
                        column: x => x.last_retry_by_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notification_outbox_staff_accounts_processed_by_id",
                        column: x => x.processed_by_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "staff_permissions",
                columns: new[] { "code", "display_name" },
                values: new object[] { "notifications.manage", "Consultar y simular avisos al cliente" });

            migrationBuilder.InsertData(
                table: "staff_role_permissions",
                columns: new[] { "permission_code", "role_code" },
                values: new object[] { "notifications.manage", "ADMIN" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_customer_id",
                table: "notification_outbox",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_outbox_last_retry_by_id",
                table: "notification_outbox",
                column: "last_retry_by_id");

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
        }
    }
}
