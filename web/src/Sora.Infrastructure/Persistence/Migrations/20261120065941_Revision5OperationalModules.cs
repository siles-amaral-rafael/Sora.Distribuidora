using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sora.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Revision5OperationalModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    primary_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.CheckConstraint("ck_customers_coordinates", "(latitude IS NULL AND longitude IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)");
                    table.CheckConstraint("ck_customers_customer_type", "customer_type IN ('STORE','FAMILY','INSTITUTION')");
                });

            migrationBuilder.CreateTable(
                name: "dispatches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    vehicle_identifier = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    dispatch_lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    departed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatches", x => x.id);
                    table.ForeignKey(
                        name: "FK_dispatches_staff_accounts_dispatch_lead_id",
                        column: x => x.dispatch_lead_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatches_staff_accounts_driver_staff_id",
                        column: x => x.driver_staff_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    internal_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    barcode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    net_content = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    content_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    base_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_sale_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.CheckConstraint("ck_products_net_content", "net_content IS NULL OR net_content > 0");
                    table.CheckConstraint("ck_products_unit_sale_price", "unit_sale_price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "staff_roles",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    display_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_roles", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    fulfillment = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    delivery_address_snapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delivery_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    delivery_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_orders", x => x.id);
                    table.CheckConstraint("ck_sales_orders_fulfillment", "fulfillment IN ('DELIVERY','PICKUP_NOW','PICKUP_LATER')");
                    table.CheckConstraint("ck_sales_orders_status", "status IN ('REGISTERED','PREPARED','DISPATCHED','DELIVERED','DELIVERY_FAILED','CANCELLED')");
                    table.CheckConstraint("ck_sales_orders_total_amount", "total_amount >= 0");
                    table.ForeignKey(
                        name: "FK_sales_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_orders_staff_accounts_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_presentations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    units_per_presentation = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_presentations", x => x.id);
                    table.CheckConstraint("ck_product_presentations_units", "units_per_presentation > 0");
                    table.ForeignKey(
                        name: "FK_product_presentations_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_account_roles",
                columns: table => new
                {
                    staff_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    assigned_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_account_roles", x => new { x.staff_account_id, x.role_code });
                    table.ForeignKey(
                        name: "FK_staff_account_roles_staff_accounts_assigned_by_id",
                        column: x => x.assigned_by_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_account_roles_staff_accounts_staff_account_id",
                        column: x => x.staff_account_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_account_roles_staff_roles_role_code",
                        column: x => x.role_code,
                        principalTable: "staff_roles",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_receipts_staff_accounts_received_by_id",
                        column: x => x.received_by_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_receipts_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<short>(type: "smallint", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    recipient_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    recipient_relationship = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signature_bytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    signature_mime_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    attempted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_orders", x => x.id);
                    table.CheckConstraint("ck_dispatch_orders_attempt", "attempt_number BETWEEN 1 AND 2");
                    table.CheckConstraint("ck_dispatch_orders_result", "result IS NULL OR result IN ('DELIVERED','NOT_FOUND','REJECTED')");
                    table.ForeignKey(
                        name: "FK_dispatch_orders_dispatches_dispatch_id",
                        column: x => x.dispatch_id,
                        principalTable: "dispatches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_orders_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    presentation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entered_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    quantity_units = table.Column<int>(type: "integer", nullable: false),
                    unit_sale_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lines", x => x.id);
                    table.CheckConstraint("ck_order_lines_amounts", "unit_sale_price_snapshot >= 0 AND line_total >= 0");
                    table.CheckConstraint("ck_order_lines_quantities", "entered_quantity > 0 AND quantity_units > 0");
                    table.ForeignKey(
                        name: "FK_order_lines_product_presentations_presentation_id",
                        column: x => x.presentation_id,
                        principalTable: "product_presentations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_lines_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_lines_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    quantity_received = table.Column<int>(type: "integer", nullable: false),
                    unit_purchase_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_lots", x => x.id);
                    table.CheckConstraint("ck_inventory_lots_purchase_cost", "unit_purchase_cost >= 0");
                    table.CheckConstraint("ck_inventory_lots_quantity_received", "quantity_received > 0");
                    table.ForeignKey(
                        name: "FK_inventory_lots_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_lots_purchase_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "purchase_receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_lot_balances",
                columns: table => new
                {
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    on_hand_units = table.Column<int>(type: "integer", nullable: false),
                    reserved_units = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_lot_balances", x => x.lot_id);
                    table.CheckConstraint("ck_inventory_lot_balances_quantities", "on_hand_units >= reserved_units AND reserved_units >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_lot_balances_inventory_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "inventory_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    on_hand_delta = table.Column<int>(type: "integer", nullable: false),
                    reserved_delta = table.Column<int>(type: "integer", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dispatch_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    corrects_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movements", x => x.id);
                    table.CheckConstraint("ck_inventory_movements_nonzero", "on_hand_delta <> 0 OR reserved_delta <> 0 OR movement_type = 'PICK'");
                    table.CheckConstraint("ck_inventory_movements_type", "movement_type IN ('RECEIPT','RESERVE','RELEASE','PICK','DISPATCH','RETURN','ADJUSTMENT','LOSS','DAMAGE','EXPIRY')");
                    table.ForeignKey(
                        name: "FK_inventory_movements_dispatch_orders_dispatch_order_id",
                        column: x => x.dispatch_order_id,
                        principalTable: "dispatch_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_inventory_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "inventory_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_inventory_movements_corrects_movement_id",
                        column: x => x.corrects_movement_id,
                        principalTable: "inventory_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_sales_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_movements_staff_accounts_actor_id",
                        column: x => x.actor_id,
                        principalTable: "staff_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_lot_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_reserved = table.Column<int>(type: "integer", nullable: false),
                    quantity_picked = table.Column<int>(type: "integer", nullable: false),
                    unit_cost_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lot_allocations", x => x.id);
                    table.CheckConstraint("ck_order_lot_allocations_quantities", "quantity_reserved > 0 AND quantity_picked >= 0 AND quantity_picked <= quantity_reserved");
                    table.CheckConstraint("ck_order_lot_allocations_unit_cost", "unit_cost_snapshot >= 0");
                    table.ForeignKey(
                        name: "FK_order_lot_allocations_inventory_lots_lot_id",
                        column: x => x.lot_id,
                        principalTable: "inventory_lots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_lot_allocations_order_lines_order_line_id",
                        column: x => x.order_line_id,
                        principalTable: "order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "staff_roles",
                columns: new[] { "code", "display_name", "is_active" },
                values: new object[,]
                {
                    { "ADMIN", "Administrador", true },
                    { "DISPATCH_ASSISTANT", "Ayudante", true },
                    { "DISPATCH_LEAD", "Jefe de despacho", true },
                    { "DRIVER", "Repartidor", true },
                    { "SELLER", "Vendedor", true },
                    { "WAREHOUSE", "Encargado de almacén", true }
                });

            // Preserve the single role from Revision 4 as a role assignment before removing the old column.
            migrationBuilder.Sql("""
                INSERT INTO staff_account_roles (staff_account_id, role_code, assigned_at_utc)
                SELECT id,
                       CASE UPPER(BTRIM(role_name))
                           WHEN 'ADMIN' THEN 'ADMIN'
                           WHEN 'ADMINISTRADOR' THEN 'ADMIN'
                           WHEN 'SELLER' THEN 'SELLER'
                           WHEN 'VENDEDOR' THEN 'SELLER'
                           WHEN 'WAREHOUSE' THEN 'WAREHOUSE'
                           WHEN 'ENCARGADO DE ALMACÉN' THEN 'WAREHOUSE'
                           WHEN 'DISPATCH_LEAD' THEN 'DISPATCH_LEAD'
                           WHEN 'JEFE DE DESPACHO' THEN 'DISPATCH_LEAD'
                           WHEN 'DISPATCH_ASSISTANT' THEN 'DISPATCH_ASSISTANT'
                           WHEN 'AYUDANTE' THEN 'DISPATCH_ASSISTANT'
                           WHEN 'DRIVER' THEN 'DRIVER'
                           WHEN 'REPARTIDOR' THEN 'DRIVER'
                           ELSE 'SELLER'
                       END,
                       created_at_utc
                FROM staff_accounts;
                """);

            migrationBuilder.DropColumn(
                name: "role_name",
                table: "staff_accounts");

            migrationBuilder.CreateIndex(
                name: "ix_customers_phone",
                table: "customers",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_orders_route_sequence",
                table: "dispatch_orders",
                columns: new[] { "dispatch_id", "sequence_number" });

            migrationBuilder.CreateIndex(
                name: "ux_dispatch_orders_dispatch_order",
                table: "dispatch_orders",
                columns: new[] { "dispatch_id", "order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_dispatch_orders_order_attempt",
                table: "dispatch_orders",
                columns: new[] { "order_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispatches_dispatch_lead_id",
                table: "dispatches",
                column: "dispatch_lead_id");

            migrationBuilder.CreateIndex(
                name: "IX_dispatches_driver_staff_id",
                table: "dispatches",
                column: "driver_staff_id");

            migrationBuilder.CreateIndex(
                name: "ux_dispatches_number",
                table: "dispatches",
                column: "dispatch_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_lots_fefo",
                table: "inventory_lots",
                columns: new[] { "product_id", "expiry_date", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_lots_receipt_id",
                table: "inventory_lots",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_actor_id",
                table: "inventory_movements",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_corrects_movement_id",
                table: "inventory_movements",
                column: "corrects_movement_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_dispatch_order_id",
                table: "inventory_movements",
                column: "dispatch_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movements_lot_date",
                table: "inventory_movements",
                columns: new[] { "lot_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_order_id",
                table: "inventory_movements",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_order_id",
                table: "order_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_presentation_id",
                table: "order_lines",
                column: "presentation_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_product_id",
                table: "order_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_lot_allocations_lot_id",
                table: "order_lot_allocations",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ux_order_lot_allocations_line_lot",
                table: "order_lot_allocations",
                columns: new[] { "order_line_id", "lot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_presentations_product_name",
                table: "product_presentations",
                columns: new[] { "product_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_name",
                table: "products",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ux_products_barcode",
                table: "products",
                column: "barcode",
                unique: true,
                filter: "barcode IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_products_internal_code",
                table: "products",
                column: "internal_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_received_by_id",
                table: "purchase_receipts",
                column: "received_by_id");

            migrationBuilder.CreateIndex(
                name: "ux_purchase_receipts_supplier_invoice",
                table: "purchase_receipts",
                columns: new[] { "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_orders_created_by_id",
                table: "sales_orders",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_orders_customer_date",
                table: "sales_orders",
                columns: new[] { "customer_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_sales_orders_number",
                table: "sales_orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_account_roles_assigned_by_id",
                table: "staff_account_roles",
                column: "assigned_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_account_roles_role_code",
                table: "staff_account_roles",
                column: "role_code");

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_inventory_movement_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Inventory movements are append-only; add a correcting movement.';
                END;
                $$;
                CREATE TRIGGER trg_inventory_movements_immutable
                BEFORE UPDATE OR DELETE ON inventory_movements
                FOR EACH ROW EXECUTE FUNCTION prevent_inventory_movement_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role_name",
                table: "staff_accounts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Vendedor");

            // Revision 4 had one role per account. On rollback, retain the most privileged assigned role.
            migrationBuilder.Sql("""
                UPDATE staff_accounts AS account
                SET role_name = COALESCE((
                    SELECT role.display_name
                    FROM staff_account_roles AS assignment
                    JOIN staff_roles AS role ON role.code = assignment.role_code
                    WHERE assignment.staff_account_id = account.id
                    ORDER BY CASE assignment.role_code
                        WHEN 'ADMIN' THEN 0
                        WHEN 'DISPATCH_LEAD' THEN 1
                        WHEN 'WAREHOUSE' THEN 2
                        WHEN 'SELLER' THEN 3
                        WHEN 'DRIVER' THEN 4
                        ELSE 5
                    END
                    LIMIT 1
                ), 'Vendedor');
                DROP TRIGGER IF EXISTS trg_inventory_movements_immutable ON inventory_movements;
                DROP FUNCTION IF EXISTS prevent_inventory_movement_mutation();
                """);

            migrationBuilder.DropTable(
                name: "inventory_lot_balances");

            migrationBuilder.DropTable(
                name: "inventory_movements");

            migrationBuilder.DropTable(
                name: "order_lot_allocations");

            migrationBuilder.DropTable(
                name: "staff_account_roles");

            migrationBuilder.DropTable(
                name: "dispatch_orders");

            migrationBuilder.DropTable(
                name: "inventory_lots");

            migrationBuilder.DropTable(
                name: "order_lines");

            migrationBuilder.DropTable(
                name: "staff_roles");

            migrationBuilder.DropTable(
                name: "dispatches");

            migrationBuilder.DropTable(
                name: "purchase_receipts");

            migrationBuilder.DropTable(
                name: "product_presentations");

            migrationBuilder.DropTable(
                name: "sales_orders");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "customers");

        }
    }
}
