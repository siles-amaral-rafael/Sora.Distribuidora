using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseFinalAccessAndOrderControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_orders_status",
                table: "sales_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_lines_amounts",
                table: "order_lines");

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "order_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "shortage_units",
                table: "order_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "subtotal_amount",
                table: "order_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "units_available_at_creation",
                table: "order_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE order_lines SET subtotal_amount = ROUND(quantity_units * unit_sale_price_snapshot, 2), units_available_at_creation = quantity_units WHERE subtotal_amount = 0 AND discount_amount = 0");

            migrationBuilder.CreateTable(
                name: "order_history_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_history_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_history_entries_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_history_entries_staff_accounts_actor_id",
                        column: x => x.actor_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_discount_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minimum_quantity_units = table.Column<int>(type: "integer", nullable: false),
                    discount_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_discount_rules", x => x.id);
                    table.CheckConstraint("ck_product_discount_rules_min_units", "minimum_quantity_units > 0");
                    table.CheckConstraint("ck_product_discount_rules_percent", "discount_percent > 0 AND discount_percent <= 100");
                    table.ForeignKey(
                        name: "FK_product_discount_rules_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_audit_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    target_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_audit_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_audit_entries_staff_accounts_actor_id",
                        column: x => x.actor_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_permissions",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    display_name = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_permissions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "staff_role_permissions",
                columns: table => new
                {
                    role_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    permission_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_role_permissions", x => new { x.role_code, x.permission_code });
                    table.ForeignKey(
                        name: "FK_staff_role_permissions_staff_permissions_permission_code",
                        column: x => x.permission_code,
                        principalTable: "staff_permissions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_role_permissions_staff_roles_role_code",
                        column: x => x.role_code,
                        principalTable: "staff_roles",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "staff_permissions",
                columns: new[] { "code", "display_name" },
                values: new object[,]
                {
                    { "customers.read", "Consultar clientes" },
                    { "customers.write", "Registrar y modificar clientes" },
                    { "discounts.manage", "Configurar beneficios por cantidad" },
                    { "dispatches.create", "Crear salidas de despacho" },
                    { "dispatches.inspect", "Inspeccionar devoluciones" },
                    { "dispatches.read", "Consultar despachos asignados" },
                    { "dispatches.result", "Registrar resultados de entrega" },
                    { "inventory.read", "Consultar inventario" },
                    { "inventory.write", "Registrar recepciones y ajustes" },
                    { "orders.cancel", "Cancelar pedidos" },
                    { "orders.pickup", "Confirmar retiros" },
                    { "orders.prepare", "Preparar pedidos" },
                    { "orders.read", "Consultar pedidos" },
                    { "orders.write", "Registrar pedidos" },
                    { "products.read", "Consultar productos" },
                    { "products.write", "Registrar y modificar productos" },
                    { "staff.manage", "Administrar usuarios, roles y permisos" },
                    { "staff.read", "Consultar personal y auditoría" },
                    { "suppliers.read", "Consultar proveedores" },
                    { "suppliers.write", "Registrar y modificar proveedores" }
                });

            migrationBuilder.InsertData(
                table: "staff_role_permissions",
                columns: new[] { "permission_code", "role_code" },
                values: new object[,]
                {
                    { "customers.read", "ADMIN" },
                    { "customers.write", "ADMIN" },
                    { "discounts.manage", "ADMIN" },
                    { "dispatches.create", "ADMIN" },
                    { "dispatches.inspect", "ADMIN" },
                    { "dispatches.read", "ADMIN" },
                    { "dispatches.result", "ADMIN" },
                    { "inventory.read", "ADMIN" },
                    { "inventory.write", "ADMIN" },
                    { "orders.cancel", "ADMIN" },
                    { "orders.pickup", "ADMIN" },
                    { "orders.prepare", "ADMIN" },
                    { "orders.read", "ADMIN" },
                    { "orders.write", "ADMIN" },
                    { "products.read", "ADMIN" },
                    { "products.write", "ADMIN" },
                    { "staff.manage", "ADMIN" },
                    { "staff.read", "ADMIN" },
                    { "suppliers.read", "ADMIN" },
                    { "suppliers.write", "ADMIN" },
                    { "dispatches.read", "DISPATCH_ASSISTANT" },
                    { "dispatches.result", "DISPATCH_ASSISTANT" },
                    { "customers.read", "DISPATCH_LEAD" },
                    { "dispatches.create", "DISPATCH_LEAD" },
                    { "dispatches.inspect", "DISPATCH_LEAD" },
                    { "dispatches.read", "DISPATCH_LEAD" },
                    { "dispatches.result", "DISPATCH_LEAD" },
                    { "inventory.read", "DISPATCH_LEAD" },
                    { "inventory.write", "DISPATCH_LEAD" },
                    { "orders.prepare", "DISPATCH_LEAD" },
                    { "orders.read", "DISPATCH_LEAD" },
                    { "products.read", "DISPATCH_LEAD" },
                    { "dispatches.read", "DRIVER" },
                    { "dispatches.result", "DRIVER" },
                    { "customers.read", "SELLER" },
                    { "customers.write", "SELLER" },
                    { "orders.cancel", "SELLER" },
                    { "orders.read", "SELLER" },
                    { "orders.write", "SELLER" },
                    { "products.read", "SELLER" },
                    { "customers.read", "WAREHOUSE" },
                    { "inventory.read", "WAREHOUSE" },
                    { "inventory.write", "WAREHOUSE" },
                    { "orders.pickup", "WAREHOUSE" },
                    { "orders.prepare", "WAREHOUSE" },
                    { "orders.read", "WAREHOUSE" },
                    { "products.read", "WAREHOUSE" },
                    { "products.write", "WAREHOUSE" },
                    { "suppliers.read", "WAREHOUSE" },
                    { "suppliers.write", "WAREHOUSE" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_orders_status",
                table: "sales_orders",
                sql: "status IN ('PENDING_CONFIRMATION','REGISTERED','PREPARED','DISPATCHED','DELIVERED','DELIVERY_FAILED','CANCELLED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_lines_amounts",
                table: "order_lines",
                sql: "unit_sale_price_snapshot >= 0 AND subtotal_amount >= 0 AND discount_amount >= 0 AND line_total >= 0 AND discount_amount <= subtotal_amount");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_lines_availability",
                table: "order_lines",
                sql: "units_available_at_creation >= 0 AND shortage_units >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_order_history_entries_actor_id",
                table: "order_history_entries",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_history_order_time",
                table: "order_history_entries",
                columns: new[] { "order_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_product_discount_rules_product_min_units",
                table: "product_discount_rules",
                columns: new[] { "product_id", "minimum_quantity_units" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_audit_entries_actor_id",
                table: "staff_audit_entries",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_audit_entries_occurred_at",
                table: "staff_audit_entries",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_staff_role_permissions_permission_code",
                table: "staff_role_permissions",
                column: "permission_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_history_entries");

            migrationBuilder.DropTable(
                name: "product_discount_rules");

            migrationBuilder.DropTable(
                name: "staff_audit_entries");

            migrationBuilder.DropTable(
                name: "staff_role_permissions");

            migrationBuilder.DropTable(
                name: "staff_permissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_orders_status",
                table: "sales_orders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_lines_amounts",
                table: "order_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_order_lines_availability",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "shortage_units",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "subtotal_amount",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "units_available_at_creation",
                table: "order_lines");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_orders_status",
                table: "sales_orders",
                sql: "status IN ('REGISTERED','PREPARED','DISPATCHED','DELIVERED','DELIVERY_FAILED','CANCELLED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_order_lines_amounts",
                table: "order_lines",
                sql: "unit_sale_price_snapshot >= 0 AND line_total >= 0");
        }
    }
}
