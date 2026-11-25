using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Security;

namespace Sora.Infrastructure.Persistence;

public sealed class SoraDbContext(DbContextOptions<SoraDbContext> options) : DbContext(options)
{
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();
    public DbSet<StaffRole> StaffRoles => Set<StaffRole>();
    public DbSet<StaffAccountRole> StaffAccountRoles => Set<StaffAccountRole>();
    public DbSet<StaffPermission> StaffPermissions => Set<StaffPermission>();
    public DbSet<StaffRolePermission> StaffRolePermissions => Set<StaffRolePermission>();
    public DbSet<StaffAuditEntry> StaffAuditEntries => Set<StaffAuditEntry>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductPresentation> ProductPresentations => Set<ProductPresentation>();
    public DbSet<ProductDiscountRule> ProductDiscountRules => Set<ProductDiscountRule>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<InventoryLotBalance> InventoryLotBalances => Set<InventoryLotBalance>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderLotAllocation> OrderLotAllocations => Set<OrderLotAllocation>();
    public DbSet<OrderHistoryEntry> OrderHistoryEntries => Set<OrderHistoryEntry>();
    public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>();
    public DbSet<OrderCancellationResolution> OrderCancellationResolutions => Set<OrderCancellationResolution>();
    public DbSet<Dispatch> Dispatches => Set<Dispatch>();
    public DbSet<DispatchOrder> DispatchOrders => Set<DispatchOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("purchase_receipt_number_seq").StartsAt(1).IncrementsBy(1);

        var account = modelBuilder.Entity<StaffAccount>();
        account.ToTable("staff_accounts");
        account.HasKey(x => x.Id);
        account.Property(x => x.Id).HasColumnName("id");
        account.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
        account.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        account.HasIndex(x => x.Email).IsUnique();
        account.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(100).IsRequired();
        account.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        account.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        var role = modelBuilder.Entity<StaffRole>();
        role.ToTable("staff_roles");
        role.HasKey(x => x.Code);
        role.Property(x => x.Code).HasColumnName("code").HasMaxLength(40);
        role.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(80).IsRequired();
        role.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        role.HasData(
            new StaffRole("ADMIN", "Administrador"),
            new StaffRole("SELLER", "Vendedor"),
            new StaffRole("WAREHOUSE", "Encargado de almacén"),
            new StaffRole("DISPATCH_LEAD", "Jefe de despacho"),
            new StaffRole("DISPATCH_ASSISTANT", "Ayudante"),
            new StaffRole("DRIVER", "Repartidor"));

        var permission = modelBuilder.Entity<StaffPermission>();
        permission.ToTable("staff_permissions");
        permission.HasKey(x => x.Code);
        permission.Property(x => x.Code).HasColumnName("code").HasMaxLength(80);
        permission.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(140).IsRequired();
        permission.HasData(PermissionCatalog.All.Select(x => new StaffPermission(x.Code, x.DisplayName)));

        var rolePermission = modelBuilder.Entity<StaffRolePermission>();
        rolePermission.ToTable("staff_role_permissions");
        rolePermission.HasKey(x => new { x.RoleCode, x.PermissionCode });
        rolePermission.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(40);
        rolePermission.Property(x => x.PermissionCode).HasColumnName("permission_code").HasMaxLength(80);
        rolePermission.HasOne(x => x.Role).WithMany(x => x.Permissions).HasForeignKey(x => x.RoleCode)
            .OnDelete(DeleteBehavior.Restrict);
        rolePermission.HasOne(x => x.Permission).WithMany(x => x.Roles).HasForeignKey(x => x.PermissionCode)
            .OnDelete(DeleteBehavior.Restrict);
        rolePermission.HasData(PermissionCatalog.DefaultsByRole.SelectMany(role =>
            role.Value.Select(code => new StaffRolePermission(role.Key, code))));

        var audit = modelBuilder.Entity<StaffAuditEntry>();
        audit.ToTable("staff_audit_entries");
        audit.HasKey(x => x.Id);
        audit.Property(x => x.Id).HasColumnName("id");
        audit.Property(x => x.ActorId).HasColumnName("actor_id");
        audit.Property(x => x.Action).HasColumnName("action").HasMaxLength(60).IsRequired();
        audit.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(60).IsRequired();
        audit.Property(x => x.TargetId).HasColumnName("target_id").HasMaxLength(100).IsRequired();
        audit.Property(x => x.Details).HasColumnName("details").HasColumnType("jsonb").IsRequired();
        audit.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        audit.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        audit.HasIndex(x => x.OccurredAtUtc).HasDatabaseName("ix_staff_audit_entries_occurred_at");

