namespace Sora.Domain.Security;

public sealed record PermissionDefinition(string Code, string DisplayName);

public static class PermissionCatalog
{
    public static readonly PermissionDefinition[] All =
    [
        new("staff.read", "Consultar personal y auditoría"),
        new("staff.manage", "Administrar usuarios, roles y permisos"),
        new("customers.read", "Consultar clientes"),
        new("customers.write", "Registrar y modificar clientes"),
        new("products.read", "Consultar productos"),
        new("products.write", "Registrar y modificar productos"),
        new("discounts.manage", "Configurar beneficios por cantidad"),
        new("suppliers.read", "Consultar proveedores"),
        new("suppliers.write", "Registrar y modificar proveedores"),
        new("inventory.read", "Consultar inventario"),
        new("inventory.write", "Registrar recepciones y ajustes"),
        new("orders.read", "Consultar pedidos"),
        new("orders.write", "Registrar pedidos"),
        new("orders.prepare", "Preparar pedidos"),
        new("orders.cancel", "Cancelar pedidos"),
        new("orders.pickup", "Confirmar retiros"),
        new("payments.record", "Registrar pagos y abonos de caja"),
        new("dispatches.read", "Consultar despachos asignados"),
        new("dispatches.create", "Crear salidas de despacho"),
        new("dispatches.result", "Registrar resultados de entrega"),
        new("dispatches.inspect", "Inspeccionar devoluciones")
    ];

    public static readonly IReadOnlyDictionary<string, string[]> DefaultsByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = All.Select(x => x.Code).ToArray(),
            ["SELLER"] = ["customers.read", "customers.write", "products.read", "orders.read", "orders.write", "orders.cancel", "payments.record"],
            ["WAREHOUSE"] = ["customers.read", "products.read", "products.write", "suppliers.read", "suppliers.write", "inventory.read", "inventory.write", "orders.read", "orders.prepare", "orders.pickup", "payments.record"],
            ["DISPATCH_LEAD"] = ["customers.read", "products.read", "inventory.read", "inventory.write", "orders.read", "orders.prepare", "dispatches.read", "dispatches.create", "dispatches.result", "dispatches.inspect"],
            ["DISPATCH_ASSISTANT"] = ["dispatches.read", "dispatches.result"],
            ["DRIVER"] = ["dispatches.read", "dispatches.result"]
        };
}
