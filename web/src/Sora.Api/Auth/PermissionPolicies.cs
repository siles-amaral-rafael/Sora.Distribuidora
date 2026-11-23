namespace Sora.Api.Auth;

public static class PermissionPolicies
{
    public const string StaffRead = "staff.read";
    public const string StaffManage = "staff.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string ProductsRead = "products.read";
    public const string ProductsWrite = "products.write";
    public const string DiscountsManage = "discounts.manage";
    public const string SuppliersRead = "suppliers.read";
    public const string SuppliersWrite = "suppliers.write";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";
    public const string OrdersPrepare = "orders.prepare";
    public const string OrdersCancel = "orders.cancel";
    public const string OrdersPickup = "orders.pickup";
    public const string PaymentsRecord = "payments.record";
    public const string NotificationsManage = "notifications.manage";
    public const string DispatchesRead = "dispatches.read";
    public const string DispatchesCreate = "dispatches.create";
    public const string DispatchesResult = "dispatches.result";
    public const string DispatchesInspect = "dispatches.inspect";

    public static readonly string[] All =
    [
        StaffRead, StaffManage, CustomersRead, CustomersWrite, ProductsRead, ProductsWrite, DiscountsManage,
        SuppliersRead, SuppliersWrite, InventoryRead, InventoryWrite, OrdersRead, OrdersWrite,
        OrdersPrepare, OrdersCancel, OrdersPickup, PaymentsRecord, NotificationsManage, DispatchesRead, DispatchesCreate,
        DispatchesResult, DispatchesInspect
    ];
}