        var staffRole = modelBuilder.Entity<StaffAccountRole>();
        staffRole.ToTable("staff_account_roles");
        staffRole.HasKey(x => new { x.StaffAccountId, x.RoleCode });
        staffRole.Property(x => x.StaffAccountId).HasColumnName("staff_account_id");
        staffRole.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(40);
        staffRole.Property(x => x.AssignedById).HasColumnName("assigned_by_id");
        staffRole.Property(x => x.AssignedAtUtc).HasColumnName("assigned_at_utc");
        staffRole.HasOne(x => x.StaffAccount).WithMany(x => x.RoleAssignments)
            .HasForeignKey(x => x.StaffAccountId).OnDelete(DeleteBehavior.Restrict);
        staffRole.HasOne(x => x.Role).WithMany(x => x.StaffAssignments)
            .HasForeignKey(x => x.RoleCode).OnDelete(DeleteBehavior.Restrict);
        staffRole.HasOne(x => x.AssignedBy).WithMany()
            .HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);

        var customer = modelBuilder.Entity<Customer>();
        customer.ToTable("customers", t => t.HasCheckConstraint("ck_customers_customer_type",
            "customer_type IN ('STORE','FAMILY','INSTITUTION')"));
        customer.HasKey(x => x.Id);
        customer.Property(x => x.Id).HasColumnName("id");
        customer.Property(x => x.CustomerType).HasColumnName("customer_type").HasMaxLength(20).IsRequired();
        customer.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(160).IsRequired();
        customer.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40).IsRequired();
        customer.Property(x => x.PrimaryAddress).HasColumnName("primary_address").HasMaxLength(500).IsRequired();
        customer.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
        customer.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
        customer.Property(x => x.CreditBalance).HasColumnName("credit_balance").HasPrecision(12, 2).HasDefaultValue(0m);
        customer.Property(x => x.IsActive).HasColumnName("is_active");
        customer.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        customer.HasIndex(x => x.Phone).HasDatabaseName("ix_customers_phone");
        customer.ToTable(t => t.HasCheckConstraint("ck_customers_coordinates",
            "(latitude IS NULL AND longitude IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)"));

        var product = modelBuilder.Entity<Product>();
        product.ToTable("products", t => t.HasCheckConstraint("ck_products_net_content",
            "net_content IS NULL OR net_content > 0"));
        product.HasKey(x => x.Id);
        product.Property(x => x.Id).HasColumnName("id");
        product.Property(x => x.InternalCode).HasColumnName("internal_code").HasMaxLength(60).IsRequired();
        product.Property(x => x.Barcode).HasColumnName("barcode").HasMaxLength(80);
        product.Property(x => x.Name).HasColumnName("name").HasMaxLength(180).IsRequired();
        product.Property(x => x.Brand).HasColumnName("brand").HasMaxLength(100);
        product.Property(x => x.Category).HasColumnName("category").HasMaxLength(100);
        product.Property(x => x.NetContent).HasColumnName("net_content").HasPrecision(12, 3);
        product.Property(x => x.ContentUnit).HasColumnName("content_unit").HasMaxLength(20);
        product.Property(x => x.BaseUnit).HasColumnName("base_unit").HasMaxLength(30).IsRequired();
        product.Property(x => x.UnitSalePrice).HasColumnName("unit_sale_price").HasPrecision(12, 2);
        product.Property(x => x.IsActive).HasColumnName("is_active");
        product.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        product.HasIndex(x => x.InternalCode).IsUnique().HasDatabaseName("ux_products_internal_code");
        product.HasIndex(x => x.Barcode).IsUnique().HasFilter("barcode IS NOT NULL")
            .HasDatabaseName("ux_products_barcode");
        product.HasIndex(x => x.Name).HasDatabaseName("ix_products_name");
        product.ToTable(t => t.HasCheckConstraint("ck_products_unit_sale_price", "unit_sale_price >= 0"));

        var presentation = modelBuilder.Entity<ProductPresentation>();
        presentation.ToTable("product_presentations", t => t.HasCheckConstraint(
            "ck_product_presentations_units", "units_per_presentation > 0"));
        presentation.HasKey(x => x.Id);
        presentation.Property(x => x.Id).HasColumnName("id");
        presentation.Property(x => x.ProductId).HasColumnName("product_id");
        presentation.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        presentation.Property(x => x.UnitsPerPresentation).HasColumnName("units_per_presentation");
        presentation.Property(x => x.IsActive).HasColumnName("is_active");
        presentation.HasOne(x => x.Product).WithMany(x => x.Presentations)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        presentation.HasIndex(x => new { x.ProductId, x.Name }).IsUnique()
            .HasDatabaseName("ux_product_presentations_product_name");

        var discountRule = modelBuilder.Entity<ProductDiscountRule>();
        discountRule.ToTable("product_discount_rules", t =>
        {
            t.HasCheckConstraint("ck_product_discount_rules_min_units", "minimum_quantity_units > 0");
            t.HasCheckConstraint("ck_product_discount_rules_percent", "discount_percent > 0 AND discount_percent <= 100");
        });
        discountRule.HasKey(x => x.Id);
        discountRule.Property(x => x.Id).HasColumnName("id");
        discountRule.Property(x => x.ProductId).HasColumnName("product_id");
        discountRule.Property(x => x.MinimumQuantityUnits).HasColumnName("minimum_quantity_units");
        discountRule.Property(x => x.DiscountPercent).HasColumnName("discount_percent").HasPrecision(5, 2);
        discountRule.HasOne(x => x.Product).WithMany(x => x.DiscountRules).HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        discountRule.HasIndex(x => new { x.ProductId, x.MinimumQuantityUnits }).IsUnique()
            .HasDatabaseName("ux_product_discount_rules_product_min_units");

        var supplier = modelBuilder.Entity<Supplier>();
        supplier.ToTable("suppliers");
        supplier.HasKey(x => x.Id);
        supplier.Property(x => x.Id).HasColumnName("id");
        supplier.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        supplier.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40).IsRequired();
        supplier.Property(x => x.IsActive).HasColumnName("is_active");

        var supplierProduct = modelBuilder.Entity<SupplierProduct>();
        supplierProduct.ToTable("supplier_products");
        supplierProduct.HasKey(x => new { x.SupplierId, x.ProductId });
        supplierProduct.Property(x => x.SupplierId).HasColumnName("supplier_id");
        supplierProduct.Property(x => x.ProductId).HasColumnName("product_id");
        supplierProduct.HasOne(x => x.Supplier).WithMany(x => x.ProductLinks)
            .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        supplierProduct.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);

        var receipt = modelBuilder.Entity<PurchaseReceipt>();
        receipt.ToTable("purchase_receipts");
        receipt.HasKey(x => x.Id);
        receipt.Property(x => x.Id).HasColumnName("id");
        receipt.Property(x => x.SupplierId).HasColumnName("supplier_id");
        receipt.Property(x => x.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(100).IsRequired();
        receipt.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc");
        receipt.Property(x => x.ReceivedById).HasColumnName("received_by_id");
        receipt.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        receipt.HasOne(x => x.ReceivedBy).WithMany().HasForeignKey(x => x.ReceivedById).OnDelete(DeleteBehavior.Restrict);
        receipt.HasIndex(x => new { x.SupplierId, x.InvoiceNumber }).IsUnique()
            .HasDatabaseName("ux_purchase_receipts_supplier_invoice");

        var lot = modelBuilder.Entity<InventoryLot>();
        lot.ToTable("inventory_lots", t =>
        {
            t.HasCheckConstraint("ck_inventory_lots_quantity_received", "quantity_received > 0");
            t.HasCheckConstraint("ck_inventory_lots_purchase_cost", "unit_purchase_cost >= 0");
        });
        lot.HasKey(x => x.Id);
        lot.Property(x => x.Id).HasColumnName("id");
        lot.Property(x => x.ReceiptId).HasColumnName("receipt_id");
        lot.Property(x => x.ProductId).HasColumnName("product_id");
        lot.Property(x => x.LotCode).HasColumnName("lot_code").HasMaxLength(80).IsRequired();
        lot.Property(x => x.ExpiryDate).HasColumnName("expiry_date").HasColumnType("date");
        lot.Property(x => x.QuantityReceived).HasColumnName("quantity_received");
        lot.Property(x => x.UnitPurchaseCost).HasColumnName("unit_purchase_cost").HasPrecision(12, 2);
        lot.HasOne(x => x.Receipt).WithMany(x => x.Lots).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        lot.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        lot.HasIndex(x => new { x.ProductId, x.ExpiryDate, x.Id }).HasDatabaseName("ix_inventory_lots_fefo");

        var balance = modelBuilder.Entity<InventoryLotBalance>();
        balance.ToTable("inventory_lot_balances", t => t.HasCheckConstraint(
            "ck_inventory_lot_balances_quantities", "on_hand_units >= reserved_units AND reserved_units >= 0"));
        balance.HasKey(x => x.LotId);
        balance.Property(x => x.LotId).HasColumnName("lot_id");
        balance.Property(x => x.OnHandUnits).HasColumnName("on_hand_units");
        balance.Property(x => x.ReservedUnits).HasColumnName("reserved_units");
        balance.Ignore(x => x.AvailableUnits);
        balance.HasOne(x => x.Lot).WithOne(x => x.Balance).HasForeignKey<InventoryLotBalance>(x => x.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        var movement = modelBuilder.Entity<InventoryMovement>();
        movement.ToTable("inventory_movements", t =>
        {
            t.HasCheckConstraint("ck_inventory_movements_nonzero",
                "on_hand_delta <> 0 OR reserved_delta <> 0 OR movement_type = 'PICK'");
            t.HasCheckConstraint("ck_inventory_movements_type", "movement_type IN ('RECEIPT','RESERVE','RELEASE','PICK','DISPATCH','RETURN','ADJUSTMENT','LOSS','DAMAGE','EXPIRY')");
        });
        movement.HasKey(x => x.Id);
        movement.Property(x => x.Id).HasColumnName("id");
        movement.Property(x => x.LotId).HasColumnName("lot_id");
        movement.Property(x => x.MovementType).HasColumnName("movement_type").HasMaxLength(20).IsRequired();
        movement.Property(x => x.OnHandDelta).HasColumnName("on_hand_delta");
        movement.Property(x => x.ReservedDelta).HasColumnName("reserved_delta");
        movement.Property(x => x.OrderId).HasColumnName("order_id");
        movement.Property(x => x.DispatchOrderId).HasColumnName("dispatch_order_id");
        movement.Property(x => x.ActorId).HasColumnName("actor_id");
        movement.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        movement.Property(x => x.CorrectsMovementId).HasColumnName("corrects_movement_id");
        movement.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        movement.HasOne(x => x.Lot).WithMany(x => x.Movements).HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne(x => x.Order).WithMany(x => x.Movements).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne(x => x.DispatchOrder).WithMany(x => x.Movements).HasForeignKey(x => x.DispatchOrderId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne(x => x.CorrectsMovement).WithMany().HasForeignKey(x => x.CorrectsMovementId).OnDelete(DeleteBehavior.Restrict);
        movement.HasIndex(x => new { x.LotId, x.OccurredAtUtc }).HasDatabaseName("ix_inventory_movements_lot_date");

        var order = modelBuilder.Entity<SalesOrder>();
        order.ToTable("sales_orders", t =>
        {
            t.HasCheckConstraint("ck_sales_orders_status", "status IN ('PENDING_CONFIRMATION','REGISTERED','PREPARED','DISPATCHED','DELIVERED','DELIVERY_FAILED','CANCELLED')");
            t.HasCheckConstraint("ck_sales_orders_fulfillment", "fulfillment IN ('DELIVERY','PICKUP_NOW','PICKUP_LATER')");
            t.HasCheckConstraint("ck_sales_orders_total_amount", "total_amount >= 0");
            t.HasCheckConstraint("ck_sales_orders_night_delivery", "is_night_order = FALSE OR (fulfillment = 'DELIVERY' AND scheduled_delivery_date IS NOT NULL)");
        });
        order.HasKey(x => x.Id);
        order.Property(x => x.Id).HasColumnName("id");
        order.Property(x => x.OrderNumber).HasColumnName("order_number").HasMaxLength(40).IsRequired();
        order.Property(x => x.CustomerId).HasColumnName("customer_id");
        order.Property(x => x.CreatedById).HasColumnName("created_by_id");
        order.Property(x => x.Status).HasColumnName("status").HasMaxLength(24).IsRequired();
        order.Property(x => x.Fulfillment).HasColumnName("fulfillment").HasMaxLength(24).IsRequired();
        order.Property(x => x.IsNightOrder).HasColumnName("is_night_order").HasDefaultValue(false).IsRequired();
        order.Property(x => x.ScheduledDeliveryDate).HasColumnName("scheduled_delivery_date").HasColumnType("date");
        order.Property(x => x.DeliveryAddressSnapshot).HasColumnName("delivery_address_snapshot").HasMaxLength(500);
        order.Property(x => x.DeliveryLatitude).HasColumnName("delivery_latitude").HasPrecision(9, 6);
        order.Property(x => x.DeliveryLongitude).HasColumnName("delivery_longitude").HasPrecision(9, 6);
        order.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2);
        order.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        order.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        order.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        order.HasIndex(x => x.OrderNumber).IsUnique().HasDatabaseName("ux_sales_orders_number");
        order.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc }).HasDatabaseName("ix_sales_orders_customer_date");

        var payment = modelBuilder.Entity<OrderPayment>();
        payment.ToTable("order_payments", t => t.HasCheckConstraint("ck_order_payments_amount", "amount > 0"));
        payment.HasKey(x => x.Id);
        payment.Property(x => x.Id).HasColumnName("id"); payment.Property(x => x.OrderId).HasColumnName("order_id");
        payment.Property(x => x.Amount).HasColumnName("amount").HasPrecision(12, 2);
        payment.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100).IsRequired();
        payment.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(20).HasDefaultValue("CASH").IsRequired();
        payment.Property(x => x.RecordedById).HasColumnName("recorded_by_id"); payment.Property(x => x.RecordedAtUtc).HasColumnName("recorded_at_utc");
        payment.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        payment.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.Restrict);
        payment.HasIndex(x => x.OrderId).HasDatabaseName("ix_order_payments_order");

        var cancellationResolution = modelBuilder.Entity<OrderCancellationResolution>();
        cancellationResolution.ToTable("order_cancellation_resolutions", t =>
        {
            t.HasCheckConstraint("ck_order_cancellation_resolution_kind", "resolution IN ('NONE','REFUND','CREDIT')");
            t.HasCheckConstraint("ck_order_cancellation_resolution_amounts", "resolved_amount >= 0 AND cancellation_fee >= 0");
        });
        cancellationResolution.HasKey(x => x.Id);
        cancellationResolution.Property(x => x.Id).HasColumnName("id");
        cancellationResolution.Property(x => x.OrderId).HasColumnName("order_id");
        cancellationResolution.Property(x => x.Resolution).HasColumnName("resolution").HasMaxLength(20).IsRequired();
        cancellationResolution.Property(x => x.ResolvedAmount).HasColumnName("resolved_amount").HasPrecision(12, 2);
        cancellationResolution.Property(x => x.CancellationFee).HasColumnName("cancellation_fee").HasPrecision(12, 2);
        cancellationResolution.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
        cancellationResolution.Property(x => x.ActorId).HasColumnName("actor_id");
        cancellationResolution.Property(x => x.ResolvedAtUtc).HasColumnName("resolved_at_utc");
        cancellationResolution.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        cancellationResolution.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        cancellationResolution.HasIndex(x => x.OrderId).IsUnique().HasDatabaseName("ux_order_cancellation_resolution_order");

        var orderLine = modelBuilder.Entity<OrderLine>();
        orderLine.ToTable("order_lines", t =>
        {
            t.HasCheckConstraint("ck_order_lines_quantities", "entered_quantity > 0 AND quantity_units > 0");
            t.HasCheckConstraint("ck_order_lines_amounts", "unit_sale_price_snapshot >= 0 AND subtotal_amount >= 0 AND discount_amount >= 0 AND line_total >= 0 AND discount_amount <= subtotal_amount");
            t.HasCheckConstraint("ck_order_lines_availability", "units_available_at_creation >= 0 AND shortage_units >= 0");
        });
        orderLine.HasKey(x => x.Id);
        orderLine.Property(x => x.Id).HasColumnName("id");
        orderLine.Property(x => x.OrderId).HasColumnName("order_id");
        orderLine.Property(x => x.ProductId).HasColumnName("product_id");
        orderLine.Property(x => x.PresentationId).HasColumnName("presentation_id");
        orderLine.Property(x => x.EnteredQuantity).HasColumnName("entered_quantity").HasPrecision(12, 3);
        orderLine.Property(x => x.QuantityUnits).HasColumnName("quantity_units");
        orderLine.Property(x => x.UnitSalePriceSnapshot).HasColumnName("unit_sale_price_snapshot").HasPrecision(12, 2);
        orderLine.Property(x => x.SubtotalAmount).HasColumnName("subtotal_amount").HasPrecision(12, 2);
        orderLine.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasPrecision(12, 2);
        orderLine.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(12, 2);
        orderLine.Property(x => x.UnitsAvailableAtCreation).HasColumnName("units_available_at_creation");
        orderLine.Property(x => x.ShortageUnits).HasColumnName("shortage_units");
        orderLine.HasOne(x => x.Order).WithMany(x => x.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        orderLine.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        orderLine.HasOne(x => x.Presentation).WithMany().HasForeignKey(x => x.PresentationId).OnDelete(DeleteBehavior.Restrict);

        var allocation = modelBuilder.Entity<OrderLotAllocation>();
        allocation.ToTable("order_lot_allocations", t =>
        {
            t.HasCheckConstraint("ck_order_lot_allocations_quantities", "quantity_reserved > 0 AND quantity_picked >= 0 AND quantity_picked <= quantity_reserved");
            t.HasCheckConstraint("ck_order_lot_allocations_unit_cost", "unit_cost_snapshot >= 0");
        });
        allocation.HasKey(x => x.Id);
        allocation.Property(x => x.Id).HasColumnName("id");
        allocation.Property(x => x.OrderLineId).HasColumnName("order_line_id");
        allocation.Property(x => x.LotId).HasColumnName("lot_id");
        allocation.Property(x => x.QuantityReserved).HasColumnName("quantity_reserved");
        allocation.Property(x => x.QuantityPicked).HasColumnName("quantity_picked");
        allocation.Property(x => x.UnitCostSnapshot).HasColumnName("unit_cost_snapshot").HasPrecision(12, 2);
        allocation.HasOne(x => x.OrderLine).WithMany(x => x.LotAllocations).HasForeignKey(x => x.OrderLineId).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne(x => x.Lot).WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
        allocation.HasIndex(x => new { x.OrderLineId, x.LotId }).IsUnique().HasDatabaseName("ux_order_lot_allocations_line_lot");

        var orderHistory = modelBuilder.Entity<OrderHistoryEntry>();
        orderHistory.ToTable("order_history_entries");
        orderHistory.HasKey(x => x.Id);
        orderHistory.Property(x => x.Id).HasColumnName("id");
        orderHistory.Property(x => x.OrderId).HasColumnName("order_id");
        orderHistory.Property(x => x.ActorId).HasColumnName("actor_id");
        orderHistory.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(80).IsRequired();
        orderHistory.Property(x => x.Details).HasColumnName("details").HasColumnType("jsonb").IsRequired();
        orderHistory.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        orderHistory.HasOne(x => x.Order).WithMany(x => x.History).HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        orderHistory.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
        orderHistory.HasIndex(x => new { x.OrderId, x.OccurredAtUtc }).HasDatabaseName("ix_order_history_order_time");

        var dispatch = modelBuilder.Entity<Dispatch>();
        dispatch.ToTable("dispatches");
        dispatch.HasKey(x => x.Id);
        dispatch.Property(x => x.Id).HasColumnName("id");
        dispatch.Property(x => x.DispatchNumber).HasColumnName("dispatch_number").HasMaxLength(40).IsRequired();
        dispatch.Property(x => x.VehicleIdentifier).HasColumnName("vehicle_identifier").HasMaxLength(80).IsRequired();
        dispatch.Property(x => x.DispatchLeadId).HasColumnName("dispatch_lead_id");
        dispatch.Property(x => x.DriverStaffId).HasColumnName("driver_staff_id");
        dispatch.Property(x => x.DepartedAtUtc).HasColumnName("departed_at_utc");
        dispatch.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        dispatch.HasOne(x => x.DispatchLead).WithMany().HasForeignKey(x => x.DispatchLeadId).OnDelete(DeleteBehavior.Restrict);
        dispatch.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverStaffId).OnDelete(DeleteBehavior.Restrict);
        dispatch.HasIndex(x => x.DispatchNumber).IsUnique().HasDatabaseName("ux_dispatches_number");

        var dispatchOrder = modelBuilder.Entity<DispatchOrder>();
        dispatchOrder.ToTable("dispatch_orders", t =>
        {
            t.HasCheckConstraint("ck_dispatch_orders_attempt", "attempt_number BETWEEN 1 AND 2");
            t.HasCheckConstraint("ck_dispatch_orders_result", "result IS NULL OR result IN ('DELIVERED','NOT_FOUND','REJECTED','CANCELLED')");
            t.HasCheckConstraint("ck_dispatch_orders_cancellation_fee", "cancellation_fee_amount >= 0");
        });
        dispatchOrder.HasKey(x => x.Id);
        dispatchOrder.Property(x => x.Id).HasColumnName("id");
        dispatchOrder.Property(x => x.DispatchId).HasColumnName("dispatch_id");
        dispatchOrder.Property(x => x.OrderId).HasColumnName("order_id");
        dispatchOrder.Property(x => x.AttemptNumber).HasColumnName("attempt_number");
        dispatchOrder.Property(x => x.SequenceNumber).HasColumnName("sequence_number");
        dispatchOrder.Property(x => x.Result).HasColumnName("result").HasMaxLength(20);
        dispatchOrder.Property(x => x.RecipientName).HasColumnName("recipient_name").HasMaxLength(160);
        dispatchOrder.Property(x => x.RecipientRelationship).HasColumnName("recipient_relationship").HasMaxLength(80);
        dispatchOrder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        dispatchOrder.Property(x => x.SignatureBytes).HasColumnName("signature_bytes");
        dispatchOrder.Property(x => x.SignatureMimeType).HasColumnName("signature_mime_type").HasMaxLength(80);
        dispatchOrder.Property(x => x.AttemptedAtUtc).HasColumnName("attempted_at_utc");
        dispatchOrder.Property(x => x.InspectedAtUtc).HasColumnName("inspected_at_utc");
        dispatchOrder.Property(x => x.InspectorId).HasColumnName("inspector_id");
        dispatchOrder.Property(x => x.ResultRecordedById).HasColumnName("result_recorded_by_id");
        dispatchOrder.Property(x => x.InspectionNotes).HasColumnName("inspection_notes").HasMaxLength(500);
        dispatchOrder.Property(x => x.CancellationFeeAmount).HasColumnName("cancellation_fee_amount").HasPrecision(12, 2).HasDefaultValue(0m);
        dispatchOrder.HasOne<StaffAccount>().WithMany().HasForeignKey(x => x.InspectorId).OnDelete(DeleteBehavior.Restrict);
        dispatchOrder.HasOne<StaffAccount>().WithMany().HasForeignKey(x => x.ResultRecordedById).OnDelete(DeleteBehavior.Restrict);
        dispatchOrder.HasOne(x => x.Dispatch).WithMany(x => x.Orders).HasForeignKey(x => x.DispatchId).OnDelete(DeleteBehavior.Restrict);
        dispatchOrder.HasOne(x => x.Order).WithMany(x => x.DispatchAssignments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        dispatchOrder.HasIndex(x => new { x.DispatchId, x.OrderId }).IsUnique().HasDatabaseName("ux_dispatch_orders_dispatch_order");
        dispatchOrder.HasIndex(x => new { x.OrderId, x.AttemptNumber }).IsUnique().HasDatabaseName("ux_dispatch_orders_order_attempt");
        dispatchOrder.HasIndex(x => new { x.DispatchId, x.SequenceNumber }).HasDatabaseName("ix_dispatch_orders_route_sequence");
    }
}
